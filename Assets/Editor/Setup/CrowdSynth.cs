using System;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Crowd sounds made of people, not filtered noise (playtest: the old loop's pulsed noise sounded like a steam
    /// train). Each voice is a glottal buzz through three vowel formants that glide from syllable to syllable, with
    /// consonant hiss between syllables, talking in phrases with pauses of its own; dozens of voices panned across the
    /// stands, a little air absorption and a small room reverb make a crowd. Nothing in it repeats on a fixed rhythm:
    /// every voice, clap and whistle keeps its own random time. Deterministic (fixed seeds), 44.1 kHz stereo,
    /// interleaved L/R.
    /// </summary>
    public static class CrowdSynth
    {
        const int Rate = SfxSynth.Rate;

        public enum Mood { Murmur, Roar, Cheer, Boo }

        // F1, F2, F3 (Hz) for a, e, i, o, u, schwa, ae: an adult male; higher voices scale them up
        static readonly float[,] Vowels =
        {
            { 730, 1090, 2440 }, { 530, 1840, 2480 }, { 270, 2290, 3010 }, { 570, 840, 2410 },
            { 300, 870, 2240 }, { 500, 1500, 2500 }, { 660, 1720, 2410 },
        };

        /// <summary>A seamless loop: `seconds` long, the tail crossfaded into the head.</summary>
        public static float[] Loop(Mood mood, float seconds, int seed)
        {
            const float xf = 1.2f;
            var raw = Render(mood, seconds + xf + 0.05f, seed); // a hair long: float rounding must not cut the crossfade short
            int n = (int)(seconds * Rate), x0 = (int)(xf * Rate);
            var o = new float[n * 2];
            for (int i = 0; i < n; i++)
                for (int c = 0; c < 2; c++)
                {
                    float v = raw[i * 2 + c];
                    if (i < x0)
                    {
                        float k = i / (float)x0; // equal power: the summed crowd keeps its loudness through the seam
                        v = v * Mathf.Sin(k * Mathf.PI * 0.5f) + raw[(n + i) * 2 + c] * Mathf.Cos(k * Mathf.PI * 0.5f);
                    }
                    o[i * 2 + c] = v;
                }
            Normalize(o, mood == Mood.Murmur ? 0.16f : 0.2f, 0.85f);
            return o;
        }

        /// <summary>A one-shot (cheer, boo): voices come in together, swell and die away.</summary>
        public static float[] Shot(Mood mood, float seconds, int seed)
        {
            var o = Render(mood, seconds, seed);
            int fade = Rate / 50;
            int n = o.Length / 2;
            for (int i = n - fade; i < n; i++) { float k = (n - 1 - i) / (float)fade; o[i * 2] *= k; o[i * 2 + 1] *= k; }
            Normalize(o, 0.22f, 0.9f);
            return o;
        }

        static void Normalize(float[] s, float rms, float peakCap)
        {
            double sum = 0; float peak = 1e-4f;
            for (int i = 0; i < s.Length; i++) { sum += s[i] * s[i]; peak = Mathf.Max(peak, Mathf.Abs(s[i])); }
            float r = Mathf.Max(1e-5f, (float)Math.Sqrt(sum / s.Length));
            float g = Mathf.Min(rms / r, peakCap / peak);
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i] * g, -1f, 1f);
        }

        // ---------- the crowd ----------
        static float[] Render(Mood mood, float seconds, int seed)
        {
            int n = (int)(seconds * Rate);
            var l = new float[n];
            var r = new float[n];
            var rng = new System.Random(seed);
            int voices = mood == Mood.Murmur ? 56 : mood == Mood.Roar ? 72 : mood == Mood.Cheer ? 46 : 40;
            for (int v = 0; v < voices; v++) Voice(l, r, mood, seconds, rng.Next(), v / (float)voices);
            if (mood == Mood.Roar || mood == Mood.Cheer)
            {
                int clappers = mood == Mood.Roar ? 26 : 22;
                for (int c = 0; c < clappers; c++) Clapper(l, r, mood, seconds, rng.Next());
                int whistles = mood == Mood.Roar ? 7 : 3;
                for (int w = 0; w < whistles; w++) Whistle(l, r, mood, seconds, rng.Next());
            }
            Rustle(l, r, seconds, rng.Next(), mood == Mood.Murmur ? 0.05f : 0.08f);
            Air(l, mood == Mood.Murmur ? 0.42f : 0.55f);
            Air(r, mood == Mood.Murmur ? 0.42f : 0.55f);
            var o = new float[n * 2];
            Reverb(l, r, o, mood == Mood.Murmur ? 0.38f : 0.3f);
            return o;
        }

        /// <summary>One person: phrases of syllables (vowel formants gliding, consonant hiss at each onset) and pauses.</summary>
        static void Voice(float[] l, float[] r, Mood mood, float seconds, int seed, float slot)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            bool high = R() < 0.38f;
            float f0 = high ? Mathf.Lerp(175f, 255f, R()) : Mathf.Lerp(92f, 145f, R());
            float formantScale = high ? Mathf.Lerp(1.12f, 1.22f, R()) : Mathf.Lerp(0.95f, 1.05f, R());
            bool excited = mood != Mood.Murmur;
            if (excited) f0 *= Mathf.Lerp(1.25f, 1.6f, R()); // shouting raises the voice
            // distance: far voices are quieter and duller, and sit nearer the middle of the stereo field
            float near = Mathf.Pow(R(), 1.6f);
            float gain = Mathf.Lerp(0.18f, 1f, near) * (excited ? 1.25f : 1f);
            float pan = Mathf.Clamp((slot * 2f - 1f) * 0.85f + (R() - 0.5f) * 0.5f, -1f, 1f) * Mathf.Lerp(0.55f, 1f, near);
            float gl = Mathf.Cos((pan + 1f) * Mathf.PI * 0.25f), gr = Mathf.Sin((pan + 1f) * Mathf.PI * 0.25f);
            float dull = Mathf.Lerp(0.25f, 0.85f, near); // one-pole on the voice: far = muffled

            int n = l.Length;
            float t = mood == Mood.Cheer ? R() * 0.35f : mood == Mood.Boo ? R() * 0.5f : R() * 2.5f;
            double phase = R();
            float lp = 0f, dl = 0f, noiseLp = 0f, lastVoiced = 0f;
            var f1 = new Reso(); var f2 = new Reso(); var f3 = new Reso();
            int vowel = rng.Next(7);
            float F1 = Vowels[vowel, 0], F2 = Vowels[vowel, 1], F3 = Vowels[vowel, 2];
            while (t < seconds)
            {
                // a phrase
                int syllables = mood == Mood.Murmur ? 3 + rng.Next(10) : mood == Mood.Roar ? 1 + rng.Next(4) : 1 + rng.Next(2);
                float phrasePitch = Mathf.Lerp(0.88f, 1.12f, R());
                float phraseLoud = Mathf.Lerp(0.6f, 1f, R());
                for (int s = 0; s < syllables && t < seconds; s++)
                {
                    float len;
                    switch (mood)
                    {
                        case Mood.Murmur: len = Mathf.Lerp(0.09f, 0.24f, R()); break;
                        case Mood.Roar: len = Mathf.Lerp(0.22f, 0.85f, R()); break;
                        case Mood.Cheer: len = Mathf.Lerp(0.7f, 1.6f, R()); break;
                        default: len = Mathf.Lerp(0.8f, 1.5f, R()); break;
                    }
                    int next = mood == Mood.Boo ? 4 : mood == Mood.Cheer ? (R() < 0.6f ? 0 : R() < 0.5f ? 1 : 6) : rng.Next(7);
                    float T1 = Vowels[next, 0] * formantScale, T2 = Vowels[next, 1] * formantScale, T3 = Vowels[next, 2] * formantScale;
                    // pitch contour: talk wanders; a shout rises then falls; a boo sags
                    float p0 = phrasePitch * Mathf.Lerp(0.94f, 1.06f, R());
                    float p1 = mood == Mood.Murmur ? p0 * Mathf.Lerp(0.9f, 1.08f, R())
                             : mood == Mood.Boo ? p0 * Mathf.Lerp(0.78f, 0.9f, R())
                             : p0 * Mathf.Lerp(1.08f, 1.35f, R());
                    float stress = Mathf.Lerp(0.55f, 1f, R()) * phraseLoud;
                    float hiss = R() < 0.55f ? Mathf.Lerp(0.015f, 0.06f, R()) : 0f; // s, t, k, h…
                    float hissLevel = Mathf.Lerp(0.25f, 0.6f, R());
                    int i0 = (int)(t * Rate), i1 = Mathf.Min(n, (int)((t + len) * Rate));
                    float swell = mood == Mood.Cheer ? Mathf.Lerp(0.25f, 0.5f, R()) : 0f;
                    for (int i = i0; i < i1; i++)
                    {
                        float u = (i - i0) / (float)Rate, k = u / len;
                        if ((i & 31) == 0)
                        {
                            float g = 1f - Mathf.Exp(-u / 0.045f); // formants glide into the new vowel
                            f1.Set(Mathf.Lerp(F1, T1, g), 90f, Rate);
                            f2.Set(Mathf.Lerp(F2, T2, g), 120f, Rate);
                            f3.Set(Mathf.Lerp(F3, T3, g), 170f, Rate);
                        }
                        float pitch = f0 * Mathf.Lerp(p0, p1, mood == Mood.Murmur ? k : Mathf.Sin(k * Mathf.PI * 0.5f));
                        phase += pitch / Rate;
                        float ph = (float)(phase - Math.Floor(phase));
                        float buzz = 1f - 2f * ph;                        // falling saw
                        lp += 0.12f * (buzz - lp);                         // glottal tilt
                        float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                        float breath = x * 0.18f;
                        float formed = f1.Run(lp + breath) * 1.0f + f2.Run(lp + breath) * 0.55f + f3.Run(lp + breath) * 0.25f;
                        float voiced = (formed - lastVoiced) * 6f + formed * 0.35f; // the mouth radiates highs (+6 dB/oct)
                        lastVoiced = formed;
                        // syllable envelope: quick attack, a body, a soft end (a cheer swells in)
                        float env = Mathf.Clamp01(u / (swell > 0f ? swell : 0.018f)) * Mathf.Clamp01((len - u) / (excited ? 0.12f : 0.035f));
                        float cons = 0f;
                        if (u < hiss)
                        {
                            noiseLp += 0.5f * (x - noiseLp);
                            cons = (x - noiseLp) * hissLevel * (1f - u / hiss);
                            env *= Mathf.Clamp01(u / hiss);
                        }
                        float s0 = (voiced * env + cons) * stress * gain;
                        dl += dull * (s0 - dl);
                        l[i] += dl * gl;
                        r[i] += dl * gr;
                    }
                    F1 = T1; F2 = T2; F3 = T3;
                    t += len + Mathf.Lerp(0.005f, 0.06f, R());
                }
                if (mood == Mood.Cheer || mood == Mood.Boo)
                {
                    if (R() < 0.6f) break; // most shout once or twice
                    t += Mathf.Lerp(0.1f, 0.4f, R());
                }
                else t += mood == Mood.Murmur ? Mathf.Lerp(0.25f, 2.2f, R()) : Mathf.Lerp(0.08f, 0.7f, R());
            }
        }

        /// <summary>Someone clapping at their own pace and in their own spot; bursts of a few seconds, never in step.</summary>
        static void Clapper(float[] l, float[] r, Mood mood, float seconds, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float pan = R() * 2f - 1f, gl = Mathf.Cos((pan + 1f) * Mathf.PI * 0.25f), gr = Mathf.Sin((pan + 1f) * Mathf.PI * 0.25f);
            float gain = Mathf.Lerp(0.05f, 0.16f, Mathf.Pow(R(), 1.5f));
            float bright = Mathf.Lerp(0.35f, 0.7f, R());
            int n = l.Length;
            float t = R() * (mood == Mood.Cheer ? 0.3f : 3f);
            while (t < seconds)
            {
                float burst = mood == Mood.Cheer ? seconds : Mathf.Lerp(1f, 4f, R());
                float end = t + burst, every = Mathf.Lerp(0.2f, 0.36f, R());
                while (t < end && t < seconds)
                {
                    int i0 = (int)(t * Rate), i1 = Mathf.Min(n, i0 + Rate / 40);
                    float y = 0f, prev = 0f, a = gain * Mathf.Lerp(0.6f, 1f, R());
                    for (int i = i0; i < i1; i++)
                    {
                        float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                        y += bright * (x - y);
                        float hp = y - prev; prev = y;
                        float e = Mathf.Exp(-(i - i0) / (Rate * 0.0045f));
                        l[i] += hp * e * a * gl * 3f;
                        r[i] += hp * e * a * gr * 3f;
                    }
                    t += every * Mathf.Lerp(0.85f, 1.15f, R()); // a person's pace drifts
                }
                t += mood == Mood.Cheer ? seconds : Mathf.Lerp(0.5f, 3f, R());
            }
        }

        /// <summary>A two-finger whistle: a bright tone that swoops.</summary>
        static void Whistle(float[] l, float[] r, Mood mood, float seconds, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float pan = R() * 1.6f - 0.8f, gl = Mathf.Cos((pan + 1f) * Mathf.PI * 0.25f), gr = Mathf.Sin((pan + 1f) * Mathf.PI * 0.25f);
            int n = l.Length;
            float t = R() * (mood == Mood.Cheer ? 0.6f : seconds * 0.8f);
            int count = mood == Mood.Cheer ? 1 : 1 + rng.Next(2);
            for (int k = 0; k < count && t < seconds; k++)
            {
                float len = Mathf.Lerp(0.35f, 0.9f, R()), fa = Mathf.Lerp(1900f, 2600f, R()), fb = fa * Mathf.Lerp(1.08f, 1.3f, R());
                bool wolf = R() < 0.4f; // up-down "wheet-whew"
                float amp = Mathf.Lerp(0.04f, 0.09f, R());
                double phase = 0;
                int i0 = (int)(t * Rate), i1 = Mathf.Min(n, (int)((t + len) * Rate));
                for (int i = i0; i < i1; i++)
                {
                    float u = (i - i0) / (float)Rate, q = u / len;
                    float f = wolf ? Mathf.Lerp(fa, fb, Mathf.Sin(q * Mathf.PI)) : Mathf.Lerp(fa, fb, Mathf.Sqrt(q));
                    phase += f / Rate;
                    float e = Mathf.Clamp01(u / 0.03f) * Mathf.Clamp01((len - u) / 0.08f);
                    float v = Mathf.Sin((float)(phase * 2.0 * Math.PI)) * e * amp;
                    l[i] += v * gl; r[i] += v * gr;
                }
                t += len + Mathf.Lerp(1.5f, 5f, R());
            }
        }

        /// <summary>Feet, cloth, benches: a quiet bed whose level wanders on a random walk (no sine swells).</summary>
        static void Rustle(float[] l, float[] r, float seconds, int seed, float amp)
        {
            var rng = new System.Random(seed);
            float yl = 0f, yr = 0f, level = 0.7f, target = 0.7f;
            for (int i = 0; i < l.Length; i++)
            {
                if (i % (Rate / 4) == 0) target = 0.45f + 0.55f * (float)rng.NextDouble();
                level += (target - level) * 0.00004f;
                yl += 0.08f * ((float)(rng.NextDouble() * 2.0 - 1.0) - yl);
                yr += 0.08f * ((float)(rng.NextDouble() * 2.0 - 1.0) - yr);
                l[i] += yl * amp * level;
                r[i] += yr * amp * level;
            }
        }

        /// <summary>Air between the stands and the pit: a gentle one-pole low-pass (higher a = brighter).</summary>
        static void Air(float[] s, float a)
        {
            float y = 0f;
            for (int i = 0; i < s.Length; i++) { y += a * (s[i] - y); s[i] = y; }
        }

        /// <summary>A small open-air reverb (Freeverb-style damped combs + all-passes), decorrelated per side.</summary>
        static void Reverb(float[] l, float[] r, float[] o, float wet)
        {
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491 };
            int[] alls = { 556, 441, 341 };
            float[] wl = Tank(l, combs, alls, 0), wr = Tank(r, combs, alls, 23);
            for (int i = 0; i < l.Length; i++)
            {
                o[i * 2] = l[i] * (1f - wet) + wl[i] * wet;
                o[i * 2 + 1] = r[i] * (1f - wet) + wr[i] * wet;
            }
        }

        static float[] Tank(float[] x, int[] combs, int[] alls, int spread)
        {
            int n = x.Length;
            var acc = new float[n];
            foreach (int c0 in combs)
            {
                int d = c0 + spread;
                var buf = new float[d];
                int p = 0; float store = 0f;
                for (int i = 0; i < n; i++)
                {
                    float y = buf[p];
                    store = y * 0.6f + store * 0.4f;          // damping
                    buf[p] = x[i] * 0.03f + store * 0.8f;     // feedback
                    acc[i] += y;
                    if (++p >= d) p = 0;
                }
            }
            foreach (int a0 in alls)
            {
                int d = a0 + spread;
                var buf = new float[d];
                int p = 0;
                for (int i = 0; i < n; i++)
                {
                    float b = buf[p], y = -acc[i] + b;
                    buf[p] = acc[i] + b * 0.5f;
                    acc[i] = y;
                    if (++p >= d) p = 0;
                }
            }
            return acc;
        }

        /// <summary>Two-pole resonator (a formant), unity gain at its peak.</summary>
        sealed class Reso
        {
            float _a1, _a2, _g, _y1, _y2;

            public void Set(float freq, float bandwidth, int rate)
            {
                float rr = Mathf.Exp(-Mathf.PI * bandwidth / rate);
                float th = 2f * Mathf.PI * Mathf.Min(freq, rate * 0.45f) / rate;
                _a1 = 2f * rr * Mathf.Cos(th);
                _a2 = -rr * rr;
                _g = (1f - rr * rr) * 0.5f;
            }

            public float Run(float x)
            {
                float y = _g * x + _a1 * _y1 + _a2 * _y2;
                _y2 = _y1; _y1 = y;
                return y;
            }
        }
    }
}
