using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Builds an arena scene from its ArenaLayoutDef (CLAUDE.md §7.3, M3.2): pieces by role (walkable / solid / prop /
    /// decor), gates with animated doors and an invisible blocker, mud and fire zones, the instanced crowd, chest and
    /// altar anchors, the run systems, then bakes the height grid and obstacles. One menu item per arena.
    /// </summary>
    public static class ArenaBuilder
    {
        public const string SlavePitScene = "Assets/Scenes/Arenas/SlavePit.unity";
        public const string CrowdMat = LookSetup.MatDir + "/M_Crowd.mat";
        const string SpectatorModel = "Assets/Art/Models/Environment/CH_Spectator.fbx";
        const string MudModel = "Environment/PR_MudPatch";
        const string FireModel = "Environment/PR_FirePit";
        const string BarricadeModel = "Environment/PR_Barricade";
        const string SpikeGrateModel = "Environment/PR_SpikeGrate";
        const string SpikesModel = "Environment/PR_Spikes";
        const string CageModel = "Environment/SM_CageGate";
        const string JetNozzleModel = "Environment/PR_FireJetNozzle";
        const string JetFlameModel = "Environment/PR_JetFlame";
        const string BladeHubModel = "Environment/PR_BladeHub";
        const string BladeModel = "Environment/PR_Blade";
        public const string GoldenRatModel = "Assets/Art/Models/Props/PR_GoldenRat.fbx";
        public const string BookmakerModel = "Assets/Art/Models/Characters/CH_Bookmaker.fbx";

        [MenuItem("Gladiator/Arenas/Build Slave Pit")]
        public static string BuildSlavePit()
        {
            var layout = SlavePitLayout.Ensure();
            if (layout.boss == null) { layout.boss = BossSetup.EnsureGorm(); EditorUtility.SetDirty(layout); AssetDatabase.SaveAssetIfDirty(layout); }
            return Build(layout, SlavePitScene);
        }

        public static string Build(ArenaLayoutDef layout, string scenePath)
        {
            LookSetup.CreateOrUpdateAll();
            PlayerSetup.BuildPrefab();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Arenas")) AssetDatabase.CreateFolder("Assets/Scenes", "Arenas");
            string layoutPath = AssetDatabase.GetAssetPath(layout);
            var scene = SceneSetupUtil.NewEmptyScene();
            // the new scene unloads unused assets: assets the layout points at (its boss) die in memory, so reload it
            if (!string.IsNullOrEmpty(layoutPath)) layout = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(layoutPath);
            ArenaSceneCommon.AddLook(layout.sunEuler, layout.fog);
            ArenaSceneCommon.AddGameLoop();

            var env = new GameObject("Environment").transform;
            var placed = new List<(PieceRole role, GameObject go, float baseY)>(); // for the perch guards (P1)
            foreach (var p in layout.pieces) placed.Add((p.role, PlacePiece(p, env), p.position.y));

            var gateRoots = new List<Transform>();
            var gatesRoot = new GameObject("Gates").transform;
            gatesRoot.SetParent(env, false);
            string gateModel = string.IsNullOrEmpty(layout.gateModel) ? "Environment/SM_PitGate" : layout.gateModel;
            foreach (var g in layout.gates) gateRoots.Add(PlaceGate(g, gatesRoot, string.IsNullOrEmpty(g.model) ? gateModel : g.model));

            // breakables (Phase 5.4): placed before the bake so their obstacles are baked too (switched off when broken)
            var barricades = new List<Transform>();
            var braziers = new List<Transform>();
            if (layout.barricades.Length > 0 || layout.braziers.Length > 0)
            {
                var breakRoot = new GameObject("Breakables").transform;
                breakRoot.SetParent(env, false);
                foreach (var b in layout.barricades)
                {
                    var go = ArenaSceneCommon.PlaceProp(BarricadeModel, breakRoot, new Vector3(b.x, b.y, b.z), b.w);
                    UnStatic(go);
                    barricades.Add(go.transform);
                    placed.Add((PieceRole.Prop, go, b.y));
                }
                foreach (var b in layout.braziers)
                {
                    var go = ArenaSceneCommon.PlaceProp("Environment/PR_Brazier", breakRoot, b, 0f);
                    AddFireLight(go);
                    UnStatic(go);
                    braziers.Add(go.transform);
                    placed.Add((PieceRole.Prop, go, b.y));
                }
            }
            foreach (var t in layout.spikeTraps) ArenaSceneCommon.PlaceDecor(SpikeGrateModel, env, t, 0f);
            // fire-jet rows (Phase 5.13): a bronze nozzle every 2 m across each row (decor: they're set in the floor)
            const float JetSpacing = 2f;
            foreach (var r in layout.fireJets)
            {
                float yaw = r.z * Mathf.Deg2Rad;
                var along = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                int n = Mathf.Max(1, Mathf.RoundToInt(r.w / JetSpacing));
                for (int k = 0; k < n; k++)
                    ArenaSceneCommon.PlaceDecor(JetNozzleModel, env, new Vector3(r.x, 0f, r.y) + along * (-r.w * 0.5f + r.w / n * (k + 0.5f)), r.z);
            }
            foreach (var h in layout.bladeSweepers) ArenaSceneCommon.PlaceProp(BladeHubModel, env, h, 0f); // stone the horde walks round

            var mud = new List<Vector3>();
            var fires = new List<Vector4>();
            var zonesRoot = new GameObject("Zones").transform;
            zonesRoot.SetParent(env, false);
            int z = 0;
            foreach (var zone in layout.zones)
            {
                float yaw = (z++ * 67f) % 360f;
                if (zone.kind == ArenaLayoutDef.ZoneKind.Mud)
                {
                    // lifted clear of the floor's ±5 cm height noise (otherwise the floor pokes through the decal)
                    ArenaSceneCommon.PlaceDecor(MudModel, zonesRoot, zone.position + Vector3.up * 0.06f, yaw, new Vector3(zone.radius, 1f, zone.radius));
                    mud.Add(new Vector3(zone.position.x, zone.position.z, zone.radius));
                }
                else
                {
                    ArenaSceneCommon.PlaceDecor(FireModel, zonesRoot, zone.position, yaw); // walk into it and it burns
                    fires.Add(new Vector4(zone.position.x, zone.position.y, zone.position.z, zone.radius));
                }
            }

            var (player, _) = ArenaSceneCommon.AddPlayerAndCamera(layout.playerSpawn, layout.playerYaw);
            var (enemies, _) = ArenaSceneCommon.AddCombatSystems(player, layout.radius, debugEnemies: 0);
            if (layout.chestModel != null) Object.FindAnyObjectByType<Interactables>().chestModel = layout.chestModel; // note 30: each arena its own chest
            enemies.arenaCenter = layout.center;
            enemies.arenaAspect = layout.aspect > 0f ? layout.aspect : 1f;
            enemies.arenaOutline = layout.outline ?? new Vector2[0];
            // the nav bake and the spatial hash cover the whole arena (the old ±26 m box fits only the small ones)
            var c2 = new Vector2(layout.center.x, layout.center.z);
            Vector2 lo = c2 - new Vector2(layout.radius, layout.radius * enemies.arenaAspect), hi = c2 + new Vector2(layout.radius, layout.radius * enemies.arenaAspect);
            if (enemies.HasOutline)
            {
                lo = hi = layout.outline[0];
                foreach (var v in layout.outline) { lo = Vector2.Min(lo, v); hi = Vector2.Max(hi, v); }
            }
            enemies.boundsMin = Vector2.Min(c2 - new Vector2(26f, 26f), lo - new Vector2(7f, 7f));
            enemies.boundsMax = Vector2.Max(c2 + new Vector2(26f, 26f), hi + new Vector2(7f, 7f));
            AddBoundary(layout, env);
            AddSolidWalls(layout, env);
            var cageRoots = new List<Transform>();
            var cageBars = new List<Transform>();
            if (layout.cages != null && layout.cages.Length > 0) // Phase 5.8: placed before the bake (solid stone)
            {
                var cageRoot = new GameObject("Cages").transform;
                cageRoot.SetParent(env, false);
                foreach (var c in layout.cages)
                {
                    var go = ArenaSceneCommon.PlaceSolid(CageModel, cageRoot, new Vector3(c.x, c.y, c.z), c.w);
                    Transform bars = null;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "Bars") bars = t;
                    if (bars != null) UnStatic(bars.gameObject);
                    cageRoots.Add(go.transform);
                    cageBars.Add(bars);
                    placed.Add((PieceRole.Solid, go, c.y));
                }
            }
            AddPerchGuards(layout, placed); // P1: before the bake, so a fresh bake of the scene matches it
            var hypogeum = PlaceHypogeum(layout, env); // R2: before the bake, so it samples the floor over every hole
            ArenaNavBaker.Bake(enemies, System.IO.Path.GetFileNameWithoutExtension(scenePath));
            ArenaSceneCommon.AddRunSystems(player);

            var arena = new GameObject("[Arena]").transform;
            var gates = new GameObject("ArenaGates").AddComponent<ArenaGates>();
            gates.transform.SetParent(arena, false);
            gates.gates = gateRoots.ToArray();
            gates.spawnDepth = new Vector2(2.4f, 4.2f); // past the door line, inside the play radius
            var mudZones = new GameObject("MudZones").AddComponent<MudZones>();
            mudZones.transform.SetParent(arena, false);
            mudZones.circles = mud.ToArray();
            if (barricades.Count > 0 || braziers.Count > 0)
            {
                var br = new GameObject("ArenaBreakables").AddComponent<ArenaBreakables>();
                br.transform.SetParent(arena, false);
                br.barricades = barricades.ToArray();
                br.braziers = braziers.ToArray();
            }
            if (layout.spikeTraps.Length > 0)
            {
                var traps = new GameObject("SpikeTraps").AddComponent<SpikeTraps>();
                traps.transform.SetParent(arena, false);
                traps.traps = layout.spikeTraps;
                traps.spikesMesh = FirstMesh("Assets/Art/Models/" + SpikesModel + ".fbx");
                traps.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            }
            if (cageRoots.Count > 0)
            {
                var cages = new GameObject("BeastCages").AddComponent<BeastCages>();
                cages.transform.SetParent(arena, false);
                cages.cages = cageRoots.ToArray();
                cages.bars = cageBars.ToArray();
                cages.beast = layout.cageBeast;
                cages.first = layout.cageFirst;
                cages.every = layout.cageEvery;
                cages.pack = layout.cagePack;
            }
            if (layout.fireJets != null && layout.fireJets.Length > 0)
            {
                var jets = new GameObject("FireJets").AddComponent<FireJets>();
                jets.transform.SetParent(arena, false);
                jets.rows = layout.fireJets;
                jets.flameMesh = FirstMesh("Assets/Art/Models/" + JetFlameModel + ".fbx");
                jets.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
                jets.burnStatus = AssetDatabase.LoadAssetAtPath<StatusDef>("Assets/Data/Status/SE_Burn.asset");
            }
            if (layout.bladeSweepers != null && layout.bladeSweepers.Length > 0)
            {
                var blades = new GameObject("BladeSweepers").AddComponent<BladeSweepers>();
                blades.transform.SetParent(arena, false);
                blades.hubs = layout.bladeSweepers;
                blades.bladeMesh = FirstMesh("Assets/Art/Models/" + BladeModel + ".fbx");
                blades.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            }
            if (layout.trackRadius > 0f) AddChariotRace(layout, arena, env); // Phase 5.16: the Imperial Arena
            if (hypogeum != null) AddHypogeum(layout, arena, hypogeum); // R2: the Colosseum
            var firePits = new GameObject("FirePits").AddComponent<ArenaFirePits>();
            firePits.transform.SetParent(arena, false);
            firePits.pits = fires.ToArray();

            var crowd = new GameObject("Crowd").AddComponent<CrowdRenderer>();
            crowd.transform.SetParent(arena, false);
            crowd.mesh = FirstMesh(SpectatorModel);
            crowd.material = EnsureCrowdMaterial();
            crowd.spots = CrowdSpots(layout);
            new GameObject("CrowdExcitement").AddComponent<CrowdExcitement>().transform.SetParent(arena, false);
            var intro = new GameObject("RunIntro").AddComponent<RunIntro>();
            intro.transform.SetParent(arena, false);
            intro.arenaName = layout.displayName.ToUpperInvariant();

            if (layout.boss != null)
            {
                var fight = new GameObject("BossFight").AddComponent<BossFight>();
                fight.transform.SetParent(arena, false);
                fight.boss = layout.boss;
                fight.gate = layout.bossGate;
                fight.controller = new GameObject("BossController").AddComponent<BossController>();
                fight.controller.transform.SetParent(fight.transform, false);
                fight.controller.def = layout.boss;
                fight.controller.guardSpots = layout.guardSpots ?? new Vector3[0];
                var hazards = Object.FindAnyObjectByType<HazardZones>();
                if (hazards != null) hazards.capacity = Mathf.Max(hazards.capacity, 160); // + the phase-2 fire ring
            }

            var director = Object.FindAnyObjectByType<Director>();
            if (director != null)
            {
                director.maxAliveOverride = layout.maxAlive;
                director.maxSpawnDistance = layout.spawnMaxDistance;
                director.eliteEveryOverride = layout.eliteEvery;
                director.arenaDensity = layout.densityMul > 0f ? layout.densityMul : 1f;
                director.swarmMultiplier = layout.swarmMul > 0f ? layout.swarmMul : 1f;
                if (layout.spawnTable != null) director.table = layout.spawnTable;
            }
            // the arena's tier and difficulty (Phase 5.4)
            var runState = Object.FindAnyObjectByType<RunState>();
            if (runState != null)
            {
                runState.arenaTier = layout.tier;
                // on the run, not the player: the player is a prefab instance, and a field set on it here isn't saved
                runState.playerDamageTakenMul = layout.playerDamageTakenMul > 0f ? layout.playerDamageTakenMul : 1f;
            }
            enemies.hpMul = layout.enemyHpMul > 0f ? layout.enemyHpMul : 1f;
            var props = Object.FindAnyObjectByType<Interactables>();
            if (props != null) { props.chestAnchors = layout.chestAnchors; props.altarAnchors = layout.altarAnchors; }
            bool spots = layout.secretSpots != null && layout.secretSpots.Length > 0;
            if (spots && (layout.secrets & ArenaSecrets.RattlingCrate) != 0)
            {
                var crate = new GameObject("RattlingCrate").AddComponent<RattlingCrate>(); // the Pit Goblin's discovery
                crate.transform.SetParent(arena, false);
                crate.spots = layout.secretSpots;
                crate.arenaCenter = layout.center;
                crate.flag = GladiatorSetup.Flag;
                crate.crateModel = ModelStyle.Load(GladiatorSetup.CrateModel);
                crate.goblinModel = ModelStyle.Load(GladiatorSetup.GoblinModel);
                crate.envMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
                crate.charMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            }
            if (spots && (layout.secrets & ArenaSecrets.GoldenRat) != 0) // Phase 5.6: the Sling's secret
            {
                var rat = new GameObject("GoldenRat").AddComponent<GoldenRat>();
                rat.transform.SetParent(arena, false);
                rat.holes = layout.secretSpots;
                rat.model = AssetDatabase.LoadAssetAtPath<GameObject>(GoldenRatModel);
                rat.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            }
            if (spots && (layout.secrets & ArenaSecrets.Bookmaker) != 0) // Phase 5.6: Loaded Dice's secret
            {
                var bookie = new GameObject("Bookmaker").AddComponent<Bookmaker>();
                bookie.transform.SetParent(arena, false);
                bookie.spots = layout.secretSpots;
                bookie.arenaCenter = layout.center;
                bookie.model = ModelStyle.Load(BookmakerModel);
                bookie.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
                // W1: his book (E in his ring) beside the run's other panels; only arenas with a bookmaker have one
                var book = new GameObject("WagerPanel").AddComponent<Gladiator.UI.WagerPanel>();
                var runRoot = GameObject.Find("[Run]");
                book.transform.SetParent(runRoot != null ? runRoot.transform : arena, false);
            }

            if (spots && (layout.secrets & ArenaSecrets.AshenStranger) != 0) // Phase 5.12: the Ashen Oracle's secret
            {
                var stranger = new GameObject("AshenStranger").AddComponent<AshenStranger>();
                stranger.transform.SetParent(arena, false);
                stranger.spots = layout.secretSpots;
                stranger.arenaCenter = layout.center;
                stranger.flag = GladiatorSetup.OracleFlag;
                stranger.model = ModelStyle.Load(GladiatorSetup.OracleModel);
                stranger.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            }

            var bot = new GameObject("[PlaytestBot]").AddComponent<PlaytestBot>();
            bot.arenaCenter = layout.center;
            bot.arenaRadius = enemies.ArenaMinRadius - 3f; // only without an EnemyManager: it uses the arena's own edge
            bot.gameObject.SetActive(false); // enabled by -bot / smoke test

            SceneSetupUtil.SaveScene(scene, scenePath);
            return scenePath;
        }

        static GameObject PlacePiece(in ArenaLayoutDef.Piece p, Transform parent)
        {
            Vector3? scale = p.scale == Vector3.one || p.scale == Vector3.zero ? (Vector3?)null : p.scale;
            GameObject go;
            switch (p.role)
            {
                case PieceRole.Walkable: go = ArenaSceneCommon.PlaceWalkable(p.model, parent, p.position, p.yaw, scale); break;
                case PieceRole.Solid: go = ArenaSceneCommon.PlaceSolid(p.model, parent, p.position, p.yaw, scale); break;
                case PieceRole.Prop: go = ArenaSceneCommon.PlaceProp(p.model, parent, p.position, p.yaw, scale); break;
                default: go = ArenaSceneCommon.PlaceDecor(p.model, parent, p.position, p.yaw, scale); break;
            }
            if (p.model.EndsWith("PR_Brazier")) AddFireLight(go);
            return go;
        }

        /// <summary>Invisible walls that only the player hits (Props layer: the camera and the height grid ignore them), tall
        /// enough that no number of air jumps clears them: along the layout's closed boundary loop (P1), else the legacy
        /// separate walls.</summary>
        static void AddBoundary(ArenaLayoutDef layout, Transform env)
        {
            if (layout.boundaryLoop != null && layout.boundaryLoop.Length >= 3) { AddBoundaryLoop(layout, env); return; }
            if (layout.boundary == null || layout.boundary.Length == 0) return;
            var root = new GameObject("Boundary").transform;
            root.SetParent(env, false);
            foreach (var b in layout.boundary)
            {
                var go = new GameObject("Wall");
                go.layer = Layers.Props;
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(b.x, 0f, b.y), Quaternion.Euler(0f, b.z, 0f));
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(b.w, 30f, 1f);
                box.center = new Vector3(0f, 12f, -0.5f); // yaw faces inward: the inner face on the line, the box behind it
            }
        }

        const float LoopWallThickness = 1f, LoopWallHeight = 30f;

        /// <summary>P1 (the producer's playtest of v0.17.0: the player got onto the Slave Pit's platforms and behind them
        /// where neighbouring walls stood at different distances): one wall per edge of the closed boundary loop, its
        /// inner face on the line and its box behind it, each run on past a convex corner so no wedge opens behind it;
        /// and ArenaBounds on the root (the motor's last-resort clamp to the same loop).</summary>
        static void AddBoundaryLoop(ArenaLayoutDef layout, Transform env)
        {
            var loop = layout.boundaryLoop;
            var root = new GameObject("Boundary").transform;
            root.SetParent(env, false);
            var bounds = root.gameObject.AddComponent<ArenaBounds>();
            bounds.polygon = (Vector2[])loop.Clone();
            bounds.center = new Vector2(layout.center.x, layout.center.z);
            int n = loop.Length, walls = 0;
            float wind = SignedArea(loop) >= 0f ? 1f : -1f;
            for (int k = 0; k < n; k++)
            {
                Vector2 prev = loop[(k + n - 1) % n], a = loop[k], b = loop[(k + 1) % n], next = loop[(k + 2) % n];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 1e-3f) continue;
                Vector2 dir = d / len, inward = new Vector2(-dir.y, dir.x) * wind;
                float extA = Convex(prev, a, b, wind) ? LoopWallThickness : 0f, extB = Convex(a, b, next, wind) ? LoopWallThickness : 0f;
                Vector2 mid = (a + b) * 0.5f + dir * ((extB - extA) * 0.5f);
                var go = new GameObject("Wall");
                go.layer = Layers.Props;
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(mid.x, 0f, mid.y), Quaternion.LookRotation(new Vector3(inward.x, 0f, inward.y)));
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(len + extA + extB, LoopWallHeight, LoopWallThickness);
                box.center = new Vector3(0f, LoopWallHeight * 0.4f, -LoopWallThickness * 0.5f); // the inner face on the line
                walls++;
            }
            Debug.Log($"[ArenaBuilder] {layout.id}: boundary loop of {n} corners, {walls} walls, {PerimeterOf(loop):0} m round");
        }

        /// <summary>Shoelace: positive when the loop runs counter-clockwise seen from above (x right, z up).</summary>
        public static float SignedArea(Vector2[] loop)
        {
            float twice = 0f;
            for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++) twice += loop[j].x * loop[i].y - loop[i].x * loop[j].y;
            return twice * 0.5f;
        }

        static float PerimeterOf(Vector2[] loop)
        {
            float l = 0f;
            for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++) l += (loop[i] - loop[j]).magnitude;
            return l;
        }

        /// <summary>True if the loop turns toward its inside at b (an interior angle under 180°).</summary>
        static bool Convex(Vector2 a, Vector2 b, Vector2 c, float wind)
        {
            Vector2 e1 = b - a, e2 = c - b;
            return (e1.x * e2.y - e1.y * e2.x) * wind > 0f;
        }

        /// <summary>True if (x, z) is inside the loop (crossing number; editor-side twin of ArenaBounds.Contains).</summary>
        public static bool InsideLoop(Vector2[] loop, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
            {
                Vector2 a = loop[i], b = loop[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>How far above its base a top may be before the player could perch on it out of the horde's reach
        /// (enemy melee needs |dy| &lt; 1).</summary>
        public const float PerchHeight = 1f;

        /// <summary>
        /// P1 (the safe perch): every top the player could stand on more than PerchHeight above the ground round it,
        /// where enemies can't follow, slides them off (NoPerch). A prop's box collider is its whole outline, so its top
        /// is the only place to stand: NoPerch goes on it. A solid piece reaching inside the boundary loop (a statue, a
        /// pillar, the obelisk, the shed) has ledges on its mesh: an invisible box sleeve round its outline (Props
        /// layer: the camera ignores it) with NoPerch hides them all. Walkable pieces are left alone: enemies climb them.
        /// </summary>
        static void AddPerchGuards(ArenaLayoutDef layout, List<(PieceRole role, GameObject go, float baseY)> placed)
        {
            var loop = layout.boundaryLoop;
            bool haveLoop = loop != null && loop.Length >= 3;
            int props = 0, sleeves = 0;
            var names = new System.Text.StringBuilder();
            foreach (var (role, go, baseY) in placed)
            {
                if (go == null || (role != PieceRole.Prop && role != PieceRole.Solid)) continue;
                var b = PieceBounds(go);
                if (b.size == Vector3.zero) continue;
                var t = go.transform;
                float top = float.MinValue;
                bool reachesIn = false;
                for (int i = 0; i < 8; i++)
                {
                    var w = t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    top = Mathf.Max(top, w.y);
                    if (haveLoop && InsideLoop(loop, new Vector2(w.x, w.z))) reachesIn = true;
                }
                if (top - baseY <= PerchHeight) continue;
                if (role == PieceRole.Prop)
                {
                    if (go.GetComponent<BoxCollider>() == null) continue;
                    go.AddComponent<NoPerch>();
                    props++;
                    continue;
                }
                var c = t.TransformPoint(b.center);
                if (!reachesIn && !(haveLoop && InsideLoop(loop, new Vector2(c.x, c.z)))) continue; // a wall or a gate: behind the loop
                var sleeve = new GameObject("PerchSleeve");
                sleeve.layer = Layers.Props;
                sleeve.transform.SetParent(t, false);
                var box = sleeve.AddComponent<BoxCollider>();
                // a little over the piece on every side and 10 cm over its top: its own highest face (a pillar's cap)
                // must never be level with the sleeve, or the player's ground probe can find it first
                box.center = b.center + Vector3.up * (SleeveOverTop * 0.5f);
                box.size = b.size + new Vector3(SleeveOverSide * 2f, SleeveOverTop, SleeveOverSide * 2f);
                sleeve.AddComponent<NoPerch>();
                sleeves++;
                if (names.Length < 400) names.Append(' ').Append(go.name);
            }
            Debug.Log($"[ArenaBuilder] {layout.id}: perch guards on {props} props, sleeves on {sleeves} solid pieces:{names}");
        }

        const float SleeveOverTop = 0.1f, SleeveOverSide = 0.02f;

        /// <summary>A placed model's mesh bounds in its root's local space (all its parts).</summary>
        static Bounds PieceBounds(GameObject go)
        {
            var inv = go.transform.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                var mb = m.bounds;
                var toRoot = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = toRoot.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Invisible stone (Environment layer: the camera, the player and the nav bake all see it) behind a wall
        /// that is decor only, like the amphitheatre's seating bowl: one big mesh would bake as one huge obstacle.</summary>
        static void AddSolidWalls(ArenaLayoutDef layout, Transform env)
        {
            if (layout.solidWalls == null || layout.solidWalls.Length == 0) return;
            var root = new GameObject("Stone").transform;
            root.SetParent(env, false);
            float h = layout.solidWallHeight > 0f ? layout.solidWallHeight : 8f;
            foreach (var b in layout.solidWalls)
            {
                var go = new GameObject("Wall");
                go.layer = Layers.Environment;
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(b.x, 0f, b.y), Quaternion.Euler(0f, b.z, 0f));
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(b.w, h, 1f);
                box.center = new Vector3(0f, h * 0.5f, -0.5f); // yaw faces inward: the inner face on the line, the box behind it
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>The chariot races (Phase 5.16): the race with the layout's track and schedule, its chariots (inactive
        /// until they launch, not static: they move) and the lap counter's eggs (not static: one tips per race).</summary>
        static void AddChariotRace(ArenaLayoutDef layout, Transform arena, Transform env)
        {
            var race = new GameObject("ChariotRace").AddComponent<ChariotRace>();
            race.transform.SetParent(arena, false);
            race.trackRadius = layout.trackRadius;
            race.trackWestMeta = layout.trackWestMeta;
            race.trackEastMeta = layout.trackEastMeta;
            race.trackWidth = layout.trackWidth;
            race.trackStallX = layout.trackStallX;
            race.chariotFirst = layout.chariotFirst;
            race.chariotEvery = layout.chariotEvery;
            race.chariotTelegraph = layout.chariotTelegraph;
            race.chariotSpeed = layout.chariotSpeed;
            race.chariotCount = Mathf.Max(1, layout.chariotCount);
            race.chariotLaps = Mathf.Max(1, layout.chariotLaps);

            string model = string.IsNullOrEmpty(layout.chariotModel) ? "Props/PR_Chariot" : layout.chariotModel;
            var prefab = ModelStyle.Load("Assets/Art/Models/" + model + ".fbx");
            if (prefab == null) Debug.LogError("[ArenaBuilder] missing chariot model " + model);
            var envMat = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            var chariots = new Transform[prefab != null ? race.chariotCount : 0];
            for (int c = 0; c < chariots.Length; c++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, race.transform);
                go.name = "Chariot" + c;
                LookSetup.AssignMaterial(go, envMat);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Props;
                UnStatic(go);
                go.SetActive(false);
                chariots[c] = go.transform;
            }
            race.chariots = chariots;

            foreach (var t in env.GetComponentsInChildren<Transform>(true))
                if (t.name == "PR_LapCounter") { race.lapCounter = t; break; }
            if (race.lapCounter != null)
                foreach (var t in race.lapCounter.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Egg")) UnStatic(t.gameObject);
        }

        sealed class HypogeumParts
        {
            public Transform[] doors, lifts;
            public Transform center;
        }

        /// <summary>R2: the trapdoors, the lifts and the stage's lift (decor, not static where they move: the leaves and
        /// the platforms; no mesh collider), each with an invisible walkable floor box over its hole (`TrapFloor`), so the
        /// player walks over an open door and the nav bake samples the floor there. Null when the layout has none.</summary>
        static HypogeumParts PlaceHypogeum(ArenaLayoutDef layout, Transform env)
        {
            int doors = layout.trapdoors != null ? layout.trapdoors.Length : 0;
            int lifts = layout.lifts != null ? layout.lifts.Length : 0;
            if (doors == 0 && lifts == 0 && !layout.hasCenterLift) return null;
            var root = new GameObject("Hypogeum").transform;
            root.SetParent(env, false);
            string doorModel = string.IsNullOrEmpty(layout.trapdoorModel) ? "Environment/SM_ColTrapdoor" : layout.trapdoorModel;
            string liftModel = string.IsNullOrEmpty(layout.liftModel) ? "Environment/SM_ColLift" : layout.liftModel;
            float side = layout.liftSize > 0f ? layout.liftSize : 4f;
            var parts = new HypogeumParts { doors = new Transform[doors], lifts = new Transform[lifts] };
            for (int d = 0; d < doors; d++)
            {
                var t = layout.trapdoors[d];
                var go = PlaceMoving(doorModel, root, new Vector3(t.x, 0f, t.y), t.z, 1f, "Trapdoor" + d, "LeafF", "LeafB");
                AddTrapFloor(go.transform, layout.trapdoorSize.x, layout.trapdoorSize.y, 1f);
                parts.doors[d] = go.transform;
            }
            for (int l = 0; l < lifts; l++)
            {
                var go = PlaceMoving(liftModel, root, layout.lifts[l], 0f, 1f, "Lift" + l, "Platform");
                AddTrapFloor(go.transform, side, side, 1f);
                parts.lifts[l] = go.transform;
            }
            if (layout.hasCenterLift)
            {
                float scale = layout.centerLiftScale > 0f ? layout.centerLiftScale : 1f;
                var go = PlaceMoving(liftModel, root, layout.centerLift, 0f, scale, "CenterLift", "Platform");
                AddTrapFloor(go.transform, side, side, scale);
                parts.center = go.transform;
            }
            return parts;
        }

        /// <summary>A decor model whose named parts move (they lose their static flags). Placed even when the model is
        /// missing (an empty stand-in at the right spot), so the floor box still lands over its hole.</summary>
        static GameObject PlaceMoving(string model, Transform parent, Vector3 pos, float yaw, float scale, string name, params string[] moving)
        {
            var go = ArenaSceneCommon.PlaceDecor(model, parent, pos, yaw, scale != 1f ? Vector3.one * scale : (Vector3?)null);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(moving, t.name) >= 0) UnStatic(t.gameObject);
            return go;
        }

        /// <summary>The invisible floor over a hole: a thin box (`sx` × 0.2 × `sz` m in the world) whose top is the model's
        /// origin, on the Environment layer and walkable (it feeds the height grid; never an obstacle).</summary>
        static void AddTrapFloor(Transform parent, float sx, float sz, float parentScale)
        {
            var go = new GameObject("TrapFloor");
            go.layer = Layers.Environment;
            go.transform.SetParent(parent, false);
            float h = 0.2f / Mathf.Max(0.01f, parentScale);
            go.transform.localPosition = new Vector3(0f, -h * 0.5f, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<BoxCollider>().size = new Vector3(sx, h, sz);
            go.AddComponent<WalkableSurface>();
        }

        /// <summary>R2: the hypogeum's runtime (Hypogeum: drops, lifts and packs, the boss's phases, the light) with the
        /// layout's doors, lifts and clocks.</summary>
        static void AddHypogeum(ArenaLayoutDef layout, Transform arena, HypogeumParts parts)
        {
            var hyp = new GameObject("Hypogeum").AddComponent<Hypogeum>();
            hyp.transform.SetParent(arena, false);
            hyp.trapdoors = layout.trapdoors ?? new Vector4[0];
            hyp.trapdoorSize = layout.trapdoorSize;
            hyp.doors = parts.doors;
            hyp.liftSpots = layout.lifts ?? new Vector3[0];
            hyp.liftSize = layout.liftSize > 0f ? layout.liftSize : 4f;
            hyp.lifts = parts.lifts;
            hyp.hasCenterLift = layout.hasCenterLift;
            hyp.centerLiftSpot = layout.centerLift;
            hyp.centerLiftScale = layout.centerLiftScale > 0f ? layout.centerLiftScale : 1f;
            hyp.centerLift = parts.center;
            hyp.dropFirst = layout.dropFirst;
            hyp.dropEvery = layout.dropEvery;
            hyp.dropTelegraph = layout.dropTelegraph;
            hyp.dropOpen = layout.dropOpen;
            hyp.liftFirst = layout.liftFirst;
            hyp.liftEvery = layout.liftEvery;
            hyp.liftsPerWave = Mathf.Max(0, layout.liftsPerWave);
            hyp.liftMinDistance = layout.liftMinDistance;
            hyp.liftPacks = layout.liftPacks ?? new ArenaLayoutDef.LiftPack[0];
        }

        static void AddFireLight(GameObject go)
        {
            var l = new GameObject("FireLight").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 1.6f, 0);
            l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.range = 6f; l.intensity = 3f; l.shadows = LightShadows.None;
        }

        /// <summary>Breakables are switched off at runtime: they can't be static-batched.</summary>
        static void UnStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        /// <summary>Gate frame as solid geometry; the doors are visual only (they swing) and an invisible wall behind
        /// them keeps the player in when they're open.</summary>
        static Transform PlaceGate(in ArenaLayoutDef.Gate g, Transform parent, string model)
        {
            var go = ArenaSceneCommon.PlaceSolid(model, parent, g.position, g.yaw);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "DoorL" && t.name != "DoorR") continue;
                foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var d in t.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(d.gameObject, 0);
            }
            var block = new GameObject("GateBlocker");
            block.layer = Layers.Environment;
            block.transform.SetParent(go.transform, false);
            block.transform.localPosition = new Vector3(0f, 2f, 0.3f); // on the door line: closed doors look and act solid
            block.AddComponent<BoxCollider>().size = new Vector3(4.4f, 4f, 0.3f);
            block.AddComponent<NoPerch>(); // P1: its top (4 m) is inside the boundary loop and in reach of two air jumps
            return go.transform;
        }

        static Mesh FirstMesh(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m) return m;
            Debug.LogError("[ArenaBuilder] no mesh in " + path);
            return null;
        }

        static Material EnsureCrowdMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(CrowdMat);
            var sh = Shader.Find("Gladiator/Crowd");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, CrowdMat); }
            else if (m.shader != sh) m.shader = sh;
            var palette = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            if (palette != null) m.SetTexture("_BaseMap", palette.GetTexture("_BaseMap"));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Deterministic spots inside each crowd zone, facing the arena centre with a little jitter.</summary>
        static Vector4[] CrowdSpots(ArenaLayoutDef layout)
        {
            var spots = new List<Vector4>();
            var rng = new System.Random(1234);
            foreach (var zone in layout.crowd)
            {
                var rot = Quaternion.Euler(0f, zone.yaw, 0f);
                int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(zone.count * zone.size.x / Mathf.Max(0.1f, zone.size.y))));
                int rows = Mathf.Max(1, Mathf.CeilToInt(zone.count / (float)cols));
                for (int k = 0; k < zone.count; k++)
                {
                    int cx = k % cols, cz = k / cols;
                    float x = ((cx + 0.5f) / cols - 0.5f) * zone.size.x + (float)(rng.NextDouble() - 0.5) * 0.35f;
                    float zz = ((cz + 0.5f) / rows - 0.5f) * zone.size.y + (float)(rng.NextDouble() - 0.5) * 0.25f;
                    var p = zone.center + rot * new Vector3(x, 0f, zz);
                    var toCentre = layout.center - p;
                    float yaw = Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg + (float)(rng.NextDouble() - 0.5) * 30f;
                    spots.Add(new Vector4(p.x, p.y, p.z, yaw));
                }
            }
            return spots.ToArray();
        }
    }
}
