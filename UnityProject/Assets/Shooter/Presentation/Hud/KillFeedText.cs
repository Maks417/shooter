#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    public static class KillFeedText
    {
        /// <summary>
        /// Formats one kill feed line. A null or empty killer means the death had no attributable
        /// killer; the Death event carries no cause, so the label is cause-neutral ("ENVIRONMENT"
        /// covers out-of-world, self and other non-player deaths alike).
        /// </summary>
        public static string Format(string? killer, string victim)
        {
            if (!string.IsNullOrEmpty(killer)) return killer + CombatHudVisual.KillFeedSeparator + victim;
            return victim + CombatHudVisual.KillFeedSeparator + "ENVIRONMENT";
        }

        /// <summary>True when the local operator is the attributed killer (ally cyan tint).</summary>
        public static bool IsLocalKiller(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            return line.StartsWith("YOU  >  ", StringComparison.Ordinal)
                && !line.EndsWith("ENVIRONMENT", StringComparison.Ordinal);
        }

        /// <summary>True when the local operator is the victim (danger coral tint).</summary>
        public static bool IsLocalVictim(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (line.EndsWith("  >  YOU", StringComparison.Ordinal)) return true;
            return string.Equals(line, "YOU  >  ENVIRONMENT", StringComparison.Ordinal);
        }
    }
}
