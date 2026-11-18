#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// ADR-108 career document (<c>shooter.career.v1</c>): the career points that set the player's rank plus a lightweight
    /// lifetime service record. Values only grow; every counter is non-negative. Kept in its own file beside the
    /// settings and Armory files so the frozen Game-v1 settings are untouched.
    /// </summary>
    public sealed class CareerProfile
    {
        public const string SchemaId = "shooter.career.v1";
        public const int Schema = 1;
        /// <summary>Upper bound for every stored counter (keeps arithmetic and JSON safely inside 53-bit integers).</summary>
        public const long MaxCounter = 1L << 50;

        public long CareerPoints { get; }
        public long Matches { get; }
        public long Wins { get; }
        public long Losses { get; }
        public long Draws { get; }
        public long Kills { get; }
        public long Deaths { get; }
        public long Assists { get; }
        public long Headshots { get; }
        public long ScoreTotal { get; }
        public long BestScore { get; }
        public long BestStreak { get; }
        /// <summary>Active match time in 60 Hz ticks.</summary>
        public long ActiveTicks { get; }

        public CareerProfile(long careerPoints = 0, long matches = 0, long wins = 0, long losses = 0, long draws = 0,
            long kills = 0, long deaths = 0, long assists = 0, long headshots = 0, long scoreTotal = 0, long bestScore = 0,
            long bestStreak = 0, long activeTicks = 0)
        {
            CareerPoints = Check(careerPoints, nameof(careerPoints));
            Matches = Check(matches, nameof(matches));
            Wins = Check(wins, nameof(wins));
            Losses = Check(losses, nameof(losses));
            Draws = Check(draws, nameof(draws));
            Kills = Check(kills, nameof(kills));
            Deaths = Check(deaths, nameof(deaths));
            Assists = Check(assists, nameof(assists));
            Headshots = Check(headshots, nameof(headshots));
            ScoreTotal = Check(scoreTotal, nameof(scoreTotal));
            BestScore = Check(bestScore, nameof(bestScore));
            BestStreak = Check(bestStreak, nameof(bestStreak));
            ActiveTicks = Check(activeTicks, nameof(activeTicks));
        }

        public static CareerProfile Empty => new CareerProfile();

        public int Rank => CareerLadder.RankFor(CareerPoints);
        public RankDefinition RankDefinition => CareerLadder.Get(Rank);

        public static long Clamp(long value) => value < 0 ? 0 : value > MaxCounter ? MaxCounter : value;

        public bool SameAs(CareerProfile? other) =>
            other != null && other.CareerPoints == CareerPoints && other.Matches == Matches && other.Wins == Wins && other.Losses == Losses
            && other.Draws == Draws && other.Kills == Kills && other.Deaths == Deaths && other.Assists == Assists
            && other.Headshots == Headshots && other.ScoreTotal == ScoreTotal && other.BestScore == BestScore
            && other.BestStreak == BestStreak && other.ActiveTicks == ActiveTicks;

        private static long Check(long value, string name)
        {
            if (value < 0 || value > MaxCounter) throw new ArgumentOutOfRangeException(name, "Career counters are 0..2^50.");
            return value;
        }
    }

    /// <summary>ADR-108 career persistence port (file in the product, memory in tests).</summary>
    public interface ICareerStore
    {
        GameOperationResult Load(out CareerProfile profile);
        GameOperationResult Save(CareerProfile profile);
    }

    public sealed class MemoryCareerStore : ICareerStore
    {
        public CareerProfile? Stored { get; set; }
        public int SaveCount { get; private set; }

        public GameOperationResult Load(out CareerProfile profile)
        {
            profile = Stored ?? CareerProfile.Empty;
            return GameOperationResult.Ok;
        }

        public GameOperationResult Save(CareerProfile profile)
        {
            Stored = profile ?? throw new ArgumentNullException(nameof(profile));
            SaveCount++;
            return GameOperationResult.Ok;
        }
    }
}
