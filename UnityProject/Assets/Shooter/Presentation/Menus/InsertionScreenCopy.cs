#nullable enable
using System;

namespace Shooter.Presentation.Menus
{
    /// <summary>Truthful copy for boot/match insertion screens. No progress percentages.</summary>
    public static class InsertionScreenCopy
    {
        public const string MatchTitle = "PREPARING THE BATTLEFIELD";
        public const string BootTitle = "PREPARING THE BATTLEFIELD";
        public const string BootHeader = "Standing up local operations";
        public const string BootPhase = "Binding packaged content…";
        public const string BootRules = "LOCAL / OFFLINE  ·  TEAM DEATHMATCH";
        public const string BootFooter = "PACKAGED CONTENT  ·  NO NETWORK";
        public const string BindingPresentation = "Binding presentation…";

        public static string MatchRules(byte teamSize)
        {
            int n = teamSize == 6 ? 6 : 5;
            return "TEAM DEATHMATCH  ·  " + n + " v " + n + "  ·  150 KILLS / 15 MIN";
        }

        public static string MatchFooter(byte teamSize)
        {
            int n = teamSize == 6 ? 6 : 5;
            return "TEAM DEATHMATCH   /   " + n + " v " + n;
        }

        public static string LoadingPhase(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return "Loading map…";
            return "Loading " + mapId + "…";
        }

        public static string DisplayNameOrFallback(string? displayName, string mapId)
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();
            return string.IsNullOrEmpty(mapId) ? "OPERATION" : mapId;
        }
    }
}
