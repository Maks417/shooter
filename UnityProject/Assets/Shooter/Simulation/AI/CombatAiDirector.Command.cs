#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.AI
{
    /// <summary>Bot command building: aim, movement, strafing, loadout and fire control for one tick.</summary>
    public sealed partial class CombatAiDirector
    {
        private static int Rotate(AiBlackboard board, int count) =>
            (int)((board.Self.Value + (ulong)(uint)board.DestinationCursor) % (ulong)count);

        /// <summary>Atan2 yaw inside the C0 half-open interval [-π, π); float π and -π both fall outside it.</summary>
        internal static float ContractYaw(float dx, float dz)
        {
            float yaw = MathF.Atan2(dx, dz);
            if ((double)yaw >= Math.PI || (double)yaw < -Math.PI) yaw = -3.1415925f;
            return yaw;
        }

        /// <summary>
        /// Where a bot without a visible enemy should look: the last contact while the fight is fresh, a recent
        /// damage source, or a nearby heard/called-out stimulus (pre-aim instead of staring down the route).
        /// </summary>
        private Float3? ThreatLook(AiBlackboard board)
        {
            if (board.InCover && (board.State == AiState.SeekCover || board.State == AiState.Reload))
                return board.LastSeenEnemy ?? board.CoverThreat;
            if (board.State == AiState.SeekCover || board.State == AiState.Reload) return null; // run to cover facing the route
            // Something just hit or walked up on the bot and nothing has been seen since: face it first.
            if (board.LastHeard.HasValue && (board.Heard == HeardClass.Damage || board.Heard == HeardClass.Footsteps) &&
                unchecked(tick - board.LastHeardTick) <= AiTuning.CloseThreatTicks &&
                (board.LastSeenTick == 0 || board.LastHeardTick > board.LastSeenTick))
                return board.LastHeard;
            if (board.State == AiState.Engage && board.LastSeenEnemy.HasValue &&
                unchecked(tick - board.LastSeenTick) <= AiTuning.CombatMemoryTicks)
                return board.LastSeenEnemy;
            if (board.LastHeard.HasValue && unchecked(tick - board.LastHeardTick) <= AiTuning.ThreatLookTicks)
            {
                if (board.Heard == HeardClass.Damage) return board.LastHeard;
                if (Within(board.Feet, board.LastHeard.Value, AiTuning.ThreatLookRange)) return board.LastHeard;
            }
            if (board.LastSeenEnemy.HasValue && unchecked(tick - board.LastSeenTick) <= AiTuning.ThreatLookTicks * 2 &&
                Within(board.Feet, board.LastSeenEnemy.Value, AiTuning.ThreatLookRange))
                return board.LastSeenEnemy;
            return null;
        }

        private static bool Within(Float3 a, Float3 b, float meters)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz <= meters * meters;
        }

        // ---------------------------------------------------------------- command

        private InputCommand BuildCommand(AiBlackboard board, uint simulationTick, uint sequence)
        {
            var buttons = InputButtons.None;
            var eye = new Float3(board.Feet.X, board.Feet.Y + EyeHeight(board), board.Feet.Z);
            bool visible = board.HasVisibleTarget && board.VisibleTarget.HasValue;
            bool engaging = board.State == AiState.Engage && visible;
            Float3? movementTarget = board.PathCornerIndex < board.PathCornerCount
                ? board.PathCorners[board.PathCornerIndex]
                : (Float3?)null;

            // 1. Where to look.
            float desiredYaw = board.Yaw, desiredPitch = board.Pitch;
            bool combatTurn = false;
            Float3? threatLook = null;
            // Holds while the old target drops out of the vision cone, until the attacker is acquired or time runs out.
            bool turnToThreat = tick < board.TurnToThreatUntilTick && board.LastHeard.HasValue &&
                board.UnseenAttacker.HasValue && board.TargetId != board.UnseenAttacker;
            if (turnToThreat)
            {
                // Being shot from elsewhere: break off the current target and turn toward the attacker.
                var look = board.LastHeard!.Value;
                var lookPoint = new Float3(look.X, Math.Max(look.Y, board.Feet.Y + StandingAimHeight), look.Z);
                AimController.LookAngles(eye, lookPoint, out desiredYaw, out desiredPitch);
                combatTurn = true;
                threatLook = look;
                visible = false;
                engaging = false;
            }
            else if (visible)
            {
                AimController.IdealAngles(eye, board.VisibleTarget!.Value, out float iy, out float ip);
                AimController.ErrorOffset(board, tick, board.TargetDistance, board.TargetSpeed, out float ey, out float ep);
                desiredYaw = AimController.NormalizeYaw(iy + ey);
                desiredPitch = AimController.ClampPitch(ip + ep);
                if (board.DesiredSlot == 3)
                {
                    // ADR-111: lob the grenade on the ballistic arc instead of throwing along the sight line.
                    var grenade = Combat.WeaponDefinition.FromId(new DefinitionId("weapons/grenade"));
                    desiredPitch = AimController.ClampPitch(ep + AimController.GrenadeLaunchPitch(
                        eye, board.VisibleTarget!.Value, grenade.ThrowSpeedMetersPerSecond,
                        Combat.GrenadeSimulator.GravityY, Combat.GrenadeSimulator.LoftDegrees));
                }
                combatTurn = true;
            }
            else if ((threatLook = ThreatLook(board)).HasValue)
            {
                var look = threatLook.Value;
                // Stimulus points are feet-ish or muzzle height; look at standing chest height.
                var lookPoint = new Float3(look.X, Math.Max(look.Y, board.Feet.Y + StandingAimHeight), look.Z);
                AimController.LookAngles(eye, lookPoint, out desiredYaw, out desiredPitch);
                combatTurn = true;
            }
            else if (movementTarget.HasValue)
            {
                float dx = movementTarget.Value.X - board.Feet.X, dz = movementTarget.Value.Z - board.Feet.Z;
                if (dx * dx + dz * dz > 0.04f)
                {
                    // Without a threat, look where we walk so the forward vision cone sweeps the route; ADR-112 looks
                    // into the next leg as the corner nears so the head turns before the feet.
                    RouteDirection(board, movementTarget.Value, out float lx, out float lz);
                    if (lx != 0 || lz != 0) desiredYaw = ContractYaw(lx, lz);
                    desiredPitch = 0;
                }
            }
            AimController.Turn(board, desiredYaw, desiredPitch, combatTurn);
            board.AimOffsetRadians = AimController.AngleBetween(board.Yaw, board.Pitch, desiredYaw, desiredPitch);

            // 2. Where to move (world XZ), then into command space relative to the new aim yaw.
            float wx = 0, wz = 0;
            bool crouch = false, holdStill = false;
            board.StrafeFlippedThisTick = false;
            var range = FireDiscipline.Classify(board.TargetDistance);
            bool crouchShooter = board.Self.Value % AiTuning.CrouchShooterModulo == 0;
            int ledgeFlipsBefore = counters.StrafeLedgeFlips;
            if (engaging && board.TargetDistance < AiTuning.AdvanceRange)
            {
                // ADR-118: a shotgun never camps at range; it keeps moving instead of settling into a crouch.
                bool closeGun = IsCloseRangeGun(board);
                holdStill = !closeGun && (range == EngageRange.Long ||
                    (range == EngageRange.Mid && crouchShooter && FireDiscipline.InBurst(board, tick)));
                if (!holdStill) EngageMove(board, out wx, out wz);
                crouch = !closeGun && (range == EngageRange.Long || (range == EngageRange.Mid && crouchShooter && FireDiscipline.InBurst(board, tick)));
            }
            else if (movementTarget.HasValue)
            {
                RouteDirection(board, movementTarget.Value, out wx, out wz);
            }
            // ADR-112: ease the world move vector (strafe reversals and corner turns ramp over a few ticks instead of
            // flipping in one); a ledge flip snaps so the bot never coasts toward a drop.
            SmoothMove(board, ref wx, ref wz, counters.StrafeLedgeFlips != ledgeFlipsBefore || holdStill);
            if (!engaging && board.InCover && (board.State == AiState.SeekCover || board.State == AiState.Reload))
            {
                // ADR-112: behind low authored cover a bot rises to peek and shoot, then ducks again.
                bool peeking = board.State == AiState.SeekCover && board.CoverLow &&
                    unchecked(tick - board.InCoverSinceTick) % AiTuning.CoverPeekCycleTicks >= AiTuning.CoverPeekCycleTicks - AiTuning.CoverPeekStandTicks;
                crouch = !peeking;
            }

            float mx = 0, my = 0;
            if (wx != 0 || wz != 0)
            {
                // Move relative to aim yaw: +Y forward, +X right in command space.
                float fx = MathF.Sin(board.Yaw), fz = MathF.Cos(board.Yaw);
                float rx = fz, rz = -fx;
                mx = wx * rx + wz * rz;
                my = wx * fx + wz * fz;
                float mlen = MathF.Sqrt(mx * mx + my * my);
                // The eased vector keeps its magnitude (a reversal passes through a slow-down), capped to the disk.
                if (mlen > 1f) { mx /= mlen; my /= mlen; }
                else if (mlen <= 1e-5f) { mx = 0; my = 0; }
                // Keep strictly inside the contract unit-disk tolerance (≤1.0000001).
                double disk = (double)mx * mx + (double)my * my;
                if (disk > 0.999999) { double s = Math.Sqrt(0.999999 / disk); mx = (float)(mx * s); my = (float)(my * s); }
            }
            bool moving = mx != 0 || my != 0;
            board.CommandedMove = moving;

            // 3. Sprint on a route with no visible enemy and no threat to watch, mostly forward.
            bool travelState = board.State == AiState.Patrol || board.State == AiState.Investigate || board.State == AiState.Search
                || board.State == AiState.Reposition || board.State == AiState.SeekCover || board.State == AiState.Reload;
            bool sprint = travelState && !visible && !threatLook.HasValue && moving && my >= SprintMinForward;

            // 4. Loadout: keep guns when they still have ammo; otherwise knife / one grenade (T087).
            UpdateLoadout(board, visible);
            byte slot = board.DesiredSlot;

            // 5. Trigger discipline: bursts only when aligned, never while sprinting or reloading.
            bool canFire = visible && tick >= board.ReactionReadyTick && board.State != AiState.Reload && !sprint;
            bool fire = false;
            if (slot == 3)
            {
                // Cook briefly then release; one grenade per life.
                if (board.GrenadeRemaining > 0 && board.MagAmmo > 0 && visible
                    && board.TargetDistance >= AiTuning.GrenadeMinMeters
                    && board.TargetDistance <= AiTuning.GrenadeMaxMeters)
                {
                    if (!board.CookingGrenade)
                    {
                        board.CookingGrenade = true;
                        board.GrenadeCookStartTick = tick;
                        buttons |= InputButtons.FirePressed | InputButtons.FireHeld;
                        fire = true;
                    }
                    else if (unchecked(tick - board.GrenadeCookStartTick) < AiTuning.GrenadeCookTicks)
                    {
                        buttons |= InputButtons.FireHeld;
                        fire = true;
                    }
                    else
                    {
                        // Release this tick to throw; mark spent so we don't re-cook an empty slot.
                        board.CookingGrenade = false;
                        board.GrenadeRemaining = 0;
                        fire = true;
                    }
                }
                else
                {
                    board.CookingGrenade = false;
                    FireDiscipline.Cease(board, tick);
                }
            }
            else if (slot == 2)
            {
                // Knife: press-fire when inside melee range (ammo never depletes).
                if (canFire && board.TargetDistance <= AiTuning.KnifeEngageMeters)
                {
                    float tol = AimController.FireTolerance(board.TargetDistance);
                    fire = board.AimOffsetRadians <= tol * 2f;
                    if (fire)
                    {
                        buttons |= InputButtons.FirePressed | InputButtons.FireHeld;
                        counters.ShotsRequested++;
                    }
                }
                else FireDiscipline.Cease(board, tick);
            }
            else if (canFire && board.MagAmmo > 0 && !OutOfShotgunRange(board))
            {
                float tol = AimController.FireTolerance(board.TargetDistance);
                uint interval = ActiveGunFireInterval(board);
                // Precision guns only fire on settled aim; there is no follow-up spray to correct a miss.
                bool precision = IsPrecisionGun(board);
                fire = FireDiscipline.Update(board, tick, board.TargetDistance,
                    board.AimOffsetRadians <= (precision ? tol * 0.6f : tol), board.AimOffsetRadians <= (precision ? tol : tol * 2.5f), interval);
                if (fire && board.BurstStartTick == tick) counters.BurstsStarted++;
                if (fire)
                {
                    buttons |= InputButtons.FireHeld | InputButtons.FirePressed;
                    counters.ShotsRequested++;
                }
            }
            else FireDiscipline.Cease(board, tick);

            if (board.State == AiState.Reload && slot < 2) buttons |= InputButtons.ReloadPressed;
            if (engaging && holdStill && range == EngageRange.Long && slot < 2) buttons |= InputButtons.AimHeld;
            if (engaging && holdStill && range != EngageRange.Close && IsPrecisionGun(board)) buttons |= InputButtons.AimHeld;

            board.SlideIntent = false;
            if (sprint)
            {
                buttons |= InputButtons.SprintHeld;
                board.SprintTicks++;
            }
            else board.SprintTicks = 0;

            if (crouch) buttons |= InputButtons.CrouchHeld;

            // 6. Jump (edge) when stuck: only on the tick UpdateStuck scheduled it.
            bool firingOrReloading = (buttons & (InputButtons.FireHeld | InputButtons.FirePressed | InputButtons.ReloadPressed)) != 0;
            if (board.JumpPending)
            {
                if (!engaging && !firingOrReloading) buttons |= InputButtons.JumpPressed;
                board.JumpPending = false;
            }
            // ADR-100 traversal hop: a low obstacle (above the motor step, below jump clearance) right ahead on the route.
            if (!engaging && !firingOrReloading && moving && !crouch && tick >= board.NextJumpTick
                && (buttons & InputButtons.JumpPressed) == 0
                && unchecked(tick + (uint)board.Self.Value) % AiTuning.VaultProbeIntervalTicks == 0
                && LowObstacleAhead(board, wx, wz))
            {
                buttons |= InputButtons.JumpPressed;
                board.NextJumpTick = tick + JumpCooldownTicks;
                counters.TraversalJumps++;
            }
            // ADR-100 combat hop: sometimes hop as the strafe reverses, never mid-burst, reloading or crouch-shooting.
            if (engaging && board.StrafeFlippedThisTick && !holdStill && !crouch && !firingOrReloading
                && range != EngageRange.Long && !FireDiscipline.InBurst(board, tick)
                && tick >= board.NextHopTick && tick >= board.NextJumpTick
                && AiNoise.Unit(board.Self.Value, tick, AiNoise.SaltHop) < AiTuning.CombatHopChance)
            {
                buttons |= InputButtons.JumpPressed;
                board.NextHopTick = tick + AiTuning.CombatHopCooldownTicks;
                board.NextJumpTick = tick + JumpCooldownTicks;
                counters.CombatHops++;
            }

            // 7. Occasional slide when sprinting through open travel with a long straight leg left (ADR-100 widened).
            if (sprint && (buttons & InputButtons.JumpPressed) == 0
                && (board.State == AiState.SeekCover || board.State == AiState.Reposition
                    || board.State == AiState.Investigate || board.State == AiState.Search)
                && board.SprintTicks >= SlideMinSprintTicks
                && board.SpeedMps >= SlideRequestSpeed
                && tick >= board.NextSlideTick
                && StraightMetersToCorner(board) >= SlideMinStraightMeters)
            {
                board.SlideIntent = true;
                board.NextSlideTick = tick + SlideBotCooldownBaseTicks + (uint)(board.Self.Value % 5) * SlideBotCooldownSpreadTicks;
            }
            return new InputCommand(board.Self, simulationTick, sequence, mx, my, board.Yaw, board.Pitch, buttons, slot);
        }

        /// <summary>The gun in the commanded slot: the life's own primary or sidearm (ADR-102).</summary>
        private static Combat.WeaponDefinition ActiveGun(AiBlackboard board) =>
            Combat.WeaponCatalog.TryGet(board.DesiredSlot == 1 ? board.SidearmWeapon : board.PrimaryWeapon, out var entry)
                ? entry.Definition
                : Combat.WeaponCatalog.RequireDefinition(board.DesiredSlot == 1 ? Combat.WeaponLoadoutPolicy.PistolId : Combat.WeaponLoadoutPolicy.DefaultPrimaryId);

        private static int ActiveMagazineSize(AiBlackboard board) => ActiveGun(board).MagazineSize;

        private static int TacticalReloadThreshold(AiBlackboard board) =>
            Math.Max(1, ActiveMagazineSize(board) * 2 / 5);

        private static uint ActiveGunFireInterval(AiBlackboard board)
        {
            var gun = ActiveGun(board);
            uint interval = gun.FireMode == Combat.FireMode.Burst
                ? gun.BurstIntervalTicks * (uint)Math.Max(0, gun.BurstCount - 1) + gun.FireIntervalTicks
                : gun.FireIntervalTicks;
            return interval == 0 ? 1u : interval;
        }

        /// <summary>ADR-118 multi-pellet primary (the M870): only worth firing inside its falloff range.</summary>
        private static bool IsCloseRangeGun(AiBlackboard board) => board.DesiredSlot == 0 && ActiveGun(board).PelletCount > 1;

        /// <summary>True when a shotgun-armed bot's target is past the end of the pellet falloff (no point firing).</summary>
        private static bool OutOfShotgunRange(AiBlackboard board)
        {
            if (!IsCloseRangeGun(board)) return false;
            var gun = ActiveGun(board);
            float reach = gun.FalloffEndMeters > 0f ? gun.FalloffEndMeters : gun.MaxRangeMeters;
            return board.TargetDistance > reach;
        }

        /// <summary>Marksman and sniper guns: bots aim down sights from medium range and wait for settled aim.</summary>
        private static bool IsPrecisionGun(AiBlackboard board)
        {
            var gun = ActiveGun(board);
            return gun.FireMode == Combat.FireMode.SemiAutomatic && board.DesiredSlot == 0 && gun.AdsSpreadRadians < 0.1f * (float)Math.PI / 180f;
        }

        private static void UpdateLoadout(AiBlackboard board, bool visible)
        {
            if (board.DesiredSlot == 0 && board.MagAmmo <= 0 && board.ReserveAmmo <= 0)
                board.RifleEmpty = true;
            if (board.DesiredSlot == 1 && board.MagAmmo <= 0 && board.ReserveAmmo <= 0)
                board.PistolEmpty = true;

            bool gunsDry = board.RifleEmpty && board.PistolEmpty;
            if (!gunsDry)
            {
                if (board.DesiredSlot == 0 && board.RifleEmpty) board.DesiredSlot = 1;
                else if (board.DesiredSlot > 1) board.DesiredSlot = board.RifleEmpty ? (byte)1 : (byte)0;
                return;
            }

            // Guns empty: knife up close, otherwise spend the single grenade once if the geometry is right.
            if (visible && board.TargetDistance <= AiTuning.KnifeEngageMeters)
            {
                board.DesiredSlot = 2;
                board.CookingGrenade = false;
                return;
            }
            if (board.GrenadeRemaining > 0 && visible
                && board.TargetDistance >= AiTuning.GrenadeMinMeters
                && board.TargetDistance <= AiTuning.GrenadeMaxMeters)
            {
                board.DesiredSlot = 3;
                return;
            }
            board.DesiredSlot = 2;
            board.CookingGrenade = false;
        }

        /// <summary>
        /// ADR-112: unit world direction along the route. Inside <see cref="AiTuning.CornerBlendMeters"/> of the
        /// current corner it bends toward the following leg, so corners become short arcs instead of hard turns.
        /// ADR-114: the bend is taken only when the bent line to the blended point is clear of collision for the
        /// bot's body width; otherwise the bot follows the leg to the corner (never shaves a wall corner).
        /// </summary>
        private void RouteDirection(AiBlackboard board, Float3 corner, out float wx, out float wz)
        {
            wx = 0; wz = 0;
            float dx = corner.X - board.Feet.X, dz = corner.Z - board.Feet.Z;
            float len = MathF.Sqrt(dx * dx + dz * dz);
            if (len <= 0.2f) return;
            wx = dx / len; wz = dz / len;
            int next = board.PathCornerIndex + 1;
            if (len >= AiTuning.CornerBlendMeters || next >= board.PathCornerCount) return;
            var after = board.PathCorners[next];
            float nx = after.X - corner.X, nz = after.Z - corner.Z;
            float nlen = MathF.Sqrt(nx * nx + nz * nz);
            if (nlen < 0.05f) return;
            nx /= nlen; nz /= nlen;
            float t = 0.5f * (1f - len / AiTuning.CornerBlendMeters);
            float bx = wx + (nx - wx) * t, bz = wz + (nz - wz) * t;
            float blen = MathF.Sqrt(bx * bx + bz * bz);
            if (blen <= 1e-4f) return;
            bx /= blen; bz /= blen;
            if (!CornerBendClear(board, bx, bz, len)) return;
            wx = bx; wz = bz;
        }

        /// <summary>
        /// ADR-114: three horizontal collision rays (centre and both body edges, at knee height above the step) from
        /// the feet along the bent direction, as far as the blended point plus the body radius.
        /// </summary>
        private bool CornerBendClear(AiBlackboard board, float bx, float bz, float len)
        {
            if (bendCacheTick == tick && bendCacheSelf == board.Self.Value && bendCacheCorner == board.PathCornerIndex)
                return bendCacheClear;
            var filter = new QueryFilter(visionMask, board.Self, false);
            var dir = new Float3(bx, 0f, bz);
            float y = board.Feet.Y + AiTuning.CornerBendProbeHeightMeters;
            float r = AiTuning.CornerBendProbeRadiusMeters;
            float reach = len + r;
            bool clear = true;
            for (int side = -1; side <= 1 && clear; side++)
            {
                // Perpendicular offset in XZ: (bz, -bx) is the right-hand side of the bent direction.
                var origin = new Float3(board.Feet.X + bz * r * side, y, board.Feet.Z - bx * r * side);
                clear = world.Raycast(origin, dir, reach, filter).Status != QueryStatus.Hit;
            }
            bendCacheTick = tick; bendCacheSelf = board.Self.Value; bendCacheCorner = board.PathCornerIndex; bendCacheClear = clear;
            return clear;
        }

        private static void SmoothMove(AiBlackboard board, ref float wx, ref float wz, bool snap)
        {
            // Starting and stopping are left to the motor's own acceleration; only a change of direction between two
            // moving ticks is eased (strafe reversals pass through a short slow-down, corners bend).
            bool fromStop = board.SmoothMoveX * board.SmoothMoveX + board.SmoothMoveZ * board.SmoothMoveZ < 0.0025f;
            if (snap || fromStop || (wx == 0 && wz == 0))
            {
                board.SmoothMoveX = wx; board.SmoothMoveZ = wz;
                return;
            }
            float a = AiTuning.MoveEaseAlpha;
            float sx = board.SmoothMoveX + (wx - board.SmoothMoveX) * a;
            float sz = board.SmoothMoveZ + (wz - board.SmoothMoveZ) * a;
            board.SmoothMoveX = sx; board.SmoothMoveZ = sz;
            wx = sx; wz = sz;
        }

        /// <summary>
        /// Strafe across the line of fire around the nav point where the fight started (bounded offset keeps the
        /// bot near walkable ground), reversing on a seeded timer, at the leash, when blocked, or when the next
        /// strafe step lacks footing (T090 ledge probe); back off when the enemy is too close.
        /// </summary>
        private void EngageMove(AiBlackboard board, out float wx, out float wz)
        {
            wx = 0; wz = 0;
            var target = board.VisibleTarget!.Value;
            float tx = target.X - board.Feet.X, tz = target.Z - board.Feet.Z;
            float tlen = MathF.Sqrt(tx * tx + tz * tz);
            if (tlen < 1e-3f) return;
            tx /= tlen; tz /= tlen;
            float rx = tz, rz = -tx; // right of the line of fire
            if (!board.HasStrafeCenter)
            {
                board.HasStrafeCenter = true;
                board.StrafeCenter = board.Feet;
                board.StrafeCheckFeet = board.Feet;
                board.StrafeCheckTick = tick;
                board.StrafeFlipTick = tick + NextStrafeInterval(board);
            }
            bool flip = tick >= board.StrafeFlipTick;
            if (unchecked(tick - board.StrafeCheckTick) >= AiTuning.StrafeBlockedCheckTicks)
            {
                float bx = board.Feet.X - board.StrafeCheckFeet.X, bz = board.Feet.Z - board.StrafeCheckFeet.Z;
                if (bx * bx + bz * bz < AiTuning.StrafeBlockedMeters * AiTuning.StrafeBlockedMeters) flip = true;
                board.StrafeCheckFeet = board.Feet;
                board.StrafeCheckTick = tick;
            }
            float offset = (board.Feet.X - board.StrafeCenter.X) * rx + (board.Feet.Z - board.StrafeCenter.Z) * rz;
            if (offset * board.StrafeDir > AiTuning.StrafeMaxOffset) flip = true;
            if (!StrafeFootingSafe(board, rx, rz, board.StrafeDir))
            {
                flip = true;
                counters.StrafeLedgeFlips++;
            }
            if (flip)
            {
                board.StrafeDir = -board.StrafeDir;
                board.StrafeFlipTick = tick + NextStrafeInterval(board);
                board.StrafeFlippedThisTick = true;
                // If the other side is also unsafe, cancel lateral strafe this tick (back-off may still apply).
                if (!StrafeFootingSafe(board, rx, rz, board.StrafeDir))
                {
                    board.StrafeFlippedThisTick = false;
                    wx = 0; wz = 0;
                    if (board.TargetDistance < AiTuning.BackOffRange)
                    {
                        wx = -tx; wz = -tz;
                    }
                    return;
                }
            }
            wx = rx * board.StrafeDir;
            wz = rz * board.StrafeDir;
            if (board.TargetDistance < AiTuning.BackOffRange)
            {
                wx -= tx; wz -= tz;
                float len = MathF.Sqrt(wx * wx + wz * wz);
                wx /= len; wz /= len;
            }
        }

        /// <summary>
        /// T090: sample ground under the next strafe step with a downward collision ray (not the vision budget).
        /// Open/diagnostic worlds with no ground under the bot or the probe fail-open as safe.
        /// </summary>
        private bool StrafeFootingSafe(AiBlackboard board, float rx, float rz, int dir)
        {
            float step = AiTuning.StrafeProbeMeters;
            var here = board.Feet;
            var probe = new Float3(here.X + rx * dir * step, here.Y, here.Z + rz * dir * step);
            bool hereHit = TryGroundY(here, board.Self, out float hereY);
            bool probeHit = TryGroundY(probe, board.Self, out float probeY);
            if (!hereHit && !probeHit) return true;
            if (!probeHit) return false;
            if (!hereHit) return probeY >= here.Y - AiTuning.StrafeMaxDropMeters;
            return hereY - probeY <= AiTuning.StrafeMaxDropMeters;
        }

        private bool TryGroundY(Float3 feet, EntityId self, out float groundY)
        {
            var origin = new Float3(feet.X, feet.Y + AiTuning.StrafeGroundLiftMeters, feet.Z);
            float max = AiTuning.StrafeMaxDropMeters + AiTuning.StrafeGroundLiftMeters + 0.5f;
            var hit = world.Raycast(origin, new Float3(0f, -1f, 0f), max, new QueryFilter(visionMask, self, false));
            if (hit.Status != QueryStatus.Hit)
            {
                groundY = feet.Y;
                return false;
            }
            groundY = hit.Hit.Point.Y;
            return true;
        }

        /// <summary>
        /// ADR-100: two horizontal collision rays along the move direction. Hits at knee height (above the motor step)
        /// with clear space at hop clearance height mean a low obstacle the motor can only cross with a jump.
        /// </summary>
        private bool LowObstacleAhead(AiBlackboard board, float wx, float wz)
        {
            float len = MathF.Sqrt(wx * wx + wz * wz);
            if (len < 1e-4f) return false;
            var dir = new Float3(wx / len, 0f, wz / len);
            var filter = new QueryFilter(visionMask, board.Self, false);
            var feet = board.Feet;
            var knee = world.Raycast(new Float3(feet.X, feet.Y + AiTuning.VaultProbeHeightMeters, feet.Z), dir, AiTuning.VaultProbeMeters, filter);
            if (knee.Status != QueryStatus.Hit) return false;
            var clear = world.Raycast(new Float3(feet.X, feet.Y + AiTuning.VaultClearHeightMeters, feet.Z), dir, AiTuning.VaultProbeMeters + 0.3f, filter);
            return clear.Status != QueryStatus.Hit;
        }

        private uint NextStrafeInterval(AiBlackboard board) =>
            AiTuning.StrafeFlipMinTicks + AiNoise.Range(board.Self.Value, tick, AiNoise.SaltStrafe, AiTuning.StrafeFlipSpreadTicks);
    }
}
