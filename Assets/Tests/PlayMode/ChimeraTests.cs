using System.Collections;
using System.Collections.Generic;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// R2 (docs/ARENAS.md §6): the Chimera (<see cref="EnemyBehaviour.Breather"/>). Its cone shows for its 1 s wind-up
    /// with nothing hurt; then the breath hits a player in it once, not one 40° off its aim or 9 m away; another enemy in
    /// the cone takes ×3 once a breath, one beside it, another Chimera and a boss don't; three fire patches after. And the
    /// boss part's two calls: <see cref="EnemyManager.SetHpFloor"/> keeps a Scripted slot alive at the floor (hits,
    /// ticks, Execute), <see cref="EnemyManager.SetHp"/> sets its HP.
    ///
    /// The Chimera is a stand-in made here with ARENAS.md §6's numbers, rooted (moveSpeed 0) so the tests place it; the
    /// test arena with no Director, no level-ups and no weapons (R2TestArena in UndeadTests.cs).
    /// </summary>
    public class ChimeraTests
    {
        EnemyManager _em;
        EnemyDef _chimera, _dummy, _boss;
        readonly List<DamagedEvent> _hits = new List<DamagedEvent>(); // the player's, not the fire patches' ticks
        readonly List<KillEvent> _kills = new List<KillEvent>();

        void OnDamaged(in DamagedEvent e) { if (!e.Status) _hits.Add(e); }
        void OnKill(in KillEvent e) => _kills.Add(e);

        const int Move = 0, Windup = 1, Breath = 2, Recover = 3;

        IEnumerator Boot()
        {
            yield return R2TestArena.Boot();
            _em = EnemyManager.Instance;
            _chimera = R2TestArena.Chimera();
            _dummy = R2TestArena.Dummy("test_dummy");
            _boss = R2TestArena.Dummy("test_boss", EnemyBehaviour.Scripted);
            _em.Prewarm(_chimera, 3);
            _em.Prewarm(_dummy, 3);
            _em.Prewarm(_boss, 2);
            if (HazardZones.Instance != null) HazardZones.Instance.Clear();
            yield return new WaitForSeconds(0.2f);
            _hits.Clear();
            _kills.Clear();
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnKill.Unsubscribe(OnKill);
            DevSettings.ResetAll();
            if (_chimera != null) Object.Destroy(_chimera);
            if (_dummy != null) Object.Destroy(_dummy);
            if (_boss != null) Object.Destroy(_boss);
        }

        static DamageInfo Hit(float amount) => new DamageInfo { Amount = amount, Point = Vector3.zero, Direction = Vector3.forward };

        /// <summary>The beast at A, the player 5 m east of it at B (open ground near the test arena's spawn).</summary>
        static Vector3 A => R2TestArena.Spot(new Vector3(-3f, 0f, -8f));
        static Vector3 B => R2TestArena.Spot(new Vector3(2f, 0f, -8f));

        IEnumerator UntilState(int c, int state, float seconds) =>
            R2TestArena.Until(() => !_em.IsAlive(c) || _em.GetAiState(c) == state, seconds);

        bool WindupThreatAt(Vector3 p)
        {
            for (int k = 0; k < _em.ThreatCount; k++)
            {
                var t = _em.GetThreat(k);
                if (t.Windup && R2TestArena.Flat(t.Position - p).magnitude < 0.5f) return true;
            }
            return false;
        }

        static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        [UnityTest, Timeout(60000)]
        public IEnumerator TheCone_ShowsForItsWindup_ThenTheBreathHitsAPlayerInItOnce_AndLeavesThreeFirePatches()
        {
            yield return Boot();
            Vector3 a = A, b = B;
            R2TestArena.PutPlayer(b);
            yield return new WaitForSeconds(0.3f); // landed
            int breaths = _em.Breaths, hitsYou = _em.BreathHitsPlayer, zones = HazardZones.Instance.Count;

            int c = _em.Spawn(_chimera, a);
            yield return UntilState(c, Windup, 5f); // its first cooldown is at most 0.7 × 4.5 s
            Assert.AreEqual(Windup, _em.GetAiState(c), "you in front within 7 m: it plants and winds up");
            float t0 = Time.time;
            Assert.IsTrue(WindupThreatAt(_em.GetPosition(c)), "the cone shows (a wind-up threat at the beast)");
            yield return null;
            Assert.IsTrue(WindupThreatAt(_em.GetPosition(c)), "still showing");

            yield return R2TestArena.Until(() => _em.GetAiState(c) != Windup, 2f);
            Assert.AreEqual(Breath, _em.GetAiState(c), "then it breathes");
            Assert.AreEqual(1f, Time.time - t0, 0.12f, "after a 1 s wind-up");
            Assert.AreEqual(0, _hits.Count, "nothing hurt during the wind-up");
            Assert.AreEqual(breaths + 1, _em.Breaths);
            Assert.IsTrue(WindupThreatAt(_em.GetPosition(c)), "the cone stays up while it breathes");

            float t1 = Time.time;
            yield return R2TestArena.Until(() => _em.GetAiState(c) != Breath, 2f);
            Assert.AreEqual(Recover, _em.GetAiState(c), "then it stands smoking");
            Assert.AreEqual(1f, Time.time - t1, 0.12f, "after 1 s of fire");
            Assert.AreEqual(1, _hits.Count, "the player in the cone: hit once");
            Assert.AreEqual(hitsYou + 1, _em.BreathHitsPlayer);
            Assert.AreEqual(zones + 3, HazardZones.Instance.Count, "three fire patches along the cone");

            yield return UntilState(c, Move, 2f);
            Assert.AreEqual(Move, _em.GetAiState(c), "after 1.4 s");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheBreath_MissesAPlayer40DegreesOffItsAim_And9mAway()
        {
            yield return Boot();
            Vector3 a = A, b = B;
            Vector3 aim = R2TestArena.Flat(b - a).normalized;
            Vector3 off40 = R2TestArena.Spot(a + Quaternion.Euler(0f, 40f, 0f) * aim * 5f);
            Vector3 far9 = R2TestArena.Spot(a + aim * 9f);
            R2TestArena.PutPlayer(b);
            yield return new WaitForSeconds(0.3f);
            int hitsYou = _em.BreathHitsPlayer, breaths = _em.Breaths;

            int c = _em.Spawn(_chimera, a);
            yield return UntilState(c, Windup, 5f);
            Assert.AreEqual(Windup, _em.GetAiState(c));
            R2TestArena.PutPlayer(off40); // the cone is locked where you stood: step 40° off it
            yield return UntilState(c, Recover, 3f);
            Assert.AreEqual(breaths + 1, _em.Breaths);
            Assert.AreEqual(hitsYou, _em.BreathHitsPlayer, "40° off its aim: missed");
            Assert.AreEqual(0, _hits.Count);

            R2TestArena.PutPlayer(b); // in front again for the next one
            yield return UntilState(c, Windup, 8f);
            Assert.AreEqual(Windup, _em.GetAiState(c), "the next breath, a cooldown later");
            R2TestArena.PutPlayer(far9); // straight down its aim, past the cone's 8 m
            yield return UntilState(c, Recover, 3f);
            Assert.AreEqual(breaths + 2, _em.Breaths);
            Assert.AreEqual(hitsYou, _em.BreathHitsPlayer, "9 m away: missed");
            Assert.AreEqual(0, _hits.Count);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AnotherEnemyInTheCone_TakesThreeTimesOnceABreath_ButNotOneBesideIt_AnotherChimera_OrABoss()
        {
            yield return Boot();
            Vector3 a = A, b = B;
            Vector3 aim = R2TestArena.Flat(b - a).normalized, side = new Vector3(aim.z, 0f, -aim.x);
            R2TestArena.PutPlayer(b);
            yield return new WaitForSeconds(0.3f);
            int burned = _em.BreathEnemyHits;

            int inCone = _em.Spawn(_dummy, a + aim * 3f);
            int beside = _em.Spawn(_dummy, a + Quaternion.Euler(0f, 55f, 0f) * aim * 3f);
            Vector3 kinPos = R2TestArena.Ground(a + aim * 2f + side * 1f); // 27° off its aim, clear of the others
            int kin = _em.Spawn(_chimera, kinPos);
            _em.SetScripted(kin, kinPos, YawOf(-aim), false); // turned away from you (and rooted): it never breathes itself
            int boss = _em.Spawn(_boss, a + aim * 4f - side * 1f);
            float hpIn = _em.GetHp(inCone), hpBeside = _em.GetHp(beside), hpKin = _em.GetHp(kin), hpBoss = _em.GetHp(boss);

            int c = _em.Spawn(_chimera, a);
            yield return UntilState(c, Windup, 5f);
            yield return UntilState(c, Recover, 3f);
            Assert.AreEqual(Recover, _em.GetAiState(c), "it breathed");
            Assert.AreEqual(Move, _em.GetAiState(kin), "the other Chimera never wound up");

            Assert.AreEqual(hpIn - 24f * 3f, _em.GetHp(inCone), 1e-3f, "in the cone: 24 × 3, once for the whole breath");
            Assert.AreEqual(hpBeside, _em.GetHp(beside), 1e-3f, "55° off its aim: untouched");
            Assert.AreEqual(hpKin, _em.GetHp(kin), 1e-3f, "its own kind: untouched");
            Assert.AreEqual(hpBoss, _em.GetHp(boss), 1e-3f, "a boss: untouched");
            Assert.AreEqual(burned + 1, _em.BreathEnemyHits);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SetHpFloor_KeepsAScriptedSlotAlive_SetHpSetsIt_AndExecuteRespectsIt()
        {
            yield return Boot();
            Vector3 a = A;
            R2TestArena.PutPlayer(B + new Vector3(0f, 0f, 6f));
            var burn = R2TestArena.Load<StatusDef>("Assets/Data/Status/SE_Burn.asset");
            Assert.IsNotNull(burn, "SE_Burn");

            int i = _em.Spawn(_boss, a);
            Assert.AreEqual(1000f, _em.GetMaxHp(i), 1e-3f, "a scripted enemy ignores the arena's HP multiplier");
            Assert.AreEqual(0f, _em.GetHpFloor(i), "off by default");

            _em.SetHpFloor(i, 100f);
            _em.ApplyDamage(i, Hit(5000f));
            Assert.IsTrue(_em.IsAlive(i), "a hit can't take it below its floor");
            Assert.AreEqual(100f, _em.GetHp(i), 1e-3f);
            Assert.IsFalse(_em.Execute(i, 123), "Execute: not dead");
            Assert.IsTrue(_em.IsAlive(i));
            Assert.AreEqual(100f, _em.GetHp(i), 1e-3f);

            _em.SetHp(i, 400f);
            Assert.AreEqual(400f, _em.GetHp(i), 1e-3f, "SetHp sets it");
            Assert.IsFalse(_em.Execute(i, 123));
            Assert.AreEqual(100f, _em.GetHp(i), 1e-3f, "Execute brings it down to the floor");
            _em.SetHp(i, 99999f);
            Assert.AreEqual(1000f, _em.GetHp(i), 1e-3f, "clamped to its max HP");
            _em.SetHp(i, -5f);
            Assert.IsTrue(_em.IsAlive(i), "SetHp never kills");
            Assert.Greater(_em.GetHp(i), 0f);

            _em.SetHp(i, 150f);
            _em.ApplyStatus(i, burn, 500f); // 250 a tick
            yield return new WaitForSeconds(1.3f);
            Assert.IsTrue(_em.IsAlive(i), "burn ticks stop at the floor too");
            Assert.AreEqual(100f, _em.GetHp(i), 1e-3f);
            Assert.AreEqual(0, _kills.Count, "no kill while the floor holds");

            _em.SetHpFloor(i, 0f);
            _em.ApplyDamage(i, Hit(5000f));
            Assert.IsFalse(_em.IsAlive(i), "the floor off: it dies");
            Assert.AreEqual(1, _kills.Count);

            int j = _em.Spawn(_boss, a);
            Assert.AreEqual(0f, _em.GetHpFloor(j), "Spawn resets the floor");
        }
    }
}
