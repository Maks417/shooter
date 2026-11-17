#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Read-only presentation of selected weapon slot and combat action phases from authority
    /// lifecycle + combat events. Never infers reload from magazine decrease; never mutates authority.
    /// </summary>
    public sealed class ProductAuthorityActionView
    {
        public const byte RifleSlot = 0;
        public const byte PistolSlot = 1;
        public const byte KnifeSlot = 2;
        public const byte GrenadeSlot = 3;
        public const byte MaxSlot = 3;
        public const float SimulationHz = ContractVersion.SimulationHz;

        private sealed class EntityAction
        {
            public byte Slot;
            public bool Reloading;
            public uint ReloadStartTick;
            public uint ReloadEndTick;
            public float ReloadPhase = -1f;
            public float ReloadDurationSeconds;
            public bool FireImpulse;
            public bool HasSlot;
        }

        private readonly Dictionary<ulong, EntityAction> entities = new Dictionary<ulong, EntityAction>(16);
        private readonly HashSet<string> consumedEvents = new HashSet<string>(StringComparer.Ordinal);

        public int TrackedEntityCount => entities.Count;
        public int ConsumedEventCount => consumedEvents.Count;

        public void Clear()
        {
            entities.Clear();
            consumedEvents.Clear();
        }

        public void Forget(EntityId entity)
        {
            if (!entity.IsValid) return;
            entities.Remove(entity.Value);
        }

        /// <summary>Mirror authoritative ActiveWeaponSlot; clears action phases on death.</summary>
        public void SyncLifecycle(EntityId entity, byte activeWeaponSlot, bool alive)
        {
            if (!entity.IsValid) throw new ArgumentException("Entity required.", nameof(entity));
            if (activeWeaponSlot > MaxSlot)
                throw new ArgumentOutOfRangeException(nameof(activeWeaponSlot), "Weapon slot must be 0..3 (rifle/pistol/knife/grenade).");
            var state = GetOrCreate(entity.Value);
            // The weapon simulator cancels a reload on a weapon switch without emitting ReloadCompleted.
            if (state.Reloading && state.HasSlot && state.Slot != activeWeaponSlot)
            {
                state.Reloading = false;
                state.ReloadPhase = -1f;
            }
            state.Slot = activeWeaponSlot;
            state.HasSlot = true;
            if (!alive)
            {
                state.Reloading = false;
                state.ReloadPhase = -1f;
                state.FireImpulse = false;
            }
        }

        /// <summary>
        /// Level check against authority after events are applied: once the reload window has elapsed and the
        /// authoritative weapon is no longer Reloading, a missing ReloadCompleted must not leave the reload pose or
        /// the "Reloading" label stuck on.
        /// </summary>
        public void ReconcileReload(EntityId entity, WeaponAction authorityAction, uint completedTick)
        {
            if (!entity.IsValid || !entities.TryGetValue(entity.Value, out var state) || !state.Reloading) return;
            if (authorityAction == WeaponAction.Reloading) return;
            uint span = Serial32.ForwardDistance(state.ReloadEndTick, state.ReloadStartTick);
            if (Serial32.ForwardDistance(completedTick, state.ReloadStartTick) < span) return;
            state.Reloading = false;
            state.ReloadPhase = -1f;
        }

        /// <summary>
        /// Apply combat events once (EventId dedupe). ReloadStarted/Completed use ActionPayload
        /// start/end ticks for slot-correct duration; ShotAccepted raises a one-shot fire impulse.
        /// </summary>
        public void ApplyEvents(IReadOnlyList<CombatEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i] ?? throw new ArgumentException("Null combat event.", nameof(events));
                string key = ev.Id.AuthoritySessionId + ":" + ev.Id.Tick + ":" + ev.Id.EntityId + ":" + ev.Id.Ordinal;
                if (!consumedEvents.Add(key)) continue;

                if (ev.Kind == CombatEventKind.ReloadStarted)
                {
                    if (ev.Payload is not ActionPayload action) continue;
                    var state = GetOrCreate(ev.Instigator.Value);
                    state.Slot = action.Slot;
                    state.HasSlot = true;
                    state.Reloading = true;
                    state.ReloadStartTick = action.StartTick;
                    state.ReloadEndTick = action.EndTick;
                    state.ReloadDurationSeconds = DurationSeconds(action.StartTick, action.EndTick);
                    state.ReloadPhase = 0.01f;
                }
                else if (ev.Kind == CombatEventKind.ReloadCompleted)
                {
                    if (!entities.TryGetValue(ev.Instigator.Value, out var state)) continue;
                    state.Reloading = false;
                    state.ReloadPhase = -1f;
                    if (ev.Payload is ActionPayload action)
                    {
                        state.Slot = action.Slot;
                        state.HasSlot = true;
                    }
                }
                else if (ev.Kind == CombatEventKind.ShotAccepted)
                {
                    var state = GetOrCreate(ev.Instigator.Value);
                    state.FireImpulse = true;
                }
                else if (ev.Kind == CombatEventKind.Death)
                {
                    if (!entities.TryGetValue(ev.Target.Value, out var state)) continue;
                    state.Reloading = false;
                    state.ReloadPhase = -1f;
                    state.FireImpulse = false;
                }
            }
        }

        public float TickReload(EntityId entity, float dtSeconds)
        {
            if (!entity.IsValid || !entities.TryGetValue(entity.Value, out var state) || !state.Reloading)
                return -1f;
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(dtSeconds));
            float duration = state.ReloadDurationSeconds > 1e-4f
                ? state.ReloadDurationSeconds
                : DurationSeconds(state.ReloadStartTick, state.ReloadEndTick);
            state.ReloadPhase = Math.Min(1f, Math.Max(state.ReloadPhase, 0.01f) + (duration > 1e-4f ? dtSeconds / duration : 1f));
            return state.ReloadPhase;
        }

        public float ReloadPhaseAtTick(EntityId entity, uint completedTick)
        {
            if (!entity.IsValid || !entities.TryGetValue(entity.Value, out var state) || !state.Reloading)
                return -1f;
            uint span = Serial32.ForwardDistance(state.ReloadEndTick, state.ReloadStartTick);
            if (span == 0) return 1f;
            uint elapsed = Serial32.ForwardDistance(completedTick, state.ReloadStartTick);
            if (elapsed >= span) return 1f;
            float phase = elapsed / (float)span;
            return phase < 0.01f ? 0.01f : phase;
        }

        public float CurrentReloadPhase(EntityId entity)
        {
            if (!entity.IsValid || !entities.TryGetValue(entity.Value, out var state) || !state.Reloading)
                return -1f;
            return state.ReloadPhase;
        }

        public float ResolveReloadPhase(EntityId entity, uint completedTick, float dtSeconds)
        {
            if (!entity.IsValid || !entities.TryGetValue(entity.Value, out var state) || !state.Reloading)
                return -1f;
            float fromTicks = ReloadPhaseAtTick(entity, completedTick);
            float fromDt = TickReload(entity, dtSeconds);
            return Math.Max(fromTicks, fromDt);
        }

        public byte ActiveWeaponSlot(EntityId entity, byte fallbackSlot = RifleSlot)
        {
            if (entity.IsValid && entities.TryGetValue(entity.Value, out var state) && state.HasSlot)
                return state.Slot;
            return fallbackSlot;
        }

        public bool IsRifle(EntityId entity, byte fallbackSlot = RifleSlot) =>
            ActiveWeaponSlot(entity, fallbackSlot) == RifleSlot;

        public bool IsPistol(EntityId entity, byte fallbackSlot = RifleSlot) =>
            ActiveWeaponSlot(entity, fallbackSlot) == PistolSlot;

        public bool IsKnife(EntityId entity, byte fallbackSlot = RifleSlot) =>
            ActiveWeaponSlot(entity, fallbackSlot) == KnifeSlot;

        public bool IsGrenade(EntityId entity, byte fallbackSlot = RifleSlot) =>
            ActiveWeaponSlot(entity, fallbackSlot) == GrenadeSlot;

        public bool IsReloading(EntityId entity) =>
            entity.IsValid && entities.TryGetValue(entity.Value, out var state) && state.Reloading;

        public bool ConsumeFireImpulse(EntityId entity)
        {
            if (!entity.IsValid || !entities.TryGetValue(entity.Value, out var state) || !state.FireImpulse)
                return false;
            state.FireImpulse = false;
            return true;
        }

        public static bool IsRifleSlot(byte slot) => slot == RifleSlot;
        public static bool IsPistolSlot(byte slot) => slot == PistolSlot;
        public static bool IsKnifeSlot(byte slot) => slot == KnifeSlot;
        public static bool IsGrenadeSlot(byte slot) => slot == GrenadeSlot;
        public static bool IsGunSlot(byte slot) => slot == RifleSlot || slot == PistolSlot;

        public static float DurationSeconds(uint startTick, uint endTick)
        {
            uint span = Serial32.ForwardDistance(endTick, startTick);
            if (span == 0) return 1f / SimulationHz;
            return span / SimulationHz;
        }

        private EntityAction GetOrCreate(ulong id)
        {
            if (!entities.TryGetValue(id, out var state))
            {
                state = new EntityAction();
                entities[id] = state;
            }
            return state;
        }
    }
}
