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
    /// R2 (docs/ARENAS.md §6): the Skeletal Legionnaire's bone piles. Killed, it collapses (no OnKill, no gem), stands up
    /// 2.5 s later where it fell with half its max HP, and its second death is the kill (its XP, no pile); a burning
    /// one stays down; one that falls into a trapdoor stays down, and a pile on a door that opens falls too; a player
    /// walking over a pile scatters it (the kill, at the pile) and nothing rises; an elite stands up with its affix
    /// (Q7); never more than 48 piles at once (past that, they stay down).
    ///
    /// The Legionnaire is a stand-in made here with ARENAS.md §6's numbers (the real asset comes with the Colosseum's
    /// setup), in the test arena with no Director, no level-ups and no weapons.
    /// </summary>
    public class UndeadTests
    {
        EnemyManager _em;
        PlayerMotor _motor;
        ScriptedInput _in;
        EnemyDef _legion;
        readonly List<KillEvent> _kills = new List<KillEvent>();

        void OnKill(in KillEvent e) => _kills.Add(e);

        IEnumerator Boot()
        {
            yield return R2TestArena.Boot();
            _em = EnemyManager.Instance;
            _motor = Player.Main.GetComponent<PlayerMotor>();
            _in = (ScriptedInput)Player.Main.OverrideSource;
            _legion = R2TestArena.Legionnaire();
            _em.Prewarm(_legion, 70);
            yield return new WaitForSeconds(0.2f);
            PickupSystem.Instance.ClearAll();
            _kills.Clear();
            GameEvents.OnKill.Subscribe(OnKill);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnKill.Unsubscribe(OnKill);
            DevSettings.ResetAll();
            if (_legion != null) Object.Destroy(_legion);
        }

        static DamageInfo Hit(float amount) => new DamageInfo { Amount = amount, Point = Vector3.zero, Direction = Vector3.forward };

        int KillsOf(int sourceId) { int n = 0; foreach (var k in _kills) if (k.SourceId == sourceId) n++; return n; }

        /// <summary>The living Legionnaire nearest `at` (within 3 m).</summary>
        int Risen(Vector3 at)
        {
            Assert.IsTrue(_em.TryGetNearest(at, 3f, out int r), "a Legionnaire stood up near " + at);
            Assert.AreSame(_legion, _em.GetDef(r));
            return r;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AKilledLegionnaire_Collapses_StandsUpWithHalfItsHp_AndItsSecondDeathIsTheKill()
        {
            yield return Boot();
            Vector3 spot = R2TestArena.Spot(new Vector3(1.5f, 0f, -7.5f));
            R2TestArena.PutPlayer(spot + new Vector3(-9f, 0f, 3f));
            int made = _em.PilesMade, risen = _em.PilesRisen;

            int i = _em.Spawn(_legion, spot);
            Assert.GreaterOrEqual(i, 0);
            float max0 = _em.GetMaxHp(i);
            Vector3 at = _em.GetPosition(i);
            int gems = PickupSystem.Instance.ActiveCount;
            _em.ApplyDamage(i, Hit(max0 + 1f));

            Assert.IsFalse(_em.IsAlive(i), "it went down");
            Assert.AreEqual(made + 1, _em.PilesMade, "into a pile");
            Assert.AreEqual(1, _em.PileCount);
            Assert.AreEqual(0, _em.AliveOf(_legion), "a pile isn't an enemy (the Director, the weapons)");
            Assert.AreEqual(0, _kills.Count, "not a kill: no OnKill");
            Assert.AreEqual(gems, PickupSystem.Instance.ActiveCount, "no gem, no gold");
            Assert.Less(R2TestArena.Flat(_em.GetPilePosition(0) - at).magnitude, 0.01f, "the pile lies where it fell");

            float t0 = Time.time;
            yield return R2TestArena.Until(() => _em.AliveOf(_legion) > 0, 5f);
            Assert.AreEqual(1, _em.AliveOf(_legion), "it stood up");
            Assert.AreEqual(2.5f, Time.time - t0, 0.15f, "2.5 s later");
            Assert.AreEqual(0, _em.PileCount);
            Assert.AreEqual(risen + 1, _em.PilesRisen);
            int r = Risen(at);
            Assert.Less(R2TestArena.Flat(_em.GetPosition(r) - at).magnitude, 0.5f, "where it fell");
            Assert.AreEqual(max0 * 0.5f, _em.GetMaxHp(r), 1e-3f, "half its max HP");
            Assert.AreEqual(_em.GetMaxHp(r), _em.GetHp(r), 1e-3f, "at full (half) HP");
            Assert.AreEqual(1, _em.TimesRevived(r));
            Assert.AreEqual(0, _kills.Count, "standing up isn't a kill either");

            gems = PickupSystem.Instance.ActiveCount;
            _em.ApplyDamage(r, Hit(max0));
            Assert.IsFalse(_em.IsAlive(r));
            Assert.AreEqual(1, _kills.Count, "the second death is the kill");
            Assert.AreEqual(_legion.RuntimeId, _kills[0].EnemyTypeId);
            Assert.AreEqual(2.5f, _kills[0].Xp, 1e-4f, "with its XP");
            Assert.Greater(PickupSystem.Instance.ActiveCount, gems, "its gem drops now");
            Assert.AreEqual(0, _em.PileCount, "no second pile");
            Assert.AreEqual(made + 1, _em.PilesMade);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, _em.AliveOf(_legion), "it stays down");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ABurningLegionnaire_StaysDown()
        {
            yield return Boot();
            var burn = R2TestArena.Load<StatusDef>("Assets/Data/Status/SE_Burn.asset");
            Assert.IsNotNull(burn, "SE_Burn");
            Vector3 spot = R2TestArena.Spot(new Vector3(1.5f, 0f, -7.5f));
            R2TestArena.PutPlayer(spot + new Vector3(-9f, 0f, 3f));
            int burned = _em.PilesBurned;

            int i = _em.Spawn(_legion, spot);
            _em.ApplyStatus(i, burn, 0f);
            Assert.IsTrue(_em.HasStatus(i, StatusKind.Burn));
            _em.ApplyDamage(i, Hit(_em.GetMaxHp(i) * 3f));

            Assert.IsFalse(_em.IsAlive(i));
            Assert.AreEqual(0, _em.PileCount, "no pile");
            Assert.AreEqual(burned + 1, _em.PilesBurned);
            Assert.AreEqual(1, _kills.Count, "burned: the first death is the kill");
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, _em.AliveOf(_legion), "nothing stands up");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AFallenLegionnaire_StaysDown_AndAPileOnADoorThatOpensFalls()
        {
            yield return Boot();
            Vector3 a = R2TestArena.Spot(new Vector3(-1.5f, 0f, -7.5f)), b = R2TestArena.Spot(new Vector3(4f, 0f, -6f));
            R2TestArena.PutPlayer(new Vector3(-8f, 0f, 6f));
            int denied = _em.PilesDenied;

            // executed by a fall (the Hypogeum's trapdoor): the kill, and no pile
            int i = _em.Spawn(_legion, a);
            Assert.IsTrue(_em.Execute(i, EnemyManager.FallSourceId));
            Assert.AreEqual(0, _em.PileCount, "fell: no pile");
            Assert.AreEqual(denied + 1, _em.PilesDenied);
            Assert.AreEqual(1, KillsOf(EnemyManager.FallSourceId), "the fall is the kill");

            // two piles; a door opens under one of them
            int j = _em.Spawn(_legion, a), k = _em.Spawn(_legion, b);
            _em.ApplyDamage(j, Hit(_em.GetMaxHp(j) + 1f));
            _em.ApplyDamage(k, Hit(_em.GetMaxHp(k) + 1f));
            Assert.AreEqual(2, _em.PileCount);
            // a door along Z (yaw 90: its 8 m run north-south) centred on the first pile; the second is 5.5 m east of it
            Assert.AreEqual(1, _em.DropPiles(new Vector2(a.x, a.z), 90f, new Vector2(8f, 4f)), "the pile on the door falls");
            Assert.AreEqual(1, _em.PileCount, "the other one stays");
            Assert.AreEqual(2, KillsOf(EnemyManager.FallSourceId), "and that fall is a kill");
            Assert.AreEqual(denied + 2, _em.PilesDenied);
            Assert.Less(R2TestArena.Flat(_em.GetPilePosition(0) - b).magnitude, 0.5f);

            yield return R2TestArena.Until(() => _em.AliveOf(_legion) > 0, 4f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, _em.AliveOf(_legion), "only the pile off the door stood up");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator APlayerWalkingOverAPile_ScattersIt_AndNothingRises()
        {
            yield return Boot();
            Vector3 pile = R2TestArena.Spot(new Vector3(1.5f, 0f, -7.5f));
            Vector3 start = R2TestArena.Spot(new Vector3(-1.5f, 0f, -7.5f));
            R2TestArena.PutPlayer(start);
            yield return new WaitForSeconds(0.3f); // landed
            int stomped = _em.PilesStomped, risen = _em.PilesRisen;

            int i = _em.Spawn(_legion, pile);
            _em.ApplyDamage(i, Hit(_em.GetMaxHp(i) + 1f));
            Assert.AreEqual(1, _em.PileCount);
            yield return null;
            Assert.AreEqual(1, _em.PileCount, "standing 3 m away doesn't scatter it");

            float end = Time.time + 2f;
            while (Time.time < end && _em.PilesStomped == stomped)
            {
                _in.Next = new PlayerInputFrame { Move = R2TestArena.MoveToward(pile) };
                yield return null;
            }
            _in.Next = default;

            Assert.AreEqual(stomped + 1, _em.PilesStomped, "walked over it: scattered");
            Assert.AreEqual(0, _em.PileCount);
            Assert.AreEqual(1, KillsOf(EnemyManager.StompSourceId), "the stomp is the kill");
            var kill = _kills[_kills.Count - 1];
            Assert.AreEqual(_legion.RuntimeId, kill.EnemyTypeId);
            Assert.AreEqual(2.5f, kill.Xp, 1e-4f, "with its XP");
            Assert.Less(R2TestArena.Flat(kill.Position - pile).magnitude, 0.5f, "at the pile");

            _in.Next = default;
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, _em.AliveOf(_legion), "nothing rises");
            Assert.AreEqual(risen, _em.PilesRisen);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AnEliteLegionnaire_StandsUpWithItsAffix()
        {
            yield return Boot();
            var armored = R2TestArena.Load<AffixDef>("Assets/Data/Enemies/Affixes/AF_Armored.asset");
            Assert.IsNotNull(armored, "AF_Armored");
            Vector3 spot = R2TestArena.Spot(new Vector3(1.5f, 0f, -7.5f));
            R2TestArena.PutPlayer(spot + new Vector3(-9f, 0f, 3f));

            int i = _em.Spawn(_legion, spot, armored);
            Assert.IsTrue(_em.IsElite(i));
            float max0 = _em.GetMaxHp(i);
            Vector3 at = _em.GetPosition(i);
            _em.ApplyDamage(i, Hit(max0 * 20f));
            Assert.AreEqual(1, _em.PileCount, "an elite collapses too (Q7)");
            Assert.AreEqual(0, _kills.Count);

            yield return R2TestArena.Until(() => _em.AliveOf(_legion) > 0, 5f);
            int r = Risen(at);
            Assert.IsTrue(_em.IsElite(r), "still an elite");
            Assert.AreSame(armored, _em.GetAffix(r), "with its affix");
            Assert.AreEqual(max0 * 0.5f, _em.GetMaxHp(r), 1e-3f, "half its (elite) max HP");

            _em.ApplyDamage(r, Hit(max0 * 20f));
            Assert.AreEqual(1, _kills.Count, "the second death is the kill");
            Assert.IsTrue(_kills[0].Elite);
            Assert.AreEqual(2.5f * armored.xpMultiplier, _kills[0].Xp, 1e-3f, "an elite's XP");
            Assert.AreEqual(0, _em.PileCount);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NeverMoreThan48Piles_PastThatTheyStayDown()
        {
            yield return Boot();
            Vector3 c = R2TestArena.Spot(new Vector3(0f, 0f, -7f));
            R2TestArena.PutPlayer(c); // in the middle of the ring: 3 m from every pile
            yield return new WaitForSeconds(0.3f);
            int overflow = _em.PilesOverflow, made = _em.PilesMade, growths = _em.PoolGrowths;

            const int n = 60;
            var slots = new int[n];
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                slots[k] = _em.Spawn(_legion, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (k % 2 == 0 ? 3f : 4f));
                Assert.GreaterOrEqual(slots[k], 0);
            }
            for (int k = 0; k < n; k++) _em.ApplyDamage(slots[k], Hit(_em.GetMaxHp(slots[k]) + 1f));

            Assert.AreEqual(48, EnemyManager.MaxPiles);
            Assert.AreEqual(48, _em.PileCount, "48 piles at most");
            Assert.AreEqual(made + 48, _em.PilesMade);
            Assert.AreEqual(overflow + 12, _em.PilesOverflow, "the other 12 stay down");
            Assert.AreEqual(12, _kills.Count, "and those are kills");

            yield return R2TestArena.Until(() => _em.PileCount == 0, 5f);
            yield return null;
            Assert.AreEqual(48, _em.AliveOf(_legion), "the 48 stood up");
            Assert.AreEqual(growths, _em.PoolGrowths, "from the prewarmed pool");
        }
    }

    /// <summary>The test arena set up for R2's enemy tests (UndeadTests, ChimeraTests): no Director, no level-ups, no
    /// weapons, god mode, the nav obstacles off, scripted input; and the stand-in defs.</summary>
    static class R2TestArena
    {
        public const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        public static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

        public static IEnumerator Boot()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var player = Player.Main;
            player.OverrideSource = new ScriptedInput();
            player.GetComponent<WeaponSystem>().enabled = false;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // the tests place their own enemies
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // kills drop XP: a level-up would pause the clock and the waits never end
            var em = EnemyManager.Instance;
            em.KillAll();
            DevSettings.GodMode = true; // the player stands idle among them; damage is still recorded
            player.GetComponent<PlayerHealth>().invulnerableTime = 0f; // "once" must be the attack's rule, not i-frames
            em.Obstacles.ToArrays(out var circles, out var boxes, out _);
            for (int k = 0; k < circles.Length; k++) em.Obstacles.SetCircleActive(k, false);
            for (int k = 0; k < boxes.Length; k++) em.Obstacles.SetBoxActive(k, false);
        }

        static EnemyDef Base(string id, string model, EnemyDef look)
        {
            var d = ScriptableObject.CreateInstance<EnemyDef>();
            d.id = id;
            d.displayName = id + " (test)";
            d.model = string.IsNullOrEmpty(model) ? null : Load<GameObject>(model);
            if (d.model == null) d.model = look.model; // the R2 model not exported yet: a stand-in body
            d.anim = look.anim;
            d.prewarm = 0;
            d.goldChance = 0f;
            d.speedJitter = 0f;
            return d;
        }

        /// <summary>The Skeletal Legionnaire with ARENAS.md §6's numbers.</summary>
        public static EnemyDef Legionnaire()
        {
            var convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            var d = Base("test_legionnaire", "Assets/Art/Models/Enemies/EN_SkeletalLegionnaire.fbx", convict);
            d.behaviour = EnemyBehaviour.Chaser;
            d.maxHp = 34f; d.moveSpeed = 3.7f; d.radius = 0.36f; d.acceleration = 14f;
            d.contactDamage = 9f; d.contactInterval = 0.9f; d.knockbackTaken = 1f; d.turnRate = 540f; d.xp = 2.5f;
            d.reviveCount = 1; d.reviveDelay = 2.5f; d.reviveHpMul = 0.5f; d.stompRadius = 0.9f; d.burnStopsRevive = true;
            var pile = Load<GameObject>("Assets/Art/Models/Props/PR_BonePile.fbx");
            var mf = pile != null ? pile.GetComponentInChildren<MeshFilter>() : null;
            d.pileMesh = mf != null ? mf.sharedMesh : null; // not exported yet: the piles work, unseen
            return d;
        }

        /// <summary>The Chimera with ARENAS.md §6's numbers, rooted to the spot (moveSpeed 0: the tests place it).</summary>
        public static EnemyDef Chimera()
        {
            var boar = Load<EnemyDef>("Assets/Data/Enemies/EN_WarBoar.asset");
            var d = Base("test_chimera", "Assets/Art/Models/Enemies/EN_Chimera.fbx", boar);
            d.behaviour = EnemyBehaviour.Breather;
            d.maxHp = 420f; d.moveSpeed = 0f; d.radius = 0.95f; d.acceleration = 8f;
            d.contactDamage = 14f; d.contactInterval = 1f; d.knockbackTaken = 0.3f; d.turnRate = 160f;
            d.windup = 1f; d.attackRange = 7f; d.attackRadius = 8f; d.attackDamage = 24f; d.trampleMul = 3f;
            d.breathAngle = 60f; d.breathTime = 1f; d.breathPatches = 3; d.breathPatchRadius = 1.4f; d.breathKnockback = 4f;
            d.zoneDuration = 4f; d.zoneDps = 10f; d.attackCooldown = 4.5f; d.recover = 1.4f; d.xp = 30f;
            return d;
        }

        /// <summary>A rooted Convict with plenty of HP (a target for the breath).</summary>
        public static EnemyDef Dummy(string id, EnemyBehaviour behaviour = EnemyBehaviour.Chaser)
        {
            var convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            var d = Base(id, "", convict);
            d.behaviour = behaviour;
            d.maxHp = 1000f; d.moveSpeed = 0f; d.radius = 0.38f; d.contactDamage = 0f; d.knockbackTaken = 0f; d.xp = 1f;
            return d;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public static Vector3 Ground(Vector3 p)
        {
            var h = EnemyManager.Instance.Height;
            p.y = h != null ? h.Sample(p.x, p.z) : 0f;
            return p;
        }

        /// <summary>`want` on the ground, if the ground round it (2 m) is smooth; else the test can't run here.</summary>
        public static Vector3 Spot(Vector3 want)
        {
            var em = EnemyManager.Instance;
            if (em.EdgeDistance01(want, 2.5f) > 1f || !Smooth(em.Height, want, 2f))
                Assert.Inconclusive("the test arena changed: no open ground at " + want);
            return Ground(want);
        }

        static bool Smooth(HeightGrid h, Vector3 c, float r)
        {
            if (h == null) return true;
            float h0 = h.Sample(c.x, c.z);
            for (float x = -r; x <= r; x += 0.5f)
                for (float z = -r; z <= r; z += 0.5f)
                    if (x * x + z * z <= r * r && Mathf.Abs(h.Sample(c.x + x, c.z + z) - h0) > 0.15f) return false;
            return true;
        }

        public static void PutPlayer(Vector3 p)
        {
            EnemyManager.Instance.ClampInside(ref p, 1.5f);
            Player.Main.GetComponent<PlayerMotor>().Teleport(Ground(p) + Vector3.up * 0.05f);
        }

        /// <summary>The stick input that walks the player toward `target` (camera-relative).</summary>
        public static Vector2 MoveToward(Vector3 target)
        {
            var player = Player.Main;
            Vector3 d = Flat(target - player.transform.position);
            if (d.sqrMagnitude < 1e-4f) return Vector2.zero;
            d.Normalize();
            float yaw = player.ReferenceYaw * Mathf.Deg2Rad, c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector2(d.x * c - d.z * s, d.x * s + d.z * c); // world = x·(cos, −sin) + y·(sin, cos)
        }

        public static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (!done() && Time.time < end) yield return null;
        }
    }
}
