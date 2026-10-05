using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    public sealed class Announce
    {
        public string Key;
        public string Text;
        public float Start;
    }

    /// <summary>PLAN.md §11.2: up to three announce lines, 4 s each, newest at the bottom.</summary>
    public sealed class Announcer
    {
        public const int MaxLines = 3;
        public const float Life = 4f;
        public const float FadeTime = 0.3f;

        private readonly List<Announce> _lines = new List<Announce>();

        /// <summary>Oldest first; the last line is the newest, drawn at the bottom.</summary>
        public IReadOnlyList<Announce> Lines => _lines;

        public void Add(string key, string text, float now)
        {
            _lines.RemoveAll(l => l.Key == key);
            _lines.Add(new Announce { Key = key, Text = text, Start = now });
            while (_lines.Count > MaxLines)
                _lines.RemoveAt(0);
        }

        public void Tick(float now) => _lines.RemoveAll(l => now >= l.Start + Life + FadeTime);

        public void Clear() => _lines.Clear();

        public static float Alpha(Announce a, float now)
        {
            float end = a.Start + Life;
            return now <= end ? 1f : Geometry.Clamp01(1f - (now - end) / FadeTime);
        }
    }
}
