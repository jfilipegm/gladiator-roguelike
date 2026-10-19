using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// M2.2: the six behaviours and three elite affixes do what their data says. The player stands still (scripted
    /// input), the gladius is off unless a test needs it, and damage is identified by amount (contact vs. attack).
    /// </summary>
    public class EnemyRosterTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        Player _player;
        PlayerHealth _health;
        PlayerMotor _motor;
        EnemyManager _em;
        EnemyCatalog _catalog;
        readonly List<float> _damage = new List<float>();
        readonly List<HitEvent> _hits = new List<HitEvent>();
        readonly List<KillEvent> _kills = new List<KillEvent>();

        void OnDamaged(in DamagedEvent e) => _damage.Add(e.Damage);
        void OnHit(in HitEvent e) => _hits.Add(e);
        void OnKill(in KillEvent e) => _kills.Add(e);

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _player = Player.Main;
            _health = _player.GetComponent<PlayerHealth>();
            _motor = _player.GetComponent<PlayerMotor>();
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().enabled = false;
            _em = EnemyManager.Instance;
            _catalog = AdminController.Instance.catalog;
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // tests place their own enemies
            _em.KillAll();
            DevSettings.GodMode = true; // HP never runs out; damage is still recorded
            // No i-frames: a contact hit landing just before an attack would otherwise swallow it at random
            // (the i-frame rule itself is covered elsewhere); these tests check the attacks.
            _health.invulnerableTime = 0f;
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _damage.Clear(); _hits.Clear(); _kills.Clear();
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnHit.Subscribe(OnHit);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnHit.Unsubscribe(OnHit);
            GameEvents.OnKill.Unsubscribe(OnKill);
            DevSettings.ResetAll();
        }

        EnemyDef Def(string id)
        {
            foreach (var d in _catalog.enemies) if (d.id == id) return d;
            Assert.Fail("no EnemyDef " + id);
            return null;
        }

        AffixDef Affix(string id)
        {
            foreach (var a in _catalog.affixes) if (a.id == id) return a;
            Assert.Fail("no AffixDef " + id);
            return null;
        }

        Vector3 P => _player.transform.position;
        Vector3 Ground(float x, float z) => new Vector3(x, _em.Height.Sample(x, z), z);
        int CountDamage(float amount) { int n = 0; foreach (var d in _damage) if (Mathf.Abs(d - amount) < 0.01f) n++; return n; }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (!done() && Time.time < end) yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ShieldThug_BlocksFromTheFront_NotFromBehind()
        {
            yield return Boot();
            var def = Def("shieldthug");
            int i = _em.Spawn(def, P + new Vector3(0f, 0f, 3f));
            yield return null;
            Vector3 fromPlayer = (_em.GetPosition(i) - P); fromPlayer.y = 0f; fromPlayer.Normalize();
            Assert.IsTrue(_em.IsBlocking(i, fromPlayer), "facing the player: a hit from the player is blocked");
            Assert.IsFalse(_em.IsBlocking(i, -fromPlayer), "a hit from behind is not blocked");

            float hp = _em.GetHp(i);
            _em.ApplyDamage(i, new DamageInfo { Amount = 10f, Direction = fromPlayer });
            Assert.AreEqual(hp - 10f * (1f - def.blockReduction), _em.GetHp(i), 1e-3f);
            Assert.IsTrue(_hits[_hits.Count - 1].Blocked, "the hit event says blocked (grey number)");
            hp = _em.GetHp(i);
            _em.ApplyDamage(i, new DamageInfo { Amount = 10f, Direction = -fromPlayer });
            Assert.AreEqual(hp - 10f, _em.GetHp(i), 1e-3f);
            Assert.IsFalse(_hits[_hits.Count - 1].Blocked);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Slinger_KeepsDistance_StoneHitsStandingPlayer_MissesMovedOne()
        {
            yield return Boot();
            var def = Def("slinger");
            int i = _em.Spawn(def, P + new Vector3(2.5f, 0f, -2.5f));
            yield return new WaitForSeconds(1.2f);
            float d = Vector2.Distance(new Vector2(P.x, P.z), new Vector2(_em.GetPosition(i).x, _em.GetPosition(i).z));
            Assert.Greater(d, 4.2f, "backs off toward its keep-distance band");

            // first stone: stand still → hit
            yield return Until(() => CountDamage(def.attackDamage) > 0, 8f);
            Assert.AreEqual(1, CountDamage(def.attackDamage), "the stone hits a player who stands still");

            // second stone: step aside as soon as it's thrown → miss
            var proj = EnemyProjectiles.Instance;
            yield return Until(() => proj.Count > 0, 8f);
            Assert.Greater(proj.Count, 0, "slinger throws again");
            // sidestep at right angles to the throw: a fixed "player's right" could lie on the stone's line when the
            // slinger happened to strafe to that side (depended on the RNG state earlier tests left)
            Vector3 throwDir = P - _em.GetPosition(i);
            throwDir.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, throwDir.normalized);
            _motor.Teleport(P + side * 3f);
            _em.KillAll(); // no third stone (aimed at the new spot) while the second is still flying
            yield return Until(() => proj.Count == 0, 3f);
            Assert.AreEqual(1, CountDamage(def.attackDamage), "a stone aimed at your old spot misses");
        }

        /// <summary>Phase 5.4: the Shield Archer's pavise blocks from the front (but not while it recovers from a shot);
        /// its arrow hits a player who stands still; its aim stops tracking just before release, so a late sidestep
        /// dodges.</summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator ShieldArcher_BlocksFromTheFront_ArrowHitsAStillPlayer_LockedAimMissesALateSidestep()
        {
            yield return Boot();
            var def = Def("shieldarcher");
            var root = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDef>("Assets/Data/Status/SE_Root.asset");
            int i = _em.Spawn(def, P + new Vector3(0f, 0f, 10f));
            yield return null;
            Vector3 fromPlayer = _em.GetPosition(i) - P; fromPlayer.y = 0f; fromPlayer.Normalize();
            Assert.IsTrue(_em.IsBlocking(i, fromPlayer), "the pavise faces the player");
            Assert.IsFalse(_em.IsBlocking(i, -fromPlayer), "not from behind");

            // first arrow: stand still -> hit (held in place so the test is about the shot, not the footwork)
            var proj = EnemyProjectiles.Instance;
            bool sawArrow = false;
            yield return Until(() =>
            {
                if (_em.IsAlive(i)) _em.ApplyStatus(i, root, 0f);
                for (int k = 0; k < proj.capacity; k++) sawArrow |= proj.IsActive(k) && proj.IsArrow(k);
                return CountDamage(def.attackDamage) > 0;
            }, 8f);
            Assert.IsTrue(sawArrow, "an arrow, not a stone");
            Assert.AreEqual(1, CountDamage(def.attackDamage), "the arrow hits a player who stands still");
            Assert.AreEqual(3, _em.GetAiState(i), "recovering after the shot");
            Assert.IsFalse(_em.IsBlocking(i, fromPlayer), "the pavise dips while it recovers: the window");

            // second arrow: wait until the aim has locked, then step aside
            yield return Until(() => { if (_em.IsAlive(i)) _em.ApplyStatus(i, root, 0f); return _em.GetAiState(i) == 1; }, 8f);
            Assert.AreEqual(1, _em.GetAiState(i), "draws again");
            yield return new WaitForSeconds(def.windup - def.aimLockTime + 0.1f);
            Assert.AreEqual(1, _em.GetAiState(i), "still drawing, aim locked");
            Vector3 line = P - _em.GetPosition(i); line.y = 0f;
            _motor.Teleport(P + Vector3.Cross(Vector3.up, line.normalized) * 2.5f);
            yield return Until(() => proj.Count > 0, 2f);
            Assert.Greater(proj.Count, 0, "loosed");
            _em.KillAll(); // no third arrow at the new spot while this one flies
            yield return Until(() => proj.Count == 0, 3f);
            Assert.AreEqual(1, CountDamage(def.attackDamage), "a locked shot misses a late sidestep");
        }

        /// <summary>Phase 5.4: the Assassin circles to your side (not your face), winds up behind a telegraphed lane,
        /// dashes through a player who stays in it, then stands winded; a sidestep during the wind-up dodges.</summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator Assassin_FlanksToYourSide_DashesThroughTheLane_ThenStandsWinded_ASidestepDodges()
        {
            yield return Boot();
            var def = Def("assassin");
            Vector3 view = Camera.main.transform.forward; view.y = 0f; view.Normalize();
            int i = _em.Spawn(def, P + view * 9f);
            yield return Until(() => _em.GetAiState(i) == 1, 8f);
            Assert.AreEqual(1, _em.GetAiState(i), "winds up");
            Vector3 off = _em.GetPosition(i) - P; off.y = 0f;
            Assert.Greater(Vector3.Angle(off, view), 50f, "it came at you from the side, not from in front");
            Assert.AreEqual(0, CountDamage(def.attackDamage), "nothing during the wind-up");
            Vector3 start = _em.GetPosition(i);
            yield return Until(() => _em.GetAiState(i) == 3, 3f);
            Assert.AreEqual(3, _em.GetAiState(i), "dashed, now winded");
            Assert.AreEqual(1, CountDamage(def.attackDamage), "the dash cuts through a player who stayed in the lane");
            Vector3 end = _em.GetPosition(i);
            Assert.Greater(Vector3.Distance(start, end), def.dashLength * 0.7f, "a long dash");
            yield return new WaitForSeconds(def.recover * 0.6f);
            Assert.AreEqual(3, _em.GetAiState(i));
            Assert.Less(Vector3.Distance(end, _em.GetPosition(i)), 0.15f, "stands still while winded");

            // the next dash: step out of the lane during the wind-up
            yield return Until(() => _em.GetAiState(i) == 1, 10f);
            Assert.AreEqual(1, _em.GetAiState(i));
            Vector3 lane = P - _em.GetPosition(i); lane.y = 0f;
            _motor.Teleport(P + Vector3.Cross(Vector3.up, lane.normalized) * 2.5f);
            yield return Until(() => _em.GetAiState(i) == 3, 3f);
            Assert.AreEqual(3, _em.GetAiState(i));
            Assert.AreEqual(1, CountDamage(def.attackDamage), "sidestepped the lane: no second cut");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Brute_SlamIsTelegraphed_HitsGroundedPlayer_NotJumpingOne()
        {
            yield return Boot();
            var def = Def("brute");
            int i = _em.Spawn(def, P + new Vector3(0f, 0f, 2.1f));
            yield return Until(() => _em.GetAiState(i) == 1, 6f);
            Assert.AreEqual(1, _em.GetAiState(i), "winds up when the player is in range");
            yield return null;
            Assert.Greater(_em.ThreatCount, 0, "a wind-up is listed as a threat (telegraph circle + edge arrow)");
            Assert.AreEqual(0, CountDamage(def.attackDamage), "no damage during the wind-up");
            float windStart = Time.time;
            yield return Until(() => _em.GetAiState(i) != 1, 3f);
            Assert.GreaterOrEqual(Time.time - windStart, def.windup * 0.85f, "the wind-up lasts its full telegraph time");
            Assert.AreEqual(1, CountDamage(def.attackDamage), "the slam hits a grounded player inside the circle");
            yield return null;
            yield return null;
            Assert.IsFalse(_motor.IsGrounded, "the slam knocks the player up");

            // next slam: jump just before it lands
            yield return Until(() => _motor.IsGrounded, 3f);
            _motor.Teleport(_em.GetPosition(i) + new Vector3(0f, 0.1f, -1.6f));
            yield return Until(() => _em.GetAiState(i) == 1, 8f);
            Assert.AreEqual(1, _em.GetAiState(i));
            yield return new WaitForSeconds(def.windup * 0.7f);
            _motor.AddImpulse(Vector3.up * 9f);
            yield return Until(() => _em.GetAiState(i) != 1, 3f);
            Assert.AreEqual(1, CountDamage(def.attackDamage), "jumping over the slam avoids it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator PitCur_LungesFast_AndBites()
        {
            yield return Boot();
            var def = Def("pitcur");
            int i = _em.Spawn(def, P + new Vector3(0f, 0f, 6f));
            yield return Until(() => _em.GetAiState(i) == 2, 6f);
            Assert.AreEqual(2, _em.GetAiState(i), "lunges once in range");
            Vector3 a = _em.GetPosition(i);
            float t0 = Time.time;
            yield return null;
            yield return null;
            float speed = Vector3.Distance(a, _em.GetPosition(i)) / Mathf.Max(1e-3f, Time.time - t0);
            Assert.Greater(speed, def.moveSpeed * 1.5f, "the leap is much faster than its run");
            yield return Until(() => CountDamage(def.attackDamage) > 0, 2f);
            Assert.AreEqual(1, CountDamage(def.attackDamage), "the lunge bites for attackDamage");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FireZone_BurnsGroundedPlayer_NotAirborne()
        {
            yield return Boot();
            var zones = HazardZones.Instance;
            // airborne over a fresh patch: safe
            _motor.AddImpulse(Vector3.up * 9f);
            yield return null;
            Assert.IsFalse(_motor.IsGrounded);
            zones.Add(Ground(P.x, P.z), 1.8f, 3f, 10f);
            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(0, _damage.Count, "no burn while in the air");
            // landing in it: burns every 0.5 s
            yield return Until(() => _motor.IsGrounded, 3f);
            yield return new WaitForSeconds(1.1f);
            Assert.GreaterOrEqual(CountDamage(5f), 2, "10 dps in 0.5 s ticks while standing in it");
            Assert.IsTrue(zones.PlayerInside);
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(0, zones.Count, "the patch burns out");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FirePotThrower_LobsOntoThePlayer_LeavingFire()
        {
            yield return Boot();
            var def = Def("firepot");
            _em.Spawn(def, P + new Vector3(-6f, 0f, -6f));
            var proj = EnemyProjectiles.Instance;
            yield return Until(() => proj.Count > 0, 8f);
            Assert.Greater(proj.Count, 0, "throws a pot");
            yield return Until(() => HazardZones.Instance.Count > 0, 3f);
            Assert.AreEqual(1, HazardZones.Instance.Count, "the pot leaves a fire patch where it lands");
            yield return null;
            Assert.IsTrue(HazardZones.Instance.PlayerInside, "it lands on the spot the player stood on");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ExplosiveElite_BlowsUpAfterFuse_HurtingPlayerAndEnemies()
        {
            yield return Boot();
            var convict = Def("convict");
            var explosive = Affix("explosive");
            Vector3 at = P + new Vector3(0f, 0f, 1.8f);
            int bomb = _em.Spawn(convict, at, explosive);
            Assert.IsTrue(_em.IsElite(bomb));
            int bombUid = _em.GetUid(bomb);
            _em.ApplyDamage(bomb, new DamageInfo { Amount = 10000f });
            Assert.AreEqual(1, _em.PendingBlasts, "dying starts the fuse");
            Assert.IsTrue(_kills[_kills.Count - 1].Elite, "elite kill event");
            yield return new WaitForSeconds(explosive.fuse * 0.6f);
            Assert.AreEqual(0, CountDamage(explosive.explosionDamageToPlayer), "nothing before the fuse ends");
            // a bystander inside the blast radius, placed late so it can't touch the player first (contact hit = i-frames)
            int other = _em.Spawn(convict, at + new Vector3(1.2f, 0f, 0.6f));
            int otherUid = _em.GetUid(other);
            yield return new WaitForSeconds(explosive.fuse * 0.4f + 0.2f);
            Assert.AreEqual(0, _em.PendingBlasts);
            Assert.AreEqual(1, CountDamage(explosive.explosionDamageToPlayer), "the blast hurts the player");
            bool otherDied = false;
            foreach (var k in _kills) if (k.TargetId == otherUid) otherDied = true;
            Assert.IsTrue(otherDied, "the blast kills nearby enemies too");
            Assert.AreNotEqual(bombUid, otherUid);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ArmoredElite_IsBigger_Tougher_AndWorthMore()
        {
            yield return Boot();
            var convict = Def("convict");
            var armored = Affix("armored");
            int i = _em.Spawn(convict, P + new Vector3(0f, 0f, 4f), armored);
            Assert.IsTrue(_em.IsElite(i));
            Assert.AreEqual(convict.maxHp * armored.hpMultiplier, _em.GetMaxHp(i), 1e-3f);
            Assert.AreEqual(convict.radius * armored.scale, _em.GetRadius(i), 1e-3f);
            float hp = _em.GetHp(i);
            _em.ApplyDamage(i, new DamageInfo { Amount = 10f, Direction = Vector3.forward });
            Assert.AreEqual(hp - 10f * armored.damageTakenMultiplier, _em.GetHp(i), 1e-3f);
            Assert.IsTrue(_hits[_hits.Count - 1].Elite);
            _em.ApplyDamage(i, new DamageInfo { Amount = 10000f });
            var k = _kills[_kills.Count - 1];
            Assert.IsTrue(k.Elite);
            Assert.AreEqual(convict.xp * armored.xpMultiplier, k.Xp, 1e-3f);
            Assert.AreEqual(0, _em.PendingBlasts, "only Explosive explodes");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FastElite_OutrunsANormalOne()
        {
            yield return Boot();
            var convict = Def("convict");
            int slow = _em.Spawn(convict, P + new Vector3(-3f, 0f, 9f));
            int fast = _em.Spawn(convict, P + new Vector3(3f, 0f, 9f), Affix("fast"));
            Vector3 s0 = _em.GetPosition(slow), f0 = _em.GetPosition(fast);
            yield return new WaitForSeconds(1f);
            float ds = Vector3.Distance(s0, _em.GetPosition(slow)), df = Vector3.Distance(f0, _em.GetPosition(fast));
            Assert.Greater(df, ds * 1.1f, $"fast elite covered {df:0.00} m vs {ds:0.00} m"); // ±15 % speed jitter on both
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThreatIndicator_PointsAtWindUpBehindTheCamera()
        {
            yield return Boot();
            var cam = Camera.main.transform;
            Vector3 back = -new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
            int i = _em.Spawn(Def("slinger"), P + back * 7f);
            var ui = Object.FindAnyObjectByType<Gladiator.UI.ThreatIndicators>();
            Assert.IsNotNull(ui);
            yield return Until(() => _em.GetAiState(i) == 1, 8f);
            Assert.AreEqual(1, _em.GetAiState(i));
            yield return null;
            yield return null;
            Assert.Greater(ui.VisibleCount, 0, "an off-screen wind-up shows an edge arrow");
        }
    }
}
