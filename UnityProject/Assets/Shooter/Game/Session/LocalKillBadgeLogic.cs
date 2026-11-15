#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>
    /// Pure helpers for ADR-073 local multi-kill / headshot HUD awards.
    /// Does not change personal Score math; Game/Session must not reference Presentation.
    /// </summary>
    public static class LocalKillBadgeLogic
    {
        /// <summary>Matches combat head region used by kill-feed correlators.</summary>
        public const ushort HeadRegionId = 1;

        public static byte IncrementStreak(byte current) =>
            current == byte.MaxValue ? byte.MaxValue : (byte)(current + 1);

        /// <summary>
        /// True when events preceding <paramref name="deathIndex"/> include a DamageApplied from the
        /// death instigator to the victim whose last region was head. Invalid killer → false.
        /// </summary>
        public static bool IsFatalHeadshot(IReadOnlyList<CombatEvent> events, int deathIndex)
        {
            if (events == null || deathIndex < 0 || deathIndex >= events.Count) return false;
            CombatEvent death = events[deathIndex];
            if (death.Kind != CombatEventKind.Death) return false;
            if (!death.Instigator.IsValid) return false;

            bool sawDamage = false;
            ushort regionId = 0;
            for (int i = 0; i < deathIndex; i++)
            {
                CombatEvent ev = events[i];
                if (ev.Kind != CombatEventKind.DamageApplied) continue;
                if (!ev.Instigator.Equals(death.Instigator) || !ev.Target.Equals(death.Target)) continue;
                if (ev.Payload is not DamagePayload damage) continue;
                sawDamage = true;
                regionId = damage.RegionId;
            }

            return sawDamage && regionId == HeadRegionId;
        }

        /// <summary>Push Kill award (+ optional Headshot sibling) into the local award ring.</summary>
        public static void PushKillBadges(
            Action<LocalScoreAward> push,
            string victimName,
            byte streak,
            bool headshot,
            uint killPoints)
        {
            if (push == null) throw new ArgumentNullException(nameof(push));
            if (streak == 0) streak = 1;
            push(new LocalScoreAward(LocalScoreAwardKind.Kill, killPoints, victimName, streak, headshot));
            if (headshot)
                push(new LocalScoreAward(LocalScoreAwardKind.Headshot, 0, victimName, 0, true));
        }
    }
}
