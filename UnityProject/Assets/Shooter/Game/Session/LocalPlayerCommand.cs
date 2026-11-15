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

        public LocalMovementView(Stance stance, float eyeHeight, bool sliding, float tightness01 = 1f)
        {
            Stance = stance;
            EyeHeight = eyeHeight;
            Sliding = sliding;
            Tightness01 = tightness01 < 0f ? 0f : tightness01 > 1f ? 1f : tightness01;
        }
    }
}
