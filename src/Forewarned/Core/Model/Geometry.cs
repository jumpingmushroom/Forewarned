using System;

namespace Forewarned.Core.Model
{
    public static class Geometry
    {
        /// <summary>Unsigned angle between two directions, 0 to 180 degrees. 0 if either is zero.</summary>
        public static float AngleDeg(Vec2 a, Vec2 b)
        {
            Vec2 na = a.Normalized, nb = b.Normalized;
            if (na.Length == 0f || nb.Length == 0f)
                return 0f;
            double dot = Clamp(Vec2.Dot(na, nb), -1f, 1f);
            return (float)(Math.Acos(dot) * 180.0 / Math.PI);
        }

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
