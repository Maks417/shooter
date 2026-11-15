#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Analytic bolt/slide cycle and magazine detach poses driven from authority shot/reload phase.
    /// Cosmetic only — never mutates ammo, timing or ballistic origins.
    /// </summary>
    public static class FirstPersonWeaponMechanics
    {
        /// <summary>Peak open travel for rifle bolt (meters, weapon-local).</summary>
        public const float RifleBoltTravel = 0.070f;
        /// <summary>Peak open travel for pistol slide (meters, weapon-local).</summary>
        public const float PistolSlideTravel = 0.038f;
        /// <summary>Impulse applied to the cosmetic cycle spring on each local ShotAccepted.</summary>
        public const float CycleImpulse = 1f;

        /// <summary>
        /// Weapon-local offset for the detachable magazine during reload.
        /// Uses the same remove/insert arc as support-hand travel.
        /// </summary>
        public static Float3 MagazineOffset(float reloadPhase, bool magazineEmpty, bool reducedMotion) =>
            FirstPersonMagazineTravel.Offset(reloadPhase, magazineEmpty, reducedMotion);

        /// <summary>
        /// Weapon-local offset for bolt (rifle) or slide (pistol).
        /// <paramref name="cycle01"/> is 0 = closed, 1 = fully open.
        /// </summary>
        public static Float3 CycleOffset(byte slot, float cycle01, bool reducedMotion)
        {
            float t = Math.Clamp(cycle01, 0f, 1f);
            if (t <= 0f) return default;
            float amp = reducedMotion ? 0.45f : 1f;
            if (ProductAuthorityActionView.IsRifleSlot(slot))
                return new Float3(0.006f * t * amp, 0f, -RifleBoltTravel * t * amp);
            if (slot == ProductAuthorityActionView.PistolSlot)
                return new Float3(0f, 0.002f * t * amp, -PistolSlideTravel * t * amp);
            return default;
        }

        /// <summary>True when the active slot has a packaged cycle part (rifle bolt / pistol slide).</summary>
        public static bool HasCyclePart(byte slot) =>
            ProductAuthorityActionView.IsRifleSlot(slot) || slot == ProductAuthorityActionView.PistolSlot;

        /// <summary>True when the active slot has a packaged detachable magazine.</summary>
        public static bool HasMagazinePart(byte slot) =>
            ProductAuthorityActionView.IsRifleSlot(slot) || slot == ProductAuthorityActionView.PistolSlot;
    }
}
