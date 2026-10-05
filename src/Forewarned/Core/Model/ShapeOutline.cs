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

        /// <summary>J5: straight edges (a Line's four sides, a Cone's two radial edges) are subdivided so
        /// consecutive points are at most this far apart, in metres, so the ground marker follows bumpy
        /// terrain along a long edge instead of cutting straight through a hill.</summary>
        public const float MaxStep = 1.5f;

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
                    var arc = new List<Vec2>();
                    Arc(arc, origin, f, s.Range, s.Angle * 0.5f, -s.Angle, steps, true);
                    AddSteppedInterior(pts, origin, arc[0]);
                    pts.AddRange(arc);
                    AddSteppedInterior(pts, arc[arc.Count - 1], origin);
                    break;
                }
                case ShapeKind.Line:
                {
                    Vec2 side = f.Left * (s.Width * 0.5f);
                    Vec2 tip = f * s.Range;
                    Vec2 a = origin + side;
                    Vec2 b = origin + tip + side;
                    Vec2 c = origin + tip - side;
                    Vec2 d = origin - side;
                    pts.Add(a);
                    AddSteppedInterior(pts, a, b);
                    pts.Add(b);
                    AddSteppedInterior(pts, b, c);
                    pts.Add(c);
                    AddSteppedInterior(pts, c, d);
                    pts.Add(d);
                    AddSteppedInterior(pts, d, a);
                    break;
                }
            }
            return pts;
        }

        /// <summary>The points strictly between from and to (excluding both ends) that split the segment
        /// into equal steps no longer than MaxStep.</summary>
        private static void AddSteppedInterior(List<Vec2> pts, Vec2 from, Vec2 to)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Vec2.Distance(from, to) / MaxStep));
            for (int i = 1; i < steps; i++)
            {
                float t = (float)i / steps;
                pts.Add(new Vec2(from.X + (to.X - from.X) * t, from.Z + (to.Z - from.Z) * t));
            }
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
