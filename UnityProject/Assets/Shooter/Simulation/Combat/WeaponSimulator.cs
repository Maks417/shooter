#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    public readonly struct AcceptedShot
    {
        public byte Slot { get; }
        public WeaponDefinition Definition { get; }
        public uint ShotIndex { get; }
        public bool Ads { get; }
        public AcceptedShot(byte slot, WeaponDefinition definition, uint shotIndex, bool ads)
        { Slot = slot; Definition = definition; ShotIndex = shotIndex; Ads = ads; }
    }

    public readonly struct AcceptedGrenade
    {
        public byte Slot { get; }
        public WeaponDefinition Definition { get; }
        public uint ShotIndex { get; }
        public bool DetonateInHand { get; }
        public uint RemainingFuseTicks { get; }
        public AcceptedGrenade(byte slot, WeaponDefinition definition, uint shotIndex, bool detonateInHand, uint remainingFuseTicks)
        {
            Slot = slot; Definition = definition; ShotIndex = shotIndex;
            DetonateInHand = detonateInHand; RemainingFuseTicks = remainingFuseTicks;
        }
    }

    public sealed class WeaponSimulator
    {
        public const int SlotCount = 4;

        public void Step(
            uint tick,
            InputButtons buttons,
            byte selectedSlot,
            bool alive,
            bool sprinting,
            WeaponState[] slots,
            ref byte activeSlot,
            ref byte desiredSlot,
            List<AcceptedShot> acceptedShots,
            List<AcceptedGrenade> acceptedGrenades,
            List<CombatEvent> events,
            ref EventId nextEventId,
            EntityId actorId)
        {
            if (slots == null || slots.Length != SlotCount) throw new System.ArgumentException("Four weapon slots required", nameof(slots));
            if (selectedSlot >= SlotCount) throw new System.ArgumentOutOfRangeException(nameof(selectedSlot));
            acceptedShots.Clear();
            acceptedGrenades.Clear();
            if (!alive)
            {
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = Disable(slots[i], tick);
                return;
            }

            desiredSlot = selectedSlot;
            if (desiredSlot != activeSlot)
            {
                var current = slots[activeSlot];
                if (current.Action == WeaponAction.Reloading || current.Action == WeaponAction.Cooking)
                    slots[activeSlot] = Ready(current, tick);
                var next = slots[desiredSlot];
                var def = WeaponDefinition.FromId(next.DefinitionId);
                slots[desiredSlot] = new WeaponState(next.DefinitionId, WeaponAction.Equipping, next.AmmoInMagazine, next.Reserve,
                    next.NextAllowedFireTick, tick, unchecked(tick + def.EquipTicks), next.ShotIndex, false);
                activeSlot = desiredSlot;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var w = slots[i];
                if (w.Action != WeaponAction.Equipping && w.Action != WeaponAction.Reloading && w.Action != WeaponAction.FiringRecovery)
                    continue;
                if (tick < w.PhaseEndTick) continue;

                if (w.Action == WeaponAction.Reloading)
                {
                    var def = WeaponDefinition.FromId(w.DefinitionId);
                    ushort need = (ushort)(def.MagazineSize - w.AmmoInMagazine);
                    ushort take = w.Reserve < need ? w.Reserve : need;
                    uint start = w.PhaseStartTick;
                    w = new WeaponState(w.DefinitionId, WeaponAction.Ready, (ushort)(w.AmmoInMagazine + take), (ushort)(w.Reserve - take),
                        w.NextAllowedFireTick, tick, tick, w.ShotIndex, w.PreviousFireHeld);
                    events.Add(new CombatEvent(nextEventId, CombatEventKind.ReloadCompleted, actorId, default,
                        new ActionPayload((byte)i, start, tick)));
                    nextEventId = nextEventId.NextOrdinal();
                }
                else
                    w = Ready(w, tick);
                slots[i] = w;
            }

            var active = slots[activeSlot];
            var activeDef = WeaponDefinition.FromId(active.DefinitionId);
            bool fireHeld = (buttons & InputButtons.FireHeld) != 0;
            bool firePressed = (buttons & InputButtons.FirePressed) != 0;
            bool reloadPressed = (buttons & InputButtons.ReloadPressed) != 0;
            bool aiming = (buttons & InputButtons.AimHeld) != 0;

            if (activeDef.FireMode == FireMode.Throwable)
            {
                StepGrenade(tick, fireHeld, firePressed, activeDef, ref active, activeSlot, acceptedGrenades, slots);
                return;
            }

            bool rising = firePressed && !active.PreviousFireHeld;
            bool wantsFire = activeDef.FireMode == FireMode.Automatic ? (fireHeld || rising)
                : activeDef.FireMode == FireMode.Melee ? firePressed
                : firePressed;
            if (sprinting && !activeDef.AllowsSprintFire) wantsFire = false;

            bool canReload = activeDef.FireMode != FireMode.Melee && activeDef.FireMode != FireMode.Throwable
                && activeDef.ReloadTicks > 0
                && active.AmmoInMagazine < activeDef.MagazineSize && active.Reserve > 0;
            if (reloadPressed && (active.Action == WeaponAction.Ready || active.Action == WeaponAction.FiringRecovery) && canReload)
            {
                active = new WeaponState(active.DefinitionId, WeaponAction.Reloading, active.AmmoInMagazine, active.Reserve,
                    active.NextAllowedFireTick, tick, unchecked(tick + activeDef.ReloadTicks), active.ShotIndex, fireHeld);
                events.Add(new CombatEvent(nextEventId, CombatEventKind.ReloadStarted, actorId, default,
                    new ActionPayload(activeSlot, tick, active.PhaseEndTick)));
                nextEventId = nextEventId.NextOrdinal();
                slots[activeSlot] = active;
                return;
            }

            bool ammoOk = !activeDef.ConsumesAmmo || active.AmmoInMagazine > 0;
            if (wantsFire && active.Action == WeaponAction.Ready && ammoOk && tick >= active.NextAllowedFireTick)
            {
                uint shotIndex = unchecked(active.ShotIndex + 1);
                ushort ammo = active.AmmoInMagazine;
                if (activeDef.ConsumesAmmo) ammo = (ushort)(ammo - 1);
                active = new WeaponState(active.DefinitionId, WeaponAction.FiringRecovery, ammo, active.Reserve,
                    unchecked(tick + activeDef.FireIntervalTicks), tick, unchecked(tick + activeDef.FireIntervalTicks), shotIndex, fireHeld);
                acceptedShots.Add(new AcceptedShot(activeSlot, activeDef, shotIndex, aiming));
                slots[activeSlot] = active;
                return;
            }

            slots[activeSlot] = new WeaponState(active.DefinitionId, active.Action, active.AmmoInMagazine, active.Reserve,
                active.NextAllowedFireTick, active.PhaseStartTick, active.PhaseEndTick, active.ShotIndex, fireHeld);
        }

        private static void StepGrenade(
            uint tick,
            bool fireHeld,
            bool firePressed,
            WeaponDefinition def,
            ref WeaponState active,
            byte activeSlot,
            List<AcceptedGrenade> acceptedGrenades,
            WeaponState[] slots)
        {
            if (active.Action == WeaponAction.Cooking)
            {
                uint remaining = tick >= active.PhaseEndTick ? 0u : unchecked(active.PhaseEndTick - tick);
                bool release = !fireHeld;
                bool fuseExpired = remaining == 0;
                if (release || fuseExpired)
                {
                    uint shotIndex = unchecked(active.ShotIndex + 1);
                    active = new WeaponState(active.DefinitionId, WeaponAction.Ready, 0, active.Reserve,
                        tick, tick, tick, shotIndex, fireHeld);
                    acceptedGrenades.Add(new AcceptedGrenade(activeSlot, def, shotIndex, fuseExpired && !release, remaining));
                    slots[activeSlot] = active;
                    return;
                }

                slots[activeSlot] = new WeaponState(active.DefinitionId, active.Action, active.AmmoInMagazine, active.Reserve,
                    active.NextAllowedFireTick, active.PhaseStartTick, active.PhaseEndTick, active.ShotIndex, fireHeld);
                return;
            }

            if (firePressed && active.Action == WeaponAction.Ready && active.AmmoInMagazine > 0)
            {
                active = new WeaponState(active.DefinitionId, WeaponAction.Cooking, active.AmmoInMagazine, active.Reserve,
                    active.NextAllowedFireTick, tick, unchecked(tick + def.FuseTicks), active.ShotIndex, fireHeld);
                slots[activeSlot] = active;
                return;
            }

            slots[activeSlot] = new WeaponState(active.DefinitionId, active.Action, active.AmmoInMagazine, active.Reserve,
                active.NextAllowedFireTick, active.PhaseStartTick, active.PhaseEndTick, active.ShotIndex, fireHeld);
        }

        public static WeaponState CreateLoaded(WeaponDefinition def) =>
            new WeaponState(def.Id, WeaponAction.Ready, def.MagazineSize, def.ReserveCapacity, 0, 0, 0, 0, false);

        public static WeaponState[] CreateDefaultLoadout() => new[]
        {
            CreateLoaded(WeaponDefinition.Rifle),
            CreateLoaded(WeaponDefinition.Pistol),
            CreateLoaded(WeaponDefinition.Knife),
            CreateLoaded(WeaponDefinition.Grenade)
        };

        private static WeaponState Ready(WeaponState w, uint tick) =>
            new WeaponState(w.DefinitionId, WeaponAction.Ready, w.AmmoInMagazine, w.Reserve, w.NextAllowedFireTick, tick, tick, w.ShotIndex, w.PreviousFireHeld);

        private static WeaponState Disable(WeaponState w, uint tick) =>
            new WeaponState(w.DefinitionId, WeaponAction.Disabled, w.AmmoInMagazine, w.Reserve, w.NextAllowedFireTick, tick, tick, w.ShotIndex, false);
    }
}
