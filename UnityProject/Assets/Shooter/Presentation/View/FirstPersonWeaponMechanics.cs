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
        public static Float3 CycleOffset(in WeaponVisualProfile profile, float cycle01, bool reducedMotion)
        {
            float t = Math.Clamp(cycle01, 0f, 1f);
            if (t <= 0f || !profile.HasMagazineCycle || profile.CycleTravelMeters <= 0f) return default;
            float amp = reducedMotion ? 0.45f : 1f;
            float travel = profile.CycleTravelMeters;
            if (profile.Family == WeaponVisualFamily.PrimaryGun)
                return new Float3(0.006f * t * amp, 0f, -travel * t * amp);
            if (profile.Family == WeaponVisualFamily.Sidearm)
                return new Float3(0f, 0.002f * t * amp, -travel * t * amp);
            return default;
        }

        public static Float3 CycleOffset(byte slot, float cycle01, bool reducedMotion) =>
            CycleOffset(WeaponVisualProfiles.Resolve(default, slot), cycle01, reducedMotion);

        /// <summary>True when the active profile has a packaged cycle part (bolt / slide).</summary>
        public static bool HasCyclePart(in WeaponVisualProfile profile) => profile.HasMagazineCycle;

        public static bool HasCyclePart(byte slot) =>
            HasCyclePart(WeaponVisualProfiles.Resolve(default, slot));

        /// <summary>True when the active profile has a packaged detachable magazine.</summary>
        public static bool HasMagazinePart(in WeaponVisualProfile profile) => profile.HasMagazineCycle;

        public static bool HasMagazinePart(byte slot) =>
            HasMagazinePart(WeaponVisualProfiles.Resolve(default, slot));
    }
}
