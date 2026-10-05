using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class RelevanceTests
    {
        private static Scene Solo(float x, float z) =>
            new Scene { BossPos = new Vec2(0f, 0f), BossFacing = new Vec2(0f, 1f), Me = new Vec2(x, z) };

        [Fact]
        public void BossAnchoredShapesStartAtTheBoss()
        {
            Vec2 origin;
            Assert.Equal(Verdict.Inside, Relevance.Judge(Shape.Line(39f, 3f), 20f, Solo(0f, 10f), out origin));
            Assert.Equal(0f, origin.Length);
            Assert.Equal(Verdict.Outside, Relevance.Judge(Shape.Line(39f, 3f), 20f, Solo(0f, -10f), out origin));
        }

        [Fact]
        public void SoloYouAreTheTargetWhenInRange()
        {
            Vec2 origin;
            Shape meteors = Shape.Circle(15f, 0f, Anchor.Target);
            Assert.Equal(Verdict.Inside, Relevance.Judge(meteors, 30f, Solo(5f, 20f), out origin));
            Assert.Equal(5f, origin.X);
            Assert.Equal(Verdict.Outside, Relevance.Judge(meteors, 30f, Solo(0f, 40f), out origin));
        }

        [Fact]
        public void InAGroupTheBossAimsAtWhoeverItFaces()
        {
            Shape meteors = Shape.Circle(15f, 0f, Anchor.Target);
            var s = Solo(10f, 0f);
            s.Others.Add(new Vec2(0f, 10f)); // straight ahead of the boss
            Vec2? aim = Relevance.AimPoint(s, 30f);
            Assert.Equal(10f, aim.Value.Z);
            Vec2 origin;
            Assert.Equal(Verdict.Inside, Relevance.Judge(meteors, 30f, s, out origin)); // 14.1 m from the other player
            s.Me = new Vec2(20f, 0f);
            Assert.Equal(Verdict.Outside, Relevance.Judge(meteors, 30f, s, out origin)); // 22.4 m
        }

        [Fact]
        public void AShapelessAttackReachesEveryoneInItsRange()
        {
            Vec2 origin;
            Assert.Equal(Verdict.Inside, Relevance.Judge(Shape.None, 100f, Solo(0f, 50f), out origin));
            Assert.Equal(Verdict.Outside, Relevance.Judge(Shape.None, 30f, Solo(0f, 50f), out origin));
        }

        [Theory]
        [InlineData(Level.Danger, Verdict.Inside, false, Outcome.Special)]
        [InlineData(Level.Danger, Verdict.Near, false, Outcome.Special)]
        [InlineData(Level.Danger, Verdict.Outside, false, Outcome.Announce)]
        [InlineData(Level.Caution, Verdict.Inside, false, Outcome.Special)]
        [InlineData(Level.Caution, Verdict.Outside, false, Outcome.Nothing)]
        [InlineData(Level.Caution, Verdict.Outside, true, Outcome.Special)]
        [InlineData(Level.Info, Verdict.Inside, false, Outcome.Announce)]
        [InlineData(Level.Info, Verdict.Outside, true, Outcome.Announce)]
        [InlineData(Level.None, Verdict.Inside, true, Outcome.Nothing)]
        public void Decide(Level level, Verdict v, bool always, Outcome expected)
        {
            Assert.Equal(expected, Relevance.Decide(level, v, always));
        }
    }
}
