#nullable enable
using Shooter.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>Offline human intent. ADR-039 keeps the frozen C0/N1 command layout unchanged.</summary>
    public readonly struct LocalPlayerCommand
    {
        public InputCommand Command { get; }
        public bool SlidePressed { get; }

        public LocalPlayerCommand(InputCommand command, bool slidePressed = false)
        {
            Command = command;
            SlidePressed = slidePressed;
        }
    }

    /// <summary>Copied authority movement view for local camera/rig, separate from the frozen wire layout.</summary>
    public readonly struct LocalMovementView
    {
        public Stance Stance { get; }
        public float EyeHeight { get; }
        public bool Sliding { get; }
        /// <summary>Authoritative tightness fraction 0..1 (ADR-060). Defaults to full when omitted.</summary>
        public float Tightness01 { get; }
        /// <summary>Authoritative horizontal speed (m/s) for cosmetic gait — avoids frame-delta thrash.</summary>
        public float HorizontalSpeedMetersPerSecond { get; }
        /// <summary>Authoritative locomotion distance (m) for gait phase.</summary>
        public float LocomotionDistanceMeters { get; }

        public LocalMovementView(
            Stance stance,
            float eyeHeight,
            bool sliding,
            float tightness01 = 1f,
            float horizontalSpeedMetersPerSecond = 0f,
            float locomotionDistanceMeters = 0f)
        {
            Stance = stance;
            EyeHeight = eyeHeight;
            Sliding = sliding;
            Tightness01 = tightness01 < 0f ? 0f : tightness01 > 1f ? 1f : tightness01;
            HorizontalSpeedMetersPerSecond = horizontalSpeedMetersPerSecond < 0f ? 0f : horizontalSpeedMetersPerSecond;
            LocomotionDistanceMeters = locomotionDistanceMeters < 0f ? 0f : locomotionDistanceMeters;
        }
    }
}
