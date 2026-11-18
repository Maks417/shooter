#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Pose;

namespace Shooter.Simulation
{
    /// <summary>
    /// M2 combat harness authority: fixed 60 Hz motor + weapons + gameplay pose.
    /// Empty-actor mode preserves foundation smoke. Shipping composition rejects fakes via RequireReal.
    /// </summary>
    public sealed class CombatHarnessSimulation : ISimulation
    {
        private sealed class Slot { public uint Tick; public int Readers; }
        private sealed class Actor
        {
            public EntityId Id;
            public byte Team;
            public MotorBodyState Motor;
            public WeaponState[] Weapons = WeaponSimulator.CreateDefaultLoadout();
            /// <summary>ADR-107 sight mounted on the slot-0 primary (invalid for guns without mounted sights).</summary>
            public DefinitionId PrimarySight;
            /// <summary>ADR-107 Armory loadout this actor always spawns and resets with; null draws per <see cref="RandomLoadouts"/>.</summary>
            public PlayerLoadout? Pinned;
            public byte ActiveSlot;
            public byte DesiredSlot;
            public float RecoilYaw;
            public float RecoilPitch;
            public ushort Health = DamageMath.MaxHealth;
            public bool Alive = true;
            public uint LastProcessedSequence;
            public InputButtons LastButtons;
        }

        private readonly struct PendingShot
        {
            public readonly Actor Shooter;
            public readonly AcceptedShot Shot;
            public PendingShot(Actor shooter, AcceptedShot shot) { Shooter = shooter; Shot = shot; }
        }

        private readonly Slot[] snapshots = new Slot[8];
        private readonly Slot[] events = new Slot[8];
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly IWorldQueries world;
        private readonly KinematicMotor motor = new KinematicMotor();
        private readonly WeaponSimulator weapons = new WeaponSimulator();
        private readonly GrenadeSimulator grenades = new GrenadeSimulator();
        private readonly HitscanResolver hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
        private readonly GameplayPoseEvaluator pose = new GameplayPoseEvaluator();
        private readonly List<Actor> actors = new List<Actor>();
        private readonly List<CombatEvent> tickEvents = new List<CombatEvent>();
        private readonly List<AcceptedShot> acceptedShots = new List<AcceptedShot>();
        private readonly List<AcceptedGrenade> acceptedGrenades = new List<AcceptedGrenade>();
        private readonly List<GrenadeExplosion> grenadeExplosions = new List<GrenadeExplosion>();
        private readonly List<PendingShot> pendingShots = new List<PendingShot>();
        private readonly List<HitVolume> hitVolumes = new List<HitVolume>();
        private readonly ulong authoritySessionId;
        private readonly ulong shotSeed;
        private int publishedSnapshot = -1, publishedEvents = -1;
        private ulong nextEntityId = 1;
        private uint tick;
        private MatchState matchState = MatchState.Ready;
        private EventId highWater;
        private SimulationSnapshot? lastSnapshot;
        private FrozenList<CombatEvent> lastEvents;
        private Aabb? killBounds;

        /// <summary>
        /// Enables the out-of-world kill volume (see <see cref="WorldKillVolume"/>) using the map's authored bounds.
        /// Unset (default) disables the check so harness-only tests and B01 behave identically.
        /// </summary>
        public void SetWorldKillBounds(Aabb authoredBounds) { killBounds = authoredBounds; }

        /// <summary>Disables the out-of-world kill volume.</summary>
        public void ClearWorldKillBounds() { killBounds = null; }

        /// <summary>
        /// ADR-102: when set, every spawn and respawn draws a seeded random primary and sidearm. Off by
        /// default so harness tests keep the fixed default loadout.
        /// </summary>
        public bool RandomLoadouts { get; set; }

        private WeaponState[] LoadoutFor(EntityId id, uint atTick) =>
            RandomLoadouts ? WeaponLoadoutPolicy.CreateRandomLoadout(shotSeed, id, atTick) : WeaponSimulator.CreateDefaultLoadout();

