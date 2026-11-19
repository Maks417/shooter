#nullable enable
namespace Shooter.Game.Session
{
    /// <summary>Product session tuning constants that are not part of the frozen Game-v1 contract.</summary>
    public static class SessionDefaults
    {
        /// <summary>Pre-match freeze: 5 s at the fixed 60 Hz tick.</summary>
        public const int PreMatchCountdownTicks = 300;
    }
}
