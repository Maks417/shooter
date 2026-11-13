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

    public sealed class WeaponSimulator
    {
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
            List<CombatEvent> events,
            ref EventId nextEventId,
            EntityId actorId)
        {
            if (slots == null || slots.Length != 2) throw new System.ArgumentException("Two weapon slots required", nameof(slots));
            acceptedShots.Clear();
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
                if (current.Action == WeaponAction.Reloading)
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

            bool rising = firePressed && !active.PreviousFireHeld;
            bool wantsFire = activeDef.FireMode == FireMode.Automatic ? (fireHeld || rising) : rising;
            if (sprinting) wantsFire = false;

            if (reloadPressed && active.Action == WeaponAction.Ready && active.AmmoInMagazine < activeDef.MagazineSize && active.Reserve > 0)
            {
                active = new WeaponState(active.DefinitionId, WeaponAction.Reloading, active.AmmoInMagazine, active.Reserve,
                    active.NextAllowedFireTick, tick, unchecked(tick + activeDef.ReloadTicks), active.ShotIndex, fireHeld);
                events.Add(new CombatEvent(nextEventId, CombatEventKind.ReloadStarted, actorId, default,
                    new ActionPayload(activeSlot, tick, active.PhaseEndTick)));
                nextEventId = nextEventId.NextOrdinal();
                slots[activeSlot] = active;
                return;
            }

            if (wantsFire && active.Action == WeaponAction.Ready && active.AmmoInMagazine > 0 && tick >= active.NextAllowedFireTick)
            {
                uint shotIndex = unchecked(active.ShotIndex + 1);
                ushort ammo = (ushort)(active.AmmoInMagazine - 1);
                active = new WeaponState(active.DefinitionId, WeaponAction.FiringRecovery, ammo, active.Reserve,
                    unchecked(tick + activeDef.FireIntervalTicks), tick, unchecked(tick + activeDef.FireIntervalTicks), shotIndex, fireHeld);
                acceptedShots.Add(new AcceptedShot(activeSlot, activeDef, shotIndex, aiming));
                slots[activeSlot] = active;
                return;
            }

            slots[activeSlot] = new WeaponState(active.DefinitionId, active.Action, active.AmmoInMagazine, active.Reserve,
                active.NextAllowedFireTick, active.PhaseStartTick, active.PhaseEndTick, active.ShotIndex, fireHeld);
        }

        public static WeaponState CreateLoaded(WeaponDefinition def) =>
            new WeaponState(def.Id, WeaponAction.Ready, def.MagazineSize, def.ReserveCapacity, 0, 0, 0, 0, false);

        private static WeaponState Ready(WeaponState w, uint tick) =>
            new WeaponState(w.DefinitionId, WeaponAction.Ready, w.AmmoInMagazine, w.Reserve, w.NextAllowedFireTick, tick, tick, w.ShotIndex, w.PreviousFireHeld);

        private static WeaponState Disable(WeaponState w, uint tick) =>
            new WeaponState(w.DefinitionId, WeaponAction.Disabled, w.AmmoInMagazine, w.Reserve, w.NextAllowedFireTick, tick, tick, w.ShotIndex, false);
    }
}
