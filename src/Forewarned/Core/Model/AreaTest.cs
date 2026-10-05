using System;

namespace Forewarned.Core.Model
{
    public enum Verdict { Outside, Near, Inside }

    /// <summary>PLAN.md §11.2: is a point inside an attack's area, near it (within the margin), or clear?</summary>
    public static class AreaTest
    {
        /// <summary>Metres around a shape that still count as "near": a step or a turn of the boss away.</summary>
        public const float Margin = 2f;

        /// <param name="origin">The boss, for Boss-anchored shapes; where the attack lands, for Target-anchored ones.</param>
        /// <param name="facing">The boss's facing, a unit vector.</param>
        public static Verdict Classify(Shape s, Vec2 origin, Vec2 facing, Vec2 p, float margin = Margin)
        {
            switch (s.Kind)
            {
                case ShapeKind.Circle: return Disc(origin + facing * s.Offset, s.Radius, p, margin);
                case ShapeKind.Ring: return Disc(origin, s.Radius + s.Width * 0.5f, p, margin);
                case ShapeKind.Line: return Strip(origin, facing, s.Range, s.Width, p, margin);
                case ShapeKind.Cone: return Cone(origin, facing, s.Range, s.Angle, p, margin);
                default: return Verdict.Outside;
            }
        }

        private static Verdict Disc(Vec2 centre, float radius, Vec2 p, float margin)
        {
            float d = Vec2.Distance(centre, p);
            return d <= radius ? Verdict.Inside : d <= radius + margin ? Verdict.Near : Verdict.Outside;
        }

        private static Verdict Strip(Vec2 origin, Vec2 facing, float length, float width, Vec2 p, float margin)
        {
            Vec2 rel = p - origin;
            float along = Vec2.Dot(rel, facing);
            float side = Math.Abs(Vec2.Cross(facing, rel));
            float half = width * 0.5f;
            if (along >= 0f && along <= length && side <= half)
                return Verdict.Inside;
            if (along >= -margin && along <= length + margin && side <= half + margin)
                return Verdict.Near;
            return Verdict.Outside;
        }

        private static Verdict Cone(Vec2 origin, Vec2 facing, float range, float angle, Vec2 p, float margin)
        {
            Vec2 rel = p - origin;
            float d = rel.Length;
            if (d <= 0.5f)
                return Verdict.Inside; // under the boss: every sweep passes through here
            float half = angle * 0.5f;
            float off = Geometry.AngleDeg(facing, rel);
            if (d <= range && off <= half)
                return Verdict.Inside;
            if (d > range + margin)
                return Verdict.Outside;
            if (off <= half)
                return Verdict.Near; // just past the tip
            float beyond = off - half;
            if (beyond >= 90f)
                return d <= margin ? Verdict.Near : Verdict.Outside;
            float sideways = d * (float)Math.Sin(beyond * Math.PI / 180.0);
            return sideways <= margin ? Verdict.Near : Verdict.Outside;
        }
    }
}
