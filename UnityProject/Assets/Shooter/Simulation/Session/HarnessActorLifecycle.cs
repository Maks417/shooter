#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation.AI;
using Shooter.Simulation.Combat;

namespace Shooter.Simulation.Session
{
    /// <summary>
    /// T036 reusable lifecycle adapter over CombatHarnessSimulation. Preserves harness C0 behavior;
    /// shipping GameSession rejects fake map/nav adapters separately.
    /// </summary>
    public sealed class HarnessActorLifecycle : IActorLifecycle, IActorStanceSource, Shooter.Simulation.AI.IActorLoadoutSource
    {
        private sealed class Corpse
        {
            public ActorLifecycleState State;
            public uint RetiredAtActiveTick;
        }

        public const int MaxCosmeticCorpses = 4;
        public const uint CosmeticCorpseLifetimeActiveTicks = 5u * 60u;

        private readonly CombatHarnessSimulation harness;
        private readonly Dictionary<ulong, ActorLifecycleState> states = new Dictionary<ulong, ActorLifecycleState>();
        private readonly Dictionary<ulong, (ParticipantId participant, LifeId life)> lives = new Dictionary<ulong, (ParticipantId, LifeId)>();
        private readonly Dictionary<ulong, Stance> stances = new Dictionary<ulong, Stance>();
        private readonly List<Corpse> corpses = new List<Corpse>(MaxCosmeticCorpses);
        /// <summary>ADR-107 Armory loadouts by participant, applied to every life that participant spawns.</summary>
        private readonly Dictionary<ParticipantId, PlayerLoadout> pinned = new Dictionary<ParticipantId, PlayerLoadout>();
        private uint currentActiveTick;

        public HarnessActorLifecycle(CombatHarnessSimulation harness)
        {
            this.harness = harness ?? throw new ArgumentNullException(nameof(harness));
        }

        public CombatHarnessSimulation Simulation => harness;
        public int ActiveActorCount => states.Count;
        public int CosmeticCorpseCount => corpses.Count;

        public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team)
        {
            if (states.Count >= GameContractVersion.MaxActiveActors)
                throw new InvalidOperationException("actor capacity");
            var id = harness.SpawnActor(feet, yaw, team);
            if (pinned.TryGetValue(participant, out var loadout)) harness.PinLoadout(id, loadout);
            var primary = PrimaryOf(id);
            var state = new ActorLifecycleState(id, team, true, 100, feet, yaw, 0, 0, primary.MagazineSize, ReserveOf(id, primary),
                WeaponAction.Ready, primary.Id);
            states[id.Value] = state;
            lives[id.Value] = (participant, life);
            return id;
        }

        /// <summary>
        /// ADR-107: every life <paramref name="participant"/> spawns from now on carries <paramref name="loadout"/> (the
        /// human's Armory choice); null returns it to the random/default draw. Lives already spawned keep their guns.
        /// </summary>
        public void PinLoadout(ParticipantId participant, PlayerLoadout? loadout)
        {
            if (loadout == null) pinned.Remove(participant);
            else pinned[participant] = loadout;
        }

        /// <summary>ADR-107: the sight mounted on this life's primary (invalid for guns without mounted sights).</summary>
        public bool TryGetPrimarySight(EntityId entity, out DefinitionId sight) => harness.TryGetPrimarySight(entity, out sight);

        /// <summary>ADR-102: the primary and sidearm issued to this life, for bots and presentation.</summary>
        public bool TryGetLoadout(EntityId entity, out DefinitionId primary, out DefinitionId sidearm)
        {
            primary = default; sidearm = default;
            if (!harness.TryGetWeapon(entity, 0, out var p) || !harness.TryGetWeapon(entity, 1, out var s)) return false;
            primary = p.DefinitionId; sidearm = s.DefinitionId;
            return true;
        }

        /// <summary>The slot-0 gun the harness actually issued (random per life under ADR-102).</summary>
        private WeaponDefinition PrimaryOf(EntityId id) =>
            harness.TryGetWeapon(id, 0, out var weapon) ? WeaponCatalog.RequireDefinition(weapon.DefinitionId)
                : WeaponCatalog.RequireDefinition(WeaponLoadoutPolicy.DefaultPrimaryId);

        /// <summary>The issued primary reserve (ADR-108 career magazines included), else the catalog reserve.</summary>
        private ushort ReserveOf(EntityId id, WeaponDefinition primary) =>
            harness.TryGetWeapon(id, 0, out var weapon) && weapon.DefinitionId.Equals(primary.Id) ? weapon.Reserve : primary.ReserveCapacity;

