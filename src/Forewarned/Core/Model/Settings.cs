namespace Forewarned.Core.Model
{
    /// <summary>The player's choices, as the model sees them. The plugin implements this over its
    /// BepInEx config (plan 2); tests use DefaultSettings.</summary>
    public interface IAbilitySettings
    {
        bool Enabled { get; }
        bool OnlyDuringBossFight { get; }
        bool BossEnabled(BossModule m);
        /// <summary>Pull and phase announces for this boss.</summary>
        bool Announces(BossModule m);
        bool Warn(AbilitySpec a);
        bool Sound(AbilitySpec a);
        bool Visual(AbilitySpec a);
        Level LevelOf(AbilitySpec a);
        bool AlwaysWarn(AbilitySpec a);
    }

    /// <summary>The out-of-the-box choices (PLAN.md §9): the spec's level and on/off; sound for Danger
    /// and Caution; ground visuals for Danger only.</summary>
    public sealed class DefaultSettings : IAbilitySettings
    {
        public static bool DefaultSound(AbilitySpec a) => a.DefaultLevel == Level.Danger || a.DefaultLevel == Level.Caution;
        public static bool DefaultVisual(AbilitySpec a) => a.DefaultLevel == Level.Danger;

        public bool Enabled => true;
        public bool OnlyDuringBossFight => true;
        public bool BossEnabled(BossModule m) => true;
        public bool Announces(BossModule m) => true;
        public bool Warn(AbilitySpec a) => a.DefaultOn;
        public bool Sound(AbilitySpec a) => DefaultSound(a);
        public bool Visual(AbilitySpec a) => DefaultVisual(a);
        public Level LevelOf(AbilitySpec a) => a.DefaultLevel;
        public bool AlwaysWarn(AbilitySpec a) => false;
    }
}
