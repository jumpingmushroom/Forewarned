namespace Forewarned.Core.Model
{
    /// <summary>PLAN.md §11.2: which way the path arrow points for each response.</summary>
    public static class SafeDirection
    {
        /// <summary>How far behind the boss "get behind" aims.</summary>
        public const float BehindDistance = 4f;

        /// <param name="origin">The shape's origin: the boss for Boss-anchored shapes, the landing point for Target-anchored ones.</param>
        /// <returns>A unit direction for the player to move in, or Vec2.Zero when there is no useful arrow.</returns>
        public static Vec2 Toward(Response r, Shape s, Vec2 bossPos, Vec2 facing, Vec2 origin, Vec2 me)
        {
            facing = facing.Normalized;
            switch (r)
            {
                case Response.GetBehind:
                    // Heading straight for the point behind the boss can route through the attack
                    // itself (Fader's flame breath line); step out of it sideways first instead.
                    if (AreaTest.Classify(s, origin, facing, me) != Verdict.Outside)
                        goto case Response.LeaveLine;
                    return (bossPos - facing * BehindDistance - me).Normalized;
                case Response.LeaveArea:
                {
                    Vec2 centre = s.Kind == ShapeKind.Circle ? origin + facing * s.Offset : origin;
                    Vec2 away = (me - centre).Normalized;
                    return away.Length > 0f ? away : (facing * -1f).Normalized;
                }
                case Response.LeaveLine:
                    // Step out sideways, to whichever side of the boss's line you already stand on.
                    return Vec2.Cross(facing, me - origin) < 0f ? (facing.Left * -1f).Normalized : facing.Left.Normalized;
                case Response.KeepMoving:
                {
                    Vec2 fromBoss = (me - bossPos).Normalized;
                    return fromBoss.Length > 0f ? fromBoss.Left : facing.Left.Normalized;
                }
                case Response.ExitRing:
                {
                    // The wall of fire starts on the far side and closes towards the boss; leave away from the boss.
                    Vec2 away = (me - bossPos).Normalized;
                    return away.Length > 0f ? away : facing.Normalized;
                }
                default:
                    return Vec2.Zero;
            }
        }
    }
}
