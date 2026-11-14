#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.Motor
{
    public readonly struct MotorBodyState
    {
        public Float3 Feet { get; }
        public Float3 Velocity { get; }
        public float AimYaw { get; }
        public float AimPitch { get; }
        public Stance Stance { get; }
        public float CapsuleHeight { get; }
        public bool Grounded { get; }
        public SurfaceId GroundSurfaceId { get; }
        public Float3 GroundNormal { get; }
        public uint LastGroundedTick { get; }
        public uint JumpConsumedSequence { get; }
        public float LocomotionDistanceMeters { get; }
        public bool SprintActive { get; }
        public uint SlideTicksRemaining { get; }
        public uint SlideCooldownTicksRemaining { get; }
        public bool SlideActive => SlideTicksRemaining > 0;

        public MotorBodyState(
            Float3 feet, Float3 velocity, float aimYaw, float aimPitch, Stance stance, float capsuleHeight,
            bool grounded, SurfaceId groundSurfaceId, Float3 groundNormal, uint lastGroundedTick,
            uint jumpConsumedSequence, float locomotionDistanceMeters, bool sprintActive,
            uint slideTicksRemaining = 0, uint slideCooldownTicksRemaining = 0)
        {
            Feet = feet; Velocity = velocity; AimYaw = aimYaw; AimPitch = aimPitch; Stance = stance; CapsuleHeight = capsuleHeight;
            Grounded = grounded; GroundSurfaceId = groundSurfaceId; GroundNormal = groundNormal; LastGroundedTick = lastGroundedTick;
            JumpConsumedSequence = jumpConsumedSequence; LocomotionDistanceMeters = locomotionDistanceMeters; SprintActive = sprintActive;
            SlideTicksRemaining = slideTicksRemaining; SlideCooldownTicksRemaining = slideCooldownTicksRemaining;
        }
    }

    /// <summary>Authoritative capsule motor. Root translation is never driven by animation.</summary>
    public sealed class KinematicMotor
    {
        private readonly MotorConfig config;
        private readonly QueryFilter motorFilter = new QueryFilter(CollisionLayers.MotorMask, default, false);

        public KinematicMotor(MotorConfig? config = null) { this.config = config ?? MotorConfig.BaselineV1; }
        public MotorConfig Config => config;

        public MotorBodyState Step(MotorBodyState previous, InputCommand command, uint tick, IWorldQueries world, bool slidePressed = false)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (previous.Stance == Stance.Dead)
                return previous;

            bool wantCrouch = (command.Buttons & InputButtons.CrouchHeld) != 0;
            bool wantAim = (command.Buttons & InputButtons.AimHeld) != 0;
            bool wantSprint = (command.Buttons & InputButtons.SprintHeld) != 0 && !wantCrouch && !wantAim;
            bool fireOrReload = (command.Buttons & (InputButtons.FireHeld | InputButtons.FirePressed | InputButtons.ReloadPressed)) != 0;
            if (fireOrReload) wantSprint = false;

            bool wantsJump = (command.Buttons & InputButtons.JumpPressed) != 0;
            uint slideTicks = previous.SlideTicksRemaining;
            uint cooldown = previous.SlideCooldownTicksRemaining > 0 ? previous.SlideCooldownTicksRemaining - 1 : 0;
            if (!previous.Grounded || wantsJump) slideTicks = 0;
            bool startSlide = slidePressed && previous.SlideCooldownTicksRemaining == 0
                && slideTicks == 0 && previous.Grounded && previous.SprintActive && wantSprint
                && !wantsJump && command.MoveY > 0f
                && MotorMath.Length(MotorMath.Horizontal(previous.Velocity)) >= MotorConfig.SlideMinEntrySpeed;
            if (startSlide)
            {
                slideTicks = MotorConfig.SlideDurationTicks;
                cooldown = MotorConfig.SlideCooldownTicks;
            }
            bool sliding = slideTicks > 0;
            if (sliding) wantCrouch = true;

            float targetHeight = wantCrouch ? config.CrouchHeight : config.StandingHeight;
            Stance stance = wantCrouch ? Stance.Crouching : Stance.Standing;
            if (!TryResize(previous.Feet, previous.CapsuleHeight, targetHeight, world, out float height))
            {
                // Ceiling blocked crouch/stand: keep prior validated height/stance.
                height = previous.CapsuleHeight;
                stance = previous.Stance == Stance.Crouching ? Stance.Crouching : Stance.Standing;
                if (stance == Stance.Crouching) wantSprint = false;
            }

            float yaw = command.AimYaw;
            float pitch = command.AimPitch;
            var velocity = previous.Velocity;
            uint jumpConsumed = previous.JumpConsumedSequence;
            bool jumpEdge = (command.Buttons & InputButtons.JumpPressed) != 0
                && previous.Grounded
                && command.Sequence != previous.JumpConsumedSequence;
            if (jumpEdge)
            {
                velocity = new Float3(velocity.X, config.JumpSpeed, velocity.Z);
                jumpConsumed = command.Sequence;
            }

            float speed = stance == Stance.Crouching ? config.CrouchSpeed : wantSprint ? config.SprintSpeed : config.WalkSpeed;
            if (wantAim) speed *= config.AdsSpeedMultiplier;

            Float3 wish = MotorMath.Add(
                MotorMath.Scale(MotorMath.YawForward(yaw), ClampAxis(command.MoveY)),
                MotorMath.Scale(MotorMath.YawRight(yaw), ClampAxis(command.MoveX)));
            float wishLen = MotorMath.Length(wish);
            if (wishLen > 1f) wish = MotorMath.Scale(wish, 1f / wishLen);
            Float3 wishVel = MotorMath.Scale(wish, speed);

            Float3 horizontal = MotorMath.Horizontal(velocity);
            if (sliding)
            {
                // Only authority supplies the entry boost; held input/turning cannot accelerate or steer a slide.
                horizontal = startSlide
                    ? MotorMath.Scale(MotorMath.NormalizeOrZero(horizontal), MotorConfig.SlideEntrySpeed)
                    : Accelerate(horizontal, default, MotorConfig.SlideDeceleration, MotorMath.TickSeconds);
            }
            else
            {
                float accel = previous.Grounded ? (wishLen > 1e-4f ? config.GroundAccel : config.GroundDecel) : config.AirAccel;
                horizontal = Accelerate(horizontal, wishVel, accel, MotorMath.TickSeconds);
            }
            velocity = new Float3(horizontal.X, velocity.Y, horizontal.Z);

            if (!jumpEdge)
            {
                if (previous.Grounded && previous.GroundNormal.Y >= Math.Cos(config.MaxSlopeRadians) - 1e-4f)
                {
                    // Grounded: gravity is cancelled by the ground, and the horizontal wish velocity is redirected along the
                    // ground plane so ramps neither slow the actor (gravity projected onto the slope) nor launch it.
                    Float3 gn = previous.GroundNormal;
                    velocity = new Float3(velocity.X, -(gn.X * velocity.X + gn.Z * velocity.Z) / gn.Y, velocity.Z);
                }
                else
                    velocity = new Float3(velocity.X, velocity.Y + config.Gravity * MotorMath.TickSeconds, velocity.Z);
            }
            if (velocity.Y < config.TerminalFall) velocity = new Float3(velocity.X, config.TerminalFall, velocity.Z);

            Float3 feet = previous.Feet;
            Float3 displacement = MotorMath.Scale(velocity, MotorMath.TickSeconds);
            // A grounded actor that is not jumping follows the ground: contact projection (ramps) must not leave it
            // with upward velocity, which would suppress the ground probe and launch it off the ramp.
            bool followGround = previous.Grounded && !jumpEdge;
            feet = SlideMove(feet, height, ref velocity, displacement, world, followGround, out bool stepped, out SurfaceId stepSurface);
            if (followGround && velocity.Y > 0f) velocity = new Float3(velocity.X, 0f, velocity.Z);

            bool ascendingJump = velocity.Y > 0f && jumpConsumed == command.Sequence && (command.Buttons & InputButtons.JumpPressed) != 0;
            bool grounded = false;
            SurfaceId groundId = default;
            Float3 groundNormal = new Float3(0, 1, 0);
            uint lastGrounded = previous.LastGroundedTick;
            if (!ascendingJump || previous.Grounded && jumpEdge == false)
            {
                // Downhill snap: the ground can fall away by up to movedHorizontal * tan(MaxSlope) in one tick.
                float movedHorizontal = MotorMath.Length(MotorMath.Horizontal(MotorMath.Sub(feet, previous.Feet)));
                float extraProbe = followGround ? movedHorizontal * (float)Math.Tan(config.MaxSlopeRadians) : 0f;
                ProbeGround(feet, height, velocity.Y, extraProbe, world, out grounded, out groundId, out groundNormal, out feet);
                if (!grounded && stepped)
                {
                    // Resting on a step edge: supported by geometry the step-up already validated.
                    grounded = true; groundId = stepSurface; groundNormal = new Float3(0, 1, 0);
                }
            }

            if (grounded)
            {
                lastGrounded = tick;
                if (velocity.Y < 0f) velocity = new Float3(velocity.X, 0f, velocity.Z);
            }

            if (sliding)
            {
                slideTicks--;
                if (!grounded || MotorMath.Length(MotorMath.Horizontal(velocity)) < 0.1f) slideTicks = 0;
            }

            float moved = MotorMath.Length(MotorMath.Horizontal(MotorMath.Sub(feet, previous.Feet)));
            return new MotorBodyState(
                feet, velocity, yaw, pitch, stance, height, grounded, groundId, groundNormal,
                lastGrounded, jumpConsumed, previous.LocomotionDistanceMeters + moved,
                wantSprint && !sliding && stance == Stance.Standing && grounded, slideTicks, cooldown);
        }

        private static float ClampAxis(float value)
        {
            if (value > 1f) return 1f;
            if (value < -1f) return -1f;
            return value;
        }

        private static Float3 Accelerate(Float3 current, Float3 wish, float accel, float dt)
        {
            Float3 delta = MotorMath.Sub(wish, current);
            float maxDelta = accel * dt;
            float len = MotorMath.Length(delta);
            if (len <= maxDelta || len < 1e-8f) return wish;
            return MotorMath.Add(current, MotorMath.Scale(delta, maxDelta / len));
        }

        private bool TryResize(Float3 feet, float fromHeight, float toHeight, IWorldQueries world, out float height)
        {
            height = fromHeight;
            if (Math.Abs(fromHeight - toHeight) < 1e-5f) { height = toHeight; return true; }
            if (toHeight < fromHeight)
            {
                // Crouch shrink is always valid for static ceiling; floor already validated.
                height = toHeight;
                return true;
            }
            // Standing up: ceiling probe from current head sphere upward by the growth delta.
            float grow = toHeight - fromHeight;
            var capsule = MotorMath.BodyCapsule(feet, fromHeight, config.Radius);
            var head = capsule.TopSphereCenter;
            var ceiling = world.Raycast(head, new Float3(0, 1, 0), grow + config.Radius, motorFilter);
            if (ceiling.Status == QueryStatus.Hit && ceiling.Hit.DistanceMeters < grow + config.Radius - config.Skin)
                return false;
            height = toHeight;
            return true;
        }

        private Float3 SlideMove(Float3 feet, float height, ref Float3 velocity, Float3 displacement, IWorldQueries world,
            bool allowStep, out bool stepped, out SurfaceId stepSurface)
        {
            stepped = false; stepSurface = default;
            for (int i = 0; i < config.MaxSlideIterations; i++)
            {
                if (displacement.LengthSquared <= 1e-12) break;
                var capsule = MotorMath.BodyCapsule(feet, height, config.Radius);
                var sweep = world.SweepCapsule(capsule, displacement, motorFilter);
                if (sweep.Status != QueryStatus.Hit)
                {
                    feet = MotorMath.Add(feet, displacement);
                    break;
                }
                var hit = sweep.Hit;
                float travel = Math.Max(0f, hit.DistanceMeters - config.Skin);
                Float3 unit = MotorMath.NormalizeOrZero(displacement);
                feet = MotorMath.Add(feet, MotorMath.Scale(unit, travel));
                Float3 remaining = MotorMath.Sub(displacement, MotorMath.Scale(unit, travel));
                // Step-up: a grounded actor blocked by a non-walkable contact lifts by step height, spends only the
                // remaining horizontal travel and snaps down. The whole remaining displacement is consumed.
                if (allowStep && hit.Normal.Y < Math.Cos(config.MaxSlopeRadians)
                    && TryStep(feet, height, MotorMath.Horizontal(remaining), world, out Float3 stepFeet, out SurfaceId surface))
                {
                    feet = stepFeet;
                    stepped = true; stepSurface = surface;
                    break;
                }
                displacement = MotorMath.ProjectOnPlane(remaining, hit.Normal);
                velocity = MotorMath.ProjectOnPlane(velocity, hit.Normal);
                if (hit.Normal.Y >= Math.Cos(config.MaxSlopeRadians))
                {
                    // Walkable: kill into-slope velocity already projected.
                }
                else if (hit.Normal.Y < 0.1f)
                {
                    // Steep/wall: keep vertical.
                }
            }
            return feet;
        }

        private bool TryStep(Float3 feet, float height, Float3 horizontal, IWorldQueries world, out Float3 result, out SurfaceId surface)
        {
            result = feet; surface = default;
            // Lift by StepHeight plus skin so a step exactly StepHeight tall is not rejected by contact tolerance.
            float lift = config.StepHeight + config.Skin;
            Float3 up = new Float3(0, lift, 0);
            var upCapsule = MotorMath.BodyCapsule(feet, height, config.Radius);
            var upSweep = world.SweepCapsule(upCapsule, up, motorFilter);
            if (upSweep.Status == QueryStatus.Hit) return false;
            Float3 raised = MotorMath.Add(feet, up);
            float hLen = MotorMath.Length(horizontal);
            if (hLen <= 1e-6f) return false;
            // Measure the real obstacle height with a ray just past the capsule rim: edge contacts of a rounded
            // capsule must not let it climb obstacles taller than a step one rolled edge at a time.
            Float3 ahead = MotorMath.Scale(horizontal, 1f / hLen);
            const float rayLift = 0.3f;
            var rayOrigin = new Float3(
                feet.X + ahead.X * (config.Radius + 0.02f), feet.Y + lift + rayLift, feet.Z + ahead.Z * (config.Radius + 0.02f));
            var topRay = world.Raycast(rayOrigin, new Float3(0, -1, 0), rayLift + lift + config.Snap + config.Skin, motorFilter);
            if (topRay.Status != QueryStatus.Hit || topRay.Hit.Normal.Y < Math.Cos(config.MaxSlopeRadians)) return false;
            float topY = rayOrigin.Y - topRay.Hit.DistanceMeters;
            if (topY - feet.Y > lift + 1e-4f) return false;
            var forwardCapsule = MotorMath.BodyCapsule(raised, height, config.Radius);
            var forward = world.SweepCapsule(forwardCapsule, horizontal, motorFilter);
            if (forward.Status == QueryStatus.Hit && forward.Hit.DistanceMeters < hLen - config.Skin)
                return false;
            Float3 over = MotorMath.Add(raised, horizontal);
            // Snap down onto the step top (or its edge): at most the lift plus the usual snap distance.
            var downCapsule = MotorMath.BodyCapsule(over, height, config.Radius);
            var down = world.SweepCapsule(downCapsule, new Float3(0, -(lift + config.Snap + config.Skin), 0), motorFilter);
            if (down.Status != QueryStatus.Hit) return false;
            if (down.Hit.Normal.Y < 0.1f) return false;
            float drop = Math.Max(0f, down.Hit.DistanceMeters - config.Skin);
            result = new Float3(over.X, over.Y - drop, over.Z);
            // Must actually be a rise (otherwise ordinary sliding handles it) and no higher than a step.
            if (result.Y - feet.Y > lift + 1e-4f) return false;
            surface = down.Hit.StaticSurfaceId;
            return true;
        }

        private void ProbeGround(Float3 feet, float height, float verticalVelocity, float extraProbe, IWorldQueries world,
            out bool grounded, out SurfaceId groundId, out Float3 groundNormal, out Float3 snappedFeet)
        {
            grounded = false; groundId = default; groundNormal = new Float3(0, 1, 0); snappedFeet = feet;
            if (verticalVelocity > 0.01f) return;
            float probe = config.Snap + config.Skin + extraProbe;
            var capsule = MotorMath.BodyCapsule(feet, height, config.Radius);
            var sweep = world.SweepCapsule(capsule, new Float3(0, -probe, 0), motorFilter);
            if (sweep.Status != QueryStatus.Hit) return;
            var hit = sweep.Hit;
            if (hit.Normal.Y < Math.Cos(config.MaxSlopeRadians)) return;
            grounded = true;
            groundId = hit.StaticSurfaceId;
            groundNormal = hit.Normal;
            float drop = Math.Max(0f, hit.DistanceMeters - config.Skin);
            snappedFeet = new Float3(feet.X, feet.Y - drop, feet.Z);
        }
    }
}
