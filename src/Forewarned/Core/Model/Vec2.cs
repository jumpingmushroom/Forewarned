using System;
using System.Globalization;

namespace Forewarned.Core.Model
{
    /// <summary>A point or direction on the ground plane (Unity x and z). The model works in 2-D:
    /// height doesn't change who a boss attack reaches.</summary>
    public struct Vec2
    {
        public readonly float X;
        public readonly float Z;

        public Vec2(float x, float z)
        {
            X = x;
            Z = z;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Z * k);

        public float Length => (float)Math.Sqrt(X * X + Z * Z);

        /// <summary>Unit vector, or Zero for a (near) zero vector.</summary>
        public Vec2 Normalized
        {
            get
            {
                float l = Length;
                return l < 1e-5f ? Zero : new Vec2(X / l, Z / l);
            }
        }

        /// <summary>This direction turned 90° to the left, seen from above.</summary>
        public Vec2 Left => new Vec2(-Z, X);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;

        /// <summary>Positive when b points to the left of a. With a unit a, |Cross| is b's sideways distance from a's line.</summary>
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Z - a.Z * b.X;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public override string ToString() =>
            X.ToString("0.0", CultureInfo.InvariantCulture) + "," + Z.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
