#nullable enable
using System;

namespace Shooter.Presentation.Hud
{
    /// <summary>Weapon silhouette drawn between killer and victim names in the elimination feed.</summary>
    public enum KillFeedWeapon : byte
    {
        None = 0,
        Rifle = 1,
        Carbine = 2,
        Pistol = 3,
        Knife = 4,
        Grenade = 5
    }

    /// <summary>One expiring kill-feed row: names plus correlated weapon/headshot cues.</summary>
    public readonly struct KillFeedEntry
    {
        public string? Killer { get; }
        public string Victim { get; }
        public KillFeedWeapon Weapon { get; }
        public bool Headshot { get; }

        public bool HasKiller => !string.IsNullOrEmpty(Killer);

        public KillFeedEntry(string? killer, string victim, KillFeedWeapon weapon, bool headshot)
        {
            Killer = killer;
            Victim = victim ?? "";
            Weapon = weapon;
            Headshot = headshot;
        }

        public string LeftLabel => HasKiller ? Killer! : Victim;
        public string RightLabel => HasKiller ? Victim : KillFeedText.EnvironmentLabel;
    }

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

        public static KillFeedEntry Create(string? killer, string victim, KillFeedWeapon weapon, bool headshot)
            => new KillFeedEntry(killer, victim, weapon, headshot);

        /// <summary>True when the local operator is the attributed killer (ally cyan tint).</summary>
        public static bool IsLocalKiller(KillFeedEntry entry)
            => entry.HasKiller
               && string.Equals(entry.Killer, LocalOperator, StringComparison.Ordinal);

        /// <summary>True when the local operator is the victim (danger coral tint).</summary>
        public static bool IsLocalVictim(KillFeedEntry entry)
            => string.Equals(entry.Victim, LocalOperator, StringComparison.Ordinal);

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
