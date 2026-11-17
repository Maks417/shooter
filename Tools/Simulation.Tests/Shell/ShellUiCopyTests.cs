#nullable enable
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class ShellUiCopyTests
    {
        [Fact]
        public void Pause_and_setup_copy_drop_schema_jargon()
        {
            Assert.Equal("ON HOLD", ShellUiCopy.PauseHold);
            Assert.DoesNotContain("schema", ShellUiCopy.PauseHint);
            Assert.Equal("Choose your fight", ShellUiCopy.SetupTitle);
            Assert.Equal("Finding a safe drop…", ShellUiCopy.AwaitingSpawn);
            Assert.Equal("YOU + 4 ALLIED BOTS\nVS 5 ENEMY BOTS", ShellUiCopy.SetupRosterLine(5));
            Assert.Equal("YOU + 7 ALLIED BOTS\nVS 8 ENEMY BOTS", ShellUiCopy.SetupRosterLine(8));
            Assert.Equal("YOU + 14 ALLIED BOTS\nVS 15 ENEMY BOTS", ShellUiCopy.SetupRosterLine(15));
        }
    }
}
