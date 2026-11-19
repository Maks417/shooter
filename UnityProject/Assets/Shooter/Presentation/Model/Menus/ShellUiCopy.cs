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
        public const string MapCompound = "OLD TOWN";
        public const string MapDepot = "HARBOUR";
        public const string MapOil = "OIL DEPOT";

        public const string SetupTitle = "Choose your fight";
        public const string SetupSelectArea = "SELECT OPERATION AREA";
        public const string SetupCompound = "01   OLD TOWN";
        public const string SetupDepot = "02   HARBOUR";
        public const string SetupOil = "03   OIL DEPOT";
        public const string SetupMode = "TEAM DEATHMATCH";
        public const string SetupRules = "RULES";
        public const string SetupRulesBody = "150 kills to win\n15 minute time limit";
        public const string SetupBriefing = "OPERATION BRIEFING";
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
        // ADR-123 Settings copy (was inline in the screen).
        public const string SettingsSound = "SOUND LEVELS";
        public const string SettingsMuteHint = "Set a channel to 0% to mute it.";
        public const string SettingsDisplay = "DISPLAY OUTPUT";
        public const string SettingsDisplayHint = "Display changes must be kept within 15 seconds. Otherwise your previous display is restored.";
        public const string SettingsLook = "LOOK AND AIM";
        public const string SettingsBindings = "BINDINGS / ESCAPE IS RESERVED FOR PAUSE";
        public const string SettingsReadability = "READABILITY";
        public const string SettingsKeep = "KEEP";
        public const string SettingsRevert = "REVERT";
        public const string SettingsApply = "APPLY";
        public const string SettingsDefaults = "DEFAULTS";
        public const string SettingsDefaultsStaged = "Defaults staged. Apply to save.";
        public const string SettingsUnsaved = "Unsaved changes. Apply to save.";
        public const string SettingsSaved = "Preferences are saved on this PC.";
        public const string SettingsHelpFooter = "Apply saves every tab at once. Cancel or Back discards the draft.";
        public const string BindingSwap = "SWAP";
        public const string BindingListening = "LISTENING...";
        public const string BindingReserved = "Escape / reserved";

        /// <summary>"KEEP THIS DISPLAY?   12 seconds remaining".</summary>
        public static string SettingsKeepDisplay(float seconds) =>
            "KEEP THIS DISPLAY?   " + seconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " seconds remaining";

        public static string BindingConflict(string action) => "Already assigned to " + action + ". Swap or cancel?";

        public static string BindingListen(string action) => "Press a key or mouse button for " + action + ". Escape cancels.";

        /// <summary>Player-facing name of a bindable action.</summary>
        public static string BindingLabel(string action) => action switch
        {
            "Slide" => "Slide (hold Sprint)",
            "LastWeapon" => "Last weapon",
            "Slot3" => "Knife",
            "Slot4" => "Grenade",
            "Pickup" => "Pick up weapon",
            _ => action
        };

        /// <summary>Help-pane text for a Settings tab (Audio, Video, Controls, Accessibility).</summary>
        public static string SettingsHelp(int tab) => tab switch
        {
            0 => "Balance the mix between gunfire and effects, interface cues and music. Changes play back as soon as they are applied.",
            1 => "Quality sets lighting, shadows and post-processing. Gameplay is identical on every tier. Resolution and display mode changes ask you to keep them.",
            2 => "Field of view and sensitivity apply to hip fire and aiming alike. Select an action to rebind it, then press the new key or mouse button.",
            _ => "Interface scale enlarges every menu and the HUD. Reduced motion stops camera sway and menu drift; reduced flash removes muzzle and explosion flashes. Captions show sound cues as text."
        };
        public const string RecoveryTitle = "Game content needs attention";
        public const string RecoveryRetry = "RETRY";
        public const string RecoveryQuit = "QUIT TO DESKTOP";

        public const string ResultsEmpty = "Match results";
        public const string ResultsPlayAgain = "PLAY AGAIN";
        public const string ResultsMain = "MAIN MENU";
        public const string ScoreboardTitle = "Scoreboard";
        public const string TeamDeathmatch = "TEAM DEATHMATCH";

        // ADR-123 shared chrome: footer key hints, loading template, match banners.
        public const string KeyTab = "TAB";
        public const string KeyEnter = "ENTER";
        public const string KeyEsc = "ESC";
        public const string KeyQe = "Q / E";
        public const string HintNavigate = "Navigate";
        public const string HintSelect = "Select";
        public const string HintBack = "Back";
        public const string HintResume = "Resume";
        public const string HintTabs = "Tabs";
        public const string LoadingActivity = "PREPARING THE BATTLEFIELD";
        public const string LoadingTip = "FIELD TIP";
        public const string LoadingCancel = "CANCEL";
        public const string MatchStartsIn = "MATCH STARTS IN";
        public const string MatchFight = "FIGHT!";
        public const string DebriefTitle = "Match debrief";
        public const string MainRankSection = "RANK";

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
        public const string ArmoryStatDamage = "DAMAGE";
        public const string ArmoryStatRate = "FIRE RATE";
        public const string ArmoryStatMagazine = "MAGAZINE";

        // ADR-108 Career page and debrief.
        public const string Career = "CAREER";
        public const string CareerEyebrow = "SERVICE RECORD";
        public const string CareerTitle = "Career";
        public const string CareerRankSection = "CURRENT RANK";
        public const string CareerLadderSection = "RANKS AND REWARDS";
        public const string CareerStatsSection = "SERVICE RECORD";
        public const string CareerTabProgress = "PROGRESS";
        public const string CareerTabStatistics = "STATISTICS";
        public const string CareerNextReward = "NEXT REWARD";
        public const string CareerTopRank = "Highest rank reached.";
        public const string CareerHint = "Finish matches to earn career points: your score, +500 for finishing, +1000 for a win (+500 draw).";
        public const string CareerSaveFailed = "Career could not be saved; progress applies to this session only.";
        public const string CareerActiveBuffs = "ACTIVE BUFFS";
        public const string CareerNoBuffs = "None yet.";
        public const string CareerPromoted = "PROMOTED";
        // ADR-113 progress line and reward cards.
        public const string CareerProgressLine = "PROGRESS LINE";
        public const string CareerUnlocked = "UNLOCKED";
        public const string CareerNextChip = "NEXT UNLOCK";
        public const string CareerCurrent = "CURRENT";
        public const string CareerRewardWeapon = "WEAPON";
        public const string CareerRewardSight = "OPTIC";
        public const string CareerRewardBuff = "PERK";
        public const string CareerRewardKit = "STARTER KIT";
        public const string CareerRewardFinal = "INSIGNIA";

        /// <summary>"RANK 14".</summary>
        public static string CareerRankChip(int rank) => "RANK " + rank;

        /// <summary>"PVT  ·  RANK 1 OF 20".</summary>
        public static string CareerRankOf(string code, int rank, int count) => code + "  ·  RANK " + rank + " OF " + count;

        /// <summary>"2,600 CP TO CORPORAL".</summary>
        public static string CareerToNext(long points, string rankName) => Thousands(points < 0 ? 0 : points) + " CP TO " + rankName.ToUpperInvariant();

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
