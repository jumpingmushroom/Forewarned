using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Eikthyr, Meadows. PLAN.md §4 and §10. His stomp has no wind-up sound in vanilla.</summary>
    public sealed class EikthyrModule : BossModule
    {
        private static readonly string[] PrefabList = { "Eikthyr" };

        private static readonly AbilitySpec[] AbilityList =
        {
            new AbilitySpec
            {
                Id = "eikthyr.stomp", Name = "Stomp", ItemPrefab = "Eikthyr_stomp",
                Triggers = new[] { "attack_stomp" }, DefaultLevel = Level.Danger,
                ActionKey = "action.run_out_m", Response = Response.LeaveArea,
                Shape = Shape.Circle(10f, 3f, Anchor.Boss, ShapeSource.AttackSphere),
                Cooldown = 40f, WindUp = 2.89f, AiRange = 6f, MaxAngle = 60f
            },
            new AbilitySpec
            {
                Id = "eikthyr.charge", Name = "Charge", ItemPrefab = "Eikthyr_charge",
                Triggers = new[] { "attack2" }, DefaultLevel = Level.Danger,
                ActionKey = "action.sidestep_line", Response = Response.LeaveLine,
                Shape = Shape.Cone(20f, 45f), Cooldown = 25f, WindUp = 1.56f, AiRange = 15f, MaxAngle = 15f
            },
            new AbilitySpec
            {
                Id = "eikthyr.antler", Name = "Antler", ItemPrefab = "Eikthyr_antler",
                Triggers = new[] { "attack1" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.block_step_aside", Response = Response.Parry,
                Shape = Shape.Cone(4.5f, 25f), Cooldown = 5f, WindUp = 0.6f, AiRange = 4f, MaxAngle = 30f
            }
        };

        public override string Key => "eikthyr";
        public override string DisplayName => "Eikthyr";
        public override string NameToken => "$enemy_eikthyr";
        public override int Order => 3;
        public override string[] Prefabs => PrefabList;
        public override IReadOnlyList<AbilitySpec> Abilities => AbilityList;
    }
}
