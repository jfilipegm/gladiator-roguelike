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
        Splinter, Spikes,
        Squeak, Quill,
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
        [Tooltip("The crowd talking: always on, quieter and slower when nobody's fighting.")]
        public AudioClip crowdLoop;
        [Tooltip("The crowd roaring: shouts, claps, whistles; fades in with CrowdExcitement.")]
        public AudioClip crowdRoar;

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
        [Tooltip("Murmur volume when the player isn't fighting.")]
        [Range(0f, 1f)] public float crowdQuiet = 0.18f;
        [Tooltip("Roar volume at full excitement.")]
        [Range(0f, 1f)] public float crowdLoud = 0.75f;
        [Tooltip("Murmur volume while the player is fighting.")]
        [Range(0f, 1f)] public float crowdMurmurFight = 0.3f;
        [Tooltip("Excitement at which the roar starts to come in.")]
        [Range(0f, 1f)] public float crowdRoarFrom = 0.18f;
        [Tooltip("Crowd playback speed when nobody's fighting (below 1 = slower, lower: the crowd settles).")]
        [Range(0.7f, 1f)] public float crowdCalmPitch = 0.88f;
        [Tooltip("Extra playback speed at full excitement.")]
        [Range(0f, 0.2f)] public float crowdExcitedPitch = 0.06f;
        [Tooltip("Volume change per second: swelling up, and dying down.")]
        public float crowdRise = 1.6f, crowdFall = 0.35f;
    }
}
