using System;
using System.Collections.Generic;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class ShapeOutlineTests
    {
        private static readonly Vec2 Boss = new Vec2(0f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len2 = ab.X * ab.X + ab.Z * ab.Z;
            if (len2 < 1e-9f)
                return Vec2.Distance(p, a);
            float t = Geometry.Clamp01(Vec2.Dot(p - a, ab) / len2);
            Vec2 proj = a + ab * t;
            return Vec2.Distance(p, proj);
        }

        private static Vec2 Rotate(Vec2 f, float deg)
        {
            double a = deg * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            return new Vec2(f.X * cos - f.Z * sin, f.X * sin + f.Z * cos);
        }

        private static void AssertStepsAtMostMaxStep(List<Vec2> pts)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                Vec2 next = pts[(i + 1) % pts.Count];
                Assert.True(Vec2.Distance(pts[i], next) <= ShapeOutline.MaxStep + 1e-3f,
                    "points " + i + " and " + ((i + 1) % pts.Count) + " are more than MaxStep apart");
            }
        }

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
        public void ConeStartsAtTheApexWithRadialEdgesSteppedToMaxStep()
        {
            Shape shape = Shape.Cone(10f, 90f);
            List<Vec2> pts = ShapeOutline.Points(shape, Boss, North);

            // The apex is unchanged (J5).
            Assert.Equal(0f, pts[0].Length, 3);

            Vec2 f = North.Normalized;
            Vec2 arcFirst = Boss + Rotate(f, shape.Angle * 0.5f) * shape.Range;
            Vec2 arcLast = Boss + Rotate(f, -shape.Angle * 0.5f) * shape.Range;
            Assert.True(arcFirst.X < 0f, "the arc starts on the boss's left");
            Assert.Equal(90f, Geometry.AngleDeg(arcFirst, arcLast), 1);

            AssertStepsAtMostMaxStep(pts);
            foreach (Vec2 p in pts)
            {
                bool onArc = Math.Abs(Vec2.Distance(Boss, p) - shape.Range) <= 1e-3f;
                bool onLeftEdge = DistanceToSegment(p, Boss, arcFirst) <= 1e-3f;
                bool onRightEdge = DistanceToSegment(p, Boss, arcLast) <= 1e-3f;
                Assert.True(onArc || onLeftEdge || onRightEdge, "point " + p + " is off the cone's perimeter");
            }
        }

        [Fact]
        public void LineIsARectangleWithEdgesSteppedToMaxStep()
        {
            Vec2 facing = new Vec2(0f, 2f);
            Shape shape = Shape.Line(39f, 3f);
            List<Vec2> pts = ShapeOutline.Points(shape, Boss, facing);

            // The first point is unchanged (J5).
            Assert.Equal(-1.5f, pts[0].X, 3);
            Assert.Equal(0f, pts[0].Z, 3);

            Vec2 f = facing.Normalized;
            Vec2 side = f.Left * (shape.Width * 0.5f);
            Vec2 tip = f * shape.Range;
            Vec2 a = Boss + side;
            Vec2 b = Boss + tip + side;
            Vec2 c = Boss + tip - side;
            Vec2 d = Boss - side;

            AssertStepsAtMostMaxStep(pts);
            foreach (Vec2 p in pts)
            {
                bool onBoundary = DistanceToSegment(p, a, b) <= 1e-3f || DistanceToSegment(p, b, c) <= 1e-3f ||
                    DistanceToSegment(p, c, d) <= 1e-3f || DistanceToSegment(p, d, a) <= 1e-3f;
                Assert.True(onBoundary, "point " + p + " is off the rectangle's boundary");
            }
        }

        [Fact]
        public void NoShapeHasNoPoints()
        {
            Assert.Empty(ShapeOutline.Points(Shape.None, Boss, North));
        }
    }
}
