using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>What a preview line measures (beyond the player's own stats).</summary>
    public enum PreviewMetric : byte { Hit, AttacksPerSecond, Area, Projectiles, Pierce, Hp, Gold }

    /// <summary>One extra preview line: a weapon's hit or rate, current HP after a heal, gold after a chest.</summary>
    public struct PreviewLine
    {
        public string Label;
        public PreviewMetric Metric;
        public float Before, After;
        /// <summary>A weapon you don't have yet: only After means anything.</summary>
        public bool IsNew;
    }

    /// <summary>
    /// P1 previews (finding 10b): what taking a card, using the Blood Altar or opening a chest would do to every stat,
    /// "Damage 117% → 134%", plus the weapons' own numbers ("Rusty Gladius hit 34 → 41"). The card is applied to a
    /// copy of the player's stats through the same code a pick uses (UpgradeDef.AddStats, the weapon's level table, the
    /// tag synergies it would switch on), so After is what the stats will be. Preallocated: no allocation.
    /// </summary>
    public sealed class StatPreview
    {
        public const int MaxLines = 8;

        readonly StatBlock _scratch = new StatBlock(false);
        readonly StatBlock _weapon = new StatBlock(false);
        public readonly float[] Before = new float[StatInfo.Count];
        public readonly float[] After = new float[StatInfo.Count];
        public readonly PreviewLine[] Lines = new PreviewLine[MaxLines];

        public int LineCount { get; private set; }
        /// <summary>A preview is showing (Before/After are valid).</summary>
        public bool Active { get; private set; }

        public bool Changed(StatId s) => Active && StatText.Differs(s, Before[(int)s], After[(int)s]);
        public float BeforeOf(StatId s) => Before[(int)s];
        public float AfterOf(StatId s) => After[(int)s];

        public int ChangedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < StatInfo.Count; i++) if (Changed((StatId)i)) n++;
                return n;
            }
        }

        public void Clear()
        {
            Active = false;
            LineCount = 0;
        }

        void Begin(PlayerStats stats)
        {
            LineCount = 0;
            _scratch.CopyFrom(stats.Block);
            for (int i = 0; i < StatInfo.Count; i++) Before[i] = stats.Get((StatId)i);
        }

        void End()
        {
            for (int i = 0; i < StatInfo.Count; i++) After[i] = PlayerStats.WithAdmin((StatId)i, _scratch[(StatId)i]);
            Active = true;
        }

        void AddLine(string label, PreviewMetric metric, float before, float after, bool isNew = false)
        {
            if (LineCount >= MaxLines) return;
            Lines[LineCount++] = new PreviewLine { Label = label, Metric = metric, Before = before, After = after, IsNew = isNew };
        }

        /// <summary>Taking `o` (level-up, chest, altar or boss card). Any of the parts may be null; the rest still previews.</summary>
        public void ForCard(in CardOffer o, PlayerStats stats, BuildState state, UpgradeCatalog catalog, UpgradeConfig config,
            WeaponSystem weapons, PlayerHealth health, RunState run)
        {
            Clear();
            var d = o.Def;
            if (d == null || stats == null) return;
            Begin(stats);
            if (d.kind == UpgradeKind.Consumable)
            {
                End();
                if (d.healFraction > 0f && health != null)
                    AddLine("HP", PreviewMetric.Hp, health.Hp, Mathf.Min(health.MaxHp, health.Hp + health.MaxHp * d.healFraction * health.HealingMul));
                if (d.gold > 0 && run != null) AddLine("Gold", PreviewMetric.Gold, run.Gold, run.Gold + d.gold);
                return;
            }
            if (d.kind == UpgradeKind.Boon) d.AddStats(_scratch, o.Rarity, config, default);
            Synergies(o, state, catalog);
            End();
            WeaponLines(o, weapons, config);
        }

        /// <summary>The Blood Altar: max HP × (1 − cost) for good.</summary>
        public void ForAltar(PlayerStats stats, float maxHpCost)
        {
            Clear();
            if (stats == null) return;
            Begin(stats);
            _scratch.Add(new StatMod(StatId.MaxHp, ModOp.Multiply, 1f - maxHpCost));
            End();
        }

        /// <summary>A purchase (an Armory Chest): only gold changes.</summary>
        public void ForGold(PlayerStats stats, int goldBefore, int goldAfter)
        {
            Clear();
            if (stats == null) return;
            Begin(stats);
            End();
            AddLine("Gold", PreviewMetric.Gold, goldBefore, goldAfter);
        }

        // The tag synergies this card would switch on (BuildState.UpdateSynergies runs after every pick) add their mods too.
        void Synergies(in CardOffer o, BuildState state, UpgradeCatalog catalog)
        {
            var d = o.Def;
            if (state == null || catalog == null || catalog.synergies == null) return;
            UpgradeTag gain = d.evolvesFrom != null ? d.tags & ~d.evolvesFrom.tags : o.IsNew ? d.tags : UpgradeTag.None;
            if (gain == UpgradeTag.None) return;
            var syns = catalog.synergies;
            for (int i = 0; i < syns.Length; i++)
            {
                var s = syns[i];
                if (s == null || s.tag == UpgradeTag.None || (gain & s.tag) == 0 || state.HasSynergy(s) || s.mods == null) continue;
                if (state.TagCount(s.tag) + 1 < s.count) continue;
                for (int m = 0; m < s.mods.Length; m++) _scratch.Add(s.mods[m], 1f);
            }
        }

        void WeaponLines(in CardOffer o, WeaponSystem weapons, UpgradeConfig config)
        {
            if (weapons == null) return;
            var d = o.Def;
            float dmgB = Before[(int)StatId.Damage], dmgA = After[(int)StatId.Damage];
            float spdB = Before[(int)StatId.AttackSpeed], spdA = After[(int)StatId.AttackSpeed];
            if (d.kind == UpgradeKind.Weapon && d.weapon != null)
            {
                if (d.evolvesFrom != null)
                {
                    var w = d.evolvesFrom.weapon != null ? weapons.Find(d.evolvesFrom.weapon) : null;
                    if (w == null) return;
                    Compare(d.weapon.displayName, w.Def, w.Local, dmgB, spdB, d.weapon, w.Local, dmgA, spdA);
                }
                else if (o.Level <= 1)
                {
                    var n = d.weapon;
                    if (n.damage > 0f) AddLine(n.displayName, PreviewMetric.Hit, 0f, n.damage * dmgA, true);
                    AddLine(n.displayName, PreviewMetric.AttacksPerSecond, 0f, Rate(n, 1f, spdA), true);
                }
                else
                {
                    var w = weapons.Find(d.weapon);
                    if (w == null || w.Level >= w.Def.MaxLevel) return;
                    _weapon.CopyFrom(w.Local);
                    var mods = w.Def.levels[w.Level - 1].mods;
                    float mag = config != null ? config.Magnitude(o.Rarity) : 1f;
                    if (mods != null) for (int m = 0; m < mods.Length; m++) _weapon.Add(mods[m], mag);
                    Compare(w.Def.displayName, w.Def, w.Local, dmgB, spdB, w.Def, _weapon, dmgA, spdA);
                }
                return;
            }
            // a Boon or Vow: what the player's damage and attack speed do to each weapon's hits
            if (!Changed(StatId.Damage) && !Changed(StatId.AttackSpeed)) return;
            var list = weapons.Weapons;
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (w == null || w.Def == null) continue;
                Compare(w.Def.displayName, w.Def, w.Local, dmgB, spdB, w.Def, w.Local, dmgA, spdA);
            }
        }

        // WeaponSystem's own formulas: hit = damage × local × player; rate = max(0.1, local × player) / cooldown.
        static float Hit(WeaponDef d, StatBlock local, float playerDamage) => d.damage * local[StatId.Damage] * playerDamage;
        static float Rate(WeaponDef d, float local, float playerSpeed) => Mathf.Max(0.1f, local * playerSpeed) / Mathf.Max(0.01f, d.cooldown);

        void Compare(string label, WeaponDef d0, StatBlock l0, float dmg0, float spd0, WeaponDef d1, StatBlock l1, float dmg1, float spd1)
        {
            float a, b;
            if (d1.damage > 0f && Mathf.Abs((a = Hit(d0, l0, dmg0)) - (b = Hit(d1, l1, dmg1))) > 0.05f) AddLine(label, PreviewMetric.Hit, a, b);
            a = Rate(d0, l0[StatId.AttackSpeed], spd0);
            b = Rate(d1, l1[StatId.AttackSpeed], spd1);
            if (Mathf.Abs(a - b) > 0.005f) AddLine(label, PreviewMetric.AttacksPerSecond, a, b);
            float area = After[(int)StatId.Area];
            a = l0[StatId.Area] * Before[(int)StatId.Area];
            b = l1[StatId.Area] * area;
            if (!ReferenceEquals(l0, l1) && Mathf.Abs(a - b) > 0.0005f) AddLine(label, PreviewMetric.Area, a, b);
            int bonus0 = (int)Before[(int)StatId.Amount], bonus1 = (int)After[(int)StatId.Amount];
            int n0 = Mathf.Max(1, d0.amount + (int)l0[StatId.Amount] + bonus0), n1 = Mathf.Max(1, d1.amount + (int)l1[StatId.Amount] + bonus1);
            if (n0 != n1) AddLine(label, PreviewMetric.Projectiles, n0, n1);
            if (d0.pierce >= 0 && d1.pierce >= 0)
            {
                int p0 = d0.pierce + (int)l0[StatId.Pierce] + (int)Before[(int)StatId.Pierce];
                int p1 = d1.pierce + (int)l1[StatId.Pierce] + (int)After[(int)StatId.Pierce];
                if (p0 != p1) AddLine(label, PreviewMetric.Pierce, p0, p1);
            }
        }

        /// <summary>A preview line as the page shows it: "Rusty Gladius hit 34 → 41", "NEW Sling 2.5 attacks/s".</summary>
        public static void LineText(CharBuffer t, in PreviewLine l)
        {
            if (l.IsNew) t.Append("<color=").Append(StatText.Head).Append(">NEW</color> ");
            t.Append(l.Label).Append(' ');
            switch (l.Metric)
            {
                case PreviewMetric.Hit: t.Append("hit "); break;
                case PreviewMetric.AttacksPerSecond: t.Append("attacks/s "); break;
                case PreviewMetric.Area: t.Append("area "); break;
                case PreviewMetric.Projectiles: t.Append("projectiles "); break;
                case PreviewMetric.Pierce: t.Append("pierce "); break;
            }
            if (!l.IsNew) Number(t, l.Metric, l.Before).Append(" → ");
            bool better = l.After >= l.Before;
            t.Append("<color=").Append(better ? StatText.Gain : StatText.Loss).Append('>');
            Number(t, l.Metric, l.After).Append("</color>");
        }

        static CharBuffer Number(CharBuffer t, PreviewMetric m, float v)
        {
            switch (m)
            {
                case PreviewMetric.AttacksPerSecond: return t.Append(v, v >= 10f ? 1 : 2);
                case PreviewMetric.Area: return t.Append(v * 100f, 0).Append('%');
                case PreviewMetric.Hit: return t.Append(v, v >= 10f ? 0 : 1);
                default: return t.Append(Mathf.Round(v), 0);
            }
        }
    }
}
