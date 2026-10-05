namespace Forewarned.Core.Model
{
    /// <summary>What the player should do about an attack. Drives the path arrow (SafeDirection).</summary>
    public enum Response { None, GetBehind, LeaveArea, LeaveLine, KeepMoving, ExitRing, BreakLos, Parry, KillAdds, Find }
}
