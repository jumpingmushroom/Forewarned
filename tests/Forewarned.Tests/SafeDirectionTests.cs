using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class SafeDirectionTests
    {
        private static readonly Vec2 Boss = new Vec2(0f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static void Near(Vec2 expected, Vec2 actual)
        {
            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Z, actual.Z, 3);
        }

        [Fact]
        public void GetBehindHeadsForThePointBehindTheBoss()
        {
            Vec2 me = new Vec2(0f, 10f);
            Near(new Vec2(0f, -1f), SafeDirection.Toward(Response.GetBehind, Shape.Line(39f, 3f), Boss, North, Boss, me));
        }

        [Fact]
        public void LeaveAreaPointsAwayFromTheCentre()
        {
            Shape spin = Shape.Circle(8.5f);
            Near(new Vec2(0.6f, 0.8f), SafeDirection.Toward(Response.LeaveArea, spin, Boss, North, Boss, new Vec2(3f, 4f)));
        }

        [Fact]
        public void LeaveAreaFromTheVeryCentreBacksAwayFromTheBoss()
        {
            Near(new Vec2(0f, -1f), SafeDirection.Toward(Response.LeaveArea, Shape.Circle(8.5f), Boss, North, Boss, Boss));
        }

        [Fact]
        public void LeaveLineSteppsToTheNearerSide()
        {
            Shape breath = Shape.Cone(30f, 10f);
            Near(new Vec2(1f, 0f), SafeDirection.Toward(Response.LeaveLine, breath, Boss, North, Boss, new Vec2(1f, 10f)));
            Near(new Vec2(-1f, 0f), SafeDirection.Toward(Response.LeaveLine, breath, Boss, North, Boss, new Vec2(-1f, 10f)));
            Near(new Vec2(-1f, 0f), SafeDirection.Toward(Response.LeaveLine, breath, Boss, North, Boss, new Vec2(0f, 10f)));
        }

        [Fact]
        public void KeepMovingStrafesAroundTheBoss()
        {
            Near(new Vec2(-1f, 0f), SafeDirection.Toward(Response.KeepMoving, Shape.Circle(15f, 0f, Anchor.Target), Boss, North, new Vec2(0f, 10f), new Vec2(0f, 10f)));
        }

        [Fact]
        public void ExitRingLeavesAwayFromTheBoss()
        {
            Near(new Vec2(0f, 1f), SafeDirection.Toward(Response.ExitRing, Shape.Ring(8f, 4f), Boss, North, new Vec2(0f, 10f), new Vec2(0f, 10f)));
        }

        [Theory]
        [InlineData(Response.None)]
        [InlineData(Response.BreakLos)]
        [InlineData(Response.Parry)]
        [InlineData(Response.KillAdds)]
        [InlineData(Response.Find)]
        public void NoArrow(Response r)
        {
            Assert.Equal(0f, SafeDirection.Toward(r, Shape.Circle(5f), Boss, North, Boss, new Vec2(1f, 1f)).Length);
        }
    }
}
