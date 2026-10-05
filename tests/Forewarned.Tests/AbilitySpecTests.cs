using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class AbilitySpecTests
    {
        private static AbilitySpec Breath() => new AbilitySpec
        {
            Id = "fader.flamebreath", Name = "Flame breath", ItemPrefab = "Fader_Flamebreath",
            Triggers = new[] { "attack_flamebreath" }, DefaultLevel = Level.Danger,
            ActionKey = "action.get_behind", Response = Response.GetBehind,
            Shape = Shape.Line(39.45f, 3f, ShapeSource.Aoe),
            Cooldown = 25f, HpMin = 0.05f, HpMax = 0.85f, WindUp = 2.34f, AiRange = 20f, MaxAngle = 15f
        };

        [Fact]
        public void NameKeyFollowsTheId()
        {
            Assert.Equal("fader.flamebreath.name", Breath().NameKey);
        }

        [Fact]
        public void WithoutLiveDataTheSpecNumbersApply()
        {
            AbilitySpec spec = Breath();
            ResolvedAbility a = ResolvedAbility.Resolve(spec, null, null);
            Assert.Equal(25f, a.Cooldown);
            Assert.Equal(2.34f, a.WindUp);
            Assert.False(a.WindUpLearned);
            Assert.Equal(39.45f, a.Shape.Range);
            Assert.NotSame(spec.Shape, a.Shape);
        }

        [Fact]
        public void LiveNumbersOverride()
        {
            var live = new AbilityNumbers { Cooldown = 20f, HpMax = 0.8f, Range = 40f, Width = 4f, AiRange = 22f };
            ResolvedAbility a = ResolvedAbility.Resolve(Breath(), live, null);
            Assert.Equal(20f, a.Cooldown);
            Assert.Equal(0.8f, a.HpMax);
            Assert.Equal(0.05f, a.HpMin);
            Assert.Equal(22f, a.AiRange);
            Assert.Equal(40f, a.Shape.Range);
            Assert.Equal(4f, a.Shape.Width);
        }

        [Fact]
        public void FixedShapesIgnoreLiveSizes()
        {
            AbilitySpec spec = Breath();
            spec.Shape = Shape.Circle(5f, 0f, Anchor.Target, ShapeSource.Fixed);
            ResolvedAbility a = ResolvedAbility.Resolve(spec, new AbilityNumbers { Radius = 9f }, null);
            Assert.Equal(5f, a.Shape.Radius);
        }

        [Fact]
        public void LearnedWindUpWins()
        {
            ResolvedAbility a = ResolvedAbility.Resolve(Breath(), null, 2.1f);
            Assert.Equal(2.1f, a.WindUp);
            Assert.True(a.WindUpLearned);
        }

        [Theory]
        [InlineData(0.85f, true)]
        [InlineData(0.86f, false)]
        [InlineData(0.05f, true)]
        [InlineData(0.04f, false)]
        public void HealthGatesAreInclusive(float hp, bool usable)
        {
            Assert.Equal(usable, ResolvedAbility.Resolve(Breath(), null, null).UsableAt(hp));
        }
    }
}
