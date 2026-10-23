using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>Feel config assets (created once, then hand-tuned — never overwritten) and the player prefab (always rebuilt).</summary>
    public static class PlayerSetup
    {
        public const string ConfigDir = "Assets/Data/Config";
        public const string MovementPath = ConfigDir + "/MovementConfig.asset";
        public const string CameraPath = ConfigDir + "/CameraConfig.asset";
        public const string AnimPath = ConfigDir + "/PartAnim_Chibi.asset";
        public const string PrefabPath = "Assets/Prefabs/Player/P_Player.prefab";
        const string Model = "Assets/Art/Models/Characters/CH_Nobody.fbx";

        public static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        [MenuItem("Gladiator/Setup/Player/Build Player Prefab")]
        public static string BuildPrefab()
        {
            ProjectSetup.EnsureLayersAndPhysics();
            CombatSetup.CreateOrUpdate();
            var move = EnsureAsset<MovementConfig>(MovementPath);
            EnsureAsset<CameraConfig>(CameraPath);
            var anim = EnsureAsset<PartAnimProfile>(AnimPath);

            var root = new GameObject("Player");
            root.layer = Layers.Player;
            root.AddComponent<CharacterController>();
            root.AddComponent<Player>();
            var motor = root.AddComponent<PlayerMotor>();
            motor.config = move;
            var feedback = AssetDatabase.LoadAssetAtPath<HitFeedbackConfig>(CombatSetup.FeedbackPath);
            var catalog = UpgradeSetup.CreateOrUpdate();
            root.AddComponent<PlayerStats>().gladiator = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            root.AddComponent<PlayerHealth>().feedback = feedback;
            var build = root.AddComponent<PlayerBuild>();
            build.config = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeSetup.ConfigPath);
            build.catalog = catalog;
            build.meta = MetaSetup.CreateOrUpdate();
            root.AddComponent<BoonEffects>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(ModelStyle.Load(Model), visual);
            LookSetup.AssignMaterial(model, AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat));
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Player;
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var camTarget = new GameObject("CameraTarget").transform;
            camTarget.SetParent(root.transform, false);
            camTarget.localPosition = new Vector3(0f, 1.25f, 0f);

            root.AddComponent<CameraProximityFade>().target = camTarget;

            var loadout = root.AddComponent<GladiatorLoadout>(); // swaps in the chosen gladiator's model, capsule, weapon, traits
            loadout.visualRoot = visual;
            loadout.cameraTarget = camTarget;
            loadout.characterMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);

            var animator = root.AddComponent<PlayerPartAnimator>();
            animator.profile = anim;
            animator.motor = motor;
            animator.visualRoot = visual;

            var weapons = root.AddComponent<WeaponSystem>();
            weapons.startingWeapons = new[] { AssetDatabase.LoadAssetAtPath<WeaponDef>(CombatSetup.GladiusPath) };
            weapons.animator = animator;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return PrefabPath;
        }
    }
}
