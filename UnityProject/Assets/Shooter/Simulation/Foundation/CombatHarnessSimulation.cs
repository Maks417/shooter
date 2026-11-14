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
            public WeaponState[] Weapons = new WeaponState[2];
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
        private readonly HitscanResolver hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
        private readonly GameplayPoseEvaluator pose = new GameplayPoseEvaluator();
        private readonly List<Actor> actors = new List<Actor>();
        private readonly List<CombatEvent> tickEvents = new List<CombatEvent>();
        private readonly List<AcceptedShot> acceptedShots = new List<AcceptedShot>();
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
            actors.Add(new Actor
            {
                Id = id,
                Team = team,
                Motor = new MotorBodyState(feet, default, yaw, 0, Stance.Standing, 1.8f, true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false),
                Weapons = new[] { WeaponSimulator.CreateLoaded(WeaponDefinition.Rifle), WeaponSimulator.CreateLoaded(WeaponDefinition.Pistol) }
            });
            matchState = MatchState.Playing;
            return id;
        }

        public int ActiveActorCount
        {
            get { MainThread(); return actors.Count; }
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
                actor.Weapons = new[]
                {
                    WeaponSimulator.CreateLoaded(WeaponDefinition.Rifle),
                    WeaponSimulator.CreateLoaded(WeaponDefinition.Pistol)
                };
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

        public StepResult Step(uint requestedTick, ReadOnlyMemory<InputCommand> commands)
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
                if (!chosen.TryGetValue(actor.Id.Value, out var cmd))
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
                    ref actor.ActiveSlot, ref actor.DesiredSlot, acceptedShots, tickEvents, ref nextEvent, actor.Id);
                for (int s = 0; s < acceptedShots.Count; s++) pendingShots.Add(new PendingShot(actor, acceptedShots[s]));

                actor.Motor = motor.Step(actor.Motor, cmd, requestedTick, world);
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
                var muzzle = pose.CanonicalMuzzle(eye, aimYaw, aimPitch);
                var result = hitscan.Resolve(new HitscanRequest(shooter.Id, eye, muzzle, aim, shot.Definition, shot.ShotIndex, shot.Ads, shotSeed, 1), world, volumeSnapshot);
                if (result.BlockedAtMuzzle) continue;
                tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.ShotAccepted, shooter.Id, default,
                    new ShotPayload(shot.Definition.Id, shot.ShotIndex, result.Origin, result.UnitDirection, shotSeed, 1)));
                nextEvent = nextEvent.NextOrdinal();
                shots++;
                AuthoritativeRecoil.ApplyImpulse(shot.Definition, shot.ShotIndex, shotSeed, ref shooter.RecoilYaw, ref shooter.RecoilPitch);
                if (!result.Impacted) continue;
                ushort region = result.Impact.EntityId.IsValid ? result.Impact.SurfaceTag : (ushort)0;
                tickEvents.Add(new CombatEvent(nextEvent, CombatEventKind.Impact, shooter.Id, result.Impact.EntityId,
                    new ImpactPayload(result.Impact.Point, result.Impact.Normal, result.Impact.SurfaceTag, region)));
                nextEvent = nextEvent.NextOrdinal();
                if (!result.Impact.EntityId.IsValid) continue;
                Actor? target = null;
                for (int a = 0; a < actors.Count; a++) if (actors[a].Id.Equals(result.Impact.EntityId)) { target = actors[a]; break; }
                if (target == null || !target.Alive || target.Team == shooter.Team) continue;
                if (region < 1 || region > 6) region = 2;
                ushort amount = DamageMath.RoundDamage(shot.Definition.BaseDamage, region);
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
    }
}
