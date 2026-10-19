using Gladiator.Core;
using Gladiator.Gameplay;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gladiator.EditorTools
{
    /// <summary>Plumbing every gameplay scene needs: loop, look, player + camera rig, and placement helpers.</summary>
    public static class ArenaSceneCommon
    {
        const string Models = "Assets/Art/Models/";

        public static void AddLook(Vector3 sunEuler, Vector2 fog = default)
        {
            var sun = SceneSetupUtil.DirectionalLight("Sun", sunEuler, new Color(1f, 0.94f, 0.82f), 1.3f);
            var look = new GameObject("EnvironmentLighting").AddComponent<EnvironmentLighting>();
            look.skybox = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.SkyMat);
            look.sun = sun;
            if (fog.y > fog.x && fog.x > 0f) { look.fogStart = fog.x; look.fogEnd = fog.y; }
            look.Apply();
            var vol = new GameObject("PostVolume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookSetup.VolumeProfilePath);
        }

        public static GameLoop AddGameLoop() => new GameObject("[GameLoop]").AddComponent<GameLoop>();

        /// <summary>Player prefab instance + Main Camera (brain) + orbit vcam + PlayerCameraRig.</summary>
        public static (Player player, PlayerCameraRig rig) AddPlayerAndCamera(Vector3 spawn, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSetup.PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0, yaw, 0));
            var player = go.GetComponent<Player>();
            var target = go.transform.Find("CameraTarget");

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            camGo.tag = "MainCamera";
            camGo.AddComponent<CameraCollisionGuard>();
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;

            var vcamGo = new GameObject("PlayerVCam");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            vcam.LookAt = target;
            var lens = vcam.Lens;
            lens.FieldOfView = 62f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 300f;
            vcam.Lens = lens;
            vcamGo.AddComponent<CinemachineOrbitalFollow>();
            vcamGo.AddComponent<CinemachineRotationComposer>();
            vcamGo.AddComponent<CinemachineDeoccluder>();
            vcamGo.AddComponent<CinemachineDecollider>(); // after the deoccluder: extensions run in add order

            var rig = new GameObject("CameraRig").AddComponent<PlayerCameraRig>();
            rig.config = AssetDatabase.LoadAssetAtPath<CameraConfig>(PlayerSetup.CameraPath);
            rig.player = player;
            rig.motor = go.GetComponent<PlayerMotor>();
            rig.vcam = vcam;
            rig.lookTarget = target;
            rig.feedback = AssetDatabase.LoadAssetAtPath<HitFeedbackConfig>(CombatSetup.FeedbackPath);
            return (player, rig);
        }

        /// <summary>Enemies, weapon FX, death pops, blob shadows, damage numbers, bot sensor, debug spawner.</summary>
        public static (EnemyManager enemies, DebugEnemySpawner spawner) AddCombatSystems(Player player, float arenaRadius, int debugEnemies)
        {
            var feedback = AssetDatabase.LoadAssetAtPath<HitFeedbackConfig>(CombatSetup.FeedbackPath);
            var systems = new GameObject("[Combat]").transform;

            var popGo = new GameObject("DeathPopFx", typeof(ParticleSystem));
            popGo.transform.SetParent(systems, false);
            var pop = popGo.AddComponent<DeathPopFx>();
            pop.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.ParticleMat);

            var em = new GameObject("EnemyManager").AddComponent<EnemyManager>();
            em.transform.SetParent(systems, false);
            em.arenaRadius = arenaRadius;
            em.bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            em.flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.FlashMat);
            em.feedback = feedback;
            em.deathPop = pop;

            var blobs = new GameObject("BlobShadows").AddComponent<BlobShadowRenderer>();
            blobs.transform.SetParent(systems, false);
            blobs.material = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.BlobMat);

            var slash = new GameObject("SlashFx").AddComponent<SlashFx>();
            slash.transform.SetParent(systems, false);
            slash.arcMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CombatSetup.SlashMesh);
            slash.material = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.SlashMat);
            player.GetComponent<WeaponSystem>().slashFx = slash;

            var numbers = new GameObject("DamageNumbers").AddComponent<Gladiator.UI.DamageNumbers>();
            numbers.transform.SetParent(systems, false);
            numbers.feedback = feedback;
            numbers.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(CombatSetup.FontPath);
            numbers.fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.FontOutlinePath);

            var tele = new GameObject("TelegraphRenderer").AddComponent<TelegraphRenderer>();
            tele.transform.SetParent(systems, false);
            tele.material = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.TelegraphMat);

            var proj = new GameObject("EnemyProjectiles").AddComponent<EnemyProjectiles>();
            proj.transform.SetParent(systems, false);
            proj.material = em.bodyMaterial;
            proj.stoneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CombatSetup.StoneMesh);
            proj.potMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CombatSetup.PotMesh);
            proj.arrowMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CombatSetup.ArrowMesh);
            proj.feedback = feedback;
            proj.burst = pop;

            var shots = new GameObject("PlayerProjectiles").AddComponent<PlayerProjectiles>();
            shots.transform.SetParent(systems, false);
            shots.material = em.bodyMaterial;
            shots.streakMaterial = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.SlashMat);
            shots.burst = pop;

            var props = new GameObject("Interactables").AddComponent<Interactables>();
            props.transform.SetParent(systems, false);
            props.config = PlayerSetup.EnsureAsset<InteractableConfig>(CombatSetup.InteractablesPath);
            props.chestModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Props/PR_ArmoryChest.fbx");
            props.altarModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Props/PR_BloodAltar.fbx");
            props.burst = pop;
            props.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);

            var zones = new GameObject("HazardZones").AddComponent<HazardZones>();
            zones.transform.SetParent(systems, false);

            var spawner = new GameObject("DebugEnemySpawner").AddComponent<DebugEnemySpawner>();
            spawner.transform.SetParent(systems, false);
            spawner.enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(CombatSetup.ConvictPath);
            spawner.targetAlive = debugEnemies;
            spawner.ringRadius = arenaRadius - 2f;

            var director = new GameObject("Director").AddComponent<Director>();
            director.transform.SetParent(systems, false);
            director.config = AssetDatabase.LoadAssetAtPath<DirectorConfig>(CombatSetup.DirectorPath);
            director.table = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(CombatSetup.SlavePitTablePath);

            var sensor = new GameObject("RunBotSensor").AddComponent<RunBotSensor>();
            sensor.transform.SetParent(systems, false);
            sensor.stressSpawner = spawner;
            return (em, spawner);
        }

        const string Props = "Assets/Art/Models/Props/";

        /// <summary>Run state, XP gems/coins, level-up flow, HUD, level-up cards, EventSystem.</summary>
        public static void AddRunSystems(Player player)
        {
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(CombatSetup.ProgressionPath);
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(CombatSetup.FontPath);
            var fontMat = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.FontOutlinePath);
            var root = new GameObject("[Run]").transform;

            var run = new GameObject("RunState").AddComponent<RunState>();
            run.transform.SetParent(root, false);
            run.config = progression;

            var pickups = new GameObject("PickupSystem").AddComponent<PickupSystem>();
            pickups.transform.SetParent(root, false);
            pickups.config = progression;
            pickups.gemSmall = AssetDatabase.LoadAssetAtPath<Mesh>(Props + "PR_GemXP_S.fbx");
            pickups.gemMedium = AssetDatabase.LoadAssetAtPath<Mesh>(Props + "PR_GemXP_M.fbx");
            pickups.gemLarge = AssetDatabase.LoadAssetAtPath<Mesh>(Props + "PR_GemXP_L.fbx");
            pickups.coin = AssetDatabase.LoadAssetAtPath<Mesh>(Props + "PR_Coin.fbx");
            pickups.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);

            var lvl = new GameObject("LevelUpController").AddComponent<LevelUpController>();
            lvl.transform.SetParent(root, false);
            lvl.run = run;
            lvl.build = player.GetComponent<PlayerBuild>();
            lvl.burst = Object.FindAnyObjectByType<DeathPopFx>();

            var hud = new GameObject("Hud").AddComponent<Gladiator.UI.Hud>();
            hud.transform.SetParent(root, false);
            hud.font = font; hud.fontMaterial = fontMat;
            var threats = new GameObject("ThreatIndicators").AddComponent<Gladiator.UI.ThreatIndicators>();
            threats.transform.SetParent(root, false);
            var panel = new GameObject("LevelUpPanel").AddComponent<Gladiator.UI.LevelUpPanel>();
            panel.transform.SetParent(root, false);
            panel.font = font; panel.fontMaterial = fontMat;

            // How a run ends (death / timer) + the end screen.
            var end = new GameObject("RunEndController").AddComponent<RunEndController>();
            end.transform.SetParent(root, false);
            end.run = run;
            end.health = player.GetComponent<PlayerHealth>();
            end.animator = player.GetComponent<PlayerPartAnimator>();
            end.burst = lvl.burst;
            end.meta = MetaSetup.Load();
            var endPanel = new GameObject("RunEndPanel").AddComponent<Gladiator.UI.RunEndPanel>();
            endPanel.transform.SetParent(root, false);
            endPanel.font = font; endPanel.fontMaterial = fontMat;

            // Phase 4: sound (pooled voices + crowd loop), the crowd announcer, the options screen (pause → O).
            var sfx = new GameObject("SfxPlayer").AddComponent<SfxPlayer>();
            sfx.transform.SetParent(root, false);
            sfx.bank = AssetDatabase.LoadAssetAtPath<SfxBank>(AudioSetup.BankPath);
            var announcer = new GameObject("CrowdAnnouncer").AddComponent<CrowdAnnouncer>();
            announcer.transform.SetParent(root, false);
            announcer.config = AssetDatabase.LoadAssetAtPath<AnnouncerConfig>(AudioSetup.AnnouncerPath);
            // Phase 5.1: the crowd's Favor, its gifts, and the Arena Master's whims (docs/FAVOR.md)
            var favorConfig = PlayerSetup.EnsureAsset<FavorConfig>(CombatSetup.FavorPath);
            var favor = new GameObject("Favor").AddComponent<Favor>();
            favor.transform.SetParent(root, false);
            favor.config = favorConfig;
            var gifts = new GameObject("CrowdGifts").AddComponent<CrowdGifts>();
            gifts.transform.SetParent(root, false);
            gifts.config = favorConfig;
            gifts.models = new[] // by GiftKind: Amphora, GoldShower, WeaponInSand, Laurel
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(Props + "PR_GiftAmphora.fbx"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Props + "PR_GiftPouch.fbx"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Props + "PR_GiftWeapon.fbx"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Props + "PR_GiftLaurel.fbx"),
            };
            gifts.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            gifts.burst = lvl.burst;
            var master = new GameObject("ArenaMaster").AddComponent<ArenaMaster>();
            master.transform.SetParent(root, false);
            master.config = favorConfig;

            var hurt = new GameObject("HurtOverlay").AddComponent<Gladiator.UI.HurtOverlay>();
            hurt.transform.SetParent(root, false);
            hurt.feedback = AssetDatabase.LoadAssetAtPath<HitFeedbackConfig>(CombatSetup.FeedbackPath);
            var options = new GameObject("OptionsPanel").AddComponent<Gladiator.UI.OptionsPanel>();
            options.transform.SetParent(root, false);
            options.font = font; options.fontMaterial = fontMat;

            // Admin / test tools (F1; Editor and development builds).
            var admin = new GameObject("AdminController").AddComponent<AdminController>();
            admin.transform.SetParent(root, false);
            admin.catalog = EnemyCatalogUpdater.Refresh();
            admin.crowd = Object.FindAnyObjectByType<DebugEnemySpawner>();
            admin.health = player.GetComponent<PlayerHealth>();
            admin.pickups = pickups;
            var adminPanel = new GameObject("AdminPanel").AddComponent<Gladiator.UI.AdminPanel>();
            adminPanel.transform.SetParent(root, false);
            adminPanel.font = font; adminPanel.fontMaterial = fontMat;

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            es.transform.SetParent(root, false);
        }

        /// <summary>Big static geometry: Environment layer, mesh colliders, static batching. The camera avoids it.</summary>
        public static GameObject PlaceSolid(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null, bool walkable = false)
        {
            var go = Place(model, parent, pos, yaw, scale, Layers.Environment);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            if (walkable) go.AddComponent<WalkableSurface>();
            return go;
        }

        /// <summary>Ground enemies can walk on it (feeds the height grid; never an obstacle).</summary>
        public static GameObject PlaceWalkable(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null) =>
            PlaceSolid(model, parent, pos, yaw, scale, walkable: true);

        /// <summary>Small props: Props layer (player collides, camera ignores), one fitted box collider.</summary>
        public static GameObject PlaceProp(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null)
        {
            var go = Place(model, parent, pos, yaw, scale, Layers.Props);
            var b = LocalBounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
            return go;
        }

        /// <summary>Visual only (no collider).</summary>
        public static GameObject PlaceDecor(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null) =>
            Place(model, parent, pos, yaw, scale, Layers.Props);

        static GameObject Place(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale, int layer)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (prefab == null) { Debug.LogError("[ArenaSceneCommon] missing model " + model); return new GameObject("MISSING " + model); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            LookSetup.AssignMaterial(go, AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            return go;
        }

        /// <summary>
        /// Closed fence perimeter: `segments` fence pieces whose long axis follows the circle so neighbouring posts meet,
        /// each with a solid wall collider on exactly the fence line (taller than the fence so nobody jumps out;
        /// Environment layer so the camera stays inside too).
        /// </summary>
        public static void AddPerimeterFence(Transform parent, string model, float radius, int segments, float wallHeight)
        {
            var root = new GameObject("Perimeter").transform;
            root.SetParent(parent, false);
            float step = Mathf.PI * 2f / segments;
            float chord = 2f * radius * Mathf.Sin(step * 0.5f);
            float mid = radius * Mathf.Cos(step * 0.5f); // chord midpoints sit slightly inside the circle
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * step;
                var pos = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * mid;
                float yaw = a * Mathf.Rad2Deg; // local +X (the fence's long axis) → tangent (cos a, 0, -sin a)
                var fence = PlaceDecor(model, root, pos, yaw);
                fence.name = "Fence" + i;
                var w = new GameObject("FenceWall" + i);
                w.layer = Layers.Environment;
                w.transform.SetParent(root, false);
                w.transform.SetPositionAndRotation(pos + Vector3.up * wallHeight * 0.5f, Quaternion.Euler(0f, yaw, 0f));
                var bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(chord + 0.12f, wallHeight, 0.3f);
                GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
            }
        }

        static Bounds LocalBounds(GameObject go)
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
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toRoot.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
