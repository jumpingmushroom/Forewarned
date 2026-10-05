using Forewarned.Core.Model;

namespace Forewarned.Core
{
    /// <summary>The model's view of the player's F1 choices. Reads live, so changes apply at once.</summary>
    internal sealed class ConfigSettings : IAbilitySettings
    {
        public bool Enabled => PluginConfig.Enabled.Value;
        public bool OnlyDuringBossFight => PluginConfig.OnlyDuringBossFight.Value;
        public bool BossEnabled(BossModule m) => PluginConfig.BossEnabled(m);
        public bool Announces(BossModule m) => PluginConfig.Announces(m);
        public bool Warn(AbilitySpec a) => PluginConfig.Warn(a);
        public bool Sound(AbilitySpec a) => PluginConfig.Sound(a);
        public bool Visual(AbilitySpec a) => PluginConfig.Visual(a);
        public Level LevelOf(AbilitySpec a) => PluginConfig.LevelOf(a);
        public bool AlwaysWarn(AbilitySpec a) => PluginConfig.AlwaysWarn(a);
    }
}
