using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.16 (docs/ARENAS.md §5): the Imperial Arena is a hippodrome bigger than the Grand Arena that keeps the horde
    /// in along the carceres, both long sides, the sphendone's curve and at the triumphal gate; it's locked until the
    /// Gilded Patron falls, then a run there is tier 4 with its own difficulty and the biggest horde; nothing stands on the
    /// chariot track and a race comes out of the carceres' open bays; the spina is walkable at 1.2 m and the horde climbs
    /// its stairs; a Centurion from the table marches behind his line of five Praetorians, and killing him breaks it; the
    /// boss's gate is the triumphal gate, and the Emperor's Champion comes through it.
    /// </summary>
    public class ImperialArenaTests
    {
        const string Scene = "Assets/Scenes/Arenas/ImperialArena.unity";
        const string LayoutPath = "Assets/Data/Arenas/AL_ImperialArena.asset";
        const string TablePath = "Assets/Data/Arenas/ST_ImperialArena.asset";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static RunDef Imperial => Load<RunDef>("Assets/Data/Runs/RD_ImperialArena.asset");
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

        /// <summary>The arena with no Director, no level-ups and the races halted: the test stages everything.</summary>
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
            if (ChariotRace.Instance != null) ChariotRace.Instance.Halt(); // StartNow lifts it
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
        public IEnumerator TheHippodrome_IsBiggerThanTheGrandArena_AndKeepsTheHordeIn()
        {
            var imperial = Load<ArenaLayoutDef>(LayoutPath);
            var grand = Load<ArenaLayoutDef>("Assets/Data/Arenas/AL_GrandArena.asset");
            Assert.AreEqual(34, imperial.outline.Length, "two long sides, the carceres, 2 × 15 chords round the sphendone and the triumphal gate's");
            Debug.Log($"[ImperialArenaTests] play area {imperial.PlayArea():0} m² vs the Grand Arena's {grand.PlayArea():0}");
            Assert.AreEqual(15302f, imperial.PlayArea(), 30f);
            Assert.Greater(imperial.PlayArea(), grand.PlayArea() * 1.25f, "every arena is bigger than the one before it");
            Assert.GreaterOrEqual(imperial.PlayArea(), 13500f, "the GDD's floor for Arena 5");
            Assert.AreEqual(4, imperial.tier);
            yield return Boot(true);
            Assert.IsTrue(_em.HasOutline);
            // the play edge is the wall line less 1.1 m: the carceres at x = -95, the long sides at z = ±44, the
            // sphendone r 44 round (51, 0), the triumphal gate's chord at x = 93.73
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(-92.5f, 0f, 0f)), 1f, "in front of the carceres");
            Assert.Greater(_em.EdgeDistance01(new Vector3(-94.6f, 0f, 0f)), 1f, "in the carceres");
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(0f, 0f, 42.2f)), 1f, "the north field");
            Assert.Greater(_em.EdgeDistance01(new Vector3(0f, 0f, 43.6f)), 1f, "the north stands");
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(30f, 0f, -42.2f)), 1f, "the south field");
            Assert.Greater(_em.EdgeDistance01(new Vector3(30f, 0f, -43.6f)), 1f, "the south stands");
            float c = Mathf.Cos(45f * Mathf.Deg2Rad);
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(51f + 41.8f * c, 0f, 41.8f * c)), 1f, "the sphendone's floor");
            Assert.Greater(_em.EdgeDistance01(new Vector3(51f + 44f * c, 0f, 44f * c)), 1f, "the sphendone's stands");
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(91.8f, 0f, 0f)), 1f, "in front of the triumphal gate");
            Assert.Greater(_em.EdgeDistance01(new Vector3(93.4f, 0f, 0f)), 1f, "in the triumphal gate");
            var convict = Enemy("EN_Convict");
            float s = Mathf.Sin(-50f * Mathf.Deg2Rad), k = Mathf.Cos(-50f * Mathf.Deg2Rad);
            Vector3[] outside =
            {
                new Vector3(-97f, 0f, 20f), new Vector3(0f, 0f, 47f), new Vector3(-30f, 0f, -47f),
                new Vector3(51f + 47f * k, 0f, 47f * s), new Vector3(97f, 0f, 2f),
            };
            int[] slots = new int[outside.Length];
            for (int i = 0; i < outside.Length; i++) slots[i] = _em.Spawn(convict, outside[i]);
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < slots.Length; i++)
                Assert.LessOrEqual(_em.EdgeDistance01(_em.GetPosition(slots[i])), 1.001f, "pulled back in from " + outside[i]);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LockedUntilThePatronFalls_ThenARunThereIsTier4_WithItsOwnDifficulty_AndTheBiggestHorde()
        {
            SaveSandbox.Wipe();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsNotNull(Imperial, "RD_ImperialArena exists (Build Imperial Arena)");
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Imperial), "locked on a fresh save");
            Assert.IsFalse(menu.PickArena(Imperial));

            MetaProgress.CommitRun(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "patron", BossDefeatedHp01 = 0.5f, GladiatorId = "nobody" }, menu.meta);
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Imperial), "beating the Gilded Patron earns it");
            yield return LoadMenu();
            menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Invoke(menu, "OpenSelect");
            yield return null;
            Invoke(menu, "StartRun");
            yield return null;
            Assert.IsTrue(menu.PickArena(Imperial), "the menu offers it (rebuild the main menu after the arena's setup)");
            Invoke(menu, "Launch");
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != "ImperialArena") yield return null;
            yield return null;
            Assert.AreEqual("ImperialArena", SceneManager.GetActiveScene().name);
            Assert.AreSame(Imperial, GameSession.Run);

            Assert.AreEqual(4, RunState.Current.arenaTier);
            Assert.AreEqual(2.15f, EnemyManager.Instance.hpMul, 1e-4f, "+115 % enemy HP");
            Assert.AreEqual(1.45f, RunState.Current.playerDamageTakenMul, 1e-4f, "+45 % damage taken");
            var director = Director.Instance;
            Assert.AreSame(Load<SpawnTableDef>(TablePath), director.table);
            Assert.AreEqual(400, director.maxAliveOverride);
            Assert.AreEqual(1.25f, director.arenaDensity, 1e-4f, "the biggest floor gets the most of the horde");
            Assert.AreEqual(55f, director.maxSpawnDistance, 1e-4f, "the horde comes from your end of the hippodrome");
            Assert.AreEqual(60f, director.eliteEveryOverride, 1e-4f);
            Assert.AreEqual(5, ArenaGates.Instance.Count, "the triumphal gate and four side gates");
            Assert.IsNotNull(ChariotRace.Instance, "the chariot races");
            Assert.AreEqual("emperors_champion", Object.FindAnyObjectByType<BossFight>().boss.id);

            RunState.Current.End(RunOutcome.Died);
            yield return null;
            Assert.AreEqual(4, MetaProgress.Data.deepestArena, "the save knows the player reached tier 4");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NothingStandsOnTheTrack_NorWithinOnePointFiveMetresOfIt()
        {
            yield return Boot(true);
            var race = ChariotRace.Instance;
            Assert.IsNotNull(race);
            const float pad = 1.5f;
            var layout = Load<ArenaLayoutDef>(LayoutPath);
            var props = Interactables.Instance;
            foreach (var p in props.chestAnchors) Assert.IsFalse(race.InLane(p, pad), "a chest on the track at " + p);
            foreach (var p in props.altarAnchors) Assert.IsFalse(race.InLane(p, pad), "an altar on the track at " + p);
            var breakables = Object.FindAnyObjectByType<ArenaBreakables>();
            Assert.IsNotNull(breakables, "the braziers");
            Assert.AreEqual(6, breakables.braziers.Length);
            foreach (var b in breakables.braziers) Assert.IsFalse(race.InLane(b.position, pad), "a brazier on the track at " + b.position);
            Assert.IsFalse(race.InLane(layout.playerSpawn, pad), "the player spawn");
            var gates = ArenaGates.Instance;
            for (int g = 0; g < gates.Count; g++)
                foreach (float depth in new[] { gates.spawnDepth.x, gates.spawnDepth.y })
                    foreach (float side in new[] { -gates.spawnSpread, 0f, gates.spawnSpread })
                    {
                        Vector3 p = gates.Position(g) + gates.Inward(g) * depth + gates.gates[g].right * side;
                        Assert.IsFalse(race.InLane(p, pad), "gate " + g + "'s mouth is on the track at " + p);
                    }

            // no collider (piece, prop, gate, invisible wall) anywhere over the lane + 1.5 m inside the play area: a
            // body-sized sphere at 0.55-1.45 m every metre along the path, every 0.5 m across it (the floor is below it)
            var hits = new Collider[8];
            int checks = 0;
            for (float s = 0f; s <= race.PathLength; s += 1f)
            {
                Vector3 p = race.PointAt(s, out var fwd);
                var right = new Vector3(fwd.z, 0f, -fwd.x);
                for (float off = -(race.trackWidth * 0.5f + pad); off <= race.trackWidth * 0.5f + pad + 1e-3f; off += 0.5f)
                {
                    Vector3 q = p + right * off + Vector3.up;
                    if (q.x < -93f) continue; // the run-in and run-out end in the carceres' open bays, outside the play area
                    checks++;
                    int n = Physics.OverlapSphereNonAlloc(q, 0.45f, hits, Layers.SolidMask, QueryTriggerInteraction.Ignore);
                    if (n > 0) Assert.Fail($"{hits[0].name} ({hits[0].transform.root.name}) stands on the track at {q}");
                }
            }
            Assert.Greater(checks, 8000);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheTrack_IsTheLayouts_AndARaceComesOutOfTheCarceresOpenBay()
        {
            yield return Boot(true);
            var race = ChariotRace.Instance;
            Assert.IsNotNull(race, "the layout's trackRadius > 0 adds the races");
            Assert.AreEqual(14f, race.trackRadius);
            Assert.AreEqual(4f, race.trackWidth);
            Assert.AreEqual(new Vector2(-60f, 0f), race.trackWestMeta);
            Assert.AreEqual(new Vector2(40f, 0f), race.trackEastMeta);
            Assert.AreEqual(-98f, race.trackStallX, "inside the open bays' tunnels");
            Assert.AreEqual(80f, race.chariotFirst);
            Assert.AreEqual(60f, race.chariotEvery);
            Assert.AreEqual(3f, race.chariotTelegraph);
            Assert.AreEqual(22f, race.chariotSpeed);
            Assert.AreEqual(2, race.chariotCount);
            Assert.AreEqual(2, race.chariotLaps);
            Assert.AreEqual(288f, race.LapLength, 0.5f);
            Assert.AreEqual(608f, race.PathLength, 1f, "run-in 38 + 2 laps - the last west turn + run-out 38");
            Assert.AreEqual(2, race.chariots.Length);
            foreach (var t in race.chariots) { Assert.IsNotNull(t); Assert.IsFalse(t.gameObject.activeSelf, "hidden until it launches"); }
            Assert.IsNotNull(race.lapCounter, "PR_LapCounter on the spina");
            Assert.AreEqual(7, race.EggCount);
            Assert.AreEqual(80f, race.NextStart, 0.01f, "the first race at 1:20");

            // the open bays the chariots use are where the run-in and run-out meet the carceres
            int open = 0, closed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                if (t.parent != null && t.parent.name == t.name) continue; // the FBX's mesh child shares its root's name
                if (t.name == "SM_CarceresOpen") { open++; Assert.AreEqual(14f, Mathf.Abs(t.position.z), 0.01f); Assert.AreEqual(-95f, t.position.x, 0.01f); }
                if (t.name == "SM_Carceres") closed++;
            }
            Assert.AreEqual(2, open);
            Assert.AreEqual(20, closed);

            _motor.Teleport(new Vector3(-10f, 0.1f, -26f)); // the spawn, off the track
            yield return null;
            Assert.IsTrue(race.StartNow());
            Assert.IsTrue(race.Telegraphing);
            Assert.AreEqual(1, race.Races);
            yield return new WaitForSeconds(race.chariotTelegraph + 0.5f);
            Assert.IsTrue(race.Running);
            Assert.IsTrue(race.ChariotActive(0));
            Assert.IsFalse(race.ChariotActive(1), "the second leaves half a lap later");
            Vector3 p = race.ChariotPosition(0);
            Assert.AreEqual(-14f, p.z, 0.5f, "out of the south open bay, on the run-in");
            Assert.Greater(p.x, -98f);
            Assert.Less(p.x, -80f);
            Assert.Greater(race.ChariotForward(0).x, 0.95f, "heading east");
            Assert.IsTrue(race.chariots[0].gameObject.activeSelf);
            Assert.AreEqual(0f, race.chariots[0].position.y, 0.2f, "on the floor");
            Assert.AreEqual(1, race.EggsDown, "the lap counter drops an egg per race");
            yield return new WaitForSeconds(race.LaunchGap);
            Assert.IsTrue(race.ChariotActive(1), "the second one is out");
            race.Halt();
            yield return null;
            Assert.IsFalse(race.Running);
            Assert.AreEqual(0, race.PlayerHits, "the player spawn is off the track");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSpina_IsWalkableAtOnePointTwoMetres_AndTheHordeClimbsItsStairs()
        {
            yield return Boot(true);
            var h = _em.Height;
            Assert.IsNotNull(h);
            Assert.AreEqual(1.2f, h.Sample(-30f, 0f), 0.08f, "the spina's walkway");
            Assert.AreEqual(1.2f, h.Sample(15f, 2f), 0.08f, "the walkway beside the carpet");
            Assert.AreEqual(0f, h.Sample(-30f, -8f), 0.08f, "the inner strip");
            Assert.AreEqual(0f, h.Sample(-30f, -14f), 0.08f, "the track");
            Assert.AreEqual(1.2f, h.Sample(-75f, 42f), 0.08f, "the north-west terrace");
            Assert.AreEqual(1.2f, h.Sample(0f, -42f), 0.08f, "the south terrace");
            float foot = h.Sample(-20f, -5.3f), mid = h.Sample(-20f, -4f), top = h.Sample(-20f, -3.2f);
            Debug.Log($"[ImperialArenaTests] the south stairs at x = -20: foot {foot:0.00}, middle {mid:0.00}, top {top:0.00}");
            Assert.Less(foot, 0.35f, "the stairs' foot (|z| 5)");
            Assert.Greater(mid, 0.2f, "half way up");
            Assert.Less(mid, 1.1f, "half way up");
            Assert.Greater(top, 0.9f, "the walkway's edge (|z| 3)");

            // you stand on the spina above the south stairs; a convict below them comes up
            _motor.Teleport(new Vector3(-20f, SpinaTop, 0.8f));
            yield return null;
            int slot = _em.Spawn(Enemy("EN_Convict"), new Vector3(-20f, 0f, -10f));
            float end = Time.time + 10f;
            while (Time.time < end && _em.IsAlive(slot) && !(_em.GetPosition(slot).y > 1f && Mathf.Abs(_em.GetPosition(slot).z) < 3f)) yield return null;
            Assert.IsTrue(_em.IsAlive(slot));
            Vector3 at = _em.GetPosition(slot);
            Assert.Greater(at.y, 1f, "up on the walkway: " + at);
            Assert.Less(Mathf.Abs(at.z), 3f, "on the spina: " + at);
        }

        const float SpinaTop = 1.3f;

        [UnityTest, Timeout(60000)]
        public IEnumerator ACenturion_FromTheTable_MarchesBehindHisLineOfFive_AndKillingHimBreaksIt()
        {
            var centurion = Enemy("EN_Centurion");
            var praetorian = Enemy("EN_Praetorian");
            Assert.IsNotNull(centurion);
            Assert.IsNotNull(praetorian);
            // the table: the Centurion from 1:00 (never an elite), no loose Praetorian, no Beast Handler
            var table = Load<SpawnTableDef>(TablePath);
            bool found = false, loose = false, handler = false;
            foreach (var e in table.entries)
            {
                Assert.IsNotNull(e.enemy);
                loose |= e.enemy == praetorian;
                handler |= e.enemy.id == "beasthandler";
                if (e.enemy != centurion) continue;
                found = true;
                Assert.AreEqual(60f, e.from);
                Assert.AreEqual(0.15f, e.weightStart, 1e-4f);
                Assert.AreEqual(0.25f, e.weightEnd, 1e-4f);
                Assert.AreEqual(3, e.maxAlive);
                Assert.IsFalse(e.elite, "an affix would break the march");
                Assert.IsFalse(e.swarm);
            }
            Assert.IsTrue(found, "the Centurion is in ST_ImperialArena");
            Assert.IsFalse(loose, "the Praetorian only comes in a line");
            Assert.IsFalse(handler, "beasts belong to arenas 3-4");
            Assert.Greater(table.affixes.Length, 0);
            // the defs (docs/ARENAS.md §5)
            Assert.AreEqual(EnemyBehaviour.ShieldBearer, praetorian.behaviour);
            Assert.AreEqual(70f, praetorian.maxHp);
            Assert.AreEqual(120f, praetorian.blockArc);
            Assert.AreEqual(0.75f, praetorian.blockReduction, 1e-4f);
            Assert.IsNotNull(praetorian.model, "EN_Praetorian.fbx");
            Assert.AreEqual(EnemyBehaviour.Lunger, centurion.behaviour);
            Assert.AreEqual(160f, centurion.maxHp);
            Assert.AreEqual(80f, centurion.turnRate);
            Assert.IsNotNull(centurion.model, "EN_Centurion.fbx");
            Assert.AreSame(praetorian, centurion.escort);
            Assert.AreEqual(5, centurion.escortCount);
            Assert.AreEqual(5, centurion.formationSlots.Length);
            Assert.AreEqual(2.6f, centurion.formationHold, 1e-4f);
            Assert.IsFalse(centurion.freeEscortsOnDeath, "the line breaks; they fight on");
            Assert.AreSame(Load<StatusDef>("Assets/Data/Status/SE_Stagger.asset"), centurion.breakStatus);
            Assert.AreEqual(3f, centurion.breakShieldsDown, 1e-4f);

            yield return Boot(true);
            _motor.Teleport(new Vector3(-30f, 0.1f, -30f)); // the south outer field
            yield return null;
            int formations = _em.FormationsSpawned, broken = _em.LinesBroken;
            int leader = _em.Spawn(centurion, new Vector3(-40f, 0f, -30f));
            yield return null;
            Assert.AreEqual(formations + 1, _em.FormationsSpawned);
            Assert.AreEqual(5, _em.FormedCount(leader));
            Assert.AreEqual(5, _em.AliveOf(praetorian));
            var near = new int[64];
            int n = _em.QueryAlive(_em.GetPosition(leader), 8f, near), line = 0;
            for (int k = 0; k < n; k++)
            {
                int i = near[k];
                if (_em.GetDef(i) != praetorian) continue;
                line++;
                Assert.IsTrue(_em.InFormation(i));
                Assert.AreEqual(leader, _em.LeaderOf(i));
                Assert.Less(Flat(_em.GetPosition(i) - _em.SlotPosition(i)).magnitude, 0.6f, "on its slot");
                Assert.Greater(_em.GetPosition(i).x, _em.GetPosition(leader).x + 1f, "a wall of shields between him and you");
            }
            Assert.AreEqual(5, line);

            _em.ApplyDamage(leader, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(leader) });
            yield return null;
            Assert.IsFalse(_em.IsAlive(leader));
            Assert.AreEqual(broken + 1, _em.LinesBroken);
            Assert.AreEqual(5, _em.AliveOf(praetorian), "not freed: they fight on");
            n = _em.QueryAlive(new Vector3(-38f, 0f, -30f), 10f, near);
            for (int k = 0; k < n; k++)
            {
                int i = near[k];
                if (_em.GetDef(i) != praetorian) continue;
                Assert.IsFalse(_em.InFormation(i));
                Assert.IsTrue(_em.ShieldsDown(i), "staggered with its shield down");
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheBossGate_IsTheTriumphalGate_AndTheEmperorWatchesFromThePulvinar()
        {
            yield return Boot(true);
            var fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(fight);
            Assert.AreEqual(0, fight.gate);
            Assert.AreEqual("emperors_champion", fight.boss.id);
            Assert.Contains(Enemy("EN_Centurion"), fight.boss.guards, "his Guard is the Centurion and his line");
            var gates = ArenaGates.Instance;
            Assert.AreEqual(5, gates.Count);
            Assert.AreEqual("SM_TriumphalGate", gates.gates[0].name);
            Assert.AreEqual(93.73f, gates.Position(0).x, 0.05f, "on the sphendone's flattened apex");
            Assert.AreEqual(0f, gates.Position(0).z, 0.05f);
            Assert.Less(gates.Inward(0).x, -0.99f, "facing west, down the hippodrome");
            for (int g = 1; g < gates.Count; g++)
            {
                Assert.AreEqual("SM_ImpGate", gates.gates[g].name);
                Assert.AreEqual(44f, Mathf.Abs(gates.Position(g).z), 0.05f, "on a long side");
                Assert.Less(gates.Inward(g).z * gates.Position(g).z, -40f, "facing in");
            }
            var pulvinar = GameObject.Find("SM_Pulvinar");
            Assert.IsNotNull(pulvinar);
            Assert.Less(Vector3.Distance(new Vector3(-10f, 0f, 44f), pulvinar.transform.position), 0.01f, "in the north wall, facing the obelisk");
            var crowd = Object.FindAnyObjectByType<CrowdRenderer>();
            Debug.Log($"[ImperialArenaTests] crowd {crowd.Count}");
            Assert.That(crowd.Count, Is.InRange(2000, 2400), "the biggest crowd yet (~2,200)");

            _motor.Teleport(new Vector3(60f, 0.1f, -20f));
            fight.StartNow();
            float end = Time.time + 12f;
            while (Time.time < end && BossController.Active == null) yield return null;
            Assert.IsNotNull(BossController.Active, "he comes");
            yield return null;
            Vector3 at = _em.GetPosition(BossController.Active.Slot);
            Assert.Greater(at.x, 80f, "through the triumphal gate: " + at);
            Assert.Less(Mathf.Abs(at.z), 5f, "through the triumphal gate: " + at);
        }
    }
}
