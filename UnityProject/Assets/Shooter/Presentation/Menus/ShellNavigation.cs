#nullable enable
using System;

namespace Shooter.Presentation.Menus
{
    /// <summary>Pure shell navigation. Menus own focus; Playing alone allows gameplay cursor/input.</summary>
    public sealed class ShellNavigation
    {
        public ShellScreen Screen { get; private set; } = ShellScreen.Main;
        public ShellScreen SettingsReturn { get; private set; } = ShellScreen.Main;
        public string StatusMessage { get; private set; } = "";
        public bool OwnsMenuFocus => Screen != ShellScreen.Playing;
        public bool AllowsGameplayInput => Screen == ShellScreen.Playing;
        public bool ScoreboardAllowed => Screen == ShellScreen.Playing || Screen == ShellScreen.Paused;

        public void ResetToMain(string? status = null)
        {
            Screen = ShellScreen.Main;
            SettingsReturn = ShellScreen.Main;
            StatusMessage = status ?? "";
        }

        public void OpenSetup() => Screen = ShellScreen.Setup;

        public void BeginLoading(string phaseText)
        {
            Screen = ShellScreen.Loading;
            StatusMessage = phaseText ?? "";
        }

        public void SetLoadingPhase(string phaseText) => StatusMessage = phaseText ?? "";

        public void EnterPlaying()
        {
            Screen = ShellScreen.Playing;
            StatusMessage = "";
        }

        public void EnterPaused()
        {
            if (Screen == ShellScreen.Playing || Screen == ShellScreen.ConfirmLeave || Screen == ShellScreen.ConfirmRestart)
                Screen = ShellScreen.Paused;
        }

        public void ResumePlaying()
        {
            if (Screen == ShellScreen.Paused || Screen == ShellScreen.Settings ||
                Screen == ShellScreen.ConfirmLeave || Screen == ShellScreen.ConfirmRestart)
            {
                Screen = ShellScreen.Playing;
                StatusMessage = "";
            }
        }

        public void EnterResults()
        {
            Screen = ShellScreen.Results;
            StatusMessage = "";
        }

        public void OpenSettings()
        {
            SettingsReturn = Screen == ShellScreen.Paused || Screen == ShellScreen.Main || Screen == ShellScreen.Setup
                ? Screen
                : ShellScreen.Main;
            Screen = ShellScreen.Settings;
        }

        public void CloseSettings() => Screen = SettingsReturn;

        public void AskLeave() => Screen = ShellScreen.ConfirmLeave;

        public void AskRestart() => Screen = ShellScreen.ConfirmRestart;

        public void CancelConfirm()
        {
            if (Screen == ShellScreen.ConfirmLeave || Screen == ShellScreen.ConfirmRestart)
                Screen = ShellScreen.Paused;
        }

        public void FailLoad(string message)
        {
            Screen = ShellScreen.Setup;
            StatusMessage = message ?? "Load failed.";
        }

        public void CancelLoad(string? message = null)
        {
            Screen = ShellScreen.Setup;
            StatusMessage = message ?? "Load cancelled.";
        }

        /// <summary>Authority-driven transitions: Finished → Results; focus/session pause → Paused while playing.</summary>
        public void SyncFromAuthority(bool matchFinished, bool sessionPaused)
        {
            if (matchFinished)
            {
                if (Screen == ShellScreen.Playing || Screen == ShellScreen.Paused ||
                    Screen == ShellScreen.ConfirmLeave || Screen == ShellScreen.ConfirmRestart ||
                    Screen == ShellScreen.Settings)
                {
                    EnterResults();
                }
                return;
            }

            if (sessionPaused && Screen == ShellScreen.Playing)
                EnterPaused();
        }
    }
}
