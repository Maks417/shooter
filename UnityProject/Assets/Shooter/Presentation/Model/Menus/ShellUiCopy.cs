#nullable enable
using System;

namespace Shooter.Presentation.Menus
{
    /// <summary>Player-facing shell chrome (Main/Setup/Pause/Settings/Results/Recovery). Pure BCL.</summary>
    public static class ShellUiCopy
    {
        public const string Brand = "SHOOTER";
        public const string LocalMatch = "LOCAL MATCH";
        public const string EyebrowInsertion = "OPERATIONS / INSERTION";
        public const string EyebrowBriefing = "OPERATIONS / BRIEFING";
        public const string EyebrowPaused = "OPERATIONS / PAUSED";
        public const string EyebrowPreferences = "OPERATIONS / PREFERENCES";
        public const string EyebrowDebrief = "OPERATIONS / DEBRIEF";
        public const string EyebrowRoster = "OPERATIONS / LIVE ROSTER";
        public const string EyebrowRecovery = "OPERATIONS / RECOVERY";

        public const string MainHeadline = "YOUR NEXT\nOPERATION";
        public const string MainBattles = "OFFLINE BATTLES / TEAM DEATHMATCH";
        public const string MainCardSubtitle = "5v5 / 6v6  ·  LOCAL BOTS";
        public const string MainNavigate = "TAB / ARROWS  Navigate     ENTER  Select";
        public const string MapCompound = "TRAINING COMPOUND";
        public const string MapDepot = "DEPOT YARD";
        public const string MapOil = "OIL DEPOT";

        public const string SetupTitle = "Choose your fight";
        public const string SetupSelectArea = "SELECT OPERATION AREA";
        public const string SetupCompound = "01   TRAINING COMPOUND";
        public const string SetupDepot = "02   DEPOT YARD";
        public const string SetupOil = "03   OIL DEPOT";
        public const string SetupMode = "TEAM DEATHMATCH";
        public const string SetupRules = "RULES";
        public const string SetupRulesBody = "150 kills to win\n15 minute time limit";
        public const string SetupStart = "START MATCH";
        public const string SetupBack = "BACK";

        public const string PauseTitle = "Match on hold";
        public const string PauseCommands = "COMMANDS";
        public const string PauseHold = "ON HOLD";
        public const string PauseBody = "The battlefield waits.\nResume when you are ready.";
        public const string PauseHint = "ESC resumes play. Settings still use Apply / Cancel.";
        public const string PauseFooter = "ESC  Resume";

        public const string ConfirmTitle = "CONFIRM ACTION";
        public const string ConfirmLeave = "Leave match? Progress will be lost.";
        public const string ConfirmRestart = "Restart match? Progress will be lost.";
        public const string ConfirmCancel = "CANCEL";
        public const string ConfirmAccept = "CONFIRM";

        public const string SettingsTitle = "Settings";
        public const string RecoveryTitle = "Game content needs attention";
        public const string RecoveryRetry = "RETRY";
        public const string RecoveryQuit = "QUIT TO DESKTOP";

        public const string ResultsEmpty = "Match results";
        public const string ResultsPlayAgain = "PLAY AGAIN";
        public const string ResultsMain = "MAIN MENU";
        public const string ScoreboardTitle = "Scoreboard";
        public const string TeamDeathmatch = "TEAM DEATHMATCH";

        public const string Play = "PLAY";
        public const string Settings = "SETTINGS";
        public const string Quit = "QUIT";
        public const string Resume = "RESUME";
        public const string RestartMatch = "RESTART MATCH";
        public const string MainMenu = "MAIN MENU";

        public const string StatusKilled = "KILLED IN ACTION";
        public const string StatusLabel = "STATUS";
        public const string AwaitingSpawn = "Finding a safe drop…";
        public const string Reloading = "RELOADING";
        public const string OutOfAmmo = "OUT OF AMMO";
        public const string Empty = "EMPTY";
        public const string ReloadHint = "  RELOAD";
        public const string Rifle = "RIFLE";
        public const string Sidearm = "SIDEARM";
        public const string Knife = "KNIFE";
        public const string Grenade = "GRENADE";

        public static string SetupRosterLine(byte teamSize)
        {
            int n = teamSize == 6 ? 6 : 5;
            return "YOU + " + (n - 1) + " ALLIED BOTS\nVS " + n + " ENEMY BOTS";
        }

        public static string ReturningIn(float seconds) =>
            "Returning in " + seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
    }
}
