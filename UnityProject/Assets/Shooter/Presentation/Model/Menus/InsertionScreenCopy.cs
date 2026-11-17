#nullable enable
using System;

namespace Shooter.Presentation.Menus
{
    /// <summary>Truthful copy for boot/match insertion screens. No progress percentages.</summary>
    public static class InsertionScreenCopy
    {
        public const string MatchTitle = "PREPARING THE BATTLEFIELD";
        public const string BootTitle = "PREPARING THE BATTLEFIELD";
        public const string BootHeader = "Standing by";
        public const string BootPhase = "Loading your arsenal…";
        public const string BootRules = "LOCAL MATCH  ·  TEAM DEATHMATCH";
        public const string BootFooter = "SOLO OPS  ·  NO NETWORK";
        public const string BindingPresentation = "Linking the battlefield…";

        private static readonly string[] Tips = {
            "Stay with your squad. Friendly arrows show their facing.",
            "Gunfire briefly reveals enemies on the tactical map.",
            "Reload behind cover; movement reduces weapon tightness.",
            "Hold Sprint and press Slide to cross an exposed lane.",
            "Pause to change controls, audio or accessibility settings."
        };
        public static string TipAt(float seconds, bool reducedMotion) =>
            Tips[reducedMotion ? 0 : (int)(Math.Max(0, seconds) / 7f) % Tips.Length];

        public static string MatchRules(byte teamSize)
        {
            int n = teamSize == 5 || teamSize == 8 || teamSize == 10 || teamSize == 15 ? teamSize : 5;
            return "TEAM DEATHMATCH  ·  " + n + " v " + n + "  ·  150 KILLS / 15 MIN";
        }

        public static string MatchFooter(byte teamSize)
        {
            int n = teamSize == 5 || teamSize == 8 || teamSize == 10 || teamSize == 15 ? teamSize : 5;
            return "TEAM DEATHMATCH   /   " + n + " v " + n;
        }

        public static string LoadingPhase(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return "Loading the map…";
            return "Loading " + FriendlyMapName(mapId) + "…";
        }

        public static string DisplayNameOrFallback(string? displayName, string mapId)
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();
            if (string.IsNullOrEmpty(mapId)) return "OPERATION";
            return FriendlyMapName(mapId);
        }

        public static string FriendlyMapName(string mapId)
        {
            if (string.Equals(mapId, "training-compound", StringComparison.OrdinalIgnoreCase))
                return "Training Compound";
            if (string.Equals(mapId, "depot-yard", StringComparison.OrdinalIgnoreCase))
                return "Depot Yard";
            if (string.Equals(mapId, "oil-depot", StringComparison.OrdinalIgnoreCase))
                return "Oil Depot";
            return mapId;
        }
    }
}
