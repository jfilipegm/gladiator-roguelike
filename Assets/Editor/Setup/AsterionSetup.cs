using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// R3 (docs/GLADIATORS.md "Asterion, the Chained"; numbers and words in docs/tracks/R3-design.md § "Setup"): the
    /// roster's last gladiator. GL_Asterion (130 HP, 95 % speed, a 0.45 m capsule), the Labyrinth Chain (the Sweep
    /// pattern; WP_LabyrinthChain's Link and Weight meshes) and its card (HEAVY), the Gore and Rage traits, UL_Asterion
    /// (DefeatBoss eternal_victor) with the chain into the pool, and the tease at the Eternal Victor's fall ("BELOW THE
    /// SAND, A CHAIN SNAPS.", set here so BossSetup isn't touched and the order of the setups doesn't matter). Every
    /// asset is created once and is designer-owned afterwards; only missing links are filled in.
    /// </summary>
    public static class AsterionSetup
    {
        public const string Dir = GladiatorSetup.Dir;
        public const string AsterionPath = Dir + "GL_Asterion.asset";
        public const string MovePath = "Assets/Data/Config/MovementConfig_Asterion.asset";
        public const string AnimPath = "Assets/Data/Config/PartAnim_Asterion.asset";
        public const string GorePath = Dir + "TR_Gore.asset";
        public const string RagePath = Dir + "TR_Rage.asset";
        public const string ChainPath = UpgradeSetup.WeaponDir + "WP_LabyrinthChain.asset";
        public const string ChainCardPath = UpgradeSetup.Dir + "UP_W_LabyrinthChain.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_Asterion.asset";
        public const string Model = "Assets/Art/Models/Characters/CH_Asterion.fbx";
        public const string ChainModel = "Assets/Art/Models/Weapons/WP_LabyrinthChain.fbx";
        public const string RevealText = "BELOW THE SAND, A CHAIN SNAPS.";

        [MenuItem("Gladiator/Setup/Gladiators/Create Asterion Data")]
        public static GladiatorDef CreateOrUpdate()
        {
            var chain = LabyrinthChain();
            var card = WeaponCard(chain);
            var unlock = Unlock(card);
            var g = Asterion(chain, unlock);
            Tease(unlock);
            UpgradeSetup.Refresh();
            MetaSetup.CreateOrUpdate();
            AssetDatabase.SaveAssets();
            return g;
        }

        static GladiatorDef Asterion(WeaponDef chain, UnlockDef unlock)
        {
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, MovePath, m =>
            {
                m.capsuleHeight = 1.85f;
                m.slideCapsuleHeight = 1.1f;
                m.capsuleRadius = 0.45f; // the widest after the Machine's: his "big target" weakness
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, AnimPath, a =>
            {
                a.strideLength = 1.45f; // a heavy, stooped plod
                a.bobHeight = 0.08f;
                a.runLean = 16f;
                a.armSwing *= 0.6f; // long heavy arms
                a.weaponArmSwingScale = 0.25f;
                a.swingYawStart = 120f; // the chain's 240° sweep (mirrored on the way back)
                a.swingYawEnd = -120f;
                a.swingRaise = 20f;
                a.bodyTwistScale = 0.5f; // R3-STUB(chain)
                a.idleHeadLift = 18f;    // R3-STUB(chain): standing still, he looks at the sky
                // the pull (windup 0.32 of 0.8 s), then the sweep (0.3 s)
                a.swingCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 0.04f), new Keyframe(0.78f, 1f), new Keyframe(1f, 1f));
                a.poseSharpness = 24f;
            });
            var gore = Trait<GoreTrait>(GorePath, "gore", "Gore", // R3-STUB(rage)
                "Run into enemies head-first and you gore them: they're hurt and tossed out of your way.");
            var rage = Trait<RageTrait>(RagePath, "rage", "Rage", // R3-STUB(rage)
                "The crowd pressing round you fills your Rage, and so do hits. Full, you're Unchained for 8 s: the chain whirls all round, you're faster and gore everything you touch. Then you're Spent for 2 s.");

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(AsterionPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(AsterionPath);
                g.id = "asterion";
                g.displayName = "Asterion, the Chained";
                g.description = "The labyrinth's last prisoner, chained under the Colosseum for four hundred years. His chain sweeps the sand round him, his horns go through a line, and when the crowd closes in, he is Unchained.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 130f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 0.95f },
                };
                g.cameraTargetHeight = 1.5f;
                g.tradeoff = "Huge and heavy: 95 % speed, no armour, and his Rage takes him when the crowd closes in, not when you choose.";
            }
            if (g.model == null) g.model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = chain;
            if (g.traits == null || g.traits.Length == 0 || System.Array.IndexOf(g.traits, null) >= 0) g.traits = new GladiatorTrait[] { gore, rage };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        static WeaponDef LabyrinthChain()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(ChainPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(ChainPath);
                w.id = "labyrinth_chain"; w.displayName = "Labyrinth Chain";
                w.description = "A heavy chain with a block of the labyrinth's wall at its end, swept round you in a wide arc. Everything it passes is shoved aside; the stone always crits: fight at the chain's length.";
                w.pattern = WeaponPattern.Sweep; // R3-STUB(chain)
                // range = the chain's reach, arcDegrees = the sweep, tipLength = the stone (always crits), width = the
                // chain's thickness, amount = sweeps per trigger (the backswings)
                w.damage = 15f; w.critChance = 0.05f; w.critMultiplier = 2f; w.knockback = 7f;
                w.cooldown = 1.6f; w.swingDuration = 0.8f; w.windup = 0.32f;
                w.range = 5.2f; w.arcDegrees = 240f; w.width = 0.5f; w.acquireRange = 5.6f; w.amount = 1; w.tipLength = 1.4f;
                w.meshScale = 1f;
                Sweep(w); // R3-STUB(chain)
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Pct(StatId.Damage, 0.25f), Pct(StatId.Knockback, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Add(StatId.Amount, 1f), Pct(StatId.Damage, 0.15f)),
                };
            }
            if (w.mesh == null) w.mesh = MeshNamed(ChainModel, "Weight"); // the stone
            if (w.linkMesh == null) w.linkMesh = MeshNamed(ChainModel, "Link"); // R3-STUB(chain)
            if (w.mesh == null) Debug.LogWarning("[AsterionSetup] " + ChainModel + " has no Weight mesh: the chain won't draw (export it, then run this again)");
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>The Sweep's own numbers (WeaponDef's "Sweep" header).</summary>
        static void Sweep(WeaponDef w)
        {
            // R3-STUB(chain): every field below
            w.sweepTime = 0.3f;
            w.whirlTimeMul = 1.5f;
            w.sweepOutward = 0.35f;
            w.tipKnockbackMul = 1.6f;
            w.linkLength = 0.6f;
            w.chainHeight = new Vector2(1.05f, 0.55f);
            w.weightOffset = 0.33f; // the stone's centre past the last link's end (the art's Weight origin)
            w.modelPart = "Chain";  // CH_Asterion's trailing chain: hidden while the weapon swings it
        }

        static UpgradeDef WeaponCard(WeaponDef weapon)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(ChainCardPath);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(ChainCardPath);
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = "LC";
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            d.tags = UpgradeTag.Heavy; // Q8
            EditorUtility.SetDirty(d);
            return d;
        }

        static UnlockDef Unlock(UpgradeDef card)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnlockDef>(UnlockPath);
            if (u != null) return u;
            u = PlayerSetup.EnsureAsset<UnlockDef>(UnlockPath);
            u.id = "asterion";
            u.displayName = "Asterion, the Chained";
            u.description = "The Eternal Victor is dust, and the chain he kept under the stage for four hundred years has snapped. Asterion can be chosen, and his Labyrinth Chain joins the level-up pool.";
            u.hint = "Something has been chained under the Colosseum since its first games. Its jailer is the Eternal Victor.";
            u.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "eternal_victor", amount = 1f } };
            u.addsToPool = new[] { card };
            EditorUtility.SetDirty(u);
            return u;
        }

        /// <summary>The line at the Eternal Victor's fall while Asterion is still locked (Q6). Only while the boss has
        /// no line of its own: a designer's edit stays.</summary>
        static void Tease(UnlockDef unlock)
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(BossSetup.EternalVictorPath);
            if (boss == null)
            {
                Debug.LogWarning("[AsterionSetup] no " + BossSetup.EternalVictorPath + " yet: run the boss setup, then this again for the tease");
                return;
            }
            if (!string.IsNullOrEmpty(boss.revealText)) return;
            boss.revealText = RevealText;
            boss.revealUnlock = unlock;
            boss.revealDelay = 1.2f;
            boss.revealHold = 1.8f;
            EditorUtility.SetDirty(boss);
        }

        static Mesh MeshNamed(string path, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m && m.name == name) return m;
            return null;
        }

        static WeaponDef.Level L(params StatMod[] mods) => new WeaponDef.Level { mods = mods };
        static StatMod Pct(StatId s, float v) => new StatMod(s, ModOp.Percent, v);
        static StatMod Add(StatId s, float v) => new StatMod(s, ModOp.Flat, v);

        static T Trait<T>(string path, string id, string name, string description) where T : GladiatorTrait
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            if (t != null) return t;
            t = PlayerSetup.EnsureAsset<T>(path);
            t.id = id; t.displayName = name; t.description = description;
            EditorUtility.SetDirty(t);
            return t;
        }

        static T Copy<T>(string from, string to, System.Action<T> tweak) where T : ScriptableObject
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(to);
            if (t != null) return t;
            if (!AssetDatabase.CopyAsset(from, to)) return PlayerSetup.EnsureAsset<T>(to);
            t = AssetDatabase.LoadAssetAtPath<T>(to);
            tweak(t);
            EditorUtility.SetDirty(t);
            return t;
        }
    }
}
