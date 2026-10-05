using System;

namespace Forewarned.Core.Model
{
    public enum ShapeKind { None, Cone, Circle, Line, Ring }

    /// <summary>Boss: the shape starts at the boss and turns with it. Target: it lands where the boss aims.</summary>
    public enum Anchor { Boss, Target }

    /// <summary>Where LiveData reads the shape's size from in game (PLAN.md §11.3). Fixed: the spec's own numbers.</summary>
    public enum ShapeSource { Fixed, AttackCone, AttackSphere, SpawnAbility, Aoe }

    /// <summary>
    /// An attack's danger area. Cone: Range and Angle (full width, degrees). Circle: Radius, centred
    /// Offset metres ahead of the anchor. Line: a strip Range long and Width wide ahead of the anchor.
    /// Ring: a band of Radius ± Width/2 around the target; everything inside it counts as danger,
    /// since the target is trapped in it.
    /// </summary>
    public sealed class Shape
    {
        public ShapeKind Kind;
        public Anchor Anchor;
        public ShapeSource Source;
        public float Range;
        public float Angle;
        public float Width;
        public float Radius;
        public float Offset;

        public static Shape None => new Shape { Kind = ShapeKind.None };

        public static Shape Cone(float range, float angle, ShapeSource source = ShapeSource.AttackCone) =>
            new Shape { Kind = ShapeKind.Cone, Anchor = Anchor.Boss, Source = source, Range = range, Angle = angle };

        public static Shape Circle(float radius, float offset = 0f, Anchor anchor = Anchor.Boss, ShapeSource source = ShapeSource.Fixed) =>
            new Shape { Kind = ShapeKind.Circle, Anchor = anchor, Source = source, Radius = radius, Offset = offset };

        public static Shape Line(float length, float width, ShapeSource source = ShapeSource.Fixed) =>
            new Shape { Kind = ShapeKind.Line, Anchor = Anchor.Boss, Source = source, Range = length, Width = width };

        public static Shape Ring(float radius, float width, ShapeSource source = ShapeSource.Fixed) =>
            new Shape { Kind = ShapeKind.Ring, Anchor = Anchor.Target, Source = source, Radius = radius, Width = width };

        public Shape Copy() => (Shape)MemberwiseClone();

        /// <summary>How far from the shape's centre (the anchor, for cones and lines) safety starts, rounded
        /// up: the {m} in "Run out, {m} m".</summary>
        public int SafeMetres
        {
            get
            {
                switch (Kind)
                {
                    case ShapeKind.Circle: return (int)Math.Ceiling(Radius - 1e-4);
                    case ShapeKind.Ring: return (int)Math.Ceiling(Radius + Width * 0.5f - 1e-4);
                    case ShapeKind.Cone:
                    case ShapeKind.Line: return (int)Math.Ceiling(Range - 1e-4);
                    default: return 0;
                }
            }
        }
    }
}
