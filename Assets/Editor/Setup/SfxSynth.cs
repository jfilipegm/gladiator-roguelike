using System;
using System.IO;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Placeholder sound effects synthesised from sine/square/saw/noise envelopes (CLAUDE.md §4: real audio is a later
    /// task). Every sound is a small deterministic function, so the set is regenerable and diffable like the Blender
    /// scripts. 44.1 kHz mono 16-bit WAV.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;

        // ---------- building blocks ----------
        sealed class Buf
        {
            public readonly float[] S;
            public Buf(float seconds) { S = new float[Mathf.Max(1, (int)(seconds * Rate))]; }
            public int N => S.Length;
            public float T(int i) => i / (float)Rate;
        }

        static float Env(float t, float attack, float decay) =>
            t < attack ? t / Mathf.Max(1e-4f, attack) : Mathf.Exp(-(t - attack) / Mathf.Max(1e-4f, decay));

        static float Sq(float phase) => (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;
        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase)) - 1f;

        /// <summary>Adds a tone whose frequency glides from f0 to f1; wave 0 sine, 1 square, 2 saw.</summary>
        static void Tone(Buf b, float start, float len, float f0, float f1, float amp, float attack, float decay, int wave = 0, float vibrato = 0f)
        {
            double phase = 0;
            int i0 = (int)(start * Rate), i1 = Mathf.Min(b.N, i0 + (int)(len * Rate));
            for (int i = i0; i < i1; i++)
            {
                float t = (i - i0) / (float)Rate, k = t / len;
                float f = Mathf.Lerp(f0, f1, k) * (1f + vibrato * Mathf.Sin(t * 2f * Mathf.PI * 6f));
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float v = wave == 0 ? Mathf.Sin(p * 2f * Mathf.PI) : wave == 1 ? Sq(p) : Saw(p);
                b.S[i] += v * amp * Env(t, attack, decay);
            }
        }

        /// <summary>Adds noise through a one-pole low-pass whose cutoff glides from c0 to c1 (0..1 of Nyquist-ish).</summary>
        static void Noise(Buf b, float start, float len, float amp, float attack, float decay, float c0, float c1, int seed, bool highpass = false)
        {
            var rng = new System.Random(seed);
            float y = 0f, prev = 0f;
            int i0 = (int)(start * Rate), i1 = Mathf.Min(b.N, i0 + (int)(len * Rate));
            for (int i = i0; i < i1; i++)
            {
                float t = (i - i0) / (float)Rate, k = t / len;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                float a = Mathf.Clamp01(Mathf.Lerp(c0, c1, k));
                y += a * (x - y);
                float v = highpass ? y - prev : y;
                prev = y;
                b.S[i] += v * amp * Env(t, attack, decay) * (highpass ? 3f : 1f);
            }
        }

        static float[] Finish(Buf b, float gain = 1f)
        {
            float peak = 1e-4f;
            for (int i = 0; i < b.N; i++) peak = Mathf.Max(peak, Mathf.Abs(b.S[i]));
            float g = gain / peak;
            int fade = Mathf.Min(b.N / 4, Rate / 200); // 5 ms fade-out: no click
            for (int i = 0; i < b.N; i++)
            {
                float v = b.S[i] * g;
                if (i >= b.N - fade) v *= (b.N - 1 - i) / (float)fade;
                b.S[i] = Mathf.Clamp(v, -1f, 1f);
            }
            return b.S;
        }

        // ---------- the sounds ----------
        public static float[] Make(string name)
        {
            switch (name)
            {
                case "sfx_swing": { var b = new Buf(0.16f); Noise(b, 0, 0.16f, 1f, 0.03f, 0.05f, 0.05f, 0.35f, 11, true); return Finish(b, 0.7f); }
                case "sfx_thrust": { var b = new Buf(0.14f); Noise(b, 0, 0.14f, 1f, 0.01f, 0.05f, 0.4f, 0.1f, 12, true); Tone(b, 0, 0.08f, 900, 500, 0.15f, 0.002f, 0.03f); return Finish(b, 0.7f); }
                case "sfx_throw": { var b = new Buf(0.2f); Noise(b, 0, 0.2f, 1f, 0.02f, 0.07f, 0.08f, 0.5f, 13, true); return Finish(b, 0.6f); }
                case "sfx_hit": { var b = new Buf(0.1f); Noise(b, 0, 0.1f, 1f, 0.001f, 0.02f, 0.5f, 0.1f, 14); Tone(b, 0, 0.1f, 160, 90, 0.8f, 0.001f, 0.035f); return Finish(b, 0.8f); }
                case "sfx_crit": { var b = new Buf(0.28f); Noise(b, 0, 0.1f, 1f, 0.001f, 0.02f, 0.6f, 0.1f, 15); Tone(b, 0, 0.1f, 180, 90, 0.8f, 0.001f, 0.04f); Tone(b, 0.01f, 0.27f, 1760, 1700, 0.35f, 0.002f, 0.08f); Tone(b, 0.01f, 0.27f, 2640, 2600, 0.15f, 0.002f, 0.06f); return Finish(b, 0.85f); }
                case "sfx_blocked": { var b = new Buf(0.16f); Tone(b, 0, 0.16f, 620, 600, 0.6f, 0.001f, 0.04f, 1); Tone(b, 0, 0.16f, 1240, 1230, 0.3f, 0.001f, 0.05f); Noise(b, 0, 0.05f, 0.5f, 0.001f, 0.01f, 0.8f, 0.5f, 16); return Finish(b, 0.6f); }
                case "sfx_kill": { var b = new Buf(0.14f); Tone(b, 0, 0.14f, 520, 140, 0.8f, 0.002f, 0.05f, 1); Noise(b, 0, 0.08f, 0.6f, 0.001f, 0.02f, 0.4f, 0.1f, 17); return Finish(b, 0.55f); }
                case "sfx_elite_kill": { var b = new Buf(0.5f); Noise(b, 0, 0.5f, 1f, 0.002f, 0.15f, 0.3f, 0.02f, 18); Tone(b, 0, 0.5f, 110, 45, 1f, 0.002f, 0.18f); Tone(b, 0.02f, 0.3f, 880, 660, 0.25f, 0.005f, 0.1f, 1); return Finish(b, 0.9f); }
                case "sfx_player_hurt": { var b = new Buf(0.22f); Tone(b, 0, 0.22f, 140, 80, 0.8f, 0.004f, 0.08f, 1); Noise(b, 0, 0.12f, 0.7f, 0.001f, 0.04f, 0.3f, 0.05f, 19); return Finish(b, 0.85f); }
                case "sfx_gem": { var b = new Buf(0.07f); Tone(b, 0, 0.07f, 1300, 1900, 0.8f, 0.002f, 0.03f); return Finish(b, 0.35f); }
                case "sfx_coin": { var b = new Buf(0.16f); Tone(b, 0, 0.06f, 1568, 1568, 0.7f, 0.001f, 0.03f, 1); Tone(b, 0.05f, 0.11f, 2093, 2093, 0.7f, 0.001f, 0.05f, 1); return Finish(b, 0.35f); }
                case "sfx_levelup": { var b = new Buf(0.6f); float[] f = { 523.3f, 659.3f, 784f, 1046.5f }; for (int k = 0; k < 4; k++) Tone(b, k * 0.08f, 0.6f - k * 0.08f, f[k], f[k], 0.5f, 0.003f, 0.18f, 1); return Finish(b, 0.6f); }
                case "sfx_card": { var b = new Buf(0.05f); Tone(b, 0, 0.05f, 1200, 900, 0.6f, 0.001f, 0.012f, 1); return Finish(b, 0.4f); }
                case "sfx_pick": { var b = new Buf(0.25f); Tone(b, 0, 0.12f, 784, 784, 0.5f, 0.002f, 0.06f, 1); Tone(b, 0.07f, 0.18f, 1175, 1175, 0.5f, 0.002f, 0.08f, 1); return Finish(b, 0.5f); }
                case "sfx_chest": { var b = new Buf(0.8f); Noise(b, 0, 0.35f, 0.6f, 0.05f, 0.12f, 0.03f, 0.08f, 20); Tone(b, 0.3f, 0.5f, 1046.5f, 1046.5f, 0.4f, 0.003f, 0.2f); Tone(b, 0.38f, 0.42f, 1568f, 1568f, 0.35f, 0.003f, 0.2f); return Finish(b, 0.65f); }
                case "sfx_altar": { var b = new Buf(1.0f); Tone(b, 0, 1f, 110, 98, 0.7f, 0.05f, 0.4f, 2); Tone(b, 0, 1f, 164.8f, 147f, 0.4f, 0.05f, 0.4f, 2, 0.01f); Noise(b, 0, 0.6f, 0.4f, 0.1f, 0.2f, 0.02f, 0.04f, 21); return Finish(b, 0.7f); }
                case "sfx_explosion": { var b = new Buf(0.6f); Noise(b, 0, 0.6f, 1f, 0.002f, 0.2f, 0.5f, 0.02f, 22); Tone(b, 0, 0.5f, 90, 35, 1f, 0.002f, 0.15f); return Finish(b, 0.9f); }
                case "sfx_slam": { var b = new Buf(0.45f); Tone(b, 0, 0.45f, 75, 38, 1f, 0.002f, 0.14f); Noise(b, 0, 0.3f, 0.6f, 0.002f, 0.08f, 0.15f, 0.02f, 23); return Finish(b, 0.9f); }
                case "sfx_spark": { var b = new Buf(0.12f); Noise(b, 0, 0.12f, 0.7f, 0.001f, 0.03f, 0.9f, 0.6f, 24, true); Tone(b, 0, 0.1f, 2400, 3600, 0.3f, 0.001f, 0.03f, 1); return Finish(b, 0.4f); }
                case "sfx_fire_pot": { var b = new Buf(0.4f); Noise(b, 0, 0.4f, 1f, 0.01f, 0.12f, 0.2f, 0.05f, 25); Tone(b, 0, 0.12f, 300, 120, 0.5f, 0.002f, 0.04f); return Finish(b, 0.7f); }
                case "sfx_boss_roar": { var b = new Buf(1.5f); Tone(b, 0, 1.5f, 85, 65, 1f, 0.12f, 0.6f, 2, 0.04f); Tone(b, 0, 1.5f, 128, 100, 0.6f, 0.15f, 0.55f, 2, 0.05f); Noise(b, 0, 1.5f, 0.7f, 0.1f, 0.5f, 0.08f, 0.03f, 26); return Finish(b, 0.95f); }
                case "sfx_horn": { var b = new Buf(1.3f); Tone(b, 0, 1.3f, 196, 190, 0.7f, 0.08f, 0.9f, 2, 0.008f); Tone(b, 0, 1.3f, 294, 286, 0.45f, 0.1f, 0.9f, 2, 0.008f); return Finish(b, 0.8f); }
                case "sfx_gate": { var b = new Buf(1.1f); for (int k = 0; k < 9; k++) Noise(b, k * 0.11f, 0.12f, 0.6f, 0.01f, 0.04f, 0.05f, 0.03f, 27 + k); Tone(b, 0, 1.1f, 70, 60, 0.4f, 0.1f, 0.5f, 2); return Finish(b, 0.7f); }
                case "sfx_announce": { var b = new Buf(0.55f); Tone(b, 0, 0.2f, 392, 392, 0.6f, 0.005f, 0.12f, 2); Tone(b, 0.16f, 0.39f, 523.3f, 523.3f, 0.6f, 0.005f, 0.2f, 2); Tone(b, 0.16f, 0.39f, 659.3f, 659.3f, 0.3f, 0.005f, 0.2f, 2); return Finish(b, 0.6f); }
                case "sfx_fight": { var b = new Buf(0.9f); Tone(b, 0, 0.9f, 220, 220, 0.6f, 0.005f, 0.35f, 2); Tone(b, 0, 0.9f, 330, 330, 0.5f, 0.005f, 0.35f, 2); Tone(b, 0, 0.9f, 440, 440, 0.4f, 0.005f, 0.35f, 2); Noise(b, 0, 0.3f, 0.6f, 0.002f, 0.1f, 0.3f, 0.05f, 40); return Finish(b, 0.8f); }
                case "sfx_victory": { var b = new Buf(1.4f); float[] f = { 392f, 523.3f, 659.3f, 784f }; for (int k = 0; k < 4; k++) Tone(b, k * 0.12f, 1.4f - k * 0.12f, f[k], f[k], 0.45f, 0.005f, 0.45f, 2); return Finish(b, 0.75f); }
                case "sfx_death": { var b = new Buf(1.2f); Tone(b, 0, 1.2f, 220, 110, 0.6f, 0.01f, 0.5f, 2); Tone(b, 0.15f, 1.05f, 207.7f, 98f, 0.5f, 0.01f, 0.5f, 2); return Finish(b, 0.7f); }
                case "sfx_cheer": return CrowdSynth.Shot(CrowdSynth.Mood.Cheer, 2.6f, 303); // a burst of real-sounding shouts (playtest)
                // Phase 5.1 (Favor): a gift whooshing out of the stands, its landing, the crowd booing a dull show
                case "sfx_giftthrow": { var b = new Buf(0.6f); Noise(b, 0, 0.6f, 0.8f, 0.15f, 0.25f, 0.05f, 0.4f, 43, true); Tone(b, 0.05f, 0.5f, 420, 880, 0.25f, 0.05f, 0.2f); return Finish(b, 0.6f); }
                case "sfx_giftland": { var b = new Buf(0.7f); Tone(b, 0, 0.2f, 110, 60, 0.8f, 0.002f, 0.07f); Noise(b, 0, 0.12f, 0.5f, 0.001f, 0.04f, 0.2f, 0.05f, 44); float[] f = { 1318.5f, 1568f, 2093f }; for (int k = 0; k < 3; k++) Tone(b, 0.08f + k * 0.06f, 0.45f, f[k], f[k], 0.25f, 0.002f, 0.15f, 1); return Finish(b, 0.6f); }
                case "sfx_cackle": { var b = new Buf(0.75f); for (int k = 0; k < 6; k++) { float f = 820 - k * 55; Tone(b, k * 0.11f, 0.08f, f, f * 0.82f, 0.55f, 0.004f, 0.05f, 1, 0.04f); Noise(b, k * 0.11f, 0.05f, 0.18f, 0.002f, 0.03f, 0.3f, 0.2f, 60 + k, true); } return Finish(b, 0.6f); }
                case "sfx_snare": { var b = new Buf(0.3f); Noise(b, 0, 0.08f, 1f, 0.001f, 0.03f, 0.6f, 0.3f, 61, true); Tone(b, 0, 0.12f, 1900, 700, 0.5f, 0.001f, 0.05f, 1); Tone(b, 0.02f, 0.25f, 2600, 2500, 0.2f, 0.002f, 0.12f); return Finish(b, 0.6f); }
                case "sfx_rattle": { var b = new Buf(0.55f); float[] at = { 0f, 0.07f, 0.12f, 0.21f, 0.26f, 0.37f, 0.43f }; for (int k = 0; k < at.Length; k++) { Noise(b, at[k], 0.06f, 0.9f - k * 0.08f, 0.001f, 0.025f, 0.12f, 0.06f, 62 + k); Tone(b, at[k], 0.05f, 240 + (k % 3) * 60, 180, 0.35f, 0.001f, 0.02f); } return Finish(b, 0.65f); }
                case "sfx_bloodrite":
                {
                    // two heartbeat thumps, then a dark rising rush and a growl
                    var b = new Buf(0.95f);
                    Tone(b, 0f, 0.14f, 75, 42, 1f, 0.003f, 0.07f);
                    Tone(b, 0.2f, 0.16f, 70, 38, 0.9f, 0.003f, 0.08f);
                    Noise(b, 0.24f, 0.7f, 0.7f, 0.25f, 0.2f, 0.03f, 0.45f, 71);
                    Tone(b, 0.3f, 0.6f, 98, 147, 0.32f, 0.12f, 0.2f, 2, 0.05f);
                    return Finish(b, 0.8f);
                }
                case "sfx_splinter":
                {
                    // a crack, then wood clattering down
                    var b = new Buf(0.6f);
                    Noise(b, 0f, 0.12f, 1f, 0.001f, 0.06f, 0.6f, 0.2f, 81, true);
                    Tone(b, 0f, 0.1f, 180, 90, 0.6f, 0.002f, 0.05f);
                    for (int k = 0; k < 5; k++) Noise(b, 0.12f + k * 0.08f, 0.07f, 0.45f - k * 0.06f, 0.002f, 0.04f, 0.35f, 0.15f, 82 + k, true);
                    return Finish(b, 0.75f);
                }
                case "sfx_spikes":
                {
                    // iron shooting up through a grate: a scrape and a ring
                    var b = new Buf(0.4f);
                    Noise(b, 0f, 0.08f, 0.9f, 0.001f, 0.05f, 0.8f, 0.4f, 91, true);
                    Tone(b, 0.01f, 0.35f, 1250, 1180, 0.28f, 0.002f, 0.2f, 0, 0.01f);
                    Tone(b, 0.01f, 0.3f, 1870, 1800, 0.15f, 0.002f, 0.15f);
                    return Finish(b, 0.65f);
                }
                case "sfx_squeak":
                {
                    // the golden rat: two quick rising chirps
                    var b = new Buf(0.22f);
                    Tone(b, 0f, 0.06f, 2300, 3300, 0.45f, 0.003f, 0.03f);
                    Tone(b, 0.09f, 0.07f, 2500, 3600, 0.4f, 0.003f, 0.035f);
                    return Finish(b, 0.55f);
                }
                case "sfx_quill":
                {
                    // the bookmaker scratching a bet into his ledger: three dry strokes
                    var b = new Buf(0.4f);
                    for (int k = 0; k < 3; k++) Noise(b, k * 0.12f, 0.08f, 0.6f, 0.004f, 0.05f, 0.9f, 0.7f, 120 + k, true);
                    Tone(b, 0.0f, 0.05f, 900, 700, 0.12f, 0.002f, 0.03f);
                    return Finish(b, 0.5f);
                }
                case "sfx_bomb": { var b = new Buf(0.45f); Noise(b, 0, 0.45f, 0.9f, 0.002f, 0.14f, 0.35f, 0.03f, 66); Tone(b, 0, 0.35f, 150, 50, 0.9f, 0.002f, 0.1f); return Finish(b, 0.75f); }
                case "sfx_boo": return CrowdSynth.Shot(CrowdSynth.Mood.Boo, 1.9f, 404);
                // the crowd (playtest fix: the old pulsed-noise loop sounded like a steam train): a talking murmur and an
                // excited roar, crossfaded at runtime by CrowdExcitement
                case "amb_crowd": return CrowdSynth.Loop(CrowdSynth.Mood.Murmur, 16f, 101);
                case "amb_crowd_roar": return CrowdSynth.Loop(CrowdSynth.Mood.Roar, 12f, 202);
                default: throw new ArgumentException("unknown sound " + name);
            }
        }

        public static readonly string[] All =
        {
            "sfx_swing", "sfx_thrust", "sfx_throw", "sfx_hit", "sfx_crit", "sfx_blocked", "sfx_kill", "sfx_elite_kill",
            "sfx_player_hurt", "sfx_gem", "sfx_coin", "sfx_levelup", "sfx_card", "sfx_pick", "sfx_chest", "sfx_altar",
            "sfx_explosion", "sfx_slam", "sfx_spark", "sfx_fire_pot", "sfx_boss_roar", "sfx_horn", "sfx_gate",
            "sfx_announce", "sfx_fight", "sfx_victory", "sfx_death", "sfx_cheer", "sfx_giftthrow", "sfx_giftland", "sfx_boo", "sfx_cackle", "sfx_snare", "sfx_rattle", "sfx_bomb", "sfx_bloodrite", "sfx_splinter", "sfx_spikes",
            "sfx_squeak", "sfx_quill",
            "amb_crowd", "amb_crowd_roar",
        };

        /// <summary>The crowd sounds are stereo (voices spread across the stands); everything else is mono.</summary>
        public static int Channels(string name) => name.StartsWith("amb_") || name == "sfx_cheer" || name == "sfx_boo" ? 2 : 1;

        /// <summary>16-bit PCM; stereo samples are interleaved L/R.</summary>
        public static void WriteWav(string path, float[] samples, int channels = 1)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + bytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(Rate); w.Write(Rate * 2 * channels); w.Write((short)(2 * channels)); w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(bytes);
                foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
        }
    }
}
