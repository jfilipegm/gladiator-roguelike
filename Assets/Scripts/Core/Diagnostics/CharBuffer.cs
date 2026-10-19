using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Allocation-free text building for UI that changes during play (card descriptions): append into a fixed char
    /// array, then hand it to TMP with SetCharArray(Chars, 0, Length). Overflow is silently truncated.
    /// </summary>
    public sealed class CharBuffer
    {
        public readonly char[] Chars;
        public int Length { get; private set; }

        public CharBuffer(int capacity) => Chars = new char[capacity];

        public CharBuffer Clear() { Length = 0; return this; }

        public CharBuffer Append(char c)
        {
            if (Length < Chars.Length) Chars[Length++] = c;
            return this;
        }

        public CharBuffer Append(string s)
        {
            if (s == null) return this;
            for (int i = 0; i < s.Length; i++) Append(s[i]);
            return this;
        }

        /// <summary>Appends `v` rounded to `decimals` (0-3) places; trailing zeros after the point are dropped.</summary>
        public CharBuffer Append(float v, int decimals)
        {
            decimals = Mathf.Clamp(decimals, 0, 3);
            int scale = decimals == 0 ? 1 : decimals == 1 ? 10 : decimals == 2 ? 100 : 1000;
            long n = (long)System.Math.Round((double)v * scale, System.MidpointRounding.AwayFromZero);
            if (n < 0) { Append('-'); n = -n; }
            long whole = n / scale, frac = n % scale;
            AppendDigits(whole);
            if (frac == 0) return this;
            int digits = decimals;
            while (frac % 10 == 0) { frac /= 10; digits--; }
            Append('.');
            for (int d = digits - 1; d >= 0; d--)
            {
                long p = 1;
                for (int k = 0; k < d; k++) p *= 10;
                Append((char)('0' + frac / p % 10));
            }
            return this;
        }

        void AppendDigits(long n)
        {
            long p = 1;
            while (p * 10 <= n) p *= 10;
            for (; p > 0; p /= 10) Append((char)('0' + n / p % 10));
        }

        public override string ToString() => new string(Chars, 0, Length);
    }
}
