using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Fader, Ashlands. PLAN.md §4 and §10.</summary>
    public sealed class FaderModule : BossModule
    {
        private static readonly string[] PrefabList = { "Fader" };

        private static readonly AbilitySpec[] AbilityList =
        {
            new AbilitySpec
            {
                Id = "fader.flamebreath", Name = "Flame breath", ItemPrefab = "Fader_Flamebreath",
                Triggers = new[] { "attack_flamebreath" }, DefaultLevel = Level.Danger,
                ActionKey = "action.get_behind", Response = Response.GetBehind,
                Shape = Shape.Line(39.45f, 3f, ShapeSource.Aoe),
                Cooldown = 25f, HpMin = 0.05f, HpMax = 0.85f, WindUp = 2.34f, AiRange = 20f, MaxAngle = 15f
            },
            new AbilitySpec
            {
                // Fader_Fissure_Intense (below 35%, every 20 s) shares the trigger.
                Id = "fader.fissure", Name = "Fissure", ItemPrefab = "Fader_Fissure",
                Triggers = new[] { "attack_Fissure" }, DefaultLevel = Level.Danger,
                ActionKey = "action.keep_moving", Response = Response.KeepMoving,
                Shape = Shape.Circle(11f, 0f, Anchor.Target, ShapeSource.SpawnAbility),
                Cooldown = 30f, HpMax = 0.85f, WindUp = 2.88f, AiRange = 40f, MaxAngle = 180f
            },
            new AbilitySpec
            {
                Id = "fader.walloffire", Name = "Wall of fire", ItemPrefab = "Fader_WallOfFire",
                Triggers = new[] { "attack_WallOfFire" }, DefaultLevel = Level.Danger,
                ActionKey = "action.leave_gap", Response = Response.ExitRing,
                Shape = Shape.Ring(8f, 4f, ShapeSource.SpawnAbility),
                Cooldown = 60f, HpMin = 0.15f, HpMax = 0.9f, WindUp = 1.43f, AiRange = 40f, MaxAngle = 180f
            },
            new AbilitySpec
            {
                // Fader_Meteors_Intense (below 25%, every 18 s) shares the trigger.
                Id = "fader.meteors", Name = "Meteors", ItemPrefab = "Fader_Meteors",
                Triggers = new[] { "taunt" }, DefaultLevel = Level.Danger,
                ActionKey = "action.keep_moving", Response = Response.KeepMoving,
                Shape = Shape.Circle(15f, 0f, Anchor.Target, ShapeSource.SpawnAbility),
                Cooldown = 25f, WindUp = 1.17f, AiRange = 30f, MaxAngle = 20f
            },
            new AbilitySpec
            {
                Id = "fader.spin", Name = "Spin", ItemPrefab = "Fader_Spin",
                Triggers = new[] { "attack_Spin" }, DefaultLevel = Level.Danger,
                ActionKey = "action.back_out_m", Response = Response.LeaveArea,
                Shape = Shape.Circle(8.5f, 0f, Anchor.Boss, ShapeSource.AttackSphere),
                Cooldown = 20f, WindUp = 1.3f, AiRange = 8f, MaxAngle = 360f
            },
            new AbilitySpec
            {
                // Fader_Roar_Intense (below 35%, every 26 s) shares the trigger.
                Id = "fader.roar", Name = "Roar", ItemPrefab = "Fader_Roar",
                Triggers = new[] { "attack_roar" }, DefaultLevel = Level.Info,
                ActionKey = "fader.roar.action", Response = Response.KillAdds,
                Cooldown = 45f, HpMax = 0.55f, WindUp = 1.53f, AiRange = 100f, MaxAngle = 45f
            },
            new AbilitySpec
            {
                Id = "fader.bite", Name = "Bite", ItemPrefab = "Fader_Bite",
                Triggers = new[] { "attack_bite" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.parry_roll", Response = Response.Parry,
                Shape = Shape.Cone(10f, 40f), Cooldown = 3f, WindUp = 1.27f, AiRange = 9f, MaxAngle = 10f
            },
            new AbilitySpec
            {
                Id = "fader.claw", Name = "Claw", ItemPrefab = "Fader_Claw_Left",
                Triggers = new[] { "attack_ClawL", "attack_ClawR" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.parry_roll", Response = Response.Parry,
                Shape = Shape.Cone(10f, 65f), Cooldown = 3f, WindUp = 1.24f, AiRange = 9f, MaxAngle = 10f
            }
        };

        private static readonly Phase[] PhaseList =
        {
            new Phase { At = 0.85f, Key = "fader.phase.85" },
            new Phase { At = 0.55f, Key = "fader.phase.55" },
            new Phase { At = 0.35f, Key = "fader.phase.35" },
            new Phase { At = 0.25f, Key = "fader.phase.25" }
        };

        public override string Key => "fader";
        public override string DisplayName => "Fader";
        public override string NameToken => "$enemy_fader";
        public override int Order => 9;
        public override string[] Prefabs => PrefabList;
        public override IReadOnlyList<AbilitySpec> Abilities => AbilityList;
        public override IReadOnlyList<Phase> Phases => PhaseList;
    }
}
