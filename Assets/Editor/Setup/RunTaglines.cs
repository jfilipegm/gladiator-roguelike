using System;
using System.IO;
using Gladiator.Gameplay;
using UnityEditor;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// The arena select's words for each run (R9): the ladder's line (how the climb feels there, CLAUDE.md §6.2), its
    /// hazards in one line, the first arena's layout (floor, difficulty, tier, boss) and the boss's name. Fills only what
    /// is empty, so a designer's edit stands (the layout link is refreshed when missing). A run not in the table gets
    /// its tier's line. Called by the portrait bake and the menu build; nothing here renders.
    /// </summary>
    public static class RunTaglines
    {
        static readonly (string id, string tagline, string hazards)[] Table =
        {
            ("slice", "I need to survive.", "Mud that slows you, fire pits, and a burning fence once Gorm is angry."),
            ("provincial_pit", "People notice me.", "Spike grates that hurt everyone, barricades, braziers that topple into fire."),
            ("stone_amphitheater", "I'm becoming a real gladiator.", "Beast cages that loose charging war boars; columns that block the archers."),
            ("grand_arena", "I'm a star.", "Fire jets across the arms, bronze blades sweeping the plaza."),
            ("imperial_arena", "One of the greatest alive.", "Chariot races that trample the track, Praetorian formations."),
            ("colosseum", "I have become a legend.", "Trapdoors that drop into the hypogeum, lifts that raise packs from below."),
        };

        /// <summary>The ladder's lines by arena tier (CLAUDE.md §6.2 "Player feels").</summary>
        static readonly string[] ByTier =
        {
            "I need to survive.", "People notice me.", "I'm becoming a real gladiator.", "I'm a star.", "One of the greatest alive.", "I have become a legend.",
        };

        public static void Apply(RunDef r)
        {
            if (r == null) return;
            bool dirty = false;
            if (r.layout == null)
            {
                r.layout = FindLayout(r);
                dirty |= r.layout != null;
            }
            foreach (var (id, tagline, hazards) in Table)
            {
                if (id != r.id) continue;
                if (string.IsNullOrEmpty(r.tagline)) { r.tagline = tagline; dirty = true; }
                if (string.IsNullOrEmpty(r.hazards)) { r.hazards = hazards; dirty = true; }
            }
            if (string.IsNullOrEmpty(r.tagline) && r.layout != null && r.layout.tier >= 0 && r.layout.tier < ByTier.Length)
            {
                r.tagline = ByTier[r.layout.tier];
                dirty = true;
            }
            if (string.IsNullOrEmpty(r.bossName) && r.Boss != null)
            {
                r.bossName = Gladiator.UI.ArenaTablet.BossLabel(r.Boss);
                dirty = true;
            }
            if (dirty) EditorUtility.SetDirty(r);
        }

        /// <summary>The layout the run's first scene is built from: AL_&lt;scene name&gt; (the arenas' convention), else
        /// the one whose name matches the arena's.</summary>
        static ArenaLayoutDef FindLayout(RunDef r)
        {
            if (r.arenas == null || r.arenas.Length == 0) return null;
            string scene = Path.GetFileNameWithoutExtension(r.arenas[0].scenePath ?? "");
            ArenaLayoutDef byName = null;
            foreach (var guid in AssetDatabase.FindAssets("t:ArenaLayoutDef"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var l = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(path);
                if (l == null) continue;
                if (Path.GetFileNameWithoutExtension(path) == "AL_" + scene) return l;
                if (byName == null && string.Equals(l.displayName, r.arenas[0].displayName, StringComparison.OrdinalIgnoreCase)) byName = l;
            }
            return byName;
        }
    }
}
