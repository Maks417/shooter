#nullable enable
namespace Shooter.Simulation.Motor
{
    /// <summary>Numeric motor defaults mirrored from docs/fixtures/gameplay-defaults.json (gameplay.v1).</summary>
    public readonly struct MotorConfig
    {
        // ADR-039 local movement recipe; legacy gameplay.v1 defaults stay frozen.
        public const uint SlideDurationTicks = 45;
        public const uint SlideCooldownTicks = 60;
        public const float SlideEntrySpeed = 6.5f;
        public const float SlideMinEntrySpeed = 4.5f;
        public const float SlideDeceleration = 4f;
        public float StandingHeight { get; }
        public float CrouchHeight { get; }
        public float Radius { get; }
        public float StandingEye { get; }
        public float CrouchEye { get; }
        public float WalkSpeed { get; }
        public float SprintSpeed { get; }
        public float CrouchSpeed { get; }
        public float AdsSpeedMultiplier { get; }
        public float Gravity { get; }
        public float TerminalFall { get; }
        public float JumpSpeed { get; }
        public float GroundAccel { get; }
        public float GroundDecel { get; }
        public float AirAccel { get; }
        public float StepHeight { get; }
        public float Skin { get; }
        public float Snap { get; }
        public float MaxSlopeRadians { get; }
        public int MaxSlideIterations { get; }
        public int MaxOverlapRecovery { get; }

        public MotorConfig(
            float standingHeight, float crouchHeight, float radius, float standingEye, float crouchEye,
            float walkSpeed, float sprintSpeed, float crouchSpeed, float adsSpeedMultiplier,
            float gravity, float terminalFall, float jumpSpeed,
            float groundAccel, float groundDecel, float airAccel,
            float stepHeight, float skin, float snap, float maxSlopeRadians,
            int maxSlideIterations, int maxOverlapRecovery)
        {
            StandingHeight = standingHeight; CrouchHeight = crouchHeight; Radius = radius;
            StandingEye = standingEye; CrouchEye = crouchEye;
            WalkSpeed = walkSpeed; SprintSpeed = sprintSpeed; CrouchSpeed = crouchSpeed; AdsSpeedMultiplier = adsSpeedMultiplier;
            Gravity = gravity; TerminalFall = terminalFall; JumpSpeed = jumpSpeed;
            GroundAccel = groundAccel; GroundDecel = groundDecel; AirAccel = airAccel;
            StepHeight = stepHeight; Skin = skin; Snap = snap; MaxSlopeRadians = maxSlopeRadians;
            MaxSlideIterations = maxSlideIterations; MaxOverlapRecovery = maxOverlapRecovery;
        }

        public static MotorConfig BaselineV1 { get; } = new MotorConfig(
            standingHeight: 1.8f, crouchHeight: 1.2f, radius: 0.32f,
            standingEye: 1.65f, crouchEye: 1.05f,
            walkSpeed: 3.5f, sprintSpeed: 5.5f, crouchSpeed: 2.0f, adsSpeedMultiplier: 0.65f,
            gravity: -20f, terminalFall: -35f, jumpSpeed: 5f,
            groundAccel: 20f, groundDecel: 25f, airAccel: 5f,
            stepHeight: 0.25f, skin: 0.02f, snap: 0.08f,
            maxSlopeRadians: (float)(System.Math.PI * 45.0 / 180.0),
            maxSlideIterations: 4, maxOverlapRecovery: 4);
    }
}