        public void DespawnLife(EntityId entity)
        {
            RetireLife(entity, currentActiveTick);
        }

        public bool RetireLife(EntityId entity, uint activeTick)
        {
            currentActiveTick = activeTick;
            if (!states.TryGetValue(entity.Value, out var state)) return false;
            states.Remove(entity.Value);
            stances.Remove(entity.Value);
            lives.Remove(entity.Value);
            harness.RetireActor(entity);
            corpses.Add(new Corpse { State = state, RetiredAtActiveTick = activeTick });
            if (corpses.Count > MaxCosmeticCorpses) corpses.RemoveAt(0);
            return true;
        }

        public void AdvanceActiveTick(uint activeTick)
        {
            currentActiveTick = activeTick;
            for (int i = corpses.Count - 1; i >= 0; i--)
            {
                if (unchecked(activeTick - corpses[i].RetiredAtActiveTick) >= CosmeticCorpseLifetimeActiveTicks)
                    corpses.RemoveAt(i);
            }
        }

        public bool TryGetCosmeticCorpse(int index, out ActorLifecycleState state)
        {
            if (index < 0 || index >= corpses.Count)
            {
                state = default;
                return false;
            }
            state = corpses[index].State;
            return true;
        }

        public void ResetLife(EntityId entity, Float3 feet, float yaw)
        {
            if (!states.TryGetValue(entity.Value, out var prev)) throw new InvalidOperationException("unknown entity");
            if (!harness.ResetActor(entity, feet, yaw)) throw new InvalidOperationException("authority entity missing");
            var primary = PrimaryOf(entity);
            states[entity.Value] = new ActorLifecycleState(entity, prev.Team, true, 100, feet, yaw, 0, 0,
                primary.MagazineSize, ReserveOf(entity, primary), WeaponAction.Ready, primary.Id);
            stances[entity.Value] = Stance.Standing;
        }

        /// <summary>
        /// Position rescue (not a respawn): moves a living actor to <paramref name="feet"/> keeping entity, life,
        /// health and ammo. Returns false for unknown or dead actors.
        /// </summary>
        public bool RelocateLife(EntityId entity, Float3 feet, float yaw)
        {
            if (!states.TryGetValue(entity.Value, out var prev) || !prev.Alive) return false;
            if (!harness.RelocateActor(entity, feet, yaw)) return false;
            states[entity.Value] = new ActorLifecycleState(
                entity, prev.Team, true, prev.Health, feet, yaw, prev.AimPitch, prev.ActiveWeaponSlot, prev.Magazine, prev.Reserve,
                prev.WeaponAction, prev.ActiveWeaponDefinition);
            stances[entity.Value] = Stance.Standing;
            return true;
        }

        public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) => states.TryGetValue(entity.Value, out state);

        /// <summary>Last synchronized authoritative stance (Standing until the first snapshot sync).</summary>
        public bool TryGetStance(EntityId entity, out Stance stance)
        {
            if (!states.ContainsKey(entity.Value)) { stance = Stance.Standing; return false; }
            if (!stances.TryGetValue(entity.Value, out stance)) stance = Stance.Standing;
            return true;
        }

        public bool ResetCommandEpoch(EntityId entity) => harness.ResetCommandEpoch(entity);

        public bool TryGetLife(EntityId entity, out ParticipantId participant, out LifeId life)
        {
            if (lives.TryGetValue(entity.Value, out var pair))
            {
                participant = pair.participant;
                life = pair.life;
                return true;
            }
            participant = default;
            life = default;
            return false;
        }

        public void SyncFromSnapshot(SimulationSnapshot snapshot)
        {
            foreach (var entity in snapshot.Entities)
            {
                if (!states.TryGetValue(entity.Id.Value, out var prev)) continue;
                stances[entity.Id.Value] = entity.Stance;
                states[entity.Id.Value] = new ActorLifecycleState(
                    entity.Id,
                    entity.Team,
                    entity.Stance != Stance.Dead && entity.Health > 0,
                    entity.Health,
                    entity.Position,
                    entity.Yaw,
                    entity.AimPitch,
                    entity.ActiveSlot,
                    entity.Weapon.AmmoInMagazine,
                    entity.Weapon.Reserve,
                    entity.Weapon.Action,
                    entity.Weapon.DefinitionId);
            }
        }
    }
}
