#nullable enable
namespace Shooter.Game.Contracts
{
    public enum GameMatchPhase : byte
    {
        Loading = 0,
        Ready = 1,
        Playing = 2,
        Finished = 3,
        Stopped = 4
    }

    public enum MatchOutcome : byte
    {
        None = 0,
        Team1 = 1,
        Team2 = 2,
        Draw = 3
    }

    public enum FinishReason : byte
    {
        None = 0,
        ScoreLimit = 1,
        TimeLimit = 2,
        Aborted = 3
    }

    public enum SessionPauseState : byte
    {
        Running = 0,
        Paused = 1
    }

    public enum GameErrorCode : byte
    {
        None = 0,
        InvalidRequest = 1,
        UnsupportedVersion = 2,
        MissingContent = 3,
        CorruptContent = 4,
        LimitExceeded = 5,
        Cancelled = 6,
        StaleSession = 7,
        Busy = 8,
        SpawnFailure = 9,
        StorageError = 10,
        InternalError = 11
    }

    /// <summary>Presentation display mode. Device adapters filter unsupported modes.</summary>
    public enum DisplayMode : byte
    {
        Windowed = 0,
        Borderless = 1,
        Fullscreen = 2
    }
}
