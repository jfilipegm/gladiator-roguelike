using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Combat data + FX assets. Data assets are created once with starting values and then belong to the designer
    /// (never overwritten); materials are (re)configured every run.
    /// </summary>
    public static class CombatSetup
    {
        public const string FeedbackPath = "Assets/Data/Config/HitFeedbackConfig.asset";
        public const string ConvictPath = "Assets/Data/Enemies/EN_Convict.asset";
        public const string ConvictAnimPath = "Assets/Data/Config/PartAnim_Convict.asset";
        public const string GladiusPath = "Assets/Data/Weapons/WP_Gladius.asset";
        public const string SlashMat = LookSetup.MatDir + "/M_FxSlash.mat";
        public const string BlobMat = LookSetup.MatDir + "/M_BlobShadow.mat";
        public const string ParticleMat = LookSetup.MatDir + "/M_FxParticles.mat";
        public const string SlashMesh = "Assets/Art/Models/FX/FX_SlashArc.fbx";
        public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const string FontOutlinePath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";
        public const string ProgressionPath = "Assets/Data/Config/ProgressionConfig.asset";
        public const string TelegraphMat = LookSetup.MatDir + "/M_FxTelegraph.mat";
        public const string EnemyDir = "Assets/Data/Enemies/";
        public const string AffixDir = "Assets/Data/Enemies/Affixes/";
        public const string Models = "Assets/Art/Models/";
        public const string StoneMesh = Models + "Props/PR_SlingStone.fbx";
        public const string PotMesh = Models + "Props/PR_FirePot.fbx";

        [MenuItem("Gladiator/Setup/Combat/Create Or Update Combat Assets")]
        public static string CreateOrUpdate()
        {
            PlayerSetup.EnsureAsset<HitFeedbackConfig>(FeedbackPath);
            PlayerSetup.EnsureAsset<ProgressionConfig>(ProgressionPath);

            bool newAnim = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(ConvictAnimPath) == null;
            var anim = PlayerSetup.EnsureAsset<PartAnimProfile>(ConvictAnimPath);
            if (newAnim)
            {
                anim.strideLength = 1.0f; anim.legSwing = 38f; anim.armSwing = 20f; anim.bobHeight = 0.06f; anim.fullRunSpeed = 3.6f;
                EditorUtility.SetDirty(anim);
            }

            bool newConvict = AssetDatabase.LoadAssetAtPath<EnemyDef>(ConvictPath) == null;
            var convict = PlayerSetup.EnsureAsset<EnemyDef>(ConvictPath);
            if (newConvict)
            {
                convict.id = "convict";
                convict.displayName = "Starving Convict";
                convict.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Enemies/EN_Convict.fbx");
                convict.anim = anim;
                convict.maxHp = 20f; convict.moveSpeed = 3.6f; convict.radius = 0.36f; convict.contactDamage = 8f; convict.xp = 1f;
                EditorUtility.SetDirty(convict);
            }

            bool newGladius = AssetDatabase.LoadAssetAtPath<WeaponDef>(GladiusPath) == null;
            var gladius = PlayerSetup.EnsureAsset<WeaponDef>(GladiusPath);
            if (newGladius)
            {
                gladius.id = "gladius";
                gladius.displayName = "Rusty Gladius";
                gladius.description = "Fast arcs at the nearest enemy.";
                gladius.pattern = WeaponPattern.MeleeArc;
                EditorUtility.SetDirty(gladius);
            }

            CreateRoster(anim);

            var telegraph = MakeMat(TelegraphMat, "Gladiator/Telegraph");
            telegraph.enableInstancing = true;
            telegraph.SetFloat("_Intensity", 1.1f);
            EditorUtility.SetDirty(telegraph);
            var slash = MakeMat(SlashMat, "Gladiator/FxSlash");
            var blob = MakeMat(BlobMat, "Gladiator/BlobShadow");
            blob.enableInstancing = true;
            var particles = MakeMat(ParticleMat, "Universal Render Pipeline/Particles/Unlit");
            particles.SetColor("_BaseColor", Color.white);
            particles.enableInstancing = true;
            EditorUtility.SetDirty(slash); EditorUtility.SetDirty(blob); EditorUtility.SetDirty(particles);
            AssetDatabase.SaveAssets();
            return "combat assets ok";
        }

        /// <summary>
        /// M2.2 roster: five more enemy types + three elite affixes, with the starting numbers from DECISIONS.md.
        /// Created once; after that the assets are the designer's (only the model reference is re-linked if missing).
        /// </summary>
        static void CreateRoster(PartAnimProfile humanoid)
        {
            var cur = Profile("PartAnim_Cur", p => { p.strideLength = 0.85f; p.legSwing = 42f; p.armSwing = 70f; p.bobHeight = 0.05f; p.fullRunSpeed = 5.6f; });
            var brute = Profile("PartAnim_Brute", p => { p.strideLength = 1.5f; p.legSwing = 26f; p.armSwing = 22f; p.bobHeight = 0.09f; p.fullRunSpeed = 2.3f; });

            Enemy("EN_PitCur", "Pit Cur", "pitcur", cur, d =>
            {
                d.behaviour = EnemyBehaviour.Lunger;
                d.maxHp = 12f; d.moveSpeed = 5.6f; d.radius = 0.32f; d.acceleration = 20f; d.contactDamage = 6f; d.xp = 1f;
                d.attackRange = 3.5f; d.windup = 0.25f; d.lungeSpeed = 13f; d.lungeTime = 0.28f; d.attackDamage = 10f; d.attackCooldown = 2f;
                d.recover = 0.35f; d.goldChance = 0.04f; d.prewarm = 48;
            });
            Enemy("EN_ShieldThug", "Shield Thug", "shieldthug", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.ShieldBearer;
                d.maxHp = 45f; d.moveSpeed = 3.0f; d.radius = 0.45f; d.contactDamage = 10f; d.xp = 3f;
                d.blockArc = 120f; d.blockReduction = 0.85f; d.knockbackTaken = 0.35f; d.turnRate = 200f; d.goldChance = 0.08f; d.prewarm = 32;
            });
            Enemy("EN_Slinger", "Slinger", "slinger", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Ranged;
                d.maxHp = 16f; d.moveSpeed = 3.4f; d.radius = 0.36f; d.contactDamage = 5f; d.xp = 2f;
                d.keepDistance = new Vector2(6f, 10f); d.windup = 0.7f; d.attackCooldown = 2.6f; d.attackRange = 14f; d.recover = 0.35f;
                d.projectileSpeed = 13f; d.attackDamage = 9f; d.projectileRadius = 0.22f; d.goldChance = 0.06f; d.prewarm = 32;
            });
            Enemy("EN_Brute", "Brute", "brute", brute, d =>
            {
                d.behaviour = EnemyBehaviour.Slammer;
                d.maxHp = 180f; d.moveSpeed = 2.3f; d.radius = 0.75f; d.contactDamage = 14f; d.xp = 10f; d.knockbackTaken = 0.15f;
                d.attackRange = 2.4f; d.attackRadius = 2.8f; d.windup = 0.95f; d.attackDamage = 22f; d.attackCooldown = 3.2f; d.recover = 0.7f;
                d.turnRate = 160f; d.speedJitter = 0.05f; d.goldChance = 0.5f; d.prewarm = 8;
            });
            Enemy("EN_FirePot", "Fire-Pot Thrower", "firepot", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Lobber;
                d.maxHp = 22f; d.moveSpeed = 3.0f; d.radius = 0.38f; d.contactDamage = 5f; d.xp = 3f;
                d.keepDistance = new Vector2(7f, 11f); d.windup = 0.8f; d.lobTime = 0.95f; d.attackRange = 13f; d.recover = 0.4f;
                d.attackRadius = 1.8f; d.zoneDuration = 3.5f; d.zoneDps = 10f; d.attackCooldown = 3.5f; d.goldChance = 0.08f; d.prewarm = 16;
            });

            if (!AssetDatabase.IsValidFolder(AffixDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Data/Enemies", "Affixes");
            Affix("AF_Armored", "armored", "Armored", "#9FB4C8", a =>
            {
                a.hpMultiplier = 2.5f; a.damageTakenMultiplier = 0.7f; a.knockbackMultiplier = 0.3f; a.scale = 1.2f; a.xpMultiplier = 3f;
            });
            Affix("AF_Fast", "fast", "Fast", "#F0F05C", a =>
            {
                a.speedMultiplier = 1.6f; a.attackTimeMultiplier = 0.6f; a.hpMultiplier = 1.5f; a.scale = 1.15f; a.xpMultiplier = 3f;
            });
            Affix("AF_Explosive", "explosive", "Explosive", "#FF7A1F", a =>
            {
                a.hpMultiplier = 1.8f; a.scale = 1.2f; a.xpMultiplier = 3f; a.explodeOnDeath = true; a.fuse = 1f; a.explosionRadius = 2.6f;
                a.explosionDamageToPlayer = 18f; a.explosionDamageToEnemies = 60f;
            });
        }

        static PartAnimProfile Profile(string name, System.Action<PartAnimProfile> init)
        {
            string path = "Assets/Data/Config/" + name + ".asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(path) == null;
            var p = PlayerSetup.EnsureAsset<PartAnimProfile>(path);
            if (isNew) { init(p); EditorUtility.SetDirty(p); }
            return p;
        }

        static void Enemy(string asset, string displayName, string id, PartAnimProfile anim, System.Action<EnemyDef> init)
        {
            string path = EnemyDir + asset + ".asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<EnemyDef>(path) == null;
            var d = PlayerSetup.EnsureAsset<EnemyDef>(path);
            if (isNew)
            {
                d.id = id;
                d.displayName = displayName;
                d.anim = anim;
                init(d);
            }
            if (d.model == null) d.model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Enemies/" + asset + ".fbx");
            EditorUtility.SetDirty(d);
        }

        static void Affix(string asset, string id, string displayName, string hex, System.Action<AffixDef> init)
        {
            string path = AffixDir + asset + ".asset";
            if (AssetDatabase.LoadAssetAtPath<AffixDef>(path) != null) return;
            var a = PlayerSetup.EnsureAsset<AffixDef>(path);
            a.id = id;
            a.displayName = displayName;
            ColorUtility.TryParseHtmlString(hex, out a.color);
            init(a);
            EditorUtility.SetDirty(a);
        }

        static Material MakeMat(string path, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }
    }
}
