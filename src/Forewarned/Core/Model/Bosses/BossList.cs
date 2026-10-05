using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Every boss module, in game order. Milestone 2 adds the other six.</summary>
    public static class BossList
    {
        public static readonly IReadOnlyList<BossModule> All = new BossModule[]
        {
            new ModerModule(),
            new FaderModule()
        };
    }
}
