using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>
    /// PLAN.md §11.4: an attack's area as a closed loop of ground points for the marker. Circles have
    /// Segments points, cones their apex then an arc from the boss's left to its right, lines a
    /// rectangle, rings their centre line (the marker draws it as a band Width wide).
    /// </summary>
    public static class ShapeOutline
    {
        public const int Segments = 48;

        public static List<Vec2> Points(Shape s, Vec2 origin, Vec2 facing)
        {
            var pts = new List<Vec2>();
            Vec2 f = facing.Normalized;
            if (f.Length == 0f)
                f = new Vec2(0f, 1f);
            switch (s.Kind)
            {
                case ShapeKind.Circle:
                    Arc(pts, origin + f * s.Offset, f, s.Radius, -180f, 360f, Segments, false);
                    break;
                case ShapeKind.Ring:
                    Arc(pts, origin, f, s.Radius, -180f, 360f, Segments, false);
                    break;
                case ShapeKind.Cone:
                {
                    pts.Add(origin);
                    int steps = Math.Max(4, (int)Math.Ceiling(s.Angle / 5f));
                    Arc(pts, origin, f, s.Range, s.Angle * 0.5f, -s.Angle, steps, true);
                    break;
                }
                case ShapeKind.Line:
                {
                    Vec2 side = f.Left * (s.Width * 0.5f);
                    Vec2 tip = f * s.Range;
                    pts.Add(origin + side);
                    pts.Add(origin + tip + side);
                    pts.Add(origin + tip - side);
                    pts.Add(origin - side);
                    break;
                }
            }
            return pts;
        }

        /// <summary>Points at radius r around c, starting startDeg to the left of f and sweeping sweepDeg
        /// (positive turns left). With inclusive, the end point is added too.</summary>
        private static void Arc(List<Vec2> pts, Vec2 c, Vec2 f, float r, float startDeg, float sweepDeg, int steps, bool inclusive)
        {
            int count = inclusive ? steps + 1 : steps;
            for (int i = 0; i < count; i++)
            {
                double a = (startDeg + sweepDeg * i / steps) * Math.PI / 180.0;
                float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                var dir = new Vec2(f.X * cos - f.Z * sin, f.X * sin + f.Z * cos);
                pts.Add(c + dir * r);
            }
        }
    }
}
