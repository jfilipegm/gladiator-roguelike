using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Gladiator data (docs/GLADIATORS.md). Phase 5.2: the Pit Goblin (GL_PitGoblin) with its capsule, animation, the
    /// Bomb Satchel and its card, the Scavenger and Trap Slide traits, the Root status, and UL_PitGoblin (the rattling
    /// crate flag). Phase 5.3: the Blood Knight (GL_BloodKnight), the Crimson Greatsword and its card, the Bloodthirst,
    /// Bloodlust and Blood Rite traits, and UL_BloodKnight (Phase 5.9: reach Arena 3; it was "beat Gorm below 30 % HP").
    /// Phase 5.9: the Murder Machine (GL_MurderMachine), the Executioner's Cleaver (GroundSlam) and its card, the
    /// Executioner's Plate and Verdict traits, the Stagger status, and UL_MurderMachine (beat Gorm). Every asset is
    /// created once and is designer-owned afterwards; only missing links are filled in (and the knight's interim unlock
    /// is migrated once). Phase 5.10: the Thunder Champion (GL_ThunderChampion), the Storm Spear (Chain) and its card,
    /// the Stormcaller and Lightning Dash traits, and UL_ThunderChampion (beat the Beast Lord). Phase 5.11: the Golden
    /// Idol (GL_GoldenIdol), the Coin Toss (Projectile data) and its card, the Midas Body, Golden Streak and Make It Rain
    /// traits, and UL_GoldenIdol (10,000 lifetime gold). Phase 5.12: the Ashen Oracle (GL_AshenOracle), the Curse
    /// status, the Ashen Censer (Lob data + hit effects) and its card, the Contagion (Boon effects) and Smoke Veil traits,
    /// and UL_AshenOracle (the masked stranger's flag). Phase 5.14: the Net-Weaver (GL_NetWeaver), the Trident (MultiHit) and its
    /// card, the Caught (Boon effects) and Cast Net traits, and UL_NetWeaver (WinWithWeapon spear). Phase 5.15 and R3 live
    /// in their own files (BeastmasterSetup, AsterionSetup), called from here.
    /// </summary>
    public static class GladiatorSetup
    {
        public const string Dir = "Assets/Data/Gladiators/";
        public const string GoblinPath = Dir + "GL_PitGoblin.asset";
        public const string GoblinMovePath = "Assets/Data/Config/MovementConfig_Goblin.asset";
        public const string GoblinAnimPath = "Assets/Data/Config/PartAnim_Goblin.asset";
        public const string ScavengerPath = Dir + "TR_Scavenger.asset";
        public const string TrapSlidePath = Dir + "TR_TrapSlide.asset";
        public const string SatchelPath = UpgradeSetup.WeaponDir + "WP_BombSatchel.asset";
        public const string SatchelCardPath = UpgradeSetup.Dir + "UP_W_BombSatchel.asset";
        public const string GoblinUnlockPath = MetaSetup.Dir + "UL_PitGoblin.asset";
        public const string RootPath = "Assets/Data/Status/SE_Root.asset";
        public const string BleedPath = "Assets/Data/Status/SE_Bleed.asset";
        public const string GoblinModel = "Assets/Art/Models/Characters/CH_PitGoblin.fbx";
        public const string CrateModel = "Assets/Art/Models/Environment/PR_Crate.fbx";
        public const string BombMesh = "Assets/Art/Models/Weapons/WP_Bomb.fbx";
        public const string SnareMesh = "Assets/Art/Models/Props/PR_Snare.fbx";
        public const string Flag = "rattling_crate";

        public const string KnightPath = Dir + "GL_BloodKnight.asset";
        public const string KnightMovePath = "Assets/Data/Config/MovementConfig_Knight.asset";
        public const string KnightAnimPath = "Assets/Data/Config/PartAnim_Knight.asset";
        public const string BloodthirstPath = Dir + "TR_Bloodthirst.asset";
        public const string BloodlustPath = Dir + "TR_Bloodlust.asset";
        public const string BloodRitePath = Dir + "TR_BloodRite.asset";
        public const string GreatswordPath = UpgradeSetup.WeaponDir + "WP_CrimsonGreatsword.asset";
        public const string GreatswordCardPath = UpgradeSetup.Dir + "UP_W_CrimsonGreatsword.asset";
        public const string KnightUnlockPath = MetaSetup.Dir + "UL_BloodKnight.asset";
        public const string KnightModel = "Assets/Art/Models/Characters/CH_BloodKnight.fbx";

        public const string MachinePath = Dir + "GL_MurderMachine.asset";
        public const string MachineMovePath = "Assets/Data/Config/MovementConfig_Machine.asset";
        public const string MachineAnimPath = "Assets/Data/Config/PartAnim_Machine.asset";
        public const string PlatePath = Dir + "TR_ExecutionersPlate.asset";
        public const string VerdictPath = Dir + "TR_Verdict.asset";
        public const string CleaverPath = UpgradeSetup.WeaponDir + "WP_ExecutionersCleaver.asset";
        public const string CleaverCardPath = UpgradeSetup.Dir + "UP_W_ExecutionersCleaver.asset";
        public const string MachineUnlockPath = MetaSetup.Dir + "UL_MurderMachine.asset";
        public const string StaggerPath = "Assets/Data/Status/SE_Stagger.asset";
        public const string MachineModel = "Assets/Art/Models/Characters/CH_MurderMachine.fbx";

        public const string ThunderPath = Dir + "GL_ThunderChampion.asset";
        public const string ThunderMovePath = "Assets/Data/Config/MovementConfig_Thunder.asset";
        public const string ThunderAnimPath = "Assets/Data/Config/PartAnim_Thunder.asset";
        public const string StormcallerPath = Dir + "TR_Stormcaller.asset";
        public const string DashPath = Dir + "TR_LightningDash.asset";
        public const string StormSpearPath = UpgradeSetup.WeaponDir + "WP_StormSpear.asset";
        public const string StormSpearCardPath = UpgradeSetup.Dir + "UP_W_StormSpear.asset";
        public const string ThunderUnlockPath = MetaSetup.Dir + "UL_ThunderChampion.asset";
        public const string ShockPath = "Assets/Data/Status/SE_Shock.asset";
        public const string ThunderModel = "Assets/Art/Models/Characters/CH_ThunderChampion.fbx";

        public const string IdolPath = Dir + "GL_GoldenIdol.asset";
        public const string IdolMovePath = "Assets/Data/Config/MovementConfig_Idol.asset";
        public const string IdolAnimPath = "Assets/Data/Config/PartAnim_Idol.asset";
        public const string MidasPath = Dir + "TR_MidasBody.asset";
        public const string GoldenStreakPath = Dir + "TR_GoldenStreak.asset";
        public const string MakeItRainPath = Dir + "TR_MakeItRain.asset";
        public const string CoinTossPath = UpgradeSetup.WeaponDir + "WP_CoinToss.asset";
        public const string CoinTossCardPath = UpgradeSetup.Dir + "UP_W_CoinToss.asset";
        public const string IdolUnlockPath = MetaSetup.Dir + "UL_GoldenIdol.asset";
        public const string IdolModel = "Assets/Art/Models/Characters/CH_GoldenIdol.fbx";
        public const string CoinModel = "Assets/Art/Models/Props/PR_Coin.fbx";

        public const string OraclePath = Dir + "GL_AshenOracle.asset";
        public const string OracleMovePath = "Assets/Data/Config/MovementConfig_Oracle.asset";
        public const string OracleAnimPath = "Assets/Data/Config/PartAnim_Oracle.asset";
        public const string ContagionPath = Dir + "TR_Contagion.asset";
        public const string SmokeVeilPath = Dir + "TR_SmokeVeil.asset";
        public const string CenserPath = UpgradeSetup.WeaponDir + "WP_AshenCenser.asset";
        public const string CenserCardPath = UpgradeSetup.Dir + "UP_W_AshenCenser.asset";
        public const string OracleUnlockPath = MetaSetup.Dir + "UL_AshenOracle.asset";
        public const string CursePath = "Assets/Data/Status/SE_Curse.asset";
        public const string SlowPath = "Assets/Data/Status/SE_Slow.asset";
        public const string OracleModel = "Assets/Art/Models/Characters/CH_AshenOracle.fbx";
        public const string OracleFlag = "ashen_stranger";

        public const string NetWeaverPath = Dir + "GL_NetWeaver.asset";
        public const string NetWeaverMovePath = "Assets/Data/Config/MovementConfig_NetWeaver.asset";
        public const string NetWeaverAnimPath = "Assets/Data/Config/PartAnim_NetWeaver.asset";
        public const string CaughtPath = Dir + "TR_Caught.asset";
        public const string CastNetPath = Dir + "TR_CastNet.asset";
        public const string TridentPath = UpgradeSetup.WeaponDir + "WP_Trident.asset";
        public const string TridentCardPath = UpgradeSetup.Dir + "UP_W_Trident.asset";
        public const string NetWeaverUnlockPath = MetaSetup.Dir + "UL_NetWeaver.asset";
        public const string NetWeaverModel = "Assets/Art/Models/Characters/CH_NetWeaver.fbx";
        public const string TridentModel = "Assets/Art/Models/Weapons/WP_Trident.fbx";
        public const string NetModel = "Assets/Art/Models/Weapons/WP_Net.fbx";

        static WeaponDef.Level L(params StatMod[] mods) => new WeaponDef.Level { mods = mods };
        static StatMod Pct(StatId s, float v) => new StatMod(s, ModOp.Percent, v);
        static StatMod Add(StatId s, float v) => new StatMod(s, ModOp.Flat, v);

        [MenuItem("Gladiator/Setup/Gladiators/Create Gladiator Data")]
        public static GladiatorDef CreateOrUpdate()
        {
            UpgradeSetup.CreateOrUpdate();
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            if (nobody != null)
            {
                if (nobody.startingWeapon == null) nobody.startingWeapon = AssetDatabase.LoadAssetAtPath<WeaponDef>(CombatSetup.GladiusPath);
                if (string.IsNullOrEmpty(nobody.tradeoff)) nobody.tradeoff = "Steady: no strengths, no weaknesses.";
                EditorUtility.SetDirty(nobody);
            }

            var root = Status(RootPath, "root", "Root", "Rooted", StatusKind.Root, 2f, 0f, new Color(0.72f, 0.95f, 1f));
            var satchel = Satchel();
            var card = SatchelCard(satchel);
            var unlock = GoblinUnlock(card);
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, GoblinMovePath, m =>
            {
                m.capsuleHeight = 1.0f;
                m.slideCapsuleHeight = 0.6f;
                m.capsuleRadius = 0.28f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, GoblinAnimPath, a =>
            {
                a.strideLength = 1.0f;   // short legs: a quicker scurry
                a.bobHeight = 0.05f;
                a.runLean = 18f;         // hunched
            });
            var scavenger = Trait<ScavengerTrait>(ScavengerPath, "scavenger", "Scavenger",
                "Every 25 s, pulls an improvised weapon out of the sack: a weapon you don't hold, lent for 15 s.");
            var trap = Trait<TrapSlideTrait>(TrapSlidePath, "trap_slide", "Trap Slide",
                "Sliding drops a snare (3 at most). The first enemy to step in is held for 2 s and bleeds.");
            if (trap.root == null) trap.root = root;
            if (trap.bleed == null) trap.bleed = AssetDatabase.LoadAssetAtPath<StatusDef>(BleedPath);
            if (trap.mesh == null) trap.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SnareMesh);
            if (trap.material == null) trap.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            EditorUtility.SetDirty(trap);

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(GoblinPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(GoblinPath);
                g.id = "pit_goblin";
                g.displayName = "The Pit Goblin";
                g.description = "Lived under the pit floor. Lobs bombs, leaves snares, pulls junk out of its sack.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 70f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 1.12f },
                    new GladiatorDef.BaseStat { stat = StatId.Damage, value = 0.9f },
                    new GladiatorDef.BaseStat { stat = StatId.PickupRange, value = 1.25f },
                };
                g.cameraTargetHeight = 0.85f;
                g.tradeoff = "Fast and greedy, but fragile: 70 HP, -10 % damage.";
            }
            if (g.model == null) g.model = ModelStyle.Load(GoblinModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = satchel;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { scavenger, trap };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            BloodKnight();
            MurderMachine();
            ThunderChampion();
            GoldenIdol();
            AshenOracle();
            NetWeaver();
            BeastmasterSetup.CreateOrUpdate(); // Phase 5.15 (its own file: it also adds the Beast Handler to two arenas)
            AsterionSetup.CreateOrUpdate();    // R3 (its own file: it also sets the tease on the Eternal Victor)
            UpgradeSetup.Refresh();
            MetaSetup.CreateOrUpdate();
            IconSetup.Apply(); // the goblin's and the knight's weapon cards get their icons too
            AssetDatabase.SaveAssets();
            return g;
        }

        /// <summary>Every gladiator, the Nobody first (select screen and Codex order).</summary>
        public static GladiatorDef[] All()
        {
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            var goblin = AssetDatabase.LoadAssetAtPath<GladiatorDef>(GoblinPath);
            var knight = AssetDatabase.LoadAssetAtPath<GladiatorDef>(KnightPath);
            var machine = AssetDatabase.LoadAssetAtPath<GladiatorDef>(MachinePath);
            var thunder = AssetDatabase.LoadAssetAtPath<GladiatorDef>(ThunderPath);
            var idol = AssetDatabase.LoadAssetAtPath<GladiatorDef>(IdolPath);
            var oracle = AssetDatabase.LoadAssetAtPath<GladiatorDef>(OraclePath);
            var weaver = AssetDatabase.LoadAssetAtPath<GladiatorDef>(NetWeaverPath);
            var beastmaster = AssetDatabase.LoadAssetAtPath<GladiatorDef>(BeastmasterSetup.BeastmasterPath);
            var asterion = AssetDatabase.LoadAssetAtPath<GladiatorDef>(AsterionSetup.AsterionPath);
            var list = new System.Collections.Generic.List<GladiatorDef>();
            if (nobody != null) list.Add(nobody);
            if (goblin != null) list.Add(goblin);
            if (knight != null) list.Add(knight);
            if (machine != null) list.Add(machine);
            if (thunder != null) list.Add(thunder);
            if (idol != null) list.Add(idol);
            if (oracle != null) list.Add(oracle);
            if (weaver != null) list.Add(weaver);
            if (beastmaster != null) list.Add(beastmaster);
            if (asterion != null) list.Add(asterion);
            return list.ToArray();
        }

        /// <summary>Phase 5.3: the Blood Knight and everything only he brings.</summary>
        static GladiatorDef BloodKnight()
        {
            var bleed = AssetDatabase.LoadAssetAtPath<StatusDef>(BleedPath);
            var sword = Greatsword(bleed);
            var card = WeaponCard(GreatswordCardPath, sword, "GS");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(KnightUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(KnightUnlockPath);
                unlock.id = "blood_knight";
                unlock.displayName = "The Blood Knight";
                unlock.conditions = new[] { ReachTheAmphitheater() };
                unlock.addsToPool = new[] { card };
                KnightUnlockText(unlock);
                EditorUtility.SetDirty(unlock);
            }
            // Phase 5.9: Arena 3 exists, so the interim "beat Gorm below 30 % HP" becomes §6.7's "Reach Arena 3" (once;
            // a save that already unlocked him keeps him: unlocks are saved by id)
            if (unlock.conditions != null && unlock.conditions.Length == 1 && unlock.conditions[0].kind == UnlockConditionKind.DefeatBossBelowHp)
            {
                unlock.conditions = new[] { ReachTheAmphitheater() };
                KnightUnlockText(unlock);
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, KnightMovePath, m =>
            {
                m.capsuleHeight = 1.65f;
                m.slideCapsuleHeight = 0.95f;
                m.capsuleRadius = 0.36f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, KnightAnimPath, a =>
            {
                a.strideLength = 1.7f;   // long, heavy strides
                a.bobHeight = 0.05f;
                a.runLean = 8f;
                a.weaponArmSwingScale = 0.25f; // the blade stays on the shoulder while running
                a.swingYawStart = 110f;  // a wider, heavier cleave
                a.swingYawEnd = -130f;
                a.swingRaise = 95f;
            });
            var thirst = Trait<BloodthirstTrait>(BloodthirstPath, "bloodthirst", "Bloodthirst",
                "Kills heal you (1 HP, elites 8, at most 6 HP/s). Every other healing is halved.");
            var lust = Trait<BloodlustTrait>(BloodlustPath, "bloodlust", "Bloodlust",
                "Up to +60 % damage as your HP falls (full at 20 % HP or less).");
            var rite = Trait<BloodRiteTrait>(BloodRitePath, "blood_rite", "Blood Rite",
                "Kills and wounds fill the Blood meter. Slide with it full: pay 12 % of max HP for 8 s of frenzy (+40 % damage, +25 % attack speed, double kill heals).");

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(KnightPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(KnightPath);
                g.id = "blood_knight";
                g.displayName = "The Blood Knight";
                g.description = "A fallen knight who fights for the blood. Kills heal him; the closer to death, the harder he hits.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 120f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 0.92f },
                };
                g.cameraTargetHeight = 1.35f;
                g.tradeoff = "Lives on kills: slow, and other healing is halved.";
            }
            if (g.model == null) g.model = ModelStyle.Load(KnightModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = sword;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { thirst, lust, rite };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        /// <summary>Start a run in the Stone Amphitheater (arena tier 2, RunRecord.ArenaIndex).</summary>
        static UnlockCondition ReachTheAmphitheater() => new UnlockCondition { kind = UnlockConditionKind.ReachArena, amount = 2f };

        static void KnightUnlockText(UnlockDef u)
        {
            u.description = "You made it to the amphitheatre, and something in black plate took notice. The Blood Knight can be chosen, and his Crimson Greatsword joins the level-up pool.";
            u.hint = "Fight your way up to the Stone Amphitheater.";
        }

        /// <summary>Phase 5.9: the Murder Machine and everything only he brings.</summary>
        static GladiatorDef MurderMachine()
        {
            var stagger = Status(StaggerPath, "stagger", "Stagger", "Staggered", StatusKind.Root, 1f, 0f, new Color(0.78f, 0.86f, 0.92f));
            var cleaver = Cleaver();
            var card = WeaponCard(CleaverCardPath, cleaver, "EC");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(MachineUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(MachineUnlockPath);
                unlock.id = "murder_machine";
                unlock.displayName = "The Murder Machine";
                unlock.description = "Gorm is dead, and his executioner is out of work. The Murder Machine can be chosen, and his Executioner's Cleaver joins the level-up pool.";
                unlock.hint = "Gorm keeps an executioner below the pit. Kill Gorm, and he'll want new work.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 1f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, MachineMovePath, m =>
            {
                m.capsuleHeight = 2.0f;
                m.slideCapsuleHeight = 1.15f;
                m.capsuleRadius = 0.46f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, MachineAnimPath, a =>
            {
                a.strideLength = 1.25f;  // short legs, heavy steps
                a.bobHeight = 0.07f;
                a.runLean = 4f;
                a.armSwing *= 0.5f;
                a.weaponArmSwingScale = 0.15f; // the cleaver stays held out in front
                a.overheadChop = true;
                a.twoHanded = true;
                a.chopPitchStart = 165f;
                a.chopPitchEnd = 5f;
                ChopTiming(a);
            });
            var plate = Trait<ExecutionersPlateTrait>(PlatePath, "executioners_plate", "Executioner's Plate",
                "Every hit you take is 4 lower (never below 1), and nothing knocks you back.");
            var verdict = Trait<VerdictTrait>(VerdictPath, "verdict", "Verdict",
                "Hits you take fill the Wrath meter. Land from a jump with it full: a 7 m shockwave that hits everything round you and staggers the survivors.");
            if (verdict.stagger == null) { verdict.stagger = stagger; EditorUtility.SetDirty(verdict); }

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(MachinePath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(MachinePath);
                g.id = "murder_machine";
                g.displayName = "The Murder Machine";
                g.description = "The arena's executioner, sent down to the sand. Small hits barely scratch him, nothing knocks him back, and every hit he takes brings the Verdict closer.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 150f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 0.85f },
                };
                g.cameraTargetHeight = 1.6f;
                g.tradeoff = "Unstoppable but slow: 85 % speed, and the cleaver only hits in front of him.";
            }
            if (g.model == null) g.model = ModelStyle.Load(MachineModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = cleaver;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { plate, verdict };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        /// <summary>Up over the head and held, then down hard so the blade is on the ground at the impact (windup 0.38
        /// of 0.55 s = 0.69); a snappier pose blend so the downswing isn't smoothed away.</summary>
        public static void ChopTiming(PartAnimProfile a)
        {
            a.swingCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.45f, 0.06f), new Keyframe(0.62f, 1f), new Keyframe(1f, 1f));
            a.poseSharpness = 30f;
        }

        /// <summary>Phase 5.10: the Thunder Champion and everything only he brings.</summary>
        static GladiatorDef ThunderChampion()
        {
            var shock = AssetDatabase.LoadAssetAtPath<StatusDef>(ShockPath);
            var spear = StormSpear(shock);
            var card = WeaponCard(StormSpearCardPath, spear, "SS");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(ThunderUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(ThunderUnlockPath);
                unlock.id = "thunder_champion";
                unlock.displayName = "The Thunder Champion";
                unlock.description = "The Beast Lord is dead, and the storm god was watching. The Thunder Champion can be chosen, and his Storm Spear joins the level-up pool.";
                unlock.hint = "The storm god is watching the amphitheatre. Win there.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "beastlord", amount = 1f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, ThunderMovePath, m =>
            {
                m.capsuleHeight = 1.75f;
                m.slideCapsuleHeight = 1.0f;
                m.capsuleRadius = 0.36f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, ThunderAnimPath, a =>
            {
                a.strideLength = 1.75f; // long, bouncy strides
                a.bobHeight = 0.07f;
                a.runLean = 12f;
                a.weaponArmSwingScale = 0.2f; // the spear stays upright
                a.swingYawStart = 20f;  // a short jab toward the target: the lightning does the work
                a.swingYawEnd = -20f;
                a.swingRaise = 110f;
            });
            var storm = Trait<StormcallerTrait>(StormcallerPath, "stormcaller", "Stormcaller",
                "Every enemy you hit adds a Charge: +1.5 % attack speed each, up to +60 %. It drains after 2.5 s without a hit.");
            var dash = Trait<LightningDashTrait>(DashPath, "lightning_dash", "Lightning Dash",
                "Start a slide with the dash ready (every 3 s): blink up to 7 m, hitting and Shocking everything on the way.");
            if (dash.shock == null) { dash.shock = shock; EditorUtility.SetDirty(dash); }

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(ThunderPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(ThunderPath);
                g.id = "thunder_champion";
                g.displayName = "The Thunder Champion";
                g.description = "A champion who swears the storm god fights beside him. Lightning leaps from his spear through the crowd, every hit makes him faster, and his slide is a blink.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 90f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 1.05f },
                };
                g.cameraTargetHeight = 1.3f;
                g.tradeoff = "Fast and flashy but fragile: 90 HP, and a lone target leaves the lightning nothing to jump to.";
            }
            if (g.model == null) g.model = ModelStyle.Load(ThunderModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = spear;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { storm, dash };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        /// <summary>Phase 5.12: Curse, a stacking damage over time (purple).</summary>
        public static StatusDef Curse()
        {
            var c = AssetDatabase.LoadAssetAtPath<StatusDef>(CursePath);
            if (c != null) return c;
            c = PlayerSetup.EnsureAsset<StatusDef>(CursePath);
            c.id = "curse"; c.displayName = "Curse"; c.adjective = "Cursed"; c.kind = StatusKind.Curse;
            c.stacking = StatusStacking.Stack; c.duration = 4f; c.maxStacks = 5; c.speedMultiplier = 1f;
            c.color = new Color(0.66f, 0.38f, 0.95f);
            EditorUtility.SetDirty(c);
            return c;
        }

        /// <summary>Phase 5.12: the Ashen Oracle and everything only he brings.</summary>
        static GladiatorDef AshenOracle()
        {
            var curse = Curse();
            var censer = Censer(curse);
            var card = WeaponCard(CenserCardPath, censer, "AC");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(OracleUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(OracleUnlockPath);
                unlock.id = "ashen_oracle";
                unlock.displayName = "The Ashen Oracle";
                unlock.description = "The masked stranger read your future in the smoke, and liked it. The Ashen Oracle can be chosen, and his Ashen Censer joins the level-up pool.";
                unlock.hint = "A masked stranger watches the amphitheatre's fights. He wants a closer look at you.";
                unlock.category = UnlockCategory.Secret;
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.Flag, id = OracleFlag, amount = 1f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, OracleMovePath, m =>
            {
                m.capsuleHeight = 1.65f;
                m.slideCapsuleHeight = 0.95f;
                m.capsuleRadius = 0.36f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, OracleAnimPath, a =>
            {
                a.strideLength = 1.2f; // he drifts: short, smooth steps, almost no bob
                a.bobHeight = 0.02f;
                a.legSwing *= 0.6f;
                a.runLean = 6f;
                a.weaponArmSwingScale = 0.2f;
                a.swingYawStart = 30f;  // a swing of the censer
                a.swingYawEnd = -30f;
                a.swingRaise = 100f;
            });
            var contagion = Trait<EffectsTrait>(ContagionPath, "contagion", "Contagion",
                "A cursed enemy that dies bursts (6 × your damage within 3.5 m) and passes the curse on.");
            if (contagion.effects == null || contagion.effects.Length == 0)
            {
                contagion.effects = new[] { new BoonEffect { kind = EffectKind.BurstOnDeath, status = curse, power = 6f, radius = 3.5f } };
                EditorUtility.SetDirty(contagion);
            }
            var veil = Trait<SmokeVeilTrait>(SmokeVeilPath, "smoke_veil", "Smoke Veil",
                "The first hit you take every 5 s bursts into smoke: everything within 4.5 m is Cursed, Slowed and pushed back.");
            if (veil.curse == null || veil.slow == null)
            {
                veil.curse = curse;
                veil.slow = AssetDatabase.LoadAssetAtPath<StatusDef>(SlowPath);
                EditorUtility.SetDirty(veil);
            }

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(OraclePath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(OraclePath);
                g.id = "ashen_oracle";
                g.displayName = "The Ashen Oracle";
                g.description = "A masked seer who breathes smoke. His censer curses the crowd, the cursed burst when they die and curse the rest, and a hit on him explodes into smoke.";
                g.baseStats = new[] { new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 85f } };
                g.cameraTargetHeight = 1.25f;
                g.tradeoff = "Slow to start and fragile: curses take time, and a lone boss doesn't spread them.";
            }
            if (g.model == null) g.model = ModelStyle.Load(OracleModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = censer;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { contagion, veil };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        static Mesh FirstMesh(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m) return m;
            return null;
        }

        /// <summary>Phase 5.14: the Net-Weaver and everything only he brings.</summary>
        static GladiatorDef NetWeaver()
        {
            var root = AssetDatabase.LoadAssetAtPath<StatusDef>(RootPath);
            var slow = AssetDatabase.LoadAssetAtPath<StatusDef>(SlowPath);
            var trident = Trident();
            var card = WeaponCard(TridentCardPath, trident, "TR");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(NetWeaverUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(NetWeaverUnlockPath);
                unlock.id = "net_weaver";
                unlock.displayName = "The Net-Weaver";
                unlock.description = "You won with a spear in your hands, and a fisherman in the stands took note. The Net-Weaver can be chosen, and his Trident joins the level-up pool.";
                unlock.hint = "A fisherman watches the spearmen from the stands. Win a run with a spear in your hands.";
                // the Pit Spear or its evolution, the Impaler
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.WinWithWeapon, id = "spear|impaler", amount = 1f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, NetWeaverMovePath, m =>
            {
                m.capsuleHeight = 1.6f;
                m.slideCapsuleHeight = 0.95f;
                m.capsuleRadius = 0.36f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, NetWeaverAnimPath, a =>
            {
                a.strideLength = 1.5f; // light on his feet, a low wide stance
                a.bobHeight = 0.05f;
                a.runLean = 10f;
                a.weaponArmSwingScale = 0.3f; // the trident stays levelled
                a.swingYawStart = 25f;  // the flurry: short jabs left to right
                a.swingYawEnd = -25f;
                a.swingRaise = 70f;
            });
            var caught = Trait<EffectsTrait>(CaughtPath, "caught", "Caught",
                "Your hits do +50 % damage to Rooted enemies and +25 % to Slowed ones.");
            if (caught.effects == null || caught.effects.Length == 0)
            {
                caught.effects = new[]
                {
                    new BoonEffect { kind = EffectKind.DamageVsStatus, status = root, bonus = 0.5f },
                    new BoonEffect { kind = EffectKind.DamageVsStatus, status = slow, bonus = 0.25f },
                };
                EditorUtility.SetDirty(caught);
            }
            var cast = Trait<CastNetTrait>(CastNetPath, "cast_net", "Cast Net",
                "Start a slide with the net ready (every 4 s): it lands 5 m ahead, hauls everything within 3.2 m together and Roots it for 2 s. Bosses are only Slowed.");
            if (cast.root == null || cast.slow == null || cast.mesh == null || cast.material == null)
            {
                cast.root = root;
                cast.slow = slow;
                cast.mesh = FirstMesh(NetModel);
                cast.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
                cast.meshScale = 1f / 0.62f; // WP_Net's rim radius is 0.62 m
                EditorUtility.SetDirty(cast);
            }

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(NetWeaverPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(NetWeaverPath);
                g.id = "net_weaver";
                g.displayName = "The Net-Weaver";
                g.description = "A retiarius, a fisherman with a net and a trident. His slide flings a net that hauls the crowd together and holds it; the trident's flurry does the rest, harder on whatever is caught.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 95f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 1.05f },
                };
                g.cameraTargetHeight = 1.2f;
                g.tradeoff = "Control without armour: 95 HP, and a short reach when the net isn't ready.";
            }
            if (g.model == null) g.model = ModelStyle.Load(NetWeaverModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = trident;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { caught, cast };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        static WeaponDef Trident()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(TridentPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(TridentPath);
                w.id = "trident"; w.displayName = "Trident";
                w.description = "Three quick jabs fanned at the nearest enemy: all three land up close, and they spread across the crowd further out.";
                w.pattern = WeaponPattern.MultiHit;
                // range = each jab's length, width = its width, arcDegrees = the fan, prongs / prongInterval = the flurry
                w.damage = 8f; w.critChance = 0.08f; w.critMultiplier = 2f; w.knockback = 2.5f;
                w.cooldown = 1.2f; w.swingDuration = 0.34f; w.windup = 0.1f;
                w.range = 4.2f; w.width = 0.6f; w.arcDegrees = 36f; w.acquireRange = 4.6f; w.amount = 1; w.pierce = -1;
                w.prongs = 3; w.prongInterval = 0.07f; w.meshScale = 1f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.AttackSpeed, 0.15f)), L(Add(StatId.Amount, 1f)),
                    L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.CritChance, 0.1f)), L(Pct(StatId.Area, 0.15f), Pct(StatId.Damage, 0.2f)),
                };
            }
            if (w.mesh == null) w.mesh = FirstMesh(TridentModel);
            EditorUtility.SetDirty(w);
            return w;
        }

        static WeaponDef Censer(StatusDef curse)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(CenserPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(CenserPath);
                w.id = "ashen_censer"; w.displayName = "Ashen Censer";
                w.description = "Lobs a smoking orb into the crowd: it bursts, Curses everything it hits and leaves a cursed patch behind.";
                w.pattern = WeaponPattern.Lob;
                // width = burst radius, acquireRange = throw range; hit effects: Curse on hit + a cursed ground patch
                w.damage = 8f; w.critChance = 0.05f; w.critMultiplier = 2f; w.knockback = 3f;
                w.cooldown = 1.8f; w.swingDuration = 0.3f; w.windup = 0.12f;
                w.range = 10f; w.width = 2.4f; w.acquireRange = 10f; w.amount = 1;
                w.flightTime = 0.7f; w.fuse = 0.25f; w.arcHeight = 2.8f; w.meshScale = 1.1f;
                w.levels = new[]
                {
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f)), L(Pct(StatId.Area, 0.15f)),
                };
            }
            if (w.hitEffects == null || w.hitEffects.Length == 0)
                w.hitEffects = new[]
                {
                    new BoonEffect { kind = EffectKind.StatusOnHit, status = curse, chance = 1f, power = 0.4f, stacks = 2 },
                    new BoonEffect { kind = EffectKind.GroundOnHit, status = curse, radius = 1.8f, duration = 3f, power = 2f, stacks = 1 },
                };
            if (w.mesh == null) w.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BombMesh);
            EditorUtility.SetDirty(w);
            return w;
        }

        static Mesh CoinMesh()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(CoinModel)) if (o is Mesh m) return m;
            return null;
        }

        /// <summary>Phase 5.11: the Golden Idol and everything only he brings.</summary>
        static GladiatorDef GoldenIdol()
        {
            var toss = CoinToss();
            var card = WeaponCard(CoinTossCardPath, toss, "CT");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(IdolUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(IdolUnlockPath);
                unlock.id = "golden_idol";
                unlock.displayName = "The Golden Idol";
                unlock.description = "You've earned 10,000 gold on the sand, and someone has been counting. The Golden Idol can be chosen, and his Coin Toss joins the level-up pool.";
                unlock.hint = "Gold, and plenty of it: 10,000 over all your runs.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.LifetimeGold, amount = 10000f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, IdolMovePath, m =>
            {
                m.capsuleHeight = 1.7f;
                m.slideCapsuleHeight = 1.0f;
                m.capsuleRadius = 0.4f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, IdolAnimPath, a =>
            {
                a.strideLength = 1.45f; // a strut
                a.bobHeight = 0.08f;
                a.runLean = 3f;
                a.swingYawStart = 40f;  // a flick of the wrist
                a.swingYawEnd = -60f;
                a.swingRaise = 70f;
            });
            var midas = Trait<MidasBodyTrait>(MidasPath, "midas_body", "Midas Body",
                "Gold is power: +1 % damage for every 10 gold you carry, up to +100 %.");
            var streak = Trait<GoldenStreakTrait>(GoldenStreakPath, "golden_streak", "Golden Streak",
                "Kill streaks multiply your gold for 8 s: ×1.5 for a Killing Spree, up to ×3 for Unstoppable.");
            var rain = Trait<MakeItRainTrait>(MakeItRainPath, "make_it_rain", "Make It Rain",
                "Start a slide with 40 gold or more (every 2 s): pay 40 and 16 coins spray out round you.");
            if (rain.coinMesh == null) { rain.coinMesh = CoinMesh(); EditorUtility.SetDirty(rain); }

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(IdolPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(IdolPath);
                g.id = "golden_idol";
                g.displayName = "The Golden Idol";
                g.description = "A champion so rich he had himself gilded. The more gold he carries the harder he hits, kill streaks pay double, and his slide makes it rain.";
                g.baseStats = new[] { new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 100f } };
                g.cameraTargetHeight = 1.3f;
                g.tradeoff = "Weak until he's rich: every chest is a choice between an upgrade and his damage.";
            }
            if (g.model == null) g.model = ModelStyle.Load(IdolModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = toss;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { midas, streak, rain };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        static WeaponDef CoinToss()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(CoinTossPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(CoinTossPath);
                w.id = "coin_toss"; w.displayName = "Coin Toss";
                w.description = "Flicks gold coins at the nearest enemies. Each one goes through the first it hits.";
                w.pattern = WeaponPattern.Projectile;
                w.damage = 9f; w.critChance = 0.1f; w.critMultiplier = 2f; w.knockback = 2f;
                w.cooldown = 1.1f; w.swingDuration = 0.18f; w.windup = 0.06f;
                w.range = 12f; w.acquireRange = 12f; w.width = 0.4f; w.speed = 18f; w.amount = 3; w.pierce = 1;
                w.meshScale = 1.6f;
                w.levels = new[]
                {
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.2f)), L(Add(StatId.Pierce, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f)), L(Pct(StatId.AttackSpeed, 0.2f)),
                };
            }
            if (w.mesh == null) w.mesh = CoinMesh();
            EditorUtility.SetDirty(w);
            return w;
        }

        static WeaponDef StormSpear(StatusDef shock)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(StormSpearPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(StormSpearPath);
                w.id = "storm_spear"; w.displayName = "Storm Spear";
                w.description = "Lightning leaps from the spear's tip to the nearest enemy, then on through the crowd. Everything it strikes is Shocked.";
                w.pattern = WeaponPattern.Chain;
                // acquireRange = first strike's reach, width = jump range (× √Area), pierce = jumps after the first
                w.damage = 11f; w.critChance = 0.06f; w.critMultiplier = 2f; w.knockback = 1.5f;
                w.cooldown = 0.9f; w.swingDuration = 0.22f; w.windup = 0.08f;
                w.range = 9f; w.width = 5f; w.acquireRange = 9f; w.amount = 1; w.pierce = 3; w.spreadStrikes = true;
                w.onHitStatusPower = 0f; w.onHitStacks = 1;
                w.levels = new[]
                {
                    L(Add(StatId.Pierce, 1f)), L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.AttackSpeed, 0.15f)), L(Add(StatId.Amount, 1f)),
                    L(Add(StatId.Pierce, 1f)), L(Pct(StatId.Damage, 0.25f)), L(Pct(StatId.AttackSpeed, 0.2f), Pct(StatId.Area, 0.15f)),
                };
            }
            if (w.onHitStatus == null) w.onHitStatus = shock;
            EditorUtility.SetDirty(w);
            return w;
        }

        static WeaponDef Cleaver()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(CleaverPath);
            if (w != null) return w;
            w = PlayerSetup.EnsureAsset<WeaponDef>(CleaverPath);
            w.id = "executioners_cleaver"; w.displayName = "Executioner's Cleaver";
            w.description = "Raised over the head and brought down in front of you: everything where it lands is crushed and thrown back.";
            w.pattern = WeaponPattern.GroundSlam;
            // range = how far ahead it lands, width = the radius there (× √Area)
            w.damage = 34f; w.critChance = 0.06f; w.critMultiplier = 2f; w.knockback = 10f;
            w.cooldown = 2.1f; w.swingDuration = 0.55f; w.windup = 0.38f;
            w.range = 2.0f; w.width = 2.4f; w.acquireRange = 4.6f; w.amount = 1;
            w.levels = new[]
            {
                L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                L(Add(StatId.CritChance, 0.08f)), L(Pct(StatId.Damage, 0.25f), Pct(StatId.Area, 0.1f)), L(Pct(StatId.AttackSpeed, 0.2f), Pct(StatId.Knockback, 0.2f)),
            };
            EditorUtility.SetDirty(w);
            return w;
        }

        static WeaponDef Greatsword(StatusDef bleed)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(GreatswordPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(GreatswordPath);
                w.id = "crimson_greatsword"; w.displayName = "Crimson Greatsword";
                w.description = "A slow, enormous cleave around you. Every hit makes them bleed.";
                w.pattern = WeaponPattern.MeleeArc;
                w.damage = 28f; w.critChance = 0.08f; w.critMultiplier = 2f; w.knockback = 9f;
                w.cooldown = 1.5f; w.swingDuration = 0.42f; w.windup = 0.2f;
                w.range = 3.4f; w.arcDegrees = 220f; w.width = 0.6f; w.acquireRange = 3.6f; w.amount = 1;
                w.onHitStatusPower = 0.1f; w.onHitStacks = 1;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.AttackSpeed, 0.15f)), L(Add(StatId.Amount, 1f)),
                    L(Add(StatId.CritChance, 0.08f)), L(Pct(StatId.Damage, 0.25f), Pct(StatId.Area, 0.1f)), L(Pct(StatId.AttackSpeed, 0.2f), Pct(StatId.Knockback, 0.2f)),
                };
            }
            if (w.onHitStatus == null) w.onHitStatus = bleed;
            EditorUtility.SetDirty(w);
            return w;
        }

        static UpgradeDef WeaponCard(string path, WeaponDef weapon, string glyph)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(path);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(path);
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = glyph;
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
            return d;
        }

        static StatusDef Status(string path, string id, string name, string adjective, StatusKind kind, float duration, float speed, Color color)
        {
            var s = AssetDatabase.LoadAssetAtPath<StatusDef>(path);
            if (s != null) return s;
            s = PlayerSetup.EnsureAsset<StatusDef>(path);
            s.id = id; s.displayName = name; s.adjective = adjective; s.kind = kind;
            s.stacking = StatusStacking.Refresh; s.duration = duration; s.maxStacks = 1;
            s.speedMultiplier = speed; s.color = color;
            EditorUtility.SetDirty(s);
            return s;
        }

        static WeaponDef Satchel()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(SatchelPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(SatchelPath);
                w.id = "bomb_satchel"; w.displayName = "Bomb Satchel";
                w.description = "Lobs a bomb into the thickest crowd nearby. It lands, fizzes, and blows everything back.";
                w.pattern = WeaponPattern.Lob;
                // width = blast radius, acquireRange = throw range, flight/fuse shrink with Projectile Speed
                w.damage = 14f; w.critChance = 0.06f; w.critMultiplier = 2f; w.knockback = 7f;
                w.cooldown = 1.6f; w.swingDuration = 0.28f; w.windup = 0.12f;
                w.range = 9f; w.width = 2.2f; w.acquireRange = 9f; w.amount = 1;
                w.flightTime = 0.6f; w.fuse = 0.4f; w.arcHeight = 2.2f; w.meshScale = 1.1f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.ProjectileSpeed, 0.25f)),
                    L(Pct(StatId.AttackSpeed, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f), Pct(StatId.Area, 0.1f)),
                };
            }
            if (w.mesh == null) w.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BombMesh);
            EditorUtility.SetDirty(w);
            return w;
        }

        static UpgradeDef SatchelCard(WeaponDef satchel)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(SatchelCardPath);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(SatchelCardPath);
            d.id = "weapon_" + satchel.id; d.displayName = satchel.displayName; d.glyph = "BM";
            d.kind = UpgradeKind.Weapon;
            d.weapon = satchel;
            d.maxLevel = satchel.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
            return d;
        }

        static UnlockDef GoblinUnlock(UpgradeDef satchelCard)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnlockDef>(GoblinUnlockPath);
            if (u != null) return u;
            u = PlayerSetup.EnsureAsset<UnlockDef>(GoblinUnlockPath);
            u.id = "pit_goblin";
            u.displayName = "The Pit Goblin";
            u.description = "The thing in the crate wants a turn. The Pit Goblin can be chosen, and its Bomb Satchel joins the level-up pool.";
            u.hint = "Something rattles in the Slave Pit...";
            u.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.Flag, id = Flag, amount = 1f } };
            u.addsToPool = new[] { satchelCard };
            EditorUtility.SetDirty(u);
            return u;
        }

        static T Trait<T>(string path, string id, string name, string description) where T : GladiatorTrait
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            if (t != null) return t;
            t = PlayerSetup.EnsureAsset<T>(path);
            t.id = id; t.displayName = name; t.description = description;
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>A copy of `from` at `to` the first time (then `tweak`), else the existing asset.</summary>
        static T Copy<T>(string from, string to, System.Action<T> tweak) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(to);
            if (existing != null) return existing;
            if (!AssetDatabase.CopyAsset(from, to)) return PlayerSetup.EnsureAsset<T>(to);
            var t = AssetDatabase.LoadAssetAtPath<T>(to);
            tweak(t);
            EditorUtility.SetDirty(t);
            return t;
        }
    }
}
