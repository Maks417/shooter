#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    public static class KillFeedText
    {
        public const string Separator = "  >  ";
        public const string LocalOperator = "YOU";
        public const string EnvironmentLabel = "ENVIRONMENT";

        /// <summary>
        /// Formats one kill feed line. A null or empty killer means the death had no attributable
        /// killer; the Death event carries no cause, so the label is cause-neutral ("ENVIRONMENT"
        /// covers out-of-world, self and other non-player deaths alike).
        /// </summary>
        public static string Format(string? killer, string victim)
        {
            if (!string.IsNullOrEmpty(killer)) return killer + Separator + victim;
            return victim + Separator + EnvironmentLabel;
        }

        /// <summary>True when the local operator is the attributed killer (ally cyan tint).</summary>
        public static bool IsLocalKiller(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            return line.StartsWith(LocalOperator + Separator, StringComparison.Ordinal)
                && !line.EndsWith(EnvironmentLabel, StringComparison.Ordinal);
        }

        /// <summary>True when the local operator is the victim (danger coral tint).</summary>
        public static bool IsLocalVictim(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (line.EndsWith(Separator + LocalOperator, StringComparison.Ordinal)) return true;
            return string.Equals(line, LocalOperator + Separator + EnvironmentLabel, StringComparison.Ordinal);
        }
    }
}
