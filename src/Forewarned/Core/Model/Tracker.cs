using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>
    /// PLAN.md §11.2: what each boss has done. Pairs every trigger with the next hit event to learn
    /// the real wind-up per (prefab, trigger), and keeps each ability's last use and the boss's
    /// health for later "next X in ~N s" timers.
    /// </summary>
    public sealed class Tracker
    {
        /// <summary>A hit sooner than this after a trigger belongs to the previous attack.</summary>
        public const float MinWindUp = 0.2f;
        /// <summary>A hit later than this means the trigger's own hit was missed (aborted, out of range).</summary>
        public const float MaxWindUp = 6f;
        public const int MaxSamples = 9;

        private sealed class Pending
        {
            public string Prefab;
            public string Trigger;
            public string AbilityId;
            public float Time;
        }

        private readonly Dictionary<long, Pending> _pending = new Dictionary<long, Pending>();
        private readonly Dictionary<string, List<float>> _samples = new Dictionary<string, List<float>>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _lastUse = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<long, float> _health = new Dictionary<long, float>();

        public void OnTrigger(long bossId, string prefab, string trigger, string abilityId, float time)
        {
            _pending[bossId] = new Pending { Prefab = prefab, Trigger = trigger, AbilityId = abilityId, Time = time };
            _lastUse[UseKey(bossId, abilityId)] = time;
        }

        public string OnHit(long bossId, float time)
        {
            Pending p;
            if (!_pending.TryGetValue(bossId, out p))
                return null;
            float dt = time - p.Time;
            if (dt < MinWindUp)
                return null;
            _pending.Remove(bossId);
            if (dt > MaxWindUp)
                return null;
            string key = SampleKey(p.Prefab, p.Trigger);
            List<float> list;
            if (!_samples.TryGetValue(key, out list))
                _samples[key] = list = new List<float>();
            list.Add(dt);
            if (list.Count > MaxSamples)
                list.RemoveAt(0);
            return p.AbilityId;
        }

        /// <summary>Mean of the samples within 25% of their median, or null with no samples.</summary>
        public float? LearnedWindUp(string prefab, string trigger)
        {
            List<float> list;
            if (!_samples.TryGetValue(SampleKey(prefab, trigger), out list) || list.Count == 0)
                return null;
            var sorted = new List<float>(list);
            sorted.Sort();
            int n = sorted.Count;
            float median = n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
            float sum = 0f;
            int kept = 0;
            foreach (float s in sorted)
                if (Math.Abs(s - median) <= median * 0.25f)
                {
                    sum += s;
                    kept++;
                }
            return kept > 0 ? sum / kept : median;
        }

        public int SampleCount(string prefab, string trigger)
        {
            List<float> list;
            return _samples.TryGetValue(SampleKey(prefab, trigger), out list) ? list.Count : 0;
        }

        public float? LastUse(long bossId, string abilityId)
        {
            float t;
            return _lastUse.TryGetValue(UseKey(bossId, abilityId), out t) ? t : (float?)null;
        }

        public void SetHealth(long bossId, float hp) => _health[bossId] = hp;

        public float? Health(long bossId)
        {
            float hp;
            return _health.TryGetValue(bossId, out hp) ? hp : (float?)null;
        }

        /// <summary>The boss is gone. What was learned about its prefab stays.</summary>
        public void Forget(long bossId)
        {
            _pending.Remove(bossId);
            _health.Remove(bossId);
            string prefix = bossId + "\n";
            var stale = new List<string>();
            foreach (string k in _lastUse.Keys)
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                    stale.Add(k);
            foreach (string k in stale)
                _lastUse.Remove(k);
        }

        public void Clear()
        {
            _pending.Clear();
            _samples.Clear();
            _lastUse.Clear();
            _health.Clear();
        }

        private static string SampleKey(string prefab, string trigger) => prefab + "\n" + trigger;
        private static string UseKey(long bossId, string abilityId) => bossId + "\n" + abilityId;
    }
}
