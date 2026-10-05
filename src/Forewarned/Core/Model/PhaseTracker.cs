using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>
    /// Which health thresholds each boss has crossed this fight. A threshold is announced once, on the
    /// way down; healing back over it and jitter around it stay quiet. The first observation of a boss
    /// marks thresholds it is already below as crossed without announcing them.
    /// </summary>
    public sealed class PhaseTracker
    {
        private readonly Dictionary<long, HashSet<string>> _crossed = new Dictionary<long, HashSet<string>>();

        public IList<Phase> Observe(long bossId, IReadOnlyList<Phase> phases, float hp)
        {
            var result = new List<Phase>();
            HashSet<string> crossed;
            bool first = !_crossed.TryGetValue(bossId, out crossed);
            if (first)
                _crossed[bossId] = crossed = new HashSet<string>();
            foreach (Phase p in phases)
            {
                if (hp > p.At || crossed.Contains(p.Key))
                    continue;
                crossed.Add(p.Key);
                if (!first)
                    result.Add(p);
            }
            result.Sort((a, b) => b.At.CompareTo(a.At));
            return result;
        }

        public void Forget(long bossId) => _crossed.Remove(bossId);

        public void Clear() => _crossed.Clear();
    }
}
