using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Announcer lines and when they fire (CLAUDE.md §5 kill-streak announcer, §6.9 crowd reactions).</summary>
    [CreateAssetMenu(menuName = "Gladiator/Announcer Config", fileName = "AnnouncerConfig")]
    public sealed class AnnouncerConfig : ScriptableObject
    {
        [Serializable]
        public struct Streak
        {
            [Tooltip("Kills inside the window that trigger this line.")]
            public int kills;
            public string text;
            [Tooltip("Crowd excitement jumps to at least this.")]
            [Range(0f, 1f)] public float crowd;
        }

        [Tooltip("Kill-streak window (s): kills counted over the last this-many seconds.")]
        public float window = 4f;
        [Tooltip("Ascending. A line only fires when it beats the last streak line, or after the cooldown.")]
        public Streak[] streaks =
        {
            new Streak { kills = 40, text = "KILLING SPREE!", crowd = 0.55f },
            new Streak { kills = 80, text = "MASSACRE!", crowd = 0.7f },
            new Streak { kills = 140, text = "BLOODBATH!", crowd = 0.85f },
            new Streak { kills = 220, text = "UNSTOPPABLE!", crowd = 1f },
        };
        [Tooltip("Seconds before the streak ladder resets to the first line.")]
        public float streakCooldown = 20f;
        [Tooltip("Shortest gap between any two crowd lines (s).")]
        public float minGap = 6f;

        [Header("Close call")]
        [Tooltip("HP fraction that counts as near death…")]
        [Range(0f, 1f)] public float closeCallLow = 0.15f;
        [Tooltip("…and the fraction the player must climb back above.")]
        [Range(0f, 1f)] public float closeCallRecovered = 0.4f;
        public string closeCallText = "STILL STANDING!";
        public float closeCallCooldown = 30f;

        [Header("Crowd roar")]
        [Range(0f, 1f)] public float roarAt = 0.95f;
        public string roarText = "THE CROWD ROARS!";
        public float roarCooldown = 45f;

        public Color color = new Color(1f, 0.84f, 0.3f);
        public float seconds = 1.8f;
    }
}