        /// <summary>
        /// Issues a life's guns and primary sight: the pinned Armory loadout when set (ADR-107), otherwise the random
        /// (ADR-102) or default loadout with a seeded random or factory sight.
        /// </summary>
        private void IssueLoadout(Actor actor, uint atTick)
        {
            if (actor.Pinned != null)
            {
                actor.Weapons = WeaponLoadoutPolicy.CreateLoadout(actor.Pinned.Primary, actor.Pinned.Sidearm);
                actor.PrimarySight = actor.Pinned.PrimarySight;
                return;
            }
            actor.Weapons = LoadoutFor(actor.Id, atTick);
            DefinitionId primary = actor.Weapons[0].DefinitionId;
            actor.PrimarySight = RandomLoadouts
                ? WeaponLoadoutPolicy.RandomSight(shotSeed, actor.Id, atTick, primary)
                : SightCatalog.DefaultFor(primary);
        }

        /// <summary>
        /// ADR-107: pins <paramref name="loadout"/> (the human's Armory choice) to a live actor and reissues its guns
        /// now; every later <see cref="ResetActor"/> issues the same loadout. Null unpins (next life draws normally).
        /// </summary>
        public bool PinLoadout(EntityId entity, PlayerLoadout? loadout)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.Id.Equals(entity)) continue;
                actor.Pinned = loadout;
                if (loadout != null)
                {
                    IssueLoadout(actor, tick);
                    actor.ActiveSlot = 0;
                    actor.DesiredSlot = 0;
                }
                return true;
            }
            return false;
        }

        /// <summary>ADR-107: the sight mounted on a live actor's primary (invalid when the gun takes none).</summary>
        public bool TryGetPrimarySight(EntityId entity, out DefinitionId sight)
        {
            MainThread();
            sight = default;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Id.Equals(entity)) { sight = actors[i].PrimarySight; return true; }
            return false;
        }

        /// <summary>Weapon state of <paramref name="slot"/> for a live actor (lifecycle bookkeeping).</summary>
        public bool TryGetWeapon(EntityId entity, byte slot, out WeaponState weapon)
        {
            MainThread();
            weapon = default;
            if (slot >= WeaponSimulator.SlotCount) return false;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Id.Equals(entity)) { weapon = actors[i].Weapons[slot]; return true; }
            return false;
        }

        /// <summary>
        /// ADR-105: replaces a live actor's primary (slot 0) with a picked-up gun. When the primary is in hand
        /// the new gun is drawn (Equipping for its equip time), which also cancels any reload in progress.
        /// </summary>
        public bool ReplacePrimary(EntityId entity, DefinitionId definitionId, ushort magazine, ushort reserve)
            => ReplacePrimary(entity, definitionId, magazine, reserve, SightCatalog.DefaultFor(definitionId));

        /// <summary>ADR-105/107: as above, keeping the picked-up gun's own mounted sight.</summary>
        public bool ReplacePrimary(EntityId entity, DefinitionId definitionId, ushort magazine, ushort reserve, DefinitionId sight)
        {
            MainThread();
            var def = WeaponDefinition.FromId(definitionId);
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.Id.Equals(entity)) continue;
                if (!actor.Alive) return false;
                ushort mag = magazine > def.MagazineSize ? def.MagazineSize : magazine;
                actor.Weapons[0] = actor.ActiveSlot == 0
                    ? new WeaponState(definitionId, WeaponAction.Equipping, mag, reserve, tick, tick, unchecked(tick + def.EquipTicks), 0, false)
                    : new WeaponState(definitionId, WeaponAction.Ready, mag, reserve, tick, tick, tick, 0, false);
                actor.PrimarySight = SightCatalog.Resolve(definitionId, sight);
                return true;
            }
            return false;
        }

        /// <summary>ADR-105: sets a live actor's primary reserve (same-gun ammo pickup); every other field is kept.</summary>
        public bool SetPrimaryReserve(EntityId entity, ushort reserve)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.Id.Equals(entity)) continue;
                if (!actor.Alive) return false;
                var w = actor.Weapons[0];
                actor.Weapons[0] = new WeaponState(w.DefinitionId, w.Action, w.AmmoInMagazine, reserve,
                    w.NextAllowedFireTick, w.PhaseStartTick, w.PhaseEndTick, w.ShotIndex, w.PreviousFireHeld);
                return true;
            }
            return false;
        }

        public CombatHarnessSimulation(IWorldQueries world, ulong authoritySessionId = 1, ulong shotSeed = 460072030UL)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.authoritySessionId = authoritySessionId;
            this.shotSeed = shotSeed;
            for (int i = 0; i < 8; i++) { snapshots[i] = new Slot(); events[i] = new Slot(); }
            lastEvents = new FrozenList<CombatEvent>(Array.Empty<CombatEvent>(), ContractVersion.MaxEvents);
        }

        public EntityId SpawnActor(Float3 feet, float yaw, byte team)
        {
            MainThread();
            if (actors.Count >= ContractVersion.MaxActors)
                throw new InvalidOperationException("Actor capacity exhausted");
            var id = new EntityId(nextEntityId++);
            var spawned = new Actor
            {
                Id = id,
                Team = team,
                Motor = new MotorBodyState(feet, default, yaw, 0, Stance.Standing, 1.8f, true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false)
            };
            IssueLoadout(spawned, tick);
            actors.Add(spawned);
            matchState = MatchState.Playing;
            return id;
        }

        public int ActiveActorCount
        {
            get { MainThread(); return actors.Count; }
        }

        public bool TryGetMotorState(EntityId entity, out MotorBodyState state)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Id.Equals(entity)) { state = actors[i].Motor; return true; }
            state = default;
            return false;
        }

        /// <summary>
        /// Retires an authoritative life after its ordered death event has been consumed.
        /// Retired entities are absent from subsequent actor and hit snapshots and can never
        /// accept another command. Entity ids remain monotonic for the authority session.
        /// </summary>
        public bool RetireActor(EntityId entity)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].Id.Equals(entity)) continue;
                actors.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool ResetActor(EntityId entity, Float3 feet, float yaw)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.Id.Equals(entity)) continue;
                actor.Motor = new MotorBodyState(
                    feet,
                    default,
                    yaw,
                    0,
                    Stance.Standing,
                    1.8f,
                    true,
                    new SurfaceId(100),
                    new Float3(0, 1, 0),
                    tick,
                    0,
                    0,
                    false);
                IssueLoadout(actor, tick);
                actor.ActiveSlot = 0;
                actor.DesiredSlot = 0;
                actor.RecoilYaw = 0;
                actor.RecoilPitch = 0;
                actor.Health = DamageMath.MaxHealth;
                actor.Alive = true;
                actor.LastProcessedSequence = 0;
                actor.LastButtons = InputButtons.None;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Position rescue for a living actor embedded in geometry. Only the motor body moves (feet, yaw, zero
        /// velocity, standing, no slide); health, weapons/ammo, recoil and command sequence are preserved.
        /// Dead or unknown actors are left untouched.
        /// </summary>
        public bool RelocateActor(EntityId entity, Float3 feet, float yaw)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.Id.Equals(entity)) continue;
                if (!actor.Alive) return false;
                actor.Motor = new MotorBodyState(
                    feet, default, yaw, actor.Motor.AimPitch, Stance.Standing, motor.Config.StandingHeight, false,
                    default, new Float3(0, 1, 0), actor.Motor.LastGroundedTick,
                    actor.Motor.JumpConsumedSequence, actor.Motor.LocomotionDistanceMeters, false, 0, 0, false);
                return true;
            }
            return false;
        }

        public bool ResetCommandEpoch(EntityId entity)
        {
            MainThread();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.Id.Equals(entity)) continue;
                actor.LastProcessedSequence = 0;
                actor.LastButtons = InputButtons.None;
                return true;
            }
            return false;
        }

        private void MainThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Simulation is main-thread-only");
        }

        private static int Free(Slot[] slots)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].Readers == 0) return i;
            return -1;
        }

        public StepResult Step(uint requestedTick, ReadOnlyMemory<InputCommand> commands) =>
            Step(requestedTick, commands, default);

        /// <summary>Local-only slide intent, consumed with a fresh accepted command (ADR-039).</summary>
        public StepResult Step(uint requestedTick, ReadOnlyMemory<InputCommand> commands, EntityId localSlideActor) =>
            StepCore(requestedTick, commands, localSlideActor, null);

        /// <summary>
        /// Slide intent for any set of actors (local human and bots). Same ADR-039 semantics: a slide
        /// request only reaches the motor with a fresh accepted command for that actor this tick.
        /// </summary>
        public StepResult StepWithSlideActors(uint requestedTick, ReadOnlyMemory<InputCommand> commands, IReadOnlyList<EntityId>? slideActors) =>
            StepCore(requestedTick, commands, default, slideActors);

        private static bool SlideRequested(EntityId actor, EntityId single, IReadOnlyList<EntityId>? many)
        {
            if (single.IsValid && actor.Equals(single)) return true;
            if (many == null) return false;
            for (int i = 0; i < many.Count; i++) if (many[i].Equals(actor)) return true;
            return false;
        }

        private StepResult StepCore(uint requestedTick, ReadOnlyMemory<InputCommand> commands, EntityId localSlideActor, IReadOnlyList<EntityId>? slideActors)
        {
            MainThread();
            if (commands.Length > ContractVersion.MaxCommands) throw new ArgumentException("Command cap exceeded", nameof(commands));
            if (publishedSnapshot >= 0 && requestedTick != unchecked(tick + 1) || publishedSnapshot < 0 && requestedTick != 0)
                throw new ArgumentException("Nonconsecutive simulation tick", nameof(requestedTick));
            int snapshotSlot = Free(snapshots), eventSlot = Free(events);
            if (snapshotSlot < 0 || eventSlot < 0) throw new StepUnavailableException(ErrorCode.Busy);

            tickEvents.Clear();
            hitVolumes.Clear();
            pendingShots.Clear();
            grenadeExplosions.Clear();
            int accepted = 0, rejected = 0, shots = 0, damageEvents = 0;

            var activeById = new Dictionary<ulong, Actor>(actors.Count);
            for (int i = 0; i < actors.Count; i++) activeById.Add(actors[i].Id.Value, actors[i]);
            var chosen = new Dictionary<ulong, InputCommand>();
            var span = commands.Span;
            for (int i = 0; i < span.Length; i++)
            {
                var cmd = span[i];
                if (!cmd.IsValid ||
                    !activeById.TryGetValue(cmd.EntityId.Value, out var commandedActor) ||
                    cmd.Sequence <= commandedActor.LastProcessedSequence)
                {
                    rejected++;
                    continue;
                }
                if (!chosen.TryGetValue(cmd.EntityId.Value, out var prev) || cmd.Sequence > prev.Sequence)
                    chosen[cmd.EntityId.Value] = cmd;
                else rejected++;
            }

            EventId nextEvent = default;
            for (int a = 0; a < actors.Count; a++)
            {
                var actor = actors[a];
                if (!actor.Alive) continue;
                bool freshCommand = chosen.TryGetValue(actor.Id.Value, out var cmd);
                if (!freshCommand)
                {
                    cmd = new InputCommand(actor.Id, requestedTick, actor.LastProcessedSequence, 0, 0, actor.Motor.AimYaw, actor.Motor.AimPitch,
                        actor.LastButtons & ~(InputButtons.FirePressed | InputButtons.ReloadPressed | InputButtons.JumpPressed), actor.ActiveSlot);
                }
                else
                {
                    accepted++;
                    actor.LastProcessedSequence = cmd.Sequence;
                    actor.LastButtons = cmd.Buttons;
                }

                if (nextEvent.IsEmpty) nextEvent = new EventId(authoritySessionId, requestedTick, actor.Id, 0);
                weapons.Step(requestedTick, cmd.Buttons, cmd.SelectedSlot, actor.Alive, actor.Motor.SprintActive, actor.Weapons,
                    ref actor.ActiveSlot, ref actor.DesiredSlot, acceptedShots, acceptedGrenades, tickEvents, ref nextEvent, actor.Id);
                for (int s = 0; s < acceptedShots.Count; s++) pendingShots.Add(new PendingShot(actor, acceptedShots[s]));
                for (int g = 0; g < acceptedGrenades.Count; g++)
                    ResolveGrenadeAction(actor, acceptedGrenades[g], requestedTick, ref nextEvent);

                float moveScale = WeaponDefinition.FromId(actor.Weapons[actor.ActiveSlot].DefinitionId).MoveSpeedScale;
                actor.Motor = motor.Step(actor.Motor, cmd, requestedTick, world, freshCommand && SlideRequested(actor.Id, localSlideActor, slideActors), moveScale);
                AuthoritativeRecoil.Decay(ref actor.RecoilYaw, ref actor.RecoilPitch);
            }

            for (int a = 0; a < actors.Count; a++)
            {
                var actor = actors[a];
                if (!actor.Alive) continue;
                pose.Evaluate(actor.Id, actor.Motor.Feet, actor.Motor.AimYaw, actor.Motor.AimPitch, actor.Motor.Stance, actor.Motor.CapsuleHeight, requestedTick, hitVolumes);
            }
            var volumeSnapshot = hitVolumes.ToArray();

            for (int i = 0; i < pendingShots.Count; i++)
            {
                var pending = pendingShots[i];
                var shooter = pending.Shooter;
                var shot = pending.Shot;
                if (!shooter.Alive) continue;
                if (nextEvent.IsEmpty) nextEvent = new EventId(authoritySessionId, requestedTick, shooter.Id, 0);
                var eye = pose.EyePosition(shooter.Motor.Feet, shooter.Motor.Stance, shooter.Motor.CapsuleHeight);
                float aimYaw = shooter.Motor.AimYaw + shooter.RecoilYaw;
                float aimPitch = shooter.Motor.AimPitch + shooter.RecoilPitch;
                var aim = pose.AimDirection(aimYaw, aimPitch);
                // Knife: strike from the eye along aim so short range is measured from the player, not the rifle muzzle.
                Float3 origin = shot.Definition.FireMode == FireMode.Melee ? eye : pose.CanonicalMuzzle(eye, aimYaw, aimPitch);
                Float3 muzzleForProbe = shot.Definition.FireMode == FireMode.Melee ? eye : origin;
                var result = hitscan.Resolve(new HitscanRequest(shooter.Id, eye, muzzleForProbe, aim, shot.Definition, shot.ShotIndex, shot.Ads, shotSeed, 1), world, volumeSnapshot);
                if (result.BlockedAtMuzzle) continue;
                tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.ShotAccepted, shooter.Id, default,
                    new ShotPayload(shot.Definition.Id, shot.ShotIndex, result.Origin, result.UnitDirection, shotSeed, 1)));
                nextEvent = nextEvent.NextOrdinal();
                shots++;
                if (shot.Definition.FireMode != FireMode.Melee)
                    AuthoritativeRecoil.ApplyImpulse(shot.Definition, shot.ShotIndex, shotSeed, ref shooter.RecoilYaw, ref shooter.RecoilPitch);
                if (!result.Impacted) continue;
                ushort region = result.Impact.EntityId.IsValid ? result.Impact.SurfaceTag : (ushort)0;
                tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.Impact, shooter.Id, result.Impact.EntityId,
                    new ImpactPayload(result.Impact.Point, result.Impact.Normal, result.Impact.SurfaceTag, region)));
                nextEvent = nextEvent.NextOrdinal();
                if (!result.Impact.EntityId.IsValid) continue;
                // ADR-102 range falloff from the shot origin to the hit.
                float dx = result.Impact.Point.X - result.Origin.X, dy = result.Impact.Point.Y - result.Origin.Y, dz = result.Impact.Point.Z - result.Origin.Z;
                float meters = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                ushort baseDamage = DamageMath.FalloffDamage(shot.Definition.BaseDamage, shot.Definition.FalloffScale(meters));
                ApplyDirectDamage(shooter, result.Impact.EntityId, region, baseDamage, ref nextEvent, ref damageEvents);
            }

            // Preserve any same-tick in-hand detonations queued during weapon step, then append flight fuse expiry.
            var pendingHandBooms = grenadeExplosions.Count > 0 ? new List<GrenadeExplosion>(grenadeExplosions) : null;
            grenades.Step(requestedTick, 1f / ContractVersion.SimulationHz, world, grenadeExplosions);
            if (pendingHandBooms != null)
            {
                for (int i = 0; i < pendingHandBooms.Count; i++)
                    grenadeExplosions.Add(pendingHandBooms[i]);
            }
            for (int i = 0; i < grenadeExplosions.Count; i++)
                ResolveExplosion(grenadeExplosions[i], ref nextEvent, ref damageEvents);

            // Out-of-world kill volume: once per tick, after movement and after bullet resolution, so a bullet kill
            // earlier in this tick wins (the actor is already !Alive and skipped: no double death). The death is an
            // ordinary CombatEventKind.Death with an invalid (default) Instigator and DeathPayload(default) = no killer.
            if (killBounds.HasValue)
            {
                var bounds = killBounds.Value;
                for (int a = 0; a < actors.Count; a++)
                {
                    var victim = actors[a];
                    if (!victim.Alive || !WorldKillVolume.IsOutOfWorld(victim.Motor.Feet, bounds)) continue;
                    victim.Alive = false;
                    victim.Health = 0;
                    victim.Motor = new MotorBodyState(victim.Motor.Feet, default, victim.Motor.AimYaw, victim.Motor.AimPitch, Stance.Dead, victim.Motor.CapsuleHeight,
                        victim.Motor.Grounded, victim.Motor.GroundSurfaceId, victim.Motor.GroundNormal, victim.Motor.LastGroundedTick, victim.Motor.JumpConsumedSequence, victim.Motor.LocomotionDistanceMeters, false);
                    tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.Death, default, victim.Id, new DeathPayload(default)));
                    nextEvent = nextEvent.NextOrdinal();
                }
            }

            var entities = new List<EntitySnapshot>(actors.Count);
            for (int a = 0; a < actors.Count; a++)
            {
                var actor = actors[a];
                entities.Add(new EntitySnapshot(actor.Id, actor.Motor.Feet, actor.Motor.AimYaw, actor.Motor.Velocity,
                    actor.Alive ? actor.Motor.Stance : Stance.Dead, actor.Health, actor.Team, actor.ActiveSlot, actor.Weapons[actor.ActiveSlot],
                    actor.Motor.LocomotionDistanceMeters, actor.Motor.AimPitch, pose.PoseVersion));
            }

            if (!nextEvent.IsEmpty) highWater = nextEvent;
            lastEvents = new FrozenList<CombatEvent>(tickEvents.ToArray(), ContractVersion.MaxEvents);
            lastSnapshot = new SimulationSnapshot(requestedTick, 1, matchState, highWater, entities, hitVolumes);
            using var digestHash = SHA256.Create();
            var digest = new ContentId(digestHash.ComputeHash(Encoding.ASCII.GetBytes("m2.tick." + requestedTick + ".actors." + actors.Count + ".events." + tickEvents.Count)));

            snapshots[snapshotSlot].Tick = requestedTick;
            events[eventSlot].Tick = requestedTick;
            tick = requestedTick;
            publishedSnapshot = snapshotSlot;
            publishedEvents = eventSlot;
            return new StepResult(tick, digest, tickEvents.Count, new StepCounters(accepted, rejected, shots, damageEvents, 0, 0, false));
        }

        public SnapshotLease AcquireSnapshot()
        {
            MainThread();
            if (publishedSnapshot < 0 || lastSnapshot == null) throw new InvalidOperationException("No completed tick");
            var slot = snapshots[publishedSnapshot];
            slot.Readers++;
            return new SnapshotLease(lastSnapshot, () => { MainThread(); slot.Readers--; });
        }

        public EventLease AcquireEvents()
        {
            MainThread();
            if (publishedEvents < 0) throw new InvalidOperationException("No completed tick");
            var slot = events[publishedEvents];
            slot.Readers++;
            return new EventLease(lastEvents, () => { MainThread(); slot.Readers--; });
        }

        private void ResolveGrenadeAction(Actor thrower, AcceptedGrenade action, uint tick, ref EventId nextEvent)
        {
            if (nextEvent.IsEmpty) nextEvent = new EventId(authoritySessionId, tick, thrower.Id, 0);
            var eye = pose.EyePosition(thrower.Motor.Feet, thrower.Motor.Stance, thrower.Motor.CapsuleHeight);
            float yaw = thrower.Motor.AimYaw;
            float pitch = thrower.Motor.AimPitch;
            var aim = pose.AimDirection(yaw, pitch);
            var origin = pose.CanonicalMuzzle(eye, yaw, pitch);
            if (action.DetonateInHand || action.RemainingFuseTicks == 0)
            {
                grenadeExplosions.Add(new GrenadeExplosion(thrower.Id, thrower.Id, thrower.Team, thrower.Motor.Feet, action.Definition, action.ShotIndex));
                return;
            }

            if (!grenades.TrySpawn(thrower.Id, thrower.Team, origin, aim, action.Definition, action.RemainingFuseTicks, tick, action.ShotIndex, out var spawned))
                return;
            tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.ProjectileSpawned, thrower.Id, default,
                new ProjectilePayload(spawned.Id, origin, spawned.Velocity, action.Definition.Id)));
            nextEvent = nextEvent.NextOrdinal();
        }

        private void ResolveExplosion(GrenadeExplosion boom, ref EventId nextEvent, ref int damageEvents)
        {
            if (nextEvent.IsEmpty) nextEvent = new EventId(authoritySessionId, tick, boom.ThrowerId.IsValid ? boom.ThrowerId : new EntityId(1), 0);
            tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.Impact, boom.ThrowerId, default,
                new ImpactPayload(boom.Position, new Float3(0, 1, 0), 1, 0)));
            nextEvent = nextEvent.NextOrdinal();

            float radius = boom.Definition.BlastRadiusMeters;
            for (int a = 0; a < actors.Count; a++)
            {
                var victim = actors[a];
                if (!victim.Alive) continue;
                // Friendly damage stays zero; self-damage is allowed.
                if (victim.Team == boom.ThrowerTeam && !victim.Id.Equals(boom.ThrowerId)) continue;
                var torso = pose.EyePosition(victim.Motor.Feet, victim.Motor.Stance, victim.Motor.CapsuleHeight);
                float dx = torso.X - boom.Position.X, dy = torso.Y - boom.Position.Y, dz = torso.Z - boom.Position.Z;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dist >= radius) continue;
                ushort amount = GrenadeSimulator.BlastDamage(boom.Definition, dist);
                if (amount == 0) continue;
                DamageMath.Apply(victim.Health, amount, out ushort remaining);
                victim.Health = remaining;
                tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.DamageApplied, boom.ThrowerId, victim.Id,
                    new DamagePayload(amount, remaining, 2)));
                nextEvent = nextEvent.NextOrdinal();
                damageEvents++;
                if (remaining == 0)
                {
                    victim.Alive = false;
                    victim.Motor = new MotorBodyState(victim.Motor.Feet, default, victim.Motor.AimYaw, victim.Motor.AimPitch, Stance.Dead, victim.Motor.CapsuleHeight,
                        victim.Motor.Grounded, victim.Motor.GroundSurfaceId, victim.Motor.GroundNormal, victim.Motor.LastGroundedTick, victim.Motor.JumpConsumedSequence, victim.Motor.LocomotionDistanceMeters, false);
                    tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.Death, boom.ThrowerId, victim.Id, new DeathPayload(boom.ThrowerId)));
                    nextEvent = nextEvent.NextOrdinal();
                }
            }
        }

        private void ApplyDirectDamage(Actor shooter, EntityId targetId, ushort region, ushort baseDamage, ref EventId nextEvent, ref int damageEvents)
        {
            Actor? target = null;
            for (int a = 0; a < actors.Count; a++) if (actors[a].Id.Equals(targetId)) { target = actors[a]; break; }
            if (target == null || !target.Alive || target.Team == shooter.Team) return;
            if (region < 1 || region > 6) region = 2;
            ushort amount = DamageMath.RoundDamage(baseDamage, region);
            DamageMath.Apply(target.Health, amount, out ushort remaining);
            target.Health = remaining;
            tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.DamageApplied, shooter.Id, target.Id, new DamagePayload(amount, remaining, region)));
            nextEvent = nextEvent.NextOrdinal();
            damageEvents++;
            if (remaining == 0)
            {
                target.Alive = false;
                target.Motor = new MotorBodyState(target.Motor.Feet, default, target.Motor.AimYaw, target.Motor.AimPitch, Stance.Dead, target.Motor.CapsuleHeight,
                    target.Motor.Grounded, target.Motor.GroundSurfaceId, target.Motor.GroundNormal, target.Motor.LastGroundedTick, target.Motor.JumpConsumedSequence, target.Motor.LocomotionDistanceMeters, false);
                tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.Death, shooter.Id, target.Id, new DeathPayload(shooter.Id)));
                nextEvent = nextEvent.NextOrdinal();
            }
        }
    }
}
