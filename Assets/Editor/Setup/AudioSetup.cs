using System;
using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Placeholder audio (Phase 4): synthesises every clip (SfxSynth) into Assets/Audio/SFX, sets their import
    /// settings, makes sure the mixer exists (MixerSetup: Master → SFX / Crowd / Music with exposed volumes) and fills
    /// the SfxBank. Clips are regenerated every run; bank entries are designer-owned once they exist (only missing
    /// ids and missing clips are filled in).
    /// </summary>
    public static class AudioSetup
    {
        public const string SfxDir = "Assets/Audio/SFX/";
        public const string MixerPath = "Assets/Audio/GladiatorMixer.mixer";
        public const string BankPath = "Assets/Data/Audio/SfxBank.asset";
        public const string AnnouncerPath = "Assets/Data/Config/AnnouncerConfig.asset";

        [MenuItem("Gladiator/Setup/Audio/Generate Sounds + Bank")]
        public static SfxBank CreateOrUpdate()
        {
            foreach (var n in SfxSynth.All) SfxSynth.WriteWav(SfxDir + n + ".wav", SfxSynth.Make(n));
            AssetDatabase.Refresh();
            foreach (var n in SfxSynth.All) Configure(SfxDir + n + ".wav", n.StartsWith("amb_"));

            var bank = PlayerSetup.EnsureAsset<SfxBank>(BankPath);
            var mixer = MixerSetup.Ensure(MixerPath);
            bank.mixer = mixer;
            bank.sfxGroup = MixerSetup.Group(mixer, "SFX");
            bank.crowdGroup = MixerSetup.Group(mixer, "Crowd");
            bank.musicGroup = MixerSetup.Group(mixer, "Music");

            var list = new List<SfxEntry>(bank.entries);
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                if (!list.Exists(e => e.id == id)) list.Add(Default(id));
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.clip == null) { e.clip = Clip(FileOf(e.id)); list[i] = e; }
            }
            list.Sort((a, b) => a.id.CompareTo(b.id));
            bank.entries = list.ToArray();
            bank.crowdLoop = Clip("amb_crowd");
            EditorUtility.SetDirty(bank);
            PlayerSetup.EnsureAsset<AnnouncerConfig>(AnnouncerPath);
            AssetDatabase.SaveAssets();
            return bank;
        }

        static AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + file + ".wav");

        static void Configure(string path, bool loop)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) return;
            imp.forceToMono = true;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            s.quality = 0.6f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }

        static string FileOf(SfxId id)
        {
            switch (id)
            {
                case SfxId.EliteKill: return "sfx_elite_kill";
                case SfxId.PlayerHurt: return "sfx_player_hurt";
                case SfxId.LevelUp: return "sfx_levelup";
                case SfxId.FirePot: return "sfx_fire_pot";
                case SfxId.BossRoar: return "sfx_boss_roar";
                default: return "sfx_" + id.ToString().ToLowerInvariant();
            }
        }

        // volume, pitch jitter, min interval, max voices, positional, priority, crowd bus
        static SfxEntry E(SfxId id, float vol, float jitter, float interval, int voices, bool positional, int priority, bool crowd = false) =>
            new SfxEntry { id = id, volume = vol, pitchJitter = jitter, minInterval = interval, maxVoices = voices, positional = positional, priority = priority, crowdBus = crowd };

        static SfxEntry Default(SfxId id)
        {
            switch (id)
            {
                case SfxId.Swing: return E(id, 0.35f, 0.12f, 0.05f, 3, true, 1);
                case SfxId.Thrust: return E(id, 0.4f, 0.1f, 0.05f, 2, true, 1);
                case SfxId.Throw: return E(id, 0.35f, 0.12f, 0.05f, 3, true, 1);
                case SfxId.Hit: return E(id, 0.45f, 0.15f, 0.035f, 5, true, 1);
                case SfxId.Crit: return E(id, 0.55f, 0.08f, 0.06f, 3, true, 3);
                case SfxId.Blocked: return E(id, 0.4f, 0.1f, 0.08f, 2, true, 2);
                case SfxId.Kill: return E(id, 0.35f, 0.2f, 0.03f, 5, true, 1);
                case SfxId.EliteKill: return E(id, 0.8f, 0.05f, 0.1f, 2, true, 6);
                case SfxId.PlayerHurt: return E(id, 0.7f, 0.08f, 0.15f, 1, false, 8);
                case SfxId.Gem: return E(id, 0.22f, 0.15f, 0.025f, 4, false, 0);
                case SfxId.Coin: return E(id, 0.35f, 0.05f, 0.05f, 2, false, 2);
                case SfxId.LevelUp: return E(id, 0.6f, 0f, 0.3f, 1, false, 9);
                case SfxId.Card: return E(id, 0.4f, 0.05f, 0.03f, 2, false, 7);
                case SfxId.Pick: return E(id, 0.55f, 0f, 0.1f, 1, false, 9);
                case SfxId.Chest: return E(id, 0.7f, 0f, 0.3f, 1, true, 8);
                case SfxId.Altar: return E(id, 0.75f, 0f, 0.3f, 1, true, 8);
                case SfxId.Explosion: return E(id, 0.7f, 0.1f, 0.06f, 3, true, 5);
                case SfxId.Slam: return E(id, 0.75f, 0.08f, 0.08f, 2, true, 6);
                case SfxId.Spark: return E(id, 0.3f, 0.2f, 0.04f, 3, true, 1);
                case SfxId.FirePot: return E(id, 0.5f, 0.1f, 0.1f, 2, true, 3);
                case SfxId.BossRoar: return E(id, 1f, 0f, 1f, 1, false, 10);
                case SfxId.Horn: return E(id, 0.7f, 0f, 1f, 1, false, 9);
                case SfxId.Gate: return E(id, 0.6f, 0f, 0.5f, 2, true, 7);
                case SfxId.Announce: return E(id, 0.5f, 0f, 0.8f, 1, false, 8);
                case SfxId.Fight: return E(id, 0.8f, 0f, 1f, 1, false, 10);
                case SfxId.Victory: return E(id, 0.8f, 0f, 1f, 1, false, 10);
                case SfxId.Death: return E(id, 0.8f, 0f, 1f, 1, false, 10);
                case SfxId.Cheer: return E(id, 0.6f, 0.06f, 1.2f, 2, false, 5, crowd: true);
                case SfxId.GiftThrow: return E(id, 0.55f, 0.05f, 0.2f, 2, true, 6);
                case SfxId.GiftLand: return E(id, 0.6f, 0.05f, 0.2f, 2, true, 6);
                case SfxId.Boo: return E(id, 0.6f, 0.05f, 1f, 1, false, 7, crowd: true);
                case SfxId.Cackle: return E(id, 0.7f, 0.06f, 0.5f, 1, false, 8);
                case SfxId.Snare: return E(id, 0.6f, 0.08f, 0.05f, 3, true, 5);
                case SfxId.Rattle: return E(id, 0.65f, 0.1f, 0.15f, 1, true, 6);
                case SfxId.Bomb: return E(id, 0.55f, 0.12f, 0.05f, 4, true, 4);
                case SfxId.BloodRite: return E(id, 0.85f, 0f, 1f, 1, false, 9);
                default: return E(id, 0.5f, 0f, 0.1f, 2, true, 1);
            }
        }
    }
}
