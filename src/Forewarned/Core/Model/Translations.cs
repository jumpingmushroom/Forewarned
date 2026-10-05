using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>Forewarned's own strings: "key = text" lines, '#' comments. Missing keys fall back to English.</summary>
    public sealed class Translations
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Translations _fallback;

        public static Translations Parse(string text)
        {
            var t = new Translations();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length > 0 && value.Length > 0)
                    t._map[key] = value;
            }
            return t;
        }

        public Translations WithFallback(Translations fallback)
        {
            _fallback = fallback;
            return this;
        }

        public IEnumerable<string> Keys => _map.Keys;

        public string Get(string key)
        {
            if (key == null)
                return null;
            string value;
            if (_map.TryGetValue(key, out value))
                return value;
            return _fallback != null ? _fallback.Get(key) : null;
        }
    }
}
