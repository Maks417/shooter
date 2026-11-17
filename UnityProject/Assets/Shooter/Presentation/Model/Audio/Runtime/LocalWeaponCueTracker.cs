#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.View;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Local-player handling cues with no authority event (ADR-096): weapon switch, grenade pin when cooking
    /// starts, and a dry-fire click when the trigger is pulled on an empty gun. Reads the authoritative
    /// lifecycle snapshot; never writes it.
    /// </summary>
    public sealed class LocalWeaponCueTracker
    {
        public const float DryFireRepeatSeconds = 0.18f;

        private EntityId entity;
        private byte lastSlot;
        private WeaponAction lastAction;
        private bool primed;
        private float dryCooldown;

        public void Reset()
        {
            primed = false;
            entity = default;
            dryCooldown = 0f;
        }

        /// <summary>Writes up to <paramref name="cues"/>.Length cues for this frame and returns how many.</summary>
        public int Step(in ActorLifecycleState human, bool firePressed, float dtSeconds, SynthVoiceKind[] cues)
        {
            if (cues == null) throw new ArgumentNullException(nameof(cues));
            int count = 0;
            if (dryCooldown > 0f) dryCooldown = Math.Max(0f, dryCooldown - dtSeconds);
            if (!human.Alive || !human.EntityId.IsValid)
            {
                primed = false;
                return 0;
            }
            if (!primed || human.EntityId != entity)
            {
                // New life: adopt the spawn state silently.
                primed = true;
                entity = human.EntityId;
                lastSlot = human.ActiveWeaponSlot;
                lastAction = human.WeaponAction;
                return 0;
            }

            if (human.ActiveWeaponSlot != lastSlot && count < cues.Length) cues[count++] = SynthVoiceKind.WeaponSwitch;
            if (human.WeaponAction == WeaponAction.Cooking && lastAction != WeaponAction.Cooking && count < cues.Length)
                cues[count++] = SynthVoiceKind.GrenadePin;
            if (firePressed && human.Magazine == 0 && human.WeaponAction != WeaponAction.Reloading
                && human.WeaponAction != WeaponAction.Equipping && dryCooldown <= 0f && IsGun(human.ActiveWeaponDefinition)
                && count < cues.Length)
            {
                cues[count++] = SynthVoiceKind.DryFire;
                dryCooldown = DryFireRepeatSeconds;
            }
            lastSlot = human.ActiveWeaponSlot;
            lastAction = human.WeaponAction;
            return count;
        }

        private static bool IsGun(DefinitionId id) =>
            WeaponVisualProfiles.TryGet(id, out var profile) && WeaponVisualProfiles.IsGun(profile);
    }
}
