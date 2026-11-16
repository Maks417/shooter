#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.Hud
{
    /// <summary>
    /// Presentation correlator: weapon and headshot for a Death from same-batch sibling events.
    /// Does not mutate authority; DeathPayload stays killer-only (C0 freeze).
    /// </summary>
    public static class KillFeedCause
    {
        public const ushort HeadRegionId = 1;
        public const string RifleWeaponId = "weapons/rifle";
        public const string CarbineWeaponId = "weapons/rifle-carbine";
        public const string PistolWeaponId = "weapons/pistol";
        public const string KnifeWeaponId = "weapons/knife";
        public const string GrenadeWeaponId = "weapons/grenade";

        /// <summary>
        /// Resolve weapon silhouette and headshot from events preceding <paramref name="deathIndex"/>.
        /// Hitscan/melee: last ShotAccepted WeaponId for the killer. Grenade: fatal DamageApplied with
        /// no matching ShotAccepted. Environment / invalid killer: None, no headshot.
        /// </summary>
        public static void Resolve(IReadOnlyList<CombatEvent> events, int deathIndex, out KillFeedWeapon weapon, out bool headshot)
        {
            weapon = KillFeedWeapon.None;
            headshot = false;
            if (events == null || deathIndex < 0 || deathIndex >= events.Count) return;

            CombatEvent death = events[deathIndex];
            if (death.Kind != CombatEventKind.Death) return;
            if (!death.Instigator.IsValid) return;

            bool sawDamage = false;
            ushort regionId = 0;
            bool sawShot = false;
            DefinitionId shotWeapon = default;

            for (int i = 0; i < deathIndex; i++)
            {
                CombatEvent ev = events[i];
                if (ev.Kind == CombatEventKind.DamageApplied
                    && ev.Instigator.Equals(death.Instigator)
                    && ev.Target.Equals(death.Target)
                    && ev.Payload is DamagePayload damage)
                {
                    sawDamage = true;
                    regionId = damage.RegionId;
                }
                else if (ev.Kind == CombatEventKind.ShotAccepted
                    && ev.Instigator.Equals(death.Instigator)
                    && ev.Payload is ShotPayload shot)
                {
                    sawShot = true;
                    shotWeapon = shot.WeaponId;
                }
            }

            if (sawShot)
                weapon = MapWeapon(shotWeapon);
            else if (sawDamage)
                weapon = KillFeedWeapon.Grenade;

            headshot = sawDamage && regionId == HeadRegionId;
        }

        /// <summary>Kill-feed glyph from the weapon's visual profile (ADR-077 <c>hud.killFeedGlyph</c>).</summary>
        public static KillFeedWeapon MapWeapon(DefinitionId weaponId)
        {
            if (!weaponId.IsValid) return KillFeedWeapon.None;
            if (Shooter.Presentation.View.WeaponVisualProfiles.TryGet(weaponId, out var profile))
                return profile.KillFeedGlyph;
            // Unknown gun-like DefinitionId: long-gun silhouette keeps the row readable.
            return KillFeedWeapon.Rifle;
        }
    }
}
