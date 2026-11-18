#nullable enable
using System;
using Shooter.Contracts;
using Shooter.VectorMath;

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
        /// <summary>Authoritative 0..1 tightness (ADR-060). Spawn/respawn starts at <see cref="MotorConfig.TightnessMax"/>.</summary>
        public float Tightness { get; }
        /// <summary>True when overlap recovery could not resolve a penetration (invalid spawn); the motor held its previous feet with zero velocity.</summary>
        public bool OverlapUnresolved { get; }

        public MotorBodyState(
            Float3 feet, Float3 velocity, float aimYaw, float aimPitch, Stance stance, float capsuleHeight,
            bool grounded, SurfaceId groundSurfaceId, Float3 groundNormal, uint lastGroundedTick,
            uint jumpConsumedSequence, float locomotionDistanceMeters, bool sprintActive,
            uint slideTicksRemaining = 0, uint slideCooldownTicksRemaining = 0, bool overlapUnresolved = false,
            float tightness = MotorConfig.TightnessMax)
        {
            Feet = feet; Velocity = velocity; AimYaw = aimYaw; AimPitch = aimPitch; Stance = stance; CapsuleHeight = capsuleHeight;
            Grounded = grounded; GroundSurfaceId = groundSurfaceId; GroundNormal = groundNormal; LastGroundedTick = lastGroundedTick;
            JumpConsumedSequence = jumpConsumedSequence; LocomotionDistanceMeters = locomotionDistanceMeters; SprintActive = sprintActive;
            SlideTicksRemaining = slideTicksRemaining; SlideCooldownTicksRemaining = slideCooldownTicksRemaining;
            OverlapUnresolved = overlapUnresolved;
            Tightness = tightness < 0f ? 0f : tightness > MotorConfig.TightnessMax ? MotorConfig.TightnessMax : tightness;
        }
    }

    /// <summary>Authoritative capsule motor. Root translation is never driven by animation.</summary>
    public sealed class KinematicMotor
    {
        private readonly MotorConfig config;
        private readonly QueryFilter motorFilter = new QueryFilter(CollisionLayers.MotorMask, default, false);
        private const float OverlapEpsilon = 1e-4f;
        private const float ContactDistance = 1e-3f;
        /// <summary>Smallest rise above the current feet that counts as a step top (ADR-114).</summary>
        private const float MinStepRise = 0.005f;
        private readonly HitBuffer overlapBuffer = new HitBuffer(16);

        public KinematicMotor(MotorConfig? config = null) { this.config = config ?? MotorConfig.BaselineV1; }
        public MotorConfig Config => config;

        public MotorBodyState Step(MotorBodyState previous, InputCommand command, uint tick, IWorldQueries world, bool slidePressed = false, float weaponSpeedScale = 1f)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (previous.Stance == Stance.Dead)
                return previous;

            if (!RecoverOverlap(previous, world, out Float3 recoveredFeet, out Float3 recoveredVelocity))
            {
                // Unresolved overlap (invalid spawn): stop in place and report; aim still follows input.
                return new MotorBodyState(
                    previous.Feet, default, command.AimYaw, command.AimPitch, previous.Stance, previous.CapsuleHeight,
                    previous.Grounded, previous.GroundSurfaceId, previous.GroundNormal, previous.LastGroundedTick,
                    previous.JumpConsumedSequence, previous.LocomotionDistanceMeters, false, 0, previous.SlideCooldownTicksRemaining, true,
                    previous.Tightness);
            }
            if (recoveredFeet.X != previous.Feet.X || recoveredFeet.Y != previous.Feet.Y || recoveredFeet.Z != previous.Feet.Z)
                previous = new MotorBodyState(
                    recoveredFeet, recoveredVelocity, previous.AimYaw, previous.AimPitch, previous.Stance, previous.CapsuleHeight,
                    previous.Grounded, previous.GroundSurfaceId, previous.GroundNormal, previous.LastGroundedTick,
                    previous.JumpConsumedSequence, previous.LocomotionDistanceMeters, previous.SprintActive,
                    previous.SlideTicksRemaining, previous.SlideCooldownTicksRemaining, previous.OverlapUnresolved,
                    previous.Tightness);

            float tightness = previous.Tightness;

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
                && tightness >= MotorConfig.TightnessSlideCost
                && VecMath.Length(MotorMath.Horizontal(previous.Velocity)) >= MotorConfig.SlideMinEntrySpeed;
            if (startSlide)
            {
                slideTicks = MotorConfig.SlideDurationTicks;
                cooldown = MotorConfig.SlideCooldownTicks;
                tightness -= MotorConfig.TightnessSlideCost;
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
                && command.Sequence != previous.JumpConsumedSequence
                && tightness >= MotorConfig.TightnessJumpCost;
            if (jumpEdge)
            {
                velocity = new Float3(velocity.X, config.JumpSpeed, velocity.Z);
                jumpConsumed = command.Sequence;
                tightness -= MotorConfig.TightnessJumpCost;
            }

            // Sprint only while tightness remains after this tick's jump/slide costs.
            bool canSprint = wantSprint && tightness > 0f;
            float speed = stance == Stance.Crouching ? config.CrouchSpeed : canSprint ? config.SprintSpeed : config.WalkSpeed;
            if (wantAim) speed *= config.AdsSpeedMultiplier;
            // ADR-102: heavy weapons move slower; clamped so a bad definition cannot freeze or launch the body.
            speed *= weaponSpeedScale < 0.5f ? 0.5f : weaponSpeedScale > 1.2f ? 1.2f : weaponSpeedScale;

            Float3 wish = VecMath.Add(
                VecMath.Scale(MotorMath.YawForward(yaw), ClampAxis(command.MoveY)),
                VecMath.Scale(MotorMath.YawRight(yaw), ClampAxis(command.MoveX)));
            float wishLen = VecMath.Length(wish);
            if (wishLen > 1f) wish = VecMath.Scale(wish, 1f / wishLen);
            Float3 wishVel = VecMath.Scale(wish, speed);

            Float3 horizontal = MotorMath.Horizontal(velocity);
            if (sliding)
            {
                // Only authority supplies the entry boost; held input/turning cannot accelerate or steer a slide.
                horizontal = startSlide
                    ? VecMath.Scale(VecMath.NormalizeOrZero(horizontal), MotorConfig.SlideEntrySpeed)
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
            Float3 displacement = VecMath.Scale(velocity, MotorMath.TickSeconds);
            // A grounded actor that is not jumping follows the ground: contact projection (ramps) must not leave it
            // with upward velocity, which would suppress the ground probe and launch it off the ramp.
            bool followGround = previous.Grounded && !jumpEdge;
            // Preserve only an active jump ascent; wall/corner ProjectOnPlane must not create a sky launch.
            bool jumpAscent = jumpEdge
                || (jumpConsumed != 0 && jumpConsumed == previous.JumpConsumedSequence && previous.Velocity.Y > 0f);
            feet = SlideMove(feet, height, ref velocity, displacement, world, followGround, out bool stepped, out SurfaceId stepSurface);
            if (!jumpAscent && velocity.Y > 0f) velocity = new Float3(velocity.X, 0f, velocity.Z);

            bool ascendingJump = velocity.Y > 0f && jumpConsumed == command.Sequence && (command.Buttons & InputButtons.JumpPressed) != 0;
            bool grounded = false;
            SurfaceId groundId = default;
            Float3 groundNormal = new Float3(0, 1, 0);
            uint lastGrounded = previous.LastGroundedTick;
            if (!ascendingJump || previous.Grounded && jumpEdge == false)
            {
                // Downhill snap: the ground can fall away by up to movedHorizontal * tan(MaxSlope) in one tick.
                float movedHorizontal = VecMath.Length(MotorMath.Horizontal(VecMath.Sub(feet, previous.Feet)));
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
                if (!grounded || VecMath.Length(MotorMath.Horizontal(velocity)) < 0.1f) slideTicks = 0;
            }

            bool sprintApplied = canSprint && !sliding && stance == Stance.Standing && grounded;
            if (sprintApplied)
                tightness -= MotorConfig.TightnessSprintDrainPerTick;
            // Regen only while not holding Sprint (owner: restore by walking). Exhausted Shift stays empty until release.
            else if (grounded && !sliding && !wantSprint)
                tightness += MotorConfig.TightnessRegenPerTick;
            if (tightness < 0f) tightness = 0f;
            else if (tightness > MotorConfig.TightnessMax) tightness = MotorConfig.TightnessMax;

            float moved = VecMath.Length(MotorMath.Horizontal(VecMath.Sub(feet, previous.Feet)));
            return new MotorBodyState(
                feet, velocity, yaw, pitch, stance, height, grounded, groundId, groundNormal,
                lastGrounded, jumpConsumed, previous.LocomotionDistanceMeters + moved,
                sprintApplied, slideTicks, cooldown, false, tightness);
        }

        /// <summary>
        /// Depenetration: up to MaxOverlapRecovery passes. Each pass queries the capsule overlap and pushes the feet out along
        /// the single deepest hit (ties keep the lowest collider stable id, since the buffer is sorted by it), by depth plus
        /// skin, removing the velocity component into the push-out normal. Returns false when penetration remains after the
        /// last pass or the query is unusable; in that case the caller must stop the actor.
        /// </summary>
        private bool RecoverOverlap(MotorBodyState previous, IWorldQueries world, out Float3 feet, out Float3 velocity)
        {
            feet = previous.Feet; velocity = previous.Velocity;
            for (int pass = 0; pass <= config.MaxOverlapRecovery; pass++)
            {
                var capsule = MotorMath.BodyCapsule(feet, previous.CapsuleHeight, config.Radius);
                var result = world.Overlap(capsule, motorFilter, overlapBuffer);
                if (result.Status == QueryStatus.NoHit) return true;
                // An unusable query (e.g. the backend found a near-contact collider but no penetration) is not proof of
                // overlap; skip recovery rather than freezing a valid actor. Only measured, unresolved penetration stops it.
                if (result.Status == QueryStatus.InvalidInput) return true;
                if (result.Status != QueryStatus.Hit) return false;
                bool found = false;
                float bestDepth = OverlapEpsilon;
                Float3 bestNormal = default;
                for (int i = 0; i < result.WrittenCount; i++)
                {
                    var hit = overlapBuffer[i];
                    if (hit.DistanceMeters > bestDepth) { bestDepth = hit.DistanceMeters; bestNormal = hit.Normal; found = true; }
                }
                if (!found) return true;
                if (pass == config.MaxOverlapRecovery) return false;
                feet = VecMath.Add(feet, VecMath.Scale(bestNormal, bestDepth + config.Skin));
                float into = VecMath.Dot(velocity, bestNormal);
                if (into < 0f) velocity = VecMath.Sub(velocity, VecMath.Scale(bestNormal, into));
            }
            return false;
        }

        private static float ClampAxis(float value)
        {
            if (value > 1f) return 1f;
            if (value < -1f) return -1f;
            return value;
        }

        private static Float3 Accelerate(Float3 current, Float3 wish, float accel, float dt)
        {
            Float3 delta = VecMath.Sub(wish, current);
            float maxDelta = accel * dt;
            float len = VecMath.Length(delta);
            if (len <= maxDelta || len < 1e-8f) return wish;
            return VecMath.Add(current, VecMath.Scale(delta, maxDelta / len));
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
            // Standing up: sweep the current capsule upward by the growth delta so off-centre ledges within the radius block it.
            float grow = toHeight - fromHeight;
            var capsule = MotorMath.BodyCapsule(feet, fromHeight, config.Radius);
            var sweep = Sweep(capsule, new Float3(0, grow, 0), world);
            if (sweep.Status == QueryStatus.Hit && sweep.Hit.DistanceMeters < grow - config.Skin)
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
                var sweep = Sweep(capsule, displacement, world);
                if (sweep.Status != QueryStatus.Hit)
                {
                    feet = VecMath.Add(feet, displacement);
                    break;
                }
                var hit = sweep.Hit;
                float travel = Math.Max(0f, hit.DistanceMeters - config.Skin);
                Float3 unit = VecMath.NormalizeOrZero(displacement);
                feet = VecMath.Add(feet, VecMath.Scale(unit, travel));
                Float3 remaining = VecMath.Sub(displacement, VecMath.Scale(unit, travel));
                // Step-up: a grounded actor blocked by a non-walkable contact lifts by step height, spends only the
                // remaining horizontal travel and snaps down. The whole remaining displacement is consumed.
                float walkableY = (float)Math.Cos(config.MaxSlopeRadians);
                if (allowStep && hit.Normal.Y < walkableY
                    && TryStep(feet, height, MotorMath.Horizontal(remaining), world, out Float3 stepFeet, out SurfaceId surface))
                {
                    feet = stepFeet;
                    stepped = true; stepSurface = surface;
                    break;
                }
                float vyBefore = velocity.Y;
                displacement = MotorMath.ProjectOnPlane(remaining, hit.Normal);
                velocity = MotorMath.ProjectOnPlane(velocity, hit.Normal);
                if (hit.Normal.Y < walkableY)
                {
                    // ADR-114: the same holds for the position. A rounded top edge of an obstacle between StepHeight and
                    // Radius tall tilts the contact normal upward; projecting onto it would lift a ground-following actor
                    // a little every tick (it then falls back: the corner up/down cycle).
                    float maxRise = Math.Max(0f, remaining.Y);
                    if (allowStep && displacement.Y > maxRise)
                        displacement = new Float3(displacement.X, maxRise, displacement.Z);
                    // Non-walkable wall/corner: never convert horizontal sprint into upward launch.
                    if (velocity.Y > vyBefore)
                        velocity = new Float3(velocity.X, vyBefore, velocity.Z);
                    if (vyBefore <= 0f && velocity.Y > 0f)
                        velocity = new Float3(velocity.X, 0f, velocity.Z);
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
            var upSweep = Sweep(upCapsule, up, world);
            if (upSweep.Status == QueryStatus.Hit) return false;
            Float3 raised = VecMath.Add(feet, up);
            float hLen = VecMath.Length(horizontal);
            if (hLen <= 1e-6f) return false;
            // Measure the real obstacle height with a ray just past the capsule rim: edge contacts of a rounded
            // capsule must not let it climb obstacles taller than a step one rolled edge at a time.
            Float3 ahead = VecMath.Scale(horizontal, 1f / hLen);
            const float rayLift = 0.3f;
            var rayOrigin = new Float3(
                feet.X + ahead.X * (config.Radius + 0.02f), feet.Y + lift + rayLift, feet.Z + ahead.Z * (config.Radius + 0.02f));
            var topRay = world.Raycast(rayOrigin, new Float3(0, -1, 0), rayLift + lift + config.Snap + config.Skin, motorFilter);
            if (topRay.Status != QueryStatus.Hit || topRay.Hit.Normal.Y < Math.Cos(config.MaxSlopeRadians)) return false;
            float topY = rayOrigin.Y - topRay.Hit.DistanceMeters;
            if (topY - feet.Y > lift + 1e-4f) return false;
            // ADR-114: a step needs a surface above the one the actor stands on. At a vertical box corner the ray passes
            // beside the box and lands on the floor; that is a wall, not a step.
            if (topY <= feet.Y + MinStepRise) return false;
            var forwardCapsule = MotorMath.BodyCapsule(raised, height, config.Radius);
            var forward = Sweep(forwardCapsule, horizontal, world);
            if (forward.Status == QueryStatus.Hit && forward.Hit.DistanceMeters < hLen - config.Skin)
                return false;
            Float3 over = VecMath.Add(raised, horizontal);
            // Snap down onto the step top (or its edge): at most the lift plus the usual snap distance.
            var downCapsule = MotorMath.BodyCapsule(over, height, config.Radius);
            var down = Sweep(downCapsule, new Float3(0, -(lift + config.Snap + config.Skin), 0), world);
            if (down.Status != QueryStatus.Hit) return false;
            // Reject ceilings/near-horizontal walls; rounded step-edge normals stay accepted (≥0.1).
            if (down.Hit.Normal.Y < 0.1f) return false;
            float drop = Math.Max(0f, down.Hit.DistanceMeters - config.Skin);
            result = new Float3(over.X, over.Y - drop, over.Z);
            // Never end higher than the measured step top plus the usual skin gap.
            if (result.Y > topY + config.Skin + 1e-3f) result = new Float3(result.X, topY + config.Skin, result.Z);
            // Must actually be a rise (otherwise ordinary sliding handles it) and no higher than a step.
            float rise = result.Y - feet.Y;
            if (rise <= 1e-4f || rise > lift + 1e-4f) return false;
            surface = down.Hit.StaticSurfaceId;
            return true;
        }

        /// <summary>
        /// Capsule sweep with PhysX start contact resolved (ADR-114). A capsule that already touches a collider (gap below
        /// the backend contact offset) gets Hit at distance 0 with normal = -direction for every direction, which says
        /// nothing about where the contact is: at a box corner it looked like walkable ground under the feet and like an
        /// open step top. Re-sweep a capsule inset by Skin: no hit means the motion leaves the touch; a hit gives the real
        /// normal and, minus the inset, the real distance. Only if the inset capsule also starts in contact is the raw
        /// hit kept (genuine penetration, left to overlap recovery).
        /// </summary>
        private SweepHit Sweep(Capsule capsule, Float3 displacement, IWorldQueries world)
        {
            var sweep = world.SweepCapsule(capsule, displacement, motorFilter);
            if (sweep.Status != QueryStatus.Hit || sweep.Hit.DistanceMeters > ContactDistance) return sweep;
            float inset = config.Skin;
            if (capsule.Radius - inset <= 0.05f) return sweep;
            var inner = new Capsule(capsule.BottomSphereCenter, capsule.TopSphereCenter, capsule.Radius - inset);
            var retry = world.SweepCapsule(inner, displacement, motorFilter);
            if (retry.Status == QueryStatus.NoHit) return retry;
            if (retry.Status != QueryStatus.Hit || retry.Hit.DistanceMeters <= ContactDistance) return sweep;
            var h = retry.Hit;
            return new SweepHit(QueryStatus.Hit, new QueryHit(
                Math.Max(0f, h.DistanceMeters - inset), h.Point, h.Normal, h.EntityId, h.StaticSurfaceId,
                h.ColliderStableId, h.SurfaceTag, h.CollisionRevision));
        }

        private void ProbeGround(Float3 feet, float height, float verticalVelocity, float extraProbe, IWorldQueries world,
            out bool grounded, out SurfaceId groundId, out Float3 groundNormal, out Float3 snappedFeet)
        {
            grounded = false; groundId = default; groundNormal = new Float3(0, 1, 0); snappedFeet = feet;
            if (verticalVelocity > 0.01f) return;
            float probe = config.Snap + config.Skin + extraProbe;
            var capsule = MotorMath.BodyCapsule(feet, height, config.Radius);
            var sweep = Sweep(capsule, new Float3(0, -probe, 0), world);
            if (sweep.Status != QueryStatus.Hit) return;
            var hit = sweep.Hit;
            float walkable = (float)Math.Cos(config.MaxSlopeRadians);
            if (hit.Normal.Y < walkable)
            {
                // ADR-114: pressed against a wall or ledge face, the down sweep grazes that face first (PhysX contact
                // offset) and the floor two centimetres below was missed, so the actor flickered airborne. Look past
                // side contacts with a capsule inset by Skin; only a walkable surface under it counts.
                var inner = new Capsule(capsule.BottomSphereCenter, capsule.TopSphereCenter, capsule.Radius - config.Skin);
                var retry = world.SweepCapsule(inner, new Float3(0, -(probe + config.Skin), 0), motorFilter);
                if (retry.Status != QueryStatus.Hit || retry.Hit.Normal.Y < walkable || retry.Hit.DistanceMeters <= ContactDistance) return;
                var h = retry.Hit;
                float d = Math.Max(0f, h.DistanceMeters - config.Skin);
                if (d > probe) return;
                hit = new QueryHit(d, h.Point, h.Normal, h.EntityId, h.StaticSurfaceId, h.ColliderStableId, h.SurfaceTag, h.CollisionRevision);
            }
            grounded = true;
            groundId = hit.StaticSurfaceId;
            groundNormal = hit.Normal;
            // Never rest in contact: settle Skin above the surface so later sweeps start clear of it (0.3.5 spawn freeze).
            // Sweep() resolves PhysX start contacts to the true distance and normal (ADR-114), so this lift is at most
            // Skin and idempotent; it can no longer ratchet an actor up the side of a box one Skin per tick.
            float drop = hit.DistanceMeters - config.Skin;
            snappedFeet = new Float3(feet.X, feet.Y - drop, feet.Z);
        }
    }
}
