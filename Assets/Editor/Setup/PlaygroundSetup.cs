using System.Collections.Generic;
using Gladiator.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// T1 (docs/tracks/T1-playground.md): the Playground, admin mode's test arena. One menu item writes its layout
    /// (AL_Playground) and run (RD_Playground: no unlock, never in the menu's run list), builds the scene from the layout
    /// with ArenaBuilder (the run objects, the nav and height bake, the boundary), then makes it the Playground: the
    /// Director off (nothing spawns on its own; F1 → DIRECTOR turns it on), the Arena Master's whims off, the run never
    /// recorded in the save (RunEndController.meta empty), and the Showroom on the east half with every gladiator, skin
    /// and enemy (bosses included) from the data. The Showroom keeps god mode and timeless on while the scene is loaded.
    /// Last, MainMenu.playgroundRun on the menu scene points at RD_Playground (set by name: W-menu declares the field).
    /// Layout (+Z north): a 120 m round field of dirt inside a fence; the spawn in the middle facing east, at the
    /// Showroom's front row; the test arena's pieces (slide hill, platform with stairs and ramp, a stone-wall corner,
    /// pillars, mounds, props, braziers) moved 26 m west, as they stand in the test arena; chests and the altar south.
    /// The layout is code-owned (rewritten every build): change it here, not in the asset.
    /// </summary>
    public static class PlaygroundSetup
    {
        public const string ScenePath = "Assets/Scenes/Dev/Playground.unity";
        public const string LayoutPath = "Assets/Data/Arenas/AL_Playground.asset";
        public const string RunPath = "Assets/Data/Runs/RD_Playground.asset";
        public const string Id = "playground";
        /// <summary>The Showroom's place: its rows run north-south from here, each row further east, the models facing
        /// west to the spawn (its local -Z).</summary>
        public static readonly Vector3 ShowroomPosition = new Vector3(8f, 0f, 0f);
        public const float ShowroomYaw = 90f;
        /// <summary>The test arena's pieces stand as in TestArenaScene, moved by this (west of the spawn).</summary>
        public static readonly Vector3 TestCorner = new Vector3(-26f, 0f, 0f);
        const string Env = "Environment/";
        const string NobodyModel = "Assets/Art/Models/Characters/CH_Nobody.fbx";
        const float FloorScale = 3.4f;     // SM_PitFloor (46 m, rising at its rim past 18 m) → 156 m, flat to 61 m
        const float FenceRadius = 60.3f;   // the fence's posts
        const float LoopRadius = 60f;      // the player's boundary, just inside the fence
        const int Sections = 126;          // 3 m fence pieces whose posts meet

        [MenuItem("Gladiator/Setup/Dev/Build Playground")]
        public static string Build()
        {
            var layout = EnsureLayout();
            EnsureRun(layout);
            ArenaBuilder.Build(layout, ScenePath);
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[PlaygroundSetup] ArenaBuilder left " + scene.path + " open, not " + ScenePath);
                return null;
            }
            // the new scene unloaded unused assets: reload what the rest points at
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);

            var director = Object.FindAnyObjectByType<Director>();
            if (director != null) director.enabled = false; // nothing spawns on its own (F1 → DIRECTOR)
            var master = Object.FindAnyObjectByType<ArenaMaster>();
            if (master != null) master.enabled = false; // no boredom whims (their swarms need the Director anyway)
            var end = Object.FindAnyObjectByType<RunEndController>();
            if (end != null) end.meta = null; // admin play is a view, not the save: no Renown, records or unlocks
            AddShowroom();

            SceneSetupUtil.SaveScene(scene, ScenePath);
            LinkMenu(run);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // leave the built scene open, as the builders do
            Debug.Log("[PlaygroundSetup] built " + ScenePath);
            return ScenePath;
        }

        // ---------- the Showroom ----------
        static Showroom AddShowroom()
        {
            var room = new GameObject("Showroom").AddComponent<Showroom>();
            room.transform.SetPositionAndRotation(ShowroomPosition, Quaternion.Euler(0f, ShowroomYaw, 0f));
            room.gladiators = GladiatorSetup.All();
            room.skins = AllSkins();
            var (enemies, tiers) = LadderOrder(EnemyCatalogUpdater.Refresh());
            room.enemies = enemies;
            room.enemyTiers = tiers;
            room.defaultModel = ModelStyle.Load(NobodyModel);
            room.defaultProfile = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(PlayerSetup.AnimPath);
            room.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            room.flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.FlashMat);
            // the theme's title font (Marcellus SC) with its outline preset; LiberationSans without a theme
            var theme = AssetDatabase.LoadAssetAtPath<Gladiator.UI.UiTheme>(UiThemeSetup.ThemePath);
            if (theme != null && theme.title.font != null)
            {
                room.font = theme.title.font;
                room.fontMaterial = theme.title.outline;
            }
            else
            {
                room.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CombatSetup.FontPath);
                room.fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.FontOutlinePath);
            }
            int early = 0, late = 0, bosses = 0;
            foreach (var t in tiers) { if (t == 0) bosses++; else if (t <= 2) early++; else late++; }
            Debug.Log($"[PlaygroundSetup] Showroom: {room.gladiators.Length} gladiators, {room.skins.Length} skins, " +
                      $"{early} enemies of tiers I–II, {late} of tiers III–IV, {bosses} bosses");
            return room;
        }

        /// <summary>Every SkinDef under Assets/Data/Skins, by path.</summary>
        static SkinDef[] AllSkins()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:SkinDef", new[] { SkinSetup.Dir.TrimEnd('/') }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<SkinDef>();
            foreach (var p in paths)
            {
                var s = AssetDatabase.LoadAssetAtPath<SkinDef>(p);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        /// <summary>A ladder arena's tier (0 = the Slave Pit … 5 = the Colosseum) → the GDD's enemy-ladder tier (§6.3):
        /// I in Arena 1, II in Arenas 2–3, III in Arenas 4–5, IV in Arena 6.</summary>
        public static int LadderTier(int arenaTier) => arenaTier <= 0 ? 1 : arenaTier <= 2 ? 2 : arenaTier <= 4 ? 3 : 4;

        /// <summary>
        /// The catalog's enemies in ladder order, with their tiers for the Showroom's rows: an enemy's tier is the first
        /// ladder arena that brings it (its spawn table, cage beast, lift packs, its boss's guards, and the escorts any of
        /// those bring), through LadderTier; one no arena brings goes last at tier IV. Bosses (a BossDef's enemy) are tier
        /// 0, after the rest, in their arenas' order.
        /// </summary>
        public static (EnemyDef[] enemies, int[] tiers) LadderOrder(EnemyCatalog catalog)
        {
            var layouts = new List<ArenaLayoutDef>();
            foreach (var guid in AssetDatabase.FindAssets("t:ArenaLayoutDef"))
            {
                var l = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (l != null && l.id != Id) layouts.Add(l);
            }
            layouts.Sort((a, b) => a.tier.CompareTo(b.tier));
            var slaveTable = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(CombatSetup.SlavePitTablePath);
            var first = new Dictionary<EnemyDef, int>();
            var bossAt = new Dictionary<EnemyDef, int>();
            void Seen(EnemyDef e, int arena, int depth)
            {
                if (e == null || depth > 4) return;
                if (!first.TryGetValue(e, out int had) || arena < had) first[e] = arena;
                Seen(e.escort, arena, depth + 1);
            }
            foreach (var l in layouts)
            {
                var table = l.spawnTable != null ? l.spawnTable : slaveTable;
                if (table != null) foreach (var en in table.entries) Seen(en.enemy, l.tier, 0);
                Seen(l.cageBeast, l.tier, 0);
                if (l.liftPacks != null) foreach (var p in l.liftPacks) Seen(p.enemy, l.tier, 0);
                var boss = l.boss;
                if (boss == null) continue;
                if (boss.enemy != null && (!bossAt.TryGetValue(boss.enemy, out int was) || l.tier < was)) bossAt[boss.enemy] = l.tier;
                if (boss.guards != null) foreach (var g in boss.guards) Seen(g, l.tier, 0);
                Seen(boss.guardedBy, l.tier, 0);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:BossDef")) // a boss no arena fields yet: still a boss
            {
                var b = AssetDatabase.LoadAssetAtPath<BossDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (b != null && b.enemy != null && !bossAt.ContainsKey(b.enemy)) bossAt[b.enemy] = int.MaxValue;
            }

            var all = new List<EnemyDef>();
            if (catalog != null) foreach (var e in catalog.enemies) if (e != null && !all.Contains(e)) all.Add(e);
            int Key(EnemyDef e) => bossAt.TryGetValue(e, out int b) ? b : first.TryGetValue(e, out int f) ? f : int.MaxValue;
            var order = new List<EnemyDef>(all);
            order.Sort((a, b) =>
            {
                bool ba = bossAt.ContainsKey(a), bb = bossAt.ContainsKey(b);
                if (ba != bb) return ba ? 1 : -1; // the bosses last
                int k = Key(a).CompareTo(Key(b));
                return k != 0 ? k : all.IndexOf(a).CompareTo(all.IndexOf(b)); // then the catalog's (asset name) order
            });
            var tiers = new int[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var e = order[i];
                tiers[i] = bossAt.ContainsKey(e) ? 0 : first.TryGetValue(e, out int f) ? LadderTier(f) : 4;
            }
            return (order.ToArray(), tiers);
        }

        // ---------- the data ----------
        /// <summary>AL_Playground, rewritten every build (code-owned).</summary>
        public static ArenaLayoutDef EnsureLayout()
        {
            var l = PlayerSetup.EnsureAsset<ArenaLayoutDef>(LayoutPath);
            l.id = Id;
            l.displayName = "The Playground";
            l.tier = 0;
            l.spawnTable = null; // the Slave Pit's, if the Director is switched on
            l.enemyHpMul = 1f;
            l.playerDamageTakenMul = 1f;
            l.center = Vector3.zero;
            l.radius = LoopRadius - 1f; // enemies stay a metre inside the boundary
            l.aspect = 1f;
            l.outline = new Vector2[0];
            l.boundaryLoop = BoundaryLoop();
            l.boundary = new Vector4[0];
            l.fog = new Vector2(60f, 260f); // the far fence is ~120 m away: keep the field clear
            l.playerSpawn = new Vector3(0f, 0.1f, 0f);
            l.playerYaw = 90f; // facing the Showroom
            l.maxAlive = 0;
            l.spawnMaxDistance = 30f; // with the Director on, the horde walks in from the near side
            l.densityMul = 1f;
            l.swarmMul = 1f;
            l.sunEuler = new Vector3(40f, -35f, 0f);
            l.boss = null;
            l.bossGate = 0;
            l.pieces = Pieces();
            l.gates = new ArenaLayoutDef.Gate[0];
            l.zones = new ArenaLayoutDef.Zone[0];
            l.crowd = new ArenaLayoutDef.CrowdZone[0];
            // chests and the blood altar on the south side, clear of the Showroom and the test pieces
            l.chestAnchors = new[] { new Vector3(-8f, 0f, -34f), new Vector3(8f, 0f, -34f), new Vector3(0f, 0f, -44f) };
            l.altarAnchors = new[] { new Vector3(0f, 0f, -26f) };
            l.secretSpots = new Vector3[0];
            l.secrets = ArenaSecrets.None;
            l.barricades = new Vector4[0];
            l.braziers = new Vector3[0];
            l.spikeTraps = new Vector3[0];
            l.cages = new Vector4[0];
            l.cageBeast = null;
            l.solidWalls = new Vector4[0];
            l.eliteEvery = 0f;
            l.fireJets = new Vector4[0];
            l.bladeSweepers = new Vector3[0];
            l.guardSpots = new Vector3[0];
            l.trackRadius = 0f;
            l.trapdoors = new Vector4[0];
            l.lifts = new Vector3[0];
            l.hasCenterLift = false;
            l.liftPacks = new ArenaLayoutDef.LiftPack[0];
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }

        static ArenaLayoutDef.Piece[] Pieces()
        {
            var pieces = new List<ArenaLayoutDef.Piece>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });
            void Test(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) => Add(model, role, TestCorner + pos, yaw, scale);

            Add("SM_PitFloor", PieceRole.Walkable, Vector3.zero, 0f, new Vector3(FloorScale, 1f, FloorScale));

            // the test arena's pieces where TestArenaScene puts them, moved west (its spawn was (0, 0, -8) facing +Z)
            Test("SM_TestHill", PieceRole.Walkable, TestArenaScene.HillPos, 0f);   // 30° slide face north, 20° back ramp
            Test("SM_Platform", PieceRole.Walkable, new Vector3(-10f, 0f, 4f), 0f); // deck 1.2 m
            Test("SM_Stairs", PieceRole.Walkable, new Vector3(-10f, 0f, 8f), 0f);
            Test("SM_Ramp", PieceRole.Walkable, new Vector3(-4f, 0f, 4f), 90f);
            Test("SM_Wall_Stone_4m", PieceRole.Solid, new Vector3(-3f, 0f, -14f), 0f);  // the camera-occlusion corner
            Test("SM_Wall_Stone_4m", PieceRole.Solid, new Vector3(-5.45f, 0f, -11.55f), 90f);
            Test("SM_Wall_Stone_4m", PieceRole.Solid, new Vector3(4f, 0f, -16f), 15f);
            foreach (var p in new[] { new Vector3(5f, 0f, -3f), new Vector3(7f, 0f, -9f), new Vector3(-2f, 0f, 12f), new Vector3(2f, 0f, 15f) })
                Test("SM_Pillar", PieceRole.Solid, p, 0f);
            Test("SM_Mound_A", PieceRole.Walkable, new Vector3(-8f, 0f, -4f), 30f);
            Test("SM_Mound_A", PieceRole.Walkable, new Vector3(3f, 0f, 6f), 80f, new Vector3(1.2f, 0.6f, 1.2f));
            Test("PR_Crate", PieceRole.Prop, new Vector3(4f, 0f, 2f), 15f);
            Test("PR_Crate", PieceRole.Prop, new Vector3(4.9f, 0f, 2.6f), -10f);
            Test("PR_Barrel", PieceRole.Prop, new Vector3(5.6f, 0f, 1.2f), 0f);
            Test("SM_Rock_A", PieceRole.Solid, new Vector3(-14f, 0f, 8f), 10f, Vector3.one * 1.6f);
            Test("PR_Brazier", PieceRole.Prop, new Vector3(-4f, 0f, -2f), 0f); // ArenaBuilder lights a brazier
            Test("PR_Brazier", PieceRole.Prop, new Vector3(8f, 0f, 10f), 0f);

            // the fence round the field (decor: the boundary loop's invisible walls stand just inside it)
            float step = Mathf.PI * 2f / Sections;
            float mid = FenceRadius * Mathf.Cos(step * 0.5f); // chord midpoints sit slightly inside the circle
            for (int i = 0; i < Sections; i++)
            {
                float a = (i + 0.5f) * step;
                Add("SM_Fence_3m", PieceRole.Decor, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * mid, a * Mathf.Rad2Deg); // local +X along the fence
            }
            return pieces.ToArray();
        }

        /// <summary>The player's boundary: a polygon through the fence's posts, LoopRadius from the centre.</summary>
        static Vector2[] BoundaryLoop()
        {
            var loop = new Vector2[Sections];
            float step = Mathf.PI * 2f / Sections;
            for (int i = 0; i < Sections; i++) loop[i] = new Vector2(Mathf.Sin(i * step), Mathf.Cos(i * step)) * LoopRadius;
            return loop;
        }

        /// <summary>RD_Playground: the Playground's one arena; no unlock, not in the menu's run list (admin mode's
        /// PLAYGROUND button starts it through MainMenu.playgroundRun).</summary>
        public static RunDef EnsureRun(ArenaLayoutDef layout)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Runs")) AssetDatabase.CreateFolder("Assets/Data", "Runs");
            var run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
            run.id = Id;
            run.description = "Admin: a big flat field with the test arena's pieces and every character on show. Nothing spawns on its own: F1 for the tools.";
            run.unlock = null;
            run.arenas = new[] { new RunDef.Arena { displayName = "THE PLAYGROUND", scenePath = ScenePath } };
            run.layout = layout;
            run.tagline = "Every fighter, every beast, every boss.";
            run.hazards = "None. Nothing spawns unless you ask (F1).";
            run.bossName = "";
            EditorUtility.SetDirty(run);
            AssetDatabase.SaveAssetIfDirty(run);
            return run;
        }

        // ---------- the menu ----------
        /// <summary>Points MainMenu.playgroundRun on the menu scene at `run` and saves the scene. False (logged) when the
        /// menu scene, its MainMenu or the field isn't there.</summary>
        public static bool LinkMenu(RunDef run)
        {
            if (run == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSession.MenuScene) == null)
            {
                Debug.LogWarning("[PlaygroundSetup] no " + GameSession.MenuScene + " (Gladiator/Setup/Build Main Menu) or no run: the menu isn't linked");
                return false;
            }
            // opening a scene unloads unused assets: the run is loaded again from its path after the scene is open
            string runPath = AssetDatabase.GetAssetPath(run);
            var scene = EditorSceneManager.OpenScene(GameSession.MenuScene, OpenSceneMode.Single);
            run = AssetDatabase.LoadAssetAtPath<RunDef>(runPath);
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            if (menu == null)
            {
                Debug.LogWarning("[PlaygroundSetup] no MainMenu in " + GameSession.MenuScene + ": the menu isn't linked");
                return false;
            }
            if (!Link(menu, run)) return false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PlaygroundSetup] MainMenu.playgroundRun → " + RunPath);
            return true;
        }

        /// <summary>Sets `menu`'s playgroundRun by name (so this compiles before W-menu adds the field). MenuSceneBuilder
        /// can call it on the menu it builds, so a menu rebuild keeps the link.</summary>
        public static bool Link(Gladiator.UI.MainMenu menu, RunDef run)
        {
            var so = new SerializedObject(menu);
            var prop = so.FindProperty("playgroundRun");
            if (prop == null)
            {
                Debug.LogWarning("[PlaygroundSetup] MainMenu has no playgroundRun field yet (W-menu declares it): the menu isn't linked");
                return false;
            }
            prop.objectReferenceValue = run;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
