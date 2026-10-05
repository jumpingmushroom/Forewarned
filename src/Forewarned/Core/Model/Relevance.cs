using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>Where everyone stands when a trigger arrives. Others: the other players' positions.</summary>
    public sealed class Scene
    {
        public Vec2 BossPos;
        public Vec2 BossFacing;
        public Vec2 Me;
        public List<Vec2> Others = new List<Vec2>();
    }

    public enum Outcome { Nothing, Special, Announce }

    /// <summary>PLAN.md §9 "who gets a warning" and §11.2 Relevance.</summary>
    public static class Relevance
    {
        /// <summary>
        /// Where a target-anchored attack lands: among the players within aiRange (+ margin) of the
        /// boss, the one it faces most directly. Solo that's you whenever you're in range. Null when
        /// nobody is in range.
        /// </summary>
        public static Vec2? AimPoint(Scene s, float aiRange)
        {
            float reach = aiRange + AreaTest.Margin;
            Vec2? best = null;
            float bestAngle = float.MaxValue;
            Consider(s, s.Me, reach, ref best, ref bestAngle);
            foreach (Vec2 p in s.Others)
                Consider(s, p, reach, ref best, ref bestAngle);
            return best;
        }

        private static void Consider(Scene s, Vec2 p, float reach, ref Vec2? best, ref float bestAngle)
        {
            if (Vec2.Distance(s.BossPos, p) > reach)
                return;
            float angle = Geometry.AngleDeg(s.BossFacing, p - s.BossPos);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = p;
            }
        }

        /// <param name="origin">Set to where the shape is anchored: the boss, or the aim point.</param>
        public static Verdict Judge(Shape shape, float aiRange, Scene s, out Vec2 origin)
        {
            origin = s.BossPos;
            if (shape.Kind == ShapeKind.None)
                return Vec2.Distance(s.BossPos, s.Me) <= aiRange + AreaTest.Margin ? Verdict.Inside : Verdict.Outside;
            if (shape.Anchor == Anchor.Boss)
                return AreaTest.Classify(shape, s.BossPos, s.BossFacing, s.Me);
            Vec2? aim = AimPoint(s, aiRange);
            if (aim == null)
            {
                origin = s.Me;
                return Verdict.Outside;
            }
            origin = aim.Value;
            return AreaTest.Classify(shape, origin, s.BossFacing, s.Me);
        }

        /// <summary>Inside or near: the special warning. Outside: an announce for Danger, nothing for Caution.
        /// Info is always an announce; always-warn skips the area test.</summary>
        public static Outcome Decide(Level level, Verdict v, bool alwaysWarn)
        {
            switch (level)
            {
                case Level.None: return Outcome.Nothing;
                case Level.Info: return Outcome.Announce;
            }
            if (alwaysWarn || v != Verdict.Outside)
                return Outcome.Special;
            return level == Level.Danger ? Outcome.Announce : Outcome.Nothing;
        }
    }
}
