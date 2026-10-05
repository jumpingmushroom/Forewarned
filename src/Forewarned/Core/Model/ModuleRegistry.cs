using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    public enum TriggerKind { NotTracked, Ignored, Unmapped, Mapped }

    /// <summary>PLAN.md §11.2: prefab → module, (prefab, trigger) → ability.</summary>
    public sealed class ModuleRegistry
    {
        /// <summary>Non-attack triggers the game sends through the same RPC (decomp: SetTrigger call sites).</summary>
        public static readonly HashSet<string> IgnoredTriggers = new HashSet<string>(StringComparer.Ordinal)
        {
            "attack_abort", "detach", "attach", "stagger", "consume", "jump", "dodge", "eat",
            "interact", "equip_hip", "emote_stop", "gpower", "teleportin", "flyin"
        };

        private readonly List<BossModule> _modules = new List<BossModule>();
        private readonly Dictionary<string, BossModule> _byPrefab = new Dictionary<string, BossModule>(StringComparer.Ordinal);
        private readonly Dictionary<string, AbilitySpec> _byTrigger = new Dictionary<string, AbilitySpec>(StringComparer.Ordinal);

        public ModuleRegistry(IEnumerable<BossModule> modules)
        {
            foreach (BossModule m in modules)
            {
                _modules.Add(m);
                foreach (string prefab in m.Prefabs)
                {
                    if (_byPrefab.ContainsKey(prefab))
                        throw new ArgumentException("Prefab claimed twice: " + prefab);
                    _byPrefab[prefab] = m;
                    foreach (AbilitySpec a in m.Abilities)
                        foreach (string trigger in a.Triggers)
                        {
                            string key = Key(prefab, trigger);
                            if (_byTrigger.ContainsKey(key))
                                throw new ArgumentException("Trigger mapped twice: " + prefab + " " + trigger);
                            _byTrigger[key] = a;
                        }
                }
            }
        }

        public IReadOnlyList<BossModule> Modules => _modules;

        public bool Tracks(string prefab) => prefab != null && _byPrefab.ContainsKey(prefab);

        public BossModule ModuleFor(string prefab)
        {
            BossModule m;
            return prefab != null && _byPrefab.TryGetValue(prefab, out m) ? m : null;
        }

        public TriggerKind Classify(string prefab, string trigger, out AbilitySpec ability)
        {
            ability = null;
            if (!Tracks(prefab))
                return TriggerKind.NotTracked;
            if (_byTrigger.TryGetValue(Key(prefab, trigger), out ability))
                return TriggerKind.Mapped;
            return IgnoredTriggers.Contains(trigger) ? TriggerKind.Ignored : TriggerKind.Unmapped;
        }

        private static string Key(string prefab, string trigger) => prefab + "\n" + trigger;
    }
}
