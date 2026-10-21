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
    /// R2 (docs/ARENAS.md §6, docs/tracks/R2-colosseum.md): the Colosseum is a great oval bigger than the Imperial Arena
    /// that keeps the horde in on every bearing and at all six gates; it's locked until the Emperor's Champion falls, then a
    /// run there is tier 5 with its own difficulty and the hugest waves; nothing stands on a trapdoor or a lift; the stage
    /// is walkable at 1.2 m and the hills at 2.2 m, and the horde climbs both; the hypogeum in the scene is the layout's
    /// (30 doors in 6 groups, 8 lifts + the Victor's, the lifts are the boss's guard spots); the Skeletal Legionnaire and
    /// the Chimera come from the table and the lifts raise the design's packs; the gates, boxes, statues, crowd and light
    /// are the contract's. The hypogeum's drops and lifts, the dead's piles, the Chimera's breath and the Eternal Victor
    /// have their own tests (HypogeumTests, UndeadTests, ChimeraTests, EternalVictorTests).
    /// </summary>
    public class ColosseumTests
    {
        const string Scene = "Assets/Scenes/Arenas/Colosseum.unity";
        const string LayoutPath = "Assets/Data/Arenas/AL_Colosseum.asset";
        const string TablePath = "Assets/Data/Arenas/ST_Colosseum.asset";
        const float A = 98f, B = 67f, ChordX = 96.67f;

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static RunDef Colosseum => Load<RunDef>("Assets/Data/Runs/RD_Colosseum.asset");
        static ArenaLayoutDef Layout => Load<ArenaLayoutDef>(LayoutPath);
        static EnemyDef Enemy(string asset) => Load<EnemyDef>("Assets/Data/Enemies/" + asset + ".asset");

        EnemyManager _em;
        PlayerHealth _health;
        PlayerMotor _motor;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        static IEnumerator LoadMenu()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);
        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        static Vector2 Ellipse(float deg) => new Vector2(A * Mathf.Cos(deg * Mathf.Deg2Rad), B * Mathf.Sin(deg * Mathf.Deg2Rad));
        static Vector2 Outward(Vector2 p) => new Vector2(p.x / (A * A), p.y / (B * B)).normalized;

        /// <summary>Distance (XZ) from `p` to a rectangle (centre, yaw of its local X, size along X and Z); 0 inside.</summary>
        static float RectDistance(Vector2 c, float yawDeg, Vector2 size, Vector2 p)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            Vector2 rel = p - c;
            float lx = rel.x * Mathf.Cos(yaw) - rel.y * Mathf.Sin(yaw), lz = rel.x * Mathf.Sin(yaw) + rel.y * Mathf.Cos(yaw);
            float ox = Mathf.Max(0f, Mathf.Abs(lx) - size.x * 0.5f), oz = Mathf.Max(0f, Mathf.Abs(lz) - size.y * 0.5f);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>How far `p` is from the nearest trapdoor or lift (the Victor's included), from the layout's numbers.</summary>
        static float HoleClearance(ArenaLayoutDef l, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var t in l.trapdoors) best = Mathf.Min(best, RectDistance(new Vector2(t.x, t.y), t.z, l.trapdoorSize, p)); // R2-STUB(hypogeum)
            foreach (var q in l.lifts) best = Mathf.Min(best, RectDistance(XZ(q), 0f, Vector2.one * l.liftSize, p)); // R2-STUB(hypogeum)
            if (l.hasCenterLift) best = Mathf.Min(best, RectDistance(XZ(l.centerLift), 0f, Vector2.one * (l.liftSize * l.centerLiftScale), p)); // R2-STUB(hypogeum)
            return best;
        }

        /// <summary>Transforms named `name` in the scene (an FBX root and its mesh child share a name: the child is skipped).</summary>
        static List<Transform> Named(string name)
        {
            var o = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                if (t.name != name) continue;
                if (t.parent != null && t.parent.name == t.name) continue;
                o.Add(t);
            }
            return o;
        }

        /// <summary>The arena with no Director, no level-ups and the hypogeum halted: the test stages everything.</summary>
        IEnumerator Boot(bool god)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = god;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            if (Hypogeum.Instance != null) Hypogeum.Instance.Halt(); // R2-STUB(hypogeum): no drops or lift waves
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().RemoveAll();
            _em = EnemyManager.Instance;
            _em.KillAll();
            _health = Player.Main.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _motor = Player.Main.GetComponent<PlayerMotor>();
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheGreatOval_IsBiggerThanTheImperialArena_AndKeepsTheHordeIn()
        {
            var colosseum = Layout;
            var imperial = Load<ArenaLayoutDef>("Assets/Data/Arenas/AL_ImperialArena.asset");
            Assert.AreEqual(90, colosseum.outline.Length, "the ellipse every 3.75° less the great gates' spans, plus their chords");
            Debug.Log($"[ColosseumTests] play area {colosseum.PlayArea():0} m² vs the Imperial Arena's {imperial.PlayArea():0}");
            Assert.AreEqual(20005f, colosseum.PlayArea(), 30f);
            Assert.Greater(colosseum.PlayArea(), imperial.PlayArea() * 1.25f, "every arena is bigger than the one before it");
            Assert.GreaterOrEqual(colosseum.PlayArea(), 17500f, "the GDD's floor for Arena 6");
            Assert.AreEqual(5, colosseum.tier);
            yield return Boot(true);
            Assert.IsTrue(_em.HasOutline);
            // the play edge is the wall line less 1.1 m: on 22 bearings at the wall line's corners (every 15°)...
            for (int k = 1; k < 24; k++)
            {
                if (k == 12) continue; // the Gate of Life's chord: below
                Vector2 p = Ellipse(15f * k), n = Outward(p);
                Assert.LessOrEqual(_em.EdgeDistance01(V3(p - n * 1.8f)), 1f, "the floor at t = " + 15 * k + "°");
                Assert.Greater(_em.EdgeDistance01(V3(p - n * 0.5f)), 1f, "the podium at t = " + 15 * k + "°");
            }
            // ...and at the great gates' chords (x = ±96.67)
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(94.9f, 0f, 0f)), 1f, "in front of the Gate of Death");
            Assert.Greater(_em.EdgeDistance01(new Vector3(96.2f, 0f, 0f)), 1f, "in the Gate of Death");
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(-94.9f, 0f, 8f)), 1f, "in front of the Gate of Life");
            Assert.Greater(_em.EdgeDistance01(new Vector3(-96.2f, 0f, -8f)), 1f, "in the Gate of Life");

            // convicts behind the stands on 24 bearings and in all six gates' tunnels are pulled back in
            var convict = Enemy("EN_Convict");
            var outside = new List<Vector3>();
            for (int k = 0; k < 24; k++)
            {
                Vector2 p = Ellipse(7.5f + 15f * k);
                outside.Add(V3(p + Outward(p) * 3f));
            }
            var gates = ArenaGates.Instance;
            Assert.AreEqual(6, gates.Count);
            for (int g = 0; g < gates.Count; g++) outside.Add(Flat(gates.Position(g) - gates.Inward(g) * 2f));
            var slots = new int[outside.Count];
            for (int i = 0; i < outside.Count; i++) slots[i] = _em.Spawn(convict, outside[i]);
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < slots.Length; i++)
                Assert.LessOrEqual(_em.EdgeDistance01(_em.GetPosition(slots[i])), 1.001f, "pulled back in from " + outside[i]);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LockedUntilTheEmperorsChampionFalls_ThenARunThereIsTier5_WithItsOwnDifficulty_AndTheHugestWaves()
        {
            SaveSandbox.Wipe();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsNotNull(Colosseum, "RD_Colosseum exists (Build Colosseum)");
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Colosseum), "locked on a fresh save");
            Assert.IsFalse(menu.PickArena(Colosseum));

            MetaProgress.CommitRun(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "patron", BossDefeatedHp01 = 0.5f, GladiatorId = "nobody" }, menu.meta);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Colosseum), "the Patron isn't enough");
            MetaProgress.CommitRun(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "emperors_champion", BossDefeatedHp01 = 0.5f, GladiatorId = "nobody" }, menu.meta);
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Colosseum), "beating the Emperor's Champion earns it");
            // tier 5: Renown × 3.5 (MetaConfig.deeperArenaBonus 0.5 per tier)
            var sample = new RunSummary { Outcome = RunOutcome.Died, Elapsed = 480f, Kills = 2000 };
            int baseRenown = menu.meta.RenownFor(sample);
            Assert.Greater(baseRenown, 0);
            sample.ArenaIndex = 5;
            Assert.AreEqual(baseRenown * 3.5f, menu.meta.RenownFor(sample), 2f, "Renown × 3.5 (each side rounds)");

            yield return LoadMenu();
            menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Invoke(menu, "OpenSelect");
            yield return null;
            Invoke(menu, "StartRun");
            yield return null;
            Assert.IsTrue(menu.PickArena(Colosseum), "the menu offers it (rebuild the main menu after the arena's setup)");
            Invoke(menu, "Launch");
            float end = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != "Colosseum") yield return null;
            yield return null;
            Assert.AreEqual("Colosseum", SceneManager.GetActiveScene().name);
            Assert.AreSame(Colosseum, GameSession.Run);

            Assert.AreEqual(5, RunState.Current.arenaTier);
            Assert.AreEqual(2.55f, EnemyManager.Instance.hpMul, 1e-4f, "+155 % enemy HP");
            Assert.AreEqual(1.55f, RunState.Current.playerDamageTakenMul, 1e-4f, "+55 % damage taken");
            var director = Director.Instance;
            Assert.AreSame(Load<SpawnTableDef>(TablePath), director.table);
            Assert.AreEqual(450, director.maxAliveOverride, "huge waves, inside §8's ~500 at peak");
            Assert.AreEqual(1.3f, director.arenaDensity, 1e-4f);
            Assert.AreEqual(1.5f, director.swarmMultiplier, 1e-4f, "swarms × 1.5"); // R2-STUB(hypogeum)
            Assert.AreEqual(55f, director.maxSpawnDistance, 1e-4f, "the horde comes from your end of the oval");
            Assert.AreEqual(60f, director.eliteEveryOverride, 1e-4f);
            Assert.AreEqual(6, ArenaGates.Instance.Count, "the Gates of Life and Death and four side gates");
            Assert.IsNotNull(Hypogeum.Instance, "the floor over the hypogeum"); // R2-STUB(hypogeum)
            Assert.AreEqual("eternal_victor", Object.FindAnyObjectByType<BossFight>().boss.id);

            RunState.Current.End(RunOutcome.Died);
            yield return null;
            Assert.AreEqual(5, MetaProgress.Data.deepestArena, "the save knows the player reached tier 5");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NothingStandsOnATrapdoorOrALift_NorWithinAMetreOfOne()
        {
            const float pad = 1f;
            var l = Layout;
            Assert.AreEqual(30, l.trapdoors.Length); // R2-STUB(hypogeum)
            Assert.AreEqual(8, l.lifts.Length); // R2-STUB(hypogeum)
            yield return Boot(true);
            var props = Interactables.Instance;
            Assert.AreEqual(18, props.chestAnchors.Length);
            Assert.AreEqual(3, props.altarAnchors.Length);
            foreach (var p in props.chestAnchors) Assert.Greater(HoleClearance(l, XZ(p)), pad, "a chest at " + p);
            foreach (var p in props.altarAnchors) Assert.Greater(HoleClearance(l, XZ(p)), pad, "an altar at " + p);
            Assert.Greater(HoleClearance(l, XZ(l.playerSpawn)), pad, "the player spawn");
            var gates = ArenaGates.Instance;
            for (int g = 0; g < gates.Count; g++)
                foreach (float depth in new[] { gates.spawnDepth.x, (gates.spawnDepth.x + gates.spawnDepth.y) * 0.5f, gates.spawnDepth.y })
                    foreach (float side in new[] { -gates.spawnSpread, 0f, gates.spawnSpread })
                    {
                        Vector3 p = gates.Position(g) + gates.Inward(g) * depth + gates.gates[g].right * side;
                        Assert.Greater(HoleClearance(l, XZ(p)), pad, "gate " + g + "'s mouth at " + p);
                    }

            // the footprints in the scene: stairs 3 × 2 (origin at the low edge, rising toward local -Z), hills 14 × 10,
            // statues a circle of r 1.1; points every 0.25 m round each and its centre
            void Footprint(string what, Vector2 c, Vector2 ax, Vector2 az, float sx, float sz)
            {
                Assert.Greater(HoleClearance(l, c), pad, what + " at " + c);
                int nx = Mathf.CeilToInt(sx / 0.25f), nz = Mathf.CeilToInt(sz / 0.25f);
                for (int i = 0; i <= nx; i++)
                    foreach (float e in new[] { -0.5f, 0.5f })
                    {
                        Vector2 p = c + ax * (sx * (i / (float)nx - 0.5f)) + az * (sz * e);
                        Assert.Greater(HoleClearance(l, p), pad, what + "'s edge at " + p);
                    }
                for (int i = 0; i <= nz; i++)
                    foreach (float e in new[] { -0.5f, 0.5f })
                    {
                        Vector2 p = c + az * (sz * (i / (float)nz - 0.5f)) + ax * (sx * e);
                        Assert.Greater(HoleClearance(l, p), pad, what + "'s edge at " + p);
                    }
            }
            var stairs = Named("SM_ColStairs");
            Assert.AreEqual(4, stairs.Count);
            foreach (var t in stairs) Footprint("stairs", XZ(t.position - t.forward), XZ(t.right).normalized, XZ(t.forward).normalized, 3f, 2f);
            var hills = Named("SM_ColMons_A");
            hills.AddRange(Named("SM_ColMons_B"));
            Assert.AreEqual(4, hills.Count);
            foreach (var t in hills)
            {
                Footprint("hill", XZ(t.position), XZ(t.right).normalized, XZ(t.forward).normalized, 14f, 10f);
                foreach (var d in l.trapdoors) // R2-STUB(hypogeum): and no door hidden wholly under one
                {
                    Vector3 rel = new Vector3(d.x, 0f, d.y) - Flat(t.position);
                    Assert.IsFalse(Mathf.Abs(Vector3.Dot(rel, t.right)) < 7f && Mathf.Abs(Vector3.Dot(rel, t.forward)) < 5f, "a trapdoor under the hill at " + t.position);
                }
            }
            var statues = Named("PR_VictorStatue");
            Assert.AreEqual(4, statues.Count);
            foreach (var t in statues)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    Vector2 p = XZ(t.position) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.1f;
                    Assert.Greater(HoleClearance(l, p), pad, "a statue's plinth at " + p);
                }

            // no collider (piece, prop, gate, invisible wall) over any door or lift + 1 m: a body-sized sphere at 0.55-1.45 m
            // every 0.5 m (the invisible floors over the holes are below it; the Victor's lift is inside the stage)
            var hits = new Collider[8];
            int checks = 0;
            void Sweep(Vector2 c, float yawDeg, Vector2 size)
            {
                float yaw = yawDeg * Mathf.Deg2Rad;
                Vector2 ax = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)), az = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                for (float x = -size.x * 0.5f - pad; x <= size.x * 0.5f + pad + 1e-3f; x += 0.5f)
                    for (float z = -size.y * 0.5f - pad; z <= size.y * 0.5f + pad + 1e-3f; z += 0.5f)
                    {
                        Vector3 q = V3(c + ax * x + az * z, 1f);
                        checks++;
                        int n = Physics.OverlapSphereNonAlloc(q, 0.45f, hits, Layers.SolidMask, QueryTriggerInteraction.Ignore);
                        if (n > 0) Assert.Fail($"{hits[0].name} ({hits[0].transform.root.name}) stands over the hypogeum at {q}");
                    }
            }
            foreach (var t in l.trapdoors) Sweep(new Vector2(t.x, t.y), t.z, l.trapdoorSize); // R2-STUB(hypogeum)
            foreach (var q in l.lifts) Sweep(XZ(q), 0f, Vector2.one * l.liftSize); // R2-STUB(hypogeum)
            Assert.Greater(checks, 9000);
        }

        const float StageTop = 1.3f, HillTop = 2.3f;

        [UnityTest, Timeout(60000)]
        public IEnumerator TheStage_IsWalkableAtOnePointTwo_TheHillsAtTwoPointTwo_AndTheHordeClimbsThem()
        {
            yield return Boot(true);
            var h = _em.Height;
            Assert.IsNotNull(h);
            Assert.AreEqual(1.2f, h.Sample(0f, 6f), 0.08f, "the stage");
            Assert.AreEqual(1.2f, h.Sample(-6f, 0f), 0.08f, "the stage");
            Assert.AreEqual(1.2f, h.Sample(0f, 0f), 0.08f, "the Victor's lift, flush with the stage");
            Assert.AreEqual(0f, h.Sample(0f, -16f), 0.08f, "the floor");
            Assert.AreEqual(0f, h.Sample(32f, 0f), 0.08f, "a closed trapdoor: the invisible floor over its hole");
            Assert.AreEqual(0f, h.Sample(34f, 20f), 0.08f, "a lift at rest");
            Assert.AreEqual(2.2f, h.Sample(28f, 50.5f), 0.1f, "hill A (north)");
            Assert.AreEqual(2.2f, h.Sample(28f, -50.5f), 0.1f, "hill A (south)");
            Assert.AreEqual(2.2f, h.Sample(-28f, 50.5f), 0.1f, "hill B (north)");
            Assert.AreEqual(2.2f, h.Sample(-28f, -50.5f), 0.1f, "hill B (south)");
            float foot = h.Sample(0f, 12.6f), mid = h.Sample(0f, 11f), top = h.Sample(0f, 10.2f);
            Debug.Log($"[ColosseumTests] the north stairs: foot {foot:0.00}, middle {mid:0.00}, top {top:0.00}");
            Assert.Less(foot, 0.35f, "the stairs' foot (z 12)");
            Assert.Greater(mid, 0.2f, "half way up");
            Assert.Less(mid, 1.1f, "half way up");
            Assert.Greater(top, 0.9f, "the stage's edge (z 10)");

            // you stand on the stage; a convict on the floor north of it comes up
            _motor.Teleport(new Vector3(0f, StageTop, 6f));
            yield return null;
            var convict = Enemy("EN_Convict");
            int slot = _em.Spawn(convict, new Vector3(0f, 0f, 22f));
            float end = Time.time + 10f;
            while (Time.time < end && _em.IsAlive(slot) && !(_em.GetPosition(slot).y > 1f && Flat(_em.GetPosition(slot)).magnitude < 9.5f)) yield return null;
            Assert.IsTrue(_em.IsAlive(slot));
            Vector3 at = _em.GetPosition(slot);
            Assert.Greater(at.y, 1f, "up on the stage: " + at);
            Assert.Less(Flat(at).magnitude, 9.5f, "on the stage: " + at);
            _em.KillAll();

            // you stand on hill A's top; a convict below its south slope (its rock face is on the wall's side) comes up
            _motor.Teleport(new Vector3(28f, HillTop, 50.5f));
            yield return null;
            slot = _em.Spawn(convict, new Vector3(28f, 0f, 43.5f));
            end = Time.time + 10f;
            while (Time.time < end && _em.IsAlive(slot) && !(_em.GetPosition(slot).y > 2f)) yield return null;
            Assert.IsTrue(_em.IsAlive(slot));
            at = _em.GetPosition(slot);
            Assert.Greater(at.y, 2f, "up on the hill: " + at);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheHypogeum_IsTheLayouts_AndTheBossGuardSpotsAreTheLifts()
        {
            var l = Layout;
            // R2-STUB(hypogeum): every field below is the hypogeum part's
            Assert.AreEqual(30, l.trapdoors.Length);
            var perGroup = new int[6];
            foreach (var t in l.trapdoors) perGroup[(int)t.w]++;
            CollectionAssert.AreEqual(new[] { 6, 6, 6, 6, 3, 3 }, perGroup, "a broken ring of 24 in four quarters, three either side of the stage");
            Assert.AreEqual(new Vector2(8f, 4f), l.trapdoorSize);
            Assert.AreEqual("Environment/SM_ColTrapdoor", l.trapdoorModel);
            Assert.AreEqual(8, l.lifts.Length);
            Assert.AreEqual(4f, l.liftSize);
            Assert.AreEqual("Environment/SM_ColLift", l.liftModel);
            Assert.IsTrue(l.hasCenterLift);
            Assert.AreEqual(new Vector3(0f, 1.2f, 0f), l.centerLift);
            Assert.AreEqual(1.25f, l.centerLiftScale, 1e-4f);
            Assert.AreEqual(40f, l.dropFirst);
            Assert.AreEqual(14f, l.dropEvery);
            Assert.AreEqual(2f, l.dropTelegraph);
            Assert.AreEqual(4f, l.dropOpen);
            Assert.AreEqual(90f, l.liftFirst);
            Assert.AreEqual(45f, l.liftEvery);
            Assert.AreEqual(3, l.liftsPerWave);
            Assert.AreEqual(12f, l.liftMinDistance);
            Assert.AreEqual(1.5f, l.swarmMul, 1e-4f);
            CollectionAssert.AreEqual(l.lifts, l.guardSpots, "his dead rise on the lifts");
            // no two doors overlap; the closest two (on the axis) have 4 m of deck between them
            float closest = float.MaxValue;
            for (int i = 0; i < l.trapdoors.Length; i++)
                for (int j = 0; j < l.trapdoors.Length; j++)
                {
                    if (i == j) continue;
                    var b = l.trapdoors[j];
                    float yaw = b.z * Mathf.Deg2Rad;
                    Vector2 ax = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)) * 4f, az = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * 2f;
                    Vector2 c = new Vector2(b.x, b.y);
                    foreach (var corner in new[] { c - ax - az, c + ax - az, c + ax + az, c - ax + az, c - az, c + az, c - ax, c + ax })
                        closest = Mathf.Min(closest, RectDistance(new Vector2(l.trapdoors[i].x, l.trapdoors[i].y), l.trapdoors[i].z, l.trapdoorSize, corner));
                }
            Assert.AreEqual(4f, closest, 0.05f, "the axis doors' 4 m of deck");

            yield return Boot(true);
            var hyp = Hypogeum.Instance;
            Assert.IsNotNull(hyp, "the layout's trapdoors add the hypogeum");
            Assert.AreEqual(30, hyp.Count);
            Assert.AreEqual(8, hyp.LiftCount);
            for (int d = 0; d < l.trapdoors.Length; d++) Assert.AreEqual((int)l.trapdoors[d].w, hyp.GroupOf(d), "door " + d + "'s group");
            var fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(fight);
            CollectionAssert.AreEqual(l.lifts, fight.controller.guardSpots);

            // the models (the hook names them Trapdoor<d>, Lift<l>, CenterLift): flush at every door with its yaw, at
            // every lift, the Victor's in the stage at 5 × 5 m; each the real model (its moving parts), not a stand-in
            Assert.AreEqual(30, hyp.doors.Length);
            for (int d = 0; d < l.trapdoors.Length; d++)
            {
                var t = l.trapdoors[d];
                var m = hyp.doors[d];
                Assert.IsNotNull(m, "door " + d);
                Assert.Less(Vector3.Distance(m.position, new Vector3(t.x, 0f, t.y)), 0.05f, "door " + d + " at its hole");
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(m.eulerAngles.y, t.z)), 0.5f, "door " + d + "'s yaw");
                Assert.IsTrue(HasChild(m, "LeafF") && HasChild(m, "LeafB"), "door " + d + " is SM_ColTrapdoor (LeafF / LeafB)");
            }
            Assert.AreEqual(8, hyp.lifts.Length);
            for (int k = 0; k < l.lifts.Length; k++)
            {
                Assert.IsNotNull(hyp.lifts[k], "lift " + k);
                Assert.Less(Vector3.Distance(hyp.lifts[k].position, l.lifts[k]), 0.05f, "lift " + k + " at its hole");
                Assert.IsTrue(HasChild(hyp.lifts[k], "Platform"), "lift " + k + " is SM_ColLift (Platform)");
            }
            Assert.IsNotNull(hyp.centerLift, "the Victor's lift");
            Assert.Less(Vector3.Distance(hyp.centerLift.position, new Vector3(0f, 1.2f, 0f)), 0.05f, "in the stage, flush with its top");
            Assert.AreEqual(1.25f, hyp.centerLift.lossyScale.x, 0.01f, "5 × 5 m");
        }

        static bool HasChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return true;
            return false;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ALegionnaireAndAChimera_ComeFromTheTable_AndTheLiftsRaiseTheDesignsPacks()
        {
            var table = Load<SpawnTableDef>(TablePath);
            Assert.IsNotNull(table);
            // docs/ARENAS.md §6 "Spawn table": (asset, from, weight start, end, cap, swarm, elite)
            var want = new (string asset, float from, float w0, float w1, int cap, bool swarm, bool elite)[]
            {
                ("EN_Convict", 0f, 6f, 1.5f, 0, true, true),
                ("EN_PitCur", 5f, 3f, 3f, 100, true, true),
                ("EN_ShieldThug", 20f, 1.2f, 1.0f, 24, false, true),
                ("EN_ShieldArcher", 30f, 1.0f, 1.2f, 12, false, true),
                ("EN_SkeletalLegionnaire", 45f, 0.8f, 6.0f, 160, true, true),
                ("EN_Assassin", 50f, 1.0f, 1.3f, 14, false, true),
                ("EN_Murmillo", 50f, 0.6f, 0.8f, 10, false, true),
                ("EN_NetThrower", 60f, 0.7f, 1.0f, 10, false, true),
                ("EN_Centurion", 75f, 0.12f, 0.2f, 2, false, false),
                ("EN_Brute", 75f, 0.6f, 1.0f, 10, false, true),
                ("EN_Sorcerer", 90f, 0.6f, 1.0f, 10, false, true),
                ("EN_Chimera", 240f, 0.08f, 0.15f, 2, false, false),
            };
            Assert.AreEqual(want.Length, table.entries.Length, "12 types: fewer than the Imperial Arena's 13");
            foreach (var w in want)
            {
                var def = Enemy(w.asset);
                Assert.IsNotNull(def, w.asset);
                bool found = false;
                foreach (var e in table.entries)
                {
                    if (e.enemy != def) continue;
                    found = true;
                    Assert.AreEqual(w.from, e.from, w.asset);
                    Assert.AreEqual(w.w0, e.weightStart, 1e-4f, w.asset);
                    Assert.AreEqual(w.w1, e.weightEnd, 1e-4f, w.asset);
                    Assert.AreEqual(w.cap, e.maxAlive, w.asset);
                    Assert.AreEqual(w.swarm, e.swarm, w.asset);
                    Assert.AreEqual(w.elite, e.elite, w.asset);
                }
                Assert.IsTrue(found, w.asset + " is in ST_Colosseum");
            }
            foreach (var e in table.entries)
                Assert.IsFalse(e.enemy.id == "slinger" || e.enemy.id == "firepot" || e.enemy.id == "mercenary" || e.enemy.id == "beasthandler", e.enemy.id + " isn't in the Colosseum");
            Assert.Greater(table.affixes.Length, 0);

            var legionnaire = Enemy("EN_SkeletalLegionnaire");
            Assert.AreEqual("skeletal_legionnaire", legionnaire.id);
            Assert.AreEqual(EnemyBehaviour.Chaser, legionnaire.behaviour);
            Assert.AreEqual(34f, legionnaire.maxHp);
            Assert.AreEqual(3.7f, legionnaire.moveSpeed, 1e-4f);
            Assert.AreEqual(0.36f, legionnaire.radius, 1e-4f);
            Assert.AreEqual(2.5f, legionnaire.xp, 1e-4f);
            Assert.AreEqual(140, legionnaire.prewarm);
            Assert.AreEqual(1, legionnaire.reviveCount); // R2-STUB(enemies)
            Assert.AreEqual(2.5f, legionnaire.reviveDelay, 1e-4f); // R2-STUB(enemies)
            Assert.AreEqual(0.5f, legionnaire.reviveHpMul, 1e-4f); // R2-STUB(enemies)
            Assert.AreEqual(0.9f, legionnaire.stompRadius, 1e-4f); // R2-STUB(enemies)
            Assert.IsTrue(legionnaire.burnStopsRevive); // R2-STUB(enemies)
            Assert.IsNotNull(legionnaire.pileMesh, "PR_BonePile's mesh"); // R2-STUB(enemies)
            Assert.IsNotNull(legionnaire.model, "EN_SkeletalLegionnaire.fbx");
            var chimera = Enemy("EN_Chimera");
            Assert.AreEqual("chimera", chimera.id);
            Assert.AreEqual(EnemyBehaviour.Breather, chimera.behaviour); // R2-STUB(enemies)
            Assert.AreEqual(420f, chimera.maxHp);
            Assert.AreEqual(3.0f, chimera.moveSpeed, 1e-4f);
            Assert.AreEqual(0.95f, chimera.radius, 1e-4f);
            Assert.AreEqual(160f, chimera.turnRate, "slow to turn: flank it");
            Assert.AreEqual(1.0f, chimera.windup, 1e-4f);
            Assert.AreEqual(7f, chimera.attackRange, 1e-4f);
            Assert.AreEqual(8f, chimera.attackRadius, 1e-4f, "the cone's length");
            Assert.AreEqual(24f, chimera.attackDamage, 1e-4f);
            Assert.AreEqual(3f, chimera.trampleMul, 1e-4f, "it burns the horde × 3");
            Assert.AreEqual(60f, chimera.breathAngle, 1e-4f); // R2-STUB(enemies)
            Assert.AreEqual(1.0f, chimera.breathTime, 1e-4f); // R2-STUB(enemies)
            Assert.AreEqual(3, chimera.breathPatches); // R2-STUB(enemies)
            Assert.AreEqual(1.4f, chimera.breathPatchRadius, 1e-4f); // R2-STUB(enemies)
            Assert.AreEqual(4f, chimera.breathKnockback, 1e-4f); // R2-STUB(enemies)
            Assert.AreEqual(4.5f, chimera.attackCooldown, 1e-4f);
            Assert.AreEqual(1.4f, chimera.recover, 1e-4f, "the window");
            Assert.AreEqual(30f, chimera.xp, 1e-4f);
            Assert.IsNotNull(chimera.model, "EN_Chimera.fbx");

            // docs/ARENAS.md §6 "Lift packs": (enemy, from, to (0 = the end), weight, count, cap, call)
            var packs = Layout.liftPacks; // R2-STUB(hypogeum)
            Assert.AreEqual(4, packs.Length);
            var boar = Enemy("EN_WarBoar");
            var wantPacks = new (EnemyDef enemy, float from, float to, float weight, Vector2Int count, int cap, string call)[]
            {
                (Enemy("EN_PitCur"), 90f, 240f, 3f, new Vector2Int(4, 6), 100, "THE BEASTS RISE!"),
                (boar, 150f, 0f, 2f, new Vector2Int(2, 3), 12, "THE BEASTS RISE!"),
                (chimera, 240f, 0f, 1f, new Vector2Int(1, 1), 2, "A CHIMERA!"),
                (legionnaire, 270f, 0f, 3f, new Vector2Int(5, 7), 160, "THE DEAD RISE!"),
            };
            for (int k = 0; k < wantPacks.Length; k++)
            {
                var w = wantPacks[k];
                Assert.IsNotNull(w.enemy);
                Assert.AreSame(w.enemy, packs[k].enemy, "pack " + k); // R2-STUB(hypogeum): ArenaLayoutDef.LiftPack's fields
                Assert.AreEqual(w.from, packs[k].from, "pack " + k);
                Assert.AreEqual(w.to, packs[k].to, "pack " + k);
                Assert.AreEqual(w.weight, packs[k].weight, 1e-4f, "pack " + k);
                Assert.AreEqual(w.count, packs[k].count, "pack " + k);
                Assert.AreEqual(w.cap, packs[k].maxAlive, "pack " + k);
                Assert.AreEqual(w.call, packs[k].call, "pack " + k);
            }

            yield return Boot(true);
            _motor.Teleport(new Vector3(0f, 0.1f, -26f));
            yield return null;
            int l = _em.Spawn(legionnaire, new Vector3(-10f, 0f, -36f));
            int c = _em.Spawn(chimera, new Vector3(10f, 0f, -36f));
            yield return null;
            Assert.IsTrue(_em.IsAlive(l));
            Assert.IsTrue(_em.IsAlive(c));
            Assert.AreEqual(1, _em.AliveOf(legionnaire));
            Assert.AreEqual(1, _em.AliveOf(chimera));
            Assert.AreEqual(34f * 2.55f, _em.GetMaxHp(l), 0.01f, "× the arena's HP");
            Assert.AreEqual(420f * 2.55f, _em.GetMaxHp(c), 0.01f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheGatesBoxesStatuesAndCrowd_AreTheContracts_AndTheLightIsLateAfternoon()
        {
            yield return Boot(true);
            var fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(fight);
            Assert.AreEqual(0, fight.gate, "the Gate of Death: the fallback");
            Assert.AreEqual("eternal_victor", fight.boss.id);
            Assert.IsTrue(fight.boss.entranceRise, "he rises from the stage"); // R2-STUB(boss)
            Assert.Contains(Enemy("EN_SkeletalLegionnaire"), fight.boss.guards, "his guards are the dead");

            var gates = ArenaGates.Instance;
            Assert.AreEqual(6, gates.Count);
            Assert.AreEqual("SM_ColGreatGate", gates.gates[0].name, "G0, the Gate of Death");
            Assert.AreEqual(ChordX, gates.Position(0).x, 0.05f);
            Assert.AreEqual(0f, gates.Position(0).z, 0.05f);
            Assert.Less(gates.Inward(0).x, -0.99f, "facing west, into the oval");
            Assert.AreEqual("SM_ColGreatGate", gates.gates[3].name, "G3, the Gate of Life");
            Assert.AreEqual(-ChordX, gates.Position(3).x, 0.05f);
            Assert.AreEqual(0f, gates.Position(3).z, 0.05f);
            Assert.Greater(gates.Inward(3).x, 0.99f, "facing east");
            var side = new[] { (1, 60f), (2, 120f), (4, 240f), (5, 300f) };
            foreach (var (g, t) in side)
            {
                Vector2 p = Ellipse(t);
                Assert.AreEqual("SM_ColGate", gates.gates[g].name, "G" + g);
                Assert.Less(Vector2.Distance(XZ(gates.Position(g)), p), 0.05f, "G" + g + " on the wall line at t = " + t + "°");
                Assert.Greater(Vector2.Dot(XZ(gates.Inward(g)).normalized, -Outward(p)), 0.999f, "G" + g + " faces in");
            }

            var boxes = Named("SM_ColPulvinar");
            Assert.AreEqual(2, boxes.Count, "the Emperor's and the editor's");
            foreach (var b in boxes)
            {
                Assert.AreEqual(0f, b.position.x, 0.05f);
                Assert.AreEqual(67f, Mathf.Abs(b.position.z), 0.05f, "on the podium at the apex");
                Assert.Less(b.forward.z * Mathf.Sign(b.position.z), -0.99f, "facing the stage");
            }
            for (int k = 0; k < 6; k++) Assert.AreEqual(1, Named("SM_ColCavea_" + k).Count, "cavea run " + k);
            Assert.AreEqual(1, Named("SM_ColStage").Count);
            Assert.AreEqual(1, Named("SM_ColFloor").Count);
            var statues = Named("PR_VictorStatue");
            Assert.AreEqual(4, statues.Count);
            foreach (var s in statues)
            {
                Assert.AreEqual(14f, Flat(s.position).magnitude, 0.05f, "on the diagonals at r 14");
                Assert.Greater(Vector3.Dot(s.forward, Flat(s.position).normalized), 0.99f, "facing outward");
            }

            var crowd = Object.FindAnyObjectByType<CrowdRenderer>();
            Assert.IsNotNull(crowd);
            Debug.Log($"[ColosseumTests] crowd {crowd.spots.Length}");
            Assert.Greater(crowd.spots.Length, 3300, "the loudest yet (the Imperial Arena: ~2,200)");
            Assert.Less(crowd.spots.Length, 4000);
            float top = 0f;
            foreach (var s in crowd.spots) top = Mathf.Max(top, s.y);
            Assert.AreEqual(13.9f, top, 0.05f, "maenianum III's top tier");

            var look = Object.FindAnyObjectByType<EnvironmentLighting>();
            Assert.IsNotNull(look);
            Assert.AreEqual(90f, look.fogStart);
            Assert.AreEqual(380f, look.fogEnd);
            Assert.IsNotNull(look.sun);
            Vector3 e = look.sun.transform.eulerAngles;
            Assert.AreEqual(32f, e.x, 0.1f, "a low sun");
            Assert.AreEqual(0f, Mathf.DeltaAngle(e.y, -40f), 0.1f);
        }
    }
}
