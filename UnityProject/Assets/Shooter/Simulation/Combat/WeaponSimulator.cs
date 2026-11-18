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
            => Step(tick, buttons, selectedSlot, alive, sprinting, slots, ref activeSlot, ref desiredSlot, acceptedShots, acceptedGrenades,
                events, ref nextEventId, actorId, WeaponModifiers.Identity);

        /// <summary>
        /// ADR-108: as above, with <paramref name="modifiers"/> applied to every firearm definition read this step (equip and
        /// reload times here; spread and recoil travel to the hitscan and recoil code inside <see cref="AcceptedShot.Definition"/>).
        /// </summary>
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
            EntityId actorId,
            WeaponModifiers modifiers)
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

            // Empty grenade cannot be selected (one per life); cooking keeps the slot until throw/boom.
            const byte grenadeSlot = 3;
            byte requested = selectedSlot;
            if (requested == grenadeSlot
                && slots[grenadeSlot].AmmoInMagazine == 0
                && slots[grenadeSlot].Action != WeaponAction.Cooking)
            {
                requested = activeSlot == grenadeSlot ? (byte)0 : activeSlot;
            }

            desiredSlot = requested;
            if (desiredSlot != activeSlot)
            {
                var current = slots[activeSlot];
                if (current.Action == WeaponAction.Reloading || current.Action == WeaponAction.Cooking)
                    slots[activeSlot] = Ready(current, tick);
                var next = slots[desiredSlot];
                var def = modifiers.Apply(WeaponDefinition.FromId(next.DefinitionId));
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
                    var def = modifiers.Apply(WeaponDefinition.FromId(w.DefinitionId));
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
                {
                    // Equip complete clears PreviousFireHeld so a hold started mid-draw can rising-edge fire/cook.
                    bool clearHeld = w.Action == WeaponAction.Equipping;
                    w = Ready(w, tick, clearHeld);
                }
                slots[i] = w;
            }

            var active = slots[activeSlot];
            var activeDef = modifiers.Apply(WeaponDefinition.FromId(active.DefinitionId));
            bool fireHeld = (buttons & InputButtons.FireHeld) != 0;
            bool firePressed = (buttons & InputButtons.FirePressed) != 0;
            bool reloadPressed = (buttons & InputButtons.ReloadPressed) != 0;
            bool aiming = (buttons & InputButtons.AimHeld) != 0;

            if (activeDef.FireMode == FireMode.Throwable)
            {
                StepGrenade(tick, fireHeld, firePressed, activeDef, ref active, activeSlot, acceptedGrenades, slots);
                // After the only grenade is spent, leave the empty slot so the player is not empty-handed.
                if (activeSlot == 3
                    && slots[3].AmmoInMagazine == 0
                    && slots[3].Action != WeaponAction.Cooking
                    && slots[3].Action != WeaponAction.Equipping)
                {
                    var rifle = slots[0];
                    var rifleDef = modifiers.Apply(WeaponDefinition.FromId(rifle.DefinitionId));
                    slots[0] = new WeaponState(rifle.DefinitionId, WeaponAction.Equipping, rifle.AmmoInMagazine, rifle.Reserve,
                        rifle.NextAllowedFireTick, tick, unchecked(tick + rifleDef.EquipTicks), rifle.ShotIndex, false);
                    activeSlot = 0;
                    desiredSlot = 0;
                }
                return;
            }

            bool rising = fireHeld && !active.PreviousFireHeld;
            // ADR-102 burst: a press starts a burst and the remaining rounds follow on their own cadence.
            // Progress lives in ShotIndex (each burst starts on a multiple of BurstCount), so the frozen
            // C0 WeaponState needs no new field. Any interruption (reload, switch, sprint) ends the burst.
            bool burst = activeDef.FireMode == FireMode.Burst && activeDef.BurstCount > 1;
            bool burstContinues = burst && active.ShotIndex % activeDef.BurstCount != 0
                && active.Action == WeaponAction.Ready && tick == active.NextAllowedFireTick;
            bool wantsFire = activeDef.FireMode == FireMode.Automatic ? (fireHeld || rising)
                : activeDef.FireMode == FireMode.Melee ? (firePressed || rising)
                : burst ? (burstContinues || firePressed || rising)
                : firePressed;
            if (sprinting && !activeDef.AllowsSprintFire) wantsFire = false;

            bool canReload = activeDef.FireMode != FireMode.Melee && activeDef.FireMode != FireMode.Throwable
                && activeDef.ReloadTicks > 0
                && active.AmmoInMagazine < activeDef.MagazineSize && active.Reserve > 0;
            bool autoReload = active.AmmoInMagazine == 0 && active.Reserve > 0 && activeDef.ConsumesAmmo;
            if ((reloadPressed || autoReload)
                && (active.Action == WeaponAction.Ready || active.Action == WeaponAction.FiringRecovery)
                && canReload)
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
                uint interval = activeDef.FireIntervalTicks;
                if (burst)
                {
                    uint count = activeDef.BurstCount;
                    // A new burst starts on the next multiple of BurstCount (+1) so progress can be read back.
                    if (!burstContinues) shotIndex = unchecked((active.ShotIndex + count - 1) / count * count + 1);
                    if (shotIndex % count != 0) interval = activeDef.BurstIntervalTicks;
                }
                ushort ammo = active.AmmoInMagazine;
                if (activeDef.ConsumesAmmo) ammo = (ushort)(ammo - 1);
                active = new WeaponState(active.DefinitionId, WeaponAction.FiringRecovery, ammo, active.Reserve,
                    unchecked(tick + interval), tick, unchecked(tick + interval), shotIndex, fireHeld);
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

            bool rising = fireHeld && !active.PreviousFireHeld;
            if ((firePressed || rising) && active.Action == WeaponAction.Ready && active.AmmoInMagazine > 0)
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

        public static WeaponState[] CreateDefaultLoadout() => WeaponLoadoutPolicy.CreateDefaultLoadout();

        public static WeaponState[] CreateLoadout(DefinitionId primaryId) => WeaponLoadoutPolicy.CreateLoadout(primaryId);

        private static WeaponState Ready(WeaponState w, uint tick, bool clearPreviousFireHeld = false) =>
            new WeaponState(w.DefinitionId, WeaponAction.Ready, w.AmmoInMagazine, w.Reserve, w.NextAllowedFireTick, tick, tick, w.ShotIndex,
                clearPreviousFireHeld ? false : w.PreviousFireHeld);

        private static WeaponState Disable(WeaponState w, uint tick) =>
            new WeaponState(w.DefinitionId, WeaponAction.Disabled, w.AmmoInMagazine, w.Reserve, w.NextAllowedFireTick, tick, tick, w.ShotIndex, false);
    }
}
