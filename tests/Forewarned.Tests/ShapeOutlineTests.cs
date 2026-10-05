using System.Collections.Generic;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class ShapeOutlineTests
    {
        private static readonly Vec2 Boss = new Vec2(0f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        [Fact]
        public void CircleIsAFullRingOfPointsAroundItsCentre()
        {
            List<Vec2> pts = ShapeOutline.Points(Shape.Circle(10f, 3f), Boss, North);
            Assert.Equal(ShapeOutline.Segments, pts.Count);
            foreach (Vec2 p in pts)
                Assert.Equal(10f, Vec2.Distance(new Vec2(0f, 3f), p), 3);
        }

        [Fact]
        public void RingIsItsCentreLine()
        {
            List<Vec2> pts = ShapeOutline.Points(Shape.Ring(8f, 4f), new Vec2(5f, 5f), North);
            Assert.Equal(ShapeOutline.Segments, pts.Count);
            Assert.Equal(8f, Vec2.Distance(new Vec2(5f, 5f), pts[7]), 3);
        }

        [Fact]
        public void ConeStartsAtTheApexAndSpansItsAngle()
        {
            List<Vec2> pts = ShapeOutline.Points(Shape.Cone(10f, 90f), Boss, North);
            Assert.Equal(0f, pts[0].Length, 3);
            Vec2 first = pts[1], last = pts[pts.Count - 1];
            Assert.Equal(10f, first.Length, 3);
            Assert.Equal(90f, Geometry.AngleDeg(first, last), 1);
            Assert.True(first.X < 0f, "the arc starts on the boss's left");
        }

        [Fact]
        public void LineIsARectangleAhead()
        {
            List<Vec2> pts = ShapeOutline.Points(Shape.Line(39f, 3f), Boss, new Vec2(0f, 2f));
            Assert.Equal(4, pts.Count);
            Assert.Equal(-1.5f, pts[0].X, 3);
            Assert.Equal(0f, pts[0].Z, 3);
            Assert.Equal(39f, pts[1].Z, 3);
            Assert.Equal(1.5f, pts[2].X, 3);
        }

        [Fact]
        public void NoShapeHasNoPoints()
        {
            Assert.Empty(ShapeOutline.Points(Shape.None, Boss, North));
        }
    }
}
