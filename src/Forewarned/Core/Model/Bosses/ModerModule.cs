using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Moder (prefab "Dragon"), Mountains. PLAN.md §4 and §10.</summary>
    public sealed class ModerModule : BossModule
    {
        private static readonly string[] PrefabList = { "Dragon" };

        private static readonly AbilitySpec[] AbilityList =
        {
            new AbilitySpec
            {
                Id = "moder.breath", Name = "Cold breath", ItemPrefab = "dragon_coldbreath",
                Triggers = new[] { "attack_breath" }, DefaultLevel = Level.Danger,
                ActionKey = "action.leave_line", Response = Response.LeaveLine,
                Shape = Shape.Cone(30f, 10f), Cooldown = 8f, WindUp = 1.37f, AiRange = 20f, MaxAngle = 5f
            },
            new AbilitySpec
            {
                // Flying only. The ice shards spread around whoever she aims at; 5 m is a fixed estimate.
                Id = "moder.icebarrage", Name = "Ice barrage", ItemPrefab = "dragon_spit_shotgun",
                Triggers = new[] { "attack_iceball" }, DefaultLevel = Level.Danger,
                ActionKey = "action.keep_moving", Response = Response.KeepMoving,
                Shape = Shape.Circle(5f, 0f, Anchor.Target, ShapeSource.Fixed),
                Cooldown = 8f, WindUp = 0.89f, AiRange = 25f, MaxAngle = 5f
            },
            new AbilitySpec
            {
                Id = "moder.bite", Name = "Bite", ItemPrefab = "dragon_bite",
                Triggers = new[] { "attack_bite" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.block_sidestep", Response = Response.Parry,
                Shape = Shape.Cone(8f, 20f), Cooldown = 30f, WindUp = 0.99f, AiRange = 7f, MaxAngle = 10f
            },
            new AbilitySpec
            {
                Id = "moder.claw", Name = "Claw", ItemPrefab = "dragon_claw_left",
                Triggers = new[] { "attack_claw_left", "attack_claw_right" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.parry_step_back", Response = Response.Parry,
                Shape = Shape.Cone(12f, 50f), Cooldown = 30f, WindUp = 1.57f, AiRange = 10f, MaxAngle = 30f
            },
            new AbilitySpec
            {
                // No hit: mapped so it isn't reported as unknown, but never warned.
                Id = "moder.scream", Name = "Scream", ItemPrefab = "dragon_taunt",
                Triggers = new[] { "attack_taunt" }, DefaultLevel = Level.None, DefaultOn = false,
                Cooldown = 30f, WindUp = 1.96f, AiRange = 50f, MaxAngle = 5f
            }
        };

        public override string Key => "moder";
        public override string DisplayName => "Moder";
        public override string NameToken => "$enemy_dragon";
        public override int Order => 6;
        public override string[] Prefabs => PrefabList;
        public override IReadOnlyList<AbilitySpec> Abilities => AbilityList;

        public override string OnStateChanged(string state, bool value)
        {
            if (state != "flying")
                return null;
            return value ? "moder.takeoff" : "moder.land";
        }
    }
}
