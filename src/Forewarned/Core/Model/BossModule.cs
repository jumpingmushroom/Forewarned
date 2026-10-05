using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>A health threshold (0..1) whose downward crossing is announced with Key.</summary>
    public sealed class Phase
    {
        public float At;
        public string Key;
    }

    /// <summary>
    /// One boss (PLAN.md §11.2): the prefabs it covers, its abilities and phase thresholds, and
    /// optional boss-specific reactions. Pure data and logic; no game types.
    /// </summary>
    public abstract class BossModule
    {
        protected static readonly IReadOnlyList<Phase> NoPhases = new Phase[0];

        /// <summary>Config and translation prefix: "fader".</summary>
        public abstract string Key { get; }
        /// <summary>English name for the config section: "Fader".</summary>
        public abstract string DisplayName { get; }
        /// <summary>The game's name token, localised by the game: "$enemy_fader".</summary>
        public abstract string NameToken { get; }
        /// <summary>Game order, for the config section number: Eikthyr 3 … Kall 10.</summary>
        public abstract int Order { get; }
        public abstract string[] Prefabs { get; }
        public abstract IReadOnlyList<AbilitySpec> Abilities { get; }

        public virtual IReadOnlyList<Phase> Phases => NoPhases;

        /// <summary>An announce key for a change in a tracked state (Moder: "flying"), or null for none.</summary>
        public virtual string OnStateChanged(string state, bool value) => null;
    }
}
