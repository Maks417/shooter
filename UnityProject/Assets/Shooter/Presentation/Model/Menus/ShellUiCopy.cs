#nullable enable
using System;
using Shooter.Game.Contracts;

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
        public const string MainCardSubtitle = "5v5–15v15  ·  LOCAL BOTS";
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
        public const string Armory = "ARMORY";
        public const string Settings = "SETTINGS";

        // ADR-107 Armory page.
        public const string ArmoryEyebrow = "LOADOUT";
        public const string ArmoryTitle = "Armory";
        public const string ArmoryHint = "Your picks are saved and used every life. Bots draw random weapons and sights.";
        public const string ArmorySlotPrimary = "PRIMARY";
        public const string ArmorySlotSidearm = "SIDEARM";
        public const string ArmorySlotKnife = "KNIFE";
        public const string ArmorySlotGrenade = "GRENADE";
        public const string ArmoryFixed = "STANDARD ISSUE";
        public const string ArmoryWeapons = "WEAPONS";
        public const string ArmorySights = "SIGHT";
        public const string ArmorySidearmSights = "Sidearms use their own iron sights.";
        public const string ArmoryEquipped = "EQUIPPED";
        public const string ArmoryLoadout = "LOADOUT";
        public const string ArmorySaveFailed = "Armory could not be saved; picks apply to this session only.";
        public const string ArmoryLocked = "LOCKED";

        // ADR-108 Career page and debrief.
        public const string Career = "CAREER";
        public const string CareerEyebrow = "SERVICE RECORD";
        public const string CareerTitle = "Career";
        public const string CareerRankSection = "CURRENT RANK";
        public const string CareerLadderSection = "RANKS AND REWARDS";
        public const string CareerStatsSection = "SERVICE RECORD";
        public const string CareerNextReward = "NEXT REWARD";
        public const string CareerTopRank = "Highest rank reached.";
        public const string CareerHint = "Finish matches to earn career points: your score, +500 for finishing, +1000 for a win (+500 draw).";
        public const string CareerSaveFailed = "Career could not be saved; progress applies to this session only.";
        public const string CareerActiveBuffs = "ACTIVE BUFFS";
        public const string CareerNoBuffs = "None yet.";
        public const string CareerPromoted = "PROMOTED";

        /// <summary>"Unlocks at Sergeant".</summary>
        public static string UnlocksAt(string rankName) => "Unlocks at " + rankName;

        /// <summary>"12,400 / 15,000 CP".</summary>
        public static string CareerProgress(long points, long next) =>
            next <= 0 ? Thousands(points) + " CP" : Thousands(points) + " / " + Thousands(next) + " CP";

        /// <summary>"+3,240 CP  ·  SCORE 1,740  ·  FINISH +500  ·  WIN +1,000".</summary>
        public static string CareerAwardLine(long total, long score, long completion, long outcome, string outcomeLabel) =>
            "+" + Thousands(total) + " CP  ·  SCORE " + Thousands(score) + "  ·  FINISH +" + Thousands(completion)
            + (outcome > 0 ? "  ·  " + outcomeLabel + " +" + Thousands(outcome) : "");

        /// <summary>"PROMOTED: SERGEANT  ·  Golden Eagle sidearm".</summary>
        public static string CareerPromotion(string rankName, string reward) =>
            CareerPromoted + ": " + rankName.ToUpperInvariant() + (string.IsNullOrEmpty(reward) ? "" : "  ·  " + reward);

        /// <summary>Hours and minutes of active match time from 60 Hz ticks ("3 h 07 m", "12 m").</summary>
        public static string PlayTime(long ticks)
        {
            long minutes = ticks < 0 ? 0 : ticks / (60L * 60L);
            long hours = minutes / 60;
            return hours > 0 ? hours + " h " + (minutes % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + " m" : minutes + " m";
        }

        /// <summary>Ratio with two decimals ("1.85"); the numerator itself when the denominator is 0.</summary>
        public static string Ratio(long a, long b) =>
            (b <= 0 ? a : a / (double)b).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Whole percent ("54%"); "0%" with no samples.</summary>
        public static string Percent(long part, long whole) =>
            (whole <= 0 ? 0 : (long)Math.Round(100.0 * part / whole)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

        public static string Thousands(long value) => value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>One-line loadout summary: "M16A4 · COLLIMATOR / G17".</summary>
        public static string ArmorySummary(string primary, string sight, string sidearm) =>
            string.IsNullOrEmpty(sight) ? primary + " / " + sidearm : primary + " · " + sight + " / " + sidearm;
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
        /// <summary>ADR-105: pickup prompt for a same-model dropped gun (ammo top-up).</summary>
        public const string PickupAmmo = "TAKE AMMO";
        /// <summary>ADR-105: pickup prompt for a different dropped gun (swap the primary).</summary>
        public static string PickupWeapon(string weaponName) => "TAKE " + weaponName;
        public const string Rifle = "RIFLE";
        public const string Sidearm = "SIDEARM";
        public const string Knife = "KNIFE";
        public const string Grenade = "GRENADE";

        public static string SetupRosterLine(byte teamSize)
        {
            int n = GameContractVersion.IsAllowedTeamSize(teamSize) ? teamSize : 5;
            return "YOU + " + (n - 1) + " ALLIED BOTS\nVS " + n + " ENEMY BOTS";
        }

        public static string ReturningIn(float seconds) =>
            "Returning in " + seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
    }
}
