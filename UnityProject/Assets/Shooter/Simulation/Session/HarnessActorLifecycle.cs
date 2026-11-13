#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Simulation.Session
{
    /// <summary>
    /// T036 reusable lifecycle adapter over CombatHarnessSimulation. Preserves harness C0 behavior;
    /// shipping GameSession rejects fake map/nav adapters separately.
    /// </summary>
    public sealed class HarnessActorLifecycle : IActorLifecycle
    {
        private readonly CombatHarnessSimulation harness;
        private readonly Dictionary<ulong, ActorLifecycleState> states = new Dictionary<ulong, ActorLifecycleState>();
        private readonly Dictionary<ulong, (ParticipantId participant, LifeId life)> lives = new Dictionary<ulong, (ParticipantId, LifeId)>();

        public HarnessActorLifecycle(CombatHarnessSimulation harness)
        {
            this.harness = harness ?? throw new ArgumentNullException(nameof(harness));
        }

        public CombatHarnessSimulation Simulation => harness;
        public int ActiveActorCount => states.Count;

        public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team)
        {
            if (states.Count >= GameContractVersion.MaxActiveActors)
                throw new InvalidOperationException("actor capacity");
            var id = harness.SpawnActor(feet, yaw, team);
            var state = new ActorLifecycleState(id, team, true, 100, feet, yaw, 0, 0, 30, 90);
            states[id.Value] = state;
            lives[id.Value] = (participant, life);
            return id;
        }

        public void DespawnLife(EntityId entity)
        {
            states.Remove(entity.Value);
            lives.Remove(entity.Value);
        }

        public void ResetLife(EntityId entity, Float3 feet, float yaw)
        {
            if (!states.TryGetValue(entity.Value, out var prev)) throw new InvalidOperationException("unknown entity");
            states[entity.Value] = new ActorLifecycleState(entity, prev.Team, true, 100, feet, yaw, 0, 0, 30, 90);
        }

        public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) => states.TryGetValue(entity.Value, out state);

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
                    entity.Weapon.Reserve);
            }
        }
    }
}
