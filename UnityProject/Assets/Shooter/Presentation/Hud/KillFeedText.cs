#nullable enable
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
            if (!string.IsNullOrEmpty(killer)) return killer + "  >  " + victim;
            return victim + "  >  ENVIRONMENT";
        }
    }
}
