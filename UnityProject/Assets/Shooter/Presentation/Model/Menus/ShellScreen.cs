#nullable enable
namespace Shooter.Presentation.Menus
{
    /// <summary>T059 shell screens. Settings is a stub until T060 applies the full preference editor.</summary>
    public enum ShellScreen
    {
        Main = 0,
        Setup = 1,
        Loading = 2,
        Playing = 3,
        Paused = 4,
        Results = 5,
        Settings = 6,
        ConfirmLeave = 7,
        ConfirmRestart = 8,
        ContentRecovery = 9,
        /// <summary>ADR-107 Armory: choose the primary, its sight and the sidearm taken into every match.</summary>
        Armory = 10,
        /// <summary>ADR-108 Career: rank ladder, progress to the next rank and the lifetime service record.</summary>
        Career = 11
    }
}
