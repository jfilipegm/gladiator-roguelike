using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Phase 5.15 (docs/GLADIATORS.md "The Beastmaster"): the Beast Handler enemy (a Lunger with a whip who walks two
    /// Pit Curs on leashes; kill him and they're freed) in the Stone Amphitheater's and the Grand Arena's tables; the
    /// Beastmaster (GL_Beastmaster), the Wolf Pack (the Summon pattern, CH_Wolf) and its card, the Feral Bond and
    /// Hunting Horn traits, and UL_Beastmaster (KillEnemies beasthandler 25).
    /// Note 37 (v0.28.0 playtest: the wolves got stuck on a couple of mobs while he had no weapon of his own): the
    /// Hunting Spear (WP_HuntingSpear, a short quick Thrust) and its card (only ever levelled by whoever holds it), the
    /// Hunter's Mark trait (he starts with the spear; its hits mark enemies for the pack), and the pack's shorter leash.
    /// </summary>
    public static class BeastmasterSetup
    {
        const string EnemyDir = "Assets/Data/Enemies/";
        public const string HandlerPath = EnemyDir + "EN_BeastHandler.asset";
        public const string CurPath = EnemyDir + "EN_PitCur.asset";
        public const string Dir = GladiatorSetup.Dir;
        public const string BeastmasterPath = Dir + "GL_Beastmaster.asset";
        public const string MovePath = "Assets/Data/Config/MovementConfig_Beastmaster.asset";
        public const string AnimPath = "Assets/Data/Config/PartAnim_Beastmaster.asset";
        public const string FeralBondPath = Dir + "TR_FeralBond.asset";
        public const string HornPath = Dir + "TR_HuntingHorn.asset";
        public const string WolfPackPath = UpgradeSetup.WeaponDir + "WP_WolfPack.asset";
        public const string WolfPackCardPath = UpgradeSetup.Dir + "UP_W_WolfPack.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_Beastmaster.asset";
        public const string Model = "Assets/Art/Models/Characters/CH_Beastmaster.fbx";
        public const string WolfModel = "Assets/Art/Models/Characters/CH_Wolf.fbx";
        public const string HandlerId = "beasthandler";
        public const string MarkPath = Dir + "TR_HuntersMark.asset";
        public const string SpearPath = UpgradeSetup.WeaponDir + "WP_HuntingSpear.asset";
        public const string SpearCardPath = UpgradeSetup.Dir + "UP_W_HuntingSpear.asset";
        public const string SpearModel = "Assets/Art/Models/Weapons/WP_HuntingSpear.fbx";
        public const string SigilModel = "Assets/Art/Models/FX/FX_HuntMark.fbx";
        const string PitSpearModel = "Assets/Art/Models/Weapons/WP_Spear.fbx";

        [MenuItem("Gladiator/Setup/Gladiators/Create Beastmaster Data")]
        public static GladiatorDef CreateOrUpdate()
        {
            EnsureHandler();
            // measuring bot runs in the Amphitheater (C26): weight 0.8-1.4 killed 219 handlers, 0.3-0.5 (cap 4) 73; rare now, so 25 takes
            // a run or two and they don't flood the floor
            AddToTable(AmphitheaterSetup.EnsureTable(), 130f, 0.12f, 0.2f, 3);
            AddToTable(GrandArenaSetup.EnsureTable(), 90f, 0.12f, 0.2f, 3);
            var g = Beastmaster();
            AssetDatabase.SaveAssets();
            UpgradeSetup.Refresh(); // note 37: the Hunting Spear's card joins the catalog (PlayerBuild levels it from there)
            IconSetup.Apply();
            return g;
        }

        /// <summary>The Beast Handler: a whip-cracking Lunger (a short step and a long crack) who walks two Pit Curs on
        /// leashes. Slow: he's walking beasts. Kill him and the curs are freed.</summary>
        public static EnemyDef EnsureHandler()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            CombatSetup.Enemy("EN_BeastHandler", "Beast Handler", HandlerId, humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Lunger; // braces, then the whip cracks
                d.maxHp = 110f; d.moveSpeed = 2.5f; d.radius = 0.5f; d.acceleration = 10f; d.contactDamage = 10f; d.xp = 8f;
                d.windup = 0.6f; d.attackRange = 3.5f; d.lungeSpeed = 6f; d.lungeTime = 0.2f; d.attackDamage = 14f;
                d.attackCooldown = 3.2f; d.recover = 0.6f; d.knockbackTaken = 0.5f; d.turnRate = 280f;
                d.goldChance = 0.25f; d.prewarm = 10;
                d.escortCount = 2; d.leashLength = 3.5f;
            });
            var h = AssetDatabase.LoadAssetAtPath<EnemyDef>(HandlerPath);
            if (h != null && h.escort == null)
            {
                h.escort = AssetDatabase.LoadAssetAtPath<EnemyDef>(CurPath);
                EditorUtility.SetDirty(h);
            }
            var cur = AssetDatabase.LoadAssetAtPath<EnemyDef>(CurPath);
            if (cur != null && cur.prewarm < 110) { cur.prewarm = 110; EditorUtility.SetDirty(cur); } // + two per handler
            return h;
        }

        static void AddToTable(SpawnTableDef t, float from, float w0, float w1, int cap)
        {
            var h = AssetDatabase.LoadAssetAtPath<EnemyDef>(HandlerPath);
            if (t == null || h == null) return;
            foreach (var e in t.entries) if (e.enemy == h) return;
            var list = new System.Collections.Generic.List<SpawnTableDef.Entry>(t.entries)
            {
                new SpawnTableDef.Entry { enemy = h, from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = false, elite = true },
            };
            t.entries = list.ToArray();
            EditorUtility.SetDirty(t);
        }

        static GladiatorDef Beastmaster()
        {
            var stagger = AssetDatabase.LoadAssetAtPath<StatusDef>(GladiatorSetup.StaggerPath);
            var pack = WolfPack();
            var card = WeaponCard(pack);
            var spear = HuntingSpear();
            var spearCard = SpearCard(spear);
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(UnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(UnlockPath);
                unlock.id = "beastmaster";
                unlock.displayName = "The Beastmaster";
                unlock.description = "Twenty-five handlers dead, fifty beasts running free, and a hunter from the forests has heard about you. The Beastmaster can be chosen, and his Wolf Pack joins the level-up pool.";
                unlock.hint = "Somewhere a man is waiting for someone to free the beasts. Kill their handlers.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.KillEnemies, id = HandlerId, amount = 25f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            // note 37: the spear is held back with him (its card asks for itself, so it's never a new pick anyway; this
            // keeps the crowd's weapon-in-the-sand and the goblin's scavenging off it until he's earned)
            if (spearCard != null && System.Array.IndexOf(unlock.addsToPool ?? System.Array.Empty<UpgradeDef>(), spearCard) < 0)
            {
                var pool = new System.Collections.Generic.List<UpgradeDef>(unlock.addsToPool ?? System.Array.Empty<UpgradeDef>()) { spearCard };
                unlock.addsToPool = pool.ToArray();
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, MovePath, m =>
            {
                m.capsuleHeight = 1.65f;
                m.slideCapsuleHeight = 0.95f;
                m.capsuleRadius = 0.38f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, AnimPath, a =>
            {
                a.strideLength = 1.65f; // a loping, wide stride
                a.bobHeight = 0.06f;
                a.runLean = 12f;
            });
            var bond = Trait<FeralBondTrait>(FeralBondPath, "feral_bond", "Feral Bond",
                "When you're hit, your wolves frenzy for 4 s: they bite 60 % faster and run 25 % faster.");
            var horn = Trait<HuntingHornTrait>(HornPath, "hunting_horn", "Hunting Horn",
                "Slide with the horn ready (every 5 s): every wolf pounces ahead of you, biting three times and Staggering.");
            if (horn.status == null) { horn.status = stagger; EditorUtility.SetDirty(horn); }
            // v0.29.0: shorter lines so three traits fit his tablet (only the old texts are replaced)
            ShortenTrait(horn, "Start a slide with the horn ready (every 5 s): every wolf pounces on the enemies ahead of you, biting three times and Staggering them.",
                "Slide with the horn ready (every 5 s): every wolf pounces ahead of you, biting three times and Staggering.");
            var mark = Trait<HuntersMarkTrait>(MarkPath, "hunters_mark", "Hunter's Mark",
                "Your Hunting Spear marks what it hits for 4 s; the wolves hunt marked enemies first.");
            ShortenTrait(mark, "You also carry a Hunting Spear. Whatever it hits is marked for 4 s, and your wolves go for marked enemies first.",
                "Your Hunting Spear marks what it hits for 4 s; the wolves hunt marked enemies first.");
            if (mark.weapon == null) mark.weapon = spear;
            if (mark.sigil == null) mark.sigil = AssetDatabase.LoadAssetAtPath<Mesh>(SigilModel);
            if (mark.material == null) mark.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            EditorUtility.SetDirty(mark);

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(BeastmasterPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(BeastmasterPath);
                g.id = "beastmaster";
                g.displayName = "The Beastmaster";
                g.description = Description;
                g.baseStats = new[] { new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 100f } };
                g.cameraTargetHeight = 1.25f;
                g.tradeoff = Tradeoff;
            }
            // note 37: the texts from before the spear move on (a designer's own text stays)
            if (g.description == OldDescription || g.description == SpearDescriptionV1) g.description = Description;
            if (g.tradeoff == OldTradeoff) g.tradeoff = Tradeoff;
            if (string.IsNullOrEmpty(g.selectLine) || g.selectLine == "Starts with the Wolf Pack and a Hunting Spear") // the long one ran off the tablet's line
                g.selectLine = "Starts with Wolf Pack + Spear";
            if (g.model == null) g.model = ModelStyle.Load(Model);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = pack;
            if (g.traits == null || g.traits.Length == 0 || System.Array.IndexOf(g.traits, null) >= 0) g.traits = new GladiatorTrait[] { bond, horn, mark };
            else if (System.Array.IndexOf(g.traits, mark) < 0)
            {
                var traits = new System.Collections.Generic.List<GladiatorTrait>(g.traits) { mark };
                g.traits = traits.ToArray();
            }
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        const string SpearDescriptionV1 = "A hunter from the frontier forests who won't fight without his wolves. His spear marks the prey, the pack runs it down; they frenzy when he's hurt and pounce when he blows his horn."; // too long for the tablet with three traits
        const string OldDescription = "A hunter from the frontier forests who won't fight without his wolves. They hunt beside him, frenzy when he's hurt, and pounce when he blows his horn.";
        const string Description = "A frontier hunter who won't fight without his wolves: his spear marks the prey, the pack runs it down.";
        const string OldTradeoff = "The pack is his damage: two wolves at first, and they won't stray far from him.";
        const string Tradeoff = "The pack is his damage, and it won't stray far from him; his own spear is short and light.";

        /// <summary>
        /// Note 37: his own weapon. A short, quick Thrust with modest damage; its hits mark (HuntersMarkTrait). Knockback
        /// is low so a marked enemy stays where the pack is heading. Amount strikes the next-nearest (more marks).
        /// </summary>
        static WeaponDef HuntingSpear()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(SpearPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(SpearPath);
                w.id = "hunting_spear"; w.displayName = "Hunting Spear";
                w.description = "A short boar spear: quick jabs at the nearest enemy that mark whatever they hit for your wolves.";
                w.pattern = WeaponPattern.Thrust;
                w.damage = 8f; w.critChance = 0.08f; w.critMultiplier = 2f; w.knockback = 3f;
                w.cooldown = 0.7f; w.swingDuration = 0.18f; w.windup = 0.05f;
                w.range = 3f; w.width = 0.6f; w.acquireRange = 3.4f; w.amount = 1; w.spreadStrikes = true;
                w.meshScale = 1.1f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.AttackSpeed, 0.15f)), L(Pct(StatId.Area, 0.2f)), L(Add(StatId.Amount, 1f)),
                    L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.CritChance, 0.1f)), L(Pct(StatId.AttackSpeed, 0.2f), Pct(StatId.Area, 0.15f)),
                };
            }
            // its own model (BlenderSrc/weapons/wp_huntingspear.py); the Pit Spear's stands in until that's imported
            var own = AssetDatabase.LoadAssetAtPath<Mesh>(SpearModel);
            var pit = AssetDatabase.LoadAssetAtPath<Mesh>(PitSpearModel);
            if (w.mesh == null || (own != null && w.mesh == pit)) w.mesh = own != null ? own : pit;
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>The spear's level-up card. It asks for itself (requiresAny), so it's never offered as a new weapon:
        /// only the one who starts with it sees it, to level it.</summary>
        static UpgradeDef SpearCard(WeaponDef spear)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(SpearCardPath);
            if (d == null)
            {
                d = PlayerSetup.EnsureAsset<UpgradeDef>(SpearCardPath);
                d.id = "weapon_" + spear.id; d.displayName = spear.displayName; d.glyph = "HS";
                d.kind = UpgradeKind.Weapon;
                d.weapon = spear;
                d.maxLevel = spear.MaxLevel;
                d.weight = 8f;
            }
            if (d.requiresAny == null || System.Array.IndexOf(d.requiresAny, d) < 0) d.requiresAny = new[] { d };
            EditorUtility.SetDirty(d);
            return d;
        }

        static WeaponDef WolfPack()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(WolfPackPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(WolfPackPath);
                w.id = "wolf_pack"; w.displayName = "Wolf Pack";
                w.description = PackDescription;
                w.pattern = WeaponPattern.Summon;
                // amount = wolves, cooldown = seconds between one wolf's bites, speed = run speed (m/s),
                // acquireRange = how far from you they pick targets, range = the leash (past it they come back)
                w.damage = 9f; w.critChance = 0.06f; w.critMultiplier = 2f; w.knockback = 2f;
                w.cooldown = 0.8f; w.swingDuration = 0.2f; w.windup = 0.05f;
                w.range = 9f; w.width = 0.6f; w.acquireRange = 9f; w.amount = 2; w.speed = 8.5f; w.meshScale = 1.3f;
                w.levels = new[]
                {
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f)), L(Pct(StatId.AttackSpeed, 0.2f)), L(Add(StatId.Amount, 1f)),
                    L(Pct(StatId.ProjectileSpeed, 0.2f), Pct(StatId.Area, 0.15f)), L(Pct(StatId.Damage, 0.3f)), L(Add(StatId.Amount, 1f)),
                };
            }
            if (w.summon == null) w.summon = ModelStyle.Load(WolfModel);
            // note 37: the pack fights near him: a 9 m leash, picking within it (the old 12 / 10 move; a designer's own
            // numbers stay), and the text says how they hunt now
            if (Mathf.Approximately(w.range, 12f)) w.range = 9f;
            if (Mathf.Approximately(w.acquireRange, 10f)) w.acquireRange = 9f;
            if (w.description == OldPackDescription || w.description == PackDescriptionV1) w.description = PackDescription;
            EditorUtility.SetDirty(w);
            return w;
        }

        const string OldPackDescription = "Wolves fight beside you for the whole run: each hunts the nearest enemy near you and bites. More levels, more wolves.";
        const string PackDescriptionV1 = "Wolves fight beside you all run: they go for what's nearest you (marked prey first), bite and move on. More levels, more wolves."; // too long for his tablet
        const string PackDescription = "Wolves fight beside you: they hunt what's near you (marked prey first), bite and move on.";

        static UpgradeDef WeaponCard(WeaponDef weapon)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(WolfPackCardPath);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(WolfPackCardPath);
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = "WP";
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
            return d;
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

        static void ShortenTrait(GladiatorTrait t, string old, string now)
        {
            if (t == null || t.description != old) return;
            t.description = now;
            EditorUtility.SetDirty(t);
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
