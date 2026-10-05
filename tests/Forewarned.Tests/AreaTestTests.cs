using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class AreaTestTests
    {
        private static readonly Vec2 Boss = new Vec2(0f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static Verdict At(Shape s, float x, float z) => AreaTest.Classify(s, Boss, North, new Vec2(x, z));

        [Fact]
        public void Vec2Basics()
        {
            Assert.Equal(-1f, North.Left.X, 3);
            Assert.Equal(0f, North.Left.Z, 3);
            Assert.True(Vec2.Cross(North, North.Left) > 0f);
            Assert.Equal(5f, new Vec2(3f, 4f).Length, 3);
            Assert.Equal(0.6f, new Vec2(3f, 4f).Normalized.X, 3);
            Assert.Equal(0f, Vec2.Zero.Normalized.Length, 3);
            Assert.Equal(90f, Geometry.AngleDeg(North, new Vec2(1f, 0f)), 1);
            Assert.Equal(180f, Geometry.AngleDeg(North, new Vec2(0f, -1f)), 1);
        }

        [Fact]
        public void CircleAroundTheBoss()
        {
            Shape spin = Shape.Circle(8.5f, 0f, Anchor.Boss, ShapeSource.AttackSphere);
            Assert.Equal(Verdict.Inside, At(spin, 0f, 8f));
            Assert.Equal(Verdict.Near, At(spin, 0f, 9.5f));
            Assert.Equal(Verdict.Outside, At(spin, 0f, 11f));
        }

        [Fact]
        public void CircleAheadOfTheBoss()
        {
            Shape stomp = Shape.Circle(10f, 3f, Anchor.Boss, ShapeSource.AttackSphere);
            Assert.Equal(Verdict.Inside, At(stomp, 0f, 12.9f));   // 9.9 m from the centre at (0,3)
            Assert.Equal(Verdict.Near, At(stomp, 0f, -7.5f));     // 10.5 m
            Assert.Equal(Verdict.Outside, At(stomp, 0f, -12.5f)); // 15.5 m
        }

        [Fact]
        public void Cone()
        {
            Shape bite = Shape.Cone(10f, 40f, ShapeSource.AttackCone);
            Assert.Equal(Verdict.Inside, At(bite, 0f, 9f));
            Assert.Equal(Verdict.Inside, At(bite, 0f, 0.3f));     // under the boss
            Assert.Equal(Verdict.Near, At(bite, 3f, 5f));         // 11° outside the edge, 1.1 m from it
            Assert.Equal(Verdict.Near, At(bite, 0f, 11.5f));      // just beyond the range
            Assert.Equal(Verdict.Outside, At(bite, 8f, 1f));
            Assert.Equal(Verdict.Outside, At(bite, 0f, -3f));     // behind
        }

        [Fact]
        public void Line()
        {
            Shape breath = Shape.Line(39.45f, 3f, ShapeSource.Aoe);
            Assert.Equal(Verdict.Inside, At(breath, 1f, 20f));
            Assert.Equal(Verdict.Near, At(breath, 2.5f, 20f));
            Assert.Equal(Verdict.Outside, At(breath, 4f, 20f));
            Assert.Equal(Verdict.Near, At(breath, 0f, -1f));
            Assert.Equal(Verdict.Outside, At(breath, 0f, -3f));
        }

        [Fact]
        public void RingCountsItsWholeDisc()
        {
            Shape wall = Shape.Ring(8f, 4f, ShapeSource.SpawnAbility);
            Assert.Equal(Anchor.Target, wall.Anchor);
            Assert.Equal(Verdict.Inside, At(wall, 1f, 1f));
            Assert.Equal(Verdict.Near, At(wall, 11f, 0f));
            Assert.Equal(Verdict.Outside, At(wall, 13f, 0f));
        }

        [Fact]
        public void NoShapeIsAlwaysOutside()
        {
            Assert.Equal(Verdict.Outside, At(Shape.None, 0f, 1f));
        }

        [Fact]
        public void SafeMetresRoundsUp()
        {
            Assert.Equal(9, Shape.Circle(8.5f, 0f, Anchor.Boss, ShapeSource.Fixed).SafeMetres);
            Assert.Equal(10, Shape.Ring(8f, 4f, ShapeSource.Fixed).SafeMetres);
            Assert.Equal(10, Shape.Cone(10f, 40f, ShapeSource.Fixed).SafeMetres);
            Assert.Equal(0, Shape.None.SafeMetres);
        }

        [Fact]
        public void CopyIsIndependent()
        {
            Shape a = Shape.Circle(5f, 0f, Anchor.Boss, ShapeSource.Fixed);
            Shape b = a.Copy();
            b.Radius = 7f;
            Assert.Equal(5f, a.Radius);
        }
    }
}
