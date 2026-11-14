#nullable enable
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class ShellNavigationTests
    {
        [Fact]
        public void Launch_defaults_to_main_with_menu_focus_and_no_gameplay_input()
        {
            var nav = new ShellNavigation();
            Assert.Equal(ShellScreen.Main, nav.Screen);
            Assert.True(nav.OwnsMenuFocus);
            Assert.False(nav.AllowsGameplayInput);
        }

        [Fact]
        public void Setup_load_play_pause_results_and_menu_roundtrip()
        {
            var nav = new ShellNavigation();
            nav.OpenSetup();
            nav.BeginLoading("loading");
            Assert.Equal(ShellScreen.Loading, nav.Screen);
            nav.EnterPlaying();
            Assert.True(nav.AllowsGameplayInput);
            nav.EnterPaused();
            Assert.Equal(ShellScreen.Paused, nav.Screen);
            Assert.False(nav.AllowsGameplayInput);
            nav.AskLeave();
            nav.CancelConfirm();
            Assert.Equal(ShellScreen.Paused, nav.Screen);
            nav.ResumePlaying();
            nav.SyncFromAuthority(matchFinished: true, sessionPaused: false);
            Assert.Equal(ShellScreen.Results, nav.Screen);
            nav.ResetToMain();
            Assert.Equal(ShellScreen.Main, nav.Screen);
        }

        [Fact]
        public void Load_failure_and_cancel_return_to_setup_recoverably()
        {
            var nav = new ShellNavigation();
            nav.OpenSetup();
            nav.BeginLoading("x");
            nav.FailLoad("missing package");
            Assert.Equal(ShellScreen.Setup, nav.Screen);
            Assert.Equal("missing package", nav.StatusMessage);
            nav.BeginLoading("x");
            nav.CancelLoad();
            Assert.Equal(ShellScreen.Setup, nav.Screen);
        }

        [Fact]
        public void Focus_loss_pause_while_playing_opens_pause_screen()
        {
            var nav = new ShellNavigation();
            nav.EnterPlaying();
            nav.SyncFromAuthority(matchFinished: false, sessionPaused: true);
            Assert.Equal(ShellScreen.Paused, nav.Screen);
        }
    }
}
