using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Gladiator.Gameplay
{
    /// <summary>Every sound the game plays (placeholder synth clips until real audio, CLAUDE.md §4).</summary>
    public enum SfxId
    {
        Swing, Thrust, Throw, Hit, Crit, Blocked, Kill, EliteKill, PlayerHurt, Gem, Coin, LevelUp, Card, Pick, Chest, Altar,
        Explosion, Slam, Spark, FirePot, BossRoar, Horn, Gate, Announce, Fight, Victory, Death, Cheer,
        GiftThrow, GiftLand, Boo,
        Cackle, Snare, Rattle, Bomb,
        BloodRite,
    }

    [Serializable]
    public struct SfxEntry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
        [Tooltip("± random pitch so repeats don't machine-gun.")]
        [Range(0f, 0.5f)] public float pitchJitter;
        [Tooltip("Seconds before this sound may start again (hundreds of hits a second must not all play).")]
        public float minInterval;
        [Tooltip("Most copies playing at once (0 = no limit).")]
        public int maxVoices;
        [Tooltip("Quieter with distance from the player and panned left/right; off = UI / announcer sounds.")]
        public bool positional;
        [Tooltip("When every voice is busy, a sound steals the oldest voice of lower priority.")]
        public int priority;
        [Tooltip("Routed to the Crowd bus (cheers) instead of SFX.")]
        public bool crowdBus;
    }

    /// <summary>
    /// Sound data: one entry per SfxId (clip, loudness, repeat limits), the crowd loop and the mixer groups. Built by
    /// AudioSetup from the synthesised clips; entries are designer-owned once they exist.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Sfx Bank", fileName = "SfxBank")]
    public sealed class SfxBank : ScriptableObject
    {
        public SfxEntry[] entries = Array.Empty<SfxEntry>();
        public AudioClip crowdLoop;

        [Header("Mixer")]
        public AudioMixer mixer;
        public AudioMixerGroup sfxGroup;
        public AudioMixerGroup crowdGroup;
        public AudioMixerGroup musicGroup;

        [Header("Positional sounds")]
        [Tooltip("Full volume within this distance of the player (m).")]
        public float fullVolumeDistance = 8f;
        [Tooltip("Silent beyond this distance (m).")]
        public float silentDistance = 40f;
        [Range(0f, 1f)] public float maxPan = 0.6f;

        [Header("Crowd")]
        [Range(0f, 1f)] public float crowdQuiet = 0.18f;
        [Range(0f, 1f)] public float crowdLoud = 0.75f;
    }
}
