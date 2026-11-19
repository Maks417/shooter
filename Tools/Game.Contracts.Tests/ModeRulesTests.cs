#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Session;
using Shooter.Simulation.Match;
using Shooter.Simulation.TeamDeathmatch;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>ADR-122: mode rules are data, snapshots carry team-indexed kills, the session picks match rules by mode.</summary>
    public sealed class ModeRulesTests
    {
        [Fact]
        public void Team_deathmatch_rules_keep_the_game_v1_limits()
        {
            var tdm = ModeRules.TeamDeathmatch;
            Assert.Equal(WellKnownModes.TeamDeathmatch, tdm.Id);
            Assert.Equal(2, tdm.TeamCount);
            Assert.Equal(GameContractVersion.ScoreLimit, tdm.ScoreLimit);
            Assert.Equal(GameContractVersion.TimeLimitActiveTicks, tdm.TimeLimitActiveTicks);
            Assert.Same(tdm, ModeRules.All[0]);
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.Equal(150, request.ScoreLimit);
            Assert.Equal(54000, request.TimeLimitActiveTicks);
            Assert.Equal(10, request.ParticipantCount);
        }

        [Fact]
        public void Mode_rules_validate_team_count_and_limits()
        {
            var id = new ModeId("test-mode");
            Assert.ThrowsAny<ArgumentException>(() => new ModeRules(id, "Test", 1, 10, 100));
            Assert.ThrowsAny<ArgumentException>(() => new ModeRules(id, "Test", (byte)(GameContractVersion.MaxTeams + 1), 10, 100));
            Assert.ThrowsAny<ArgumentException>(() => new ModeRules(id, "Test", 2, 0, 100));
            Assert.False(ModeRules.TryGet(id, out var fallback));
            Assert.Same(ModeRules.TeamDeathmatch, fallback);
        }

        [Fact]
        public void Two_team_constructors_and_team_indexed_kills_agree()
        {
            var a = new MatchViewSnapshot(7, 1, 1, 1, GameMatchPhase.Playing, MatchOutcome.None, FinishReason.None, 12, 9,
                Array.Empty<ParticipantState>(), 0);
            var b = new MatchViewSnapshot(7, 1, 1, 1, GameMatchPhase.Playing, MatchOutcome.None, FinishReason.None, new uint[] { 12, 9 },
                Array.Empty<ParticipantState>(), 0);
            Assert.Equal(new uint[] { 12, 9 }, a.TeamKills);
            Assert.Equal(a.Team1Kills, b.Team1Kills);
            Assert.Equal(a.Team2Kills, b.Team2Kills);
            Assert.ThrowsAny<ArgumentException>(() => new MatchViewSnapshot(7, 1, 1, 1, GameMatchPhase.Playing, MatchOutcome.None,
                FinishReason.None, new uint[] { 1 }, Array.Empty<ParticipantState>(), 0));
        }

        [Fact]
        public async Task Session_uses_the_mode_factory_and_rejects_a_mode_without_rules()
        {
            int created = 0;
            using var session = new OfflineGameSession(StaticMapCatalog.CreateReleaseSeed(),
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()),
                allowFakeMapRuntime: true,
                matchRules: mode => { created++; return mode.Id == WellKnownModes.TeamDeathmatch ? new TeamDeathmatchMatch() : null; });
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.Equal(1, created);
            Assert.True(session.Start().Succeeded);
            Assert.Equal(new uint[] { 0, 0 }, session.TryAcquireSnapshot()!.TeamKills);

            using var noRules = new OfflineGameSession(StaticMapCatalog.CreateReleaseSeed(),
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()),
                allowFakeMapRuntime: true, matchRules: _ => null);
            var refused = await noRules.BeginLoadAsync(request, CancellationToken.None);
            Assert.False(refused.Succeeded);
            Assert.Equal("unsupported-mode", refused.Diagnostic);
        }

        [Fact]
        public void Default_wiring_knows_only_team_deathmatch()
        {
            Assert.IsType<TeamDeathmatchMatch>(OfflineGameSession.DefaultMatchRules(ModeRules.TeamDeathmatch));
            Assert.IsAssignableFrom<IMatchRules>(new TeamDeathmatchMatch());
        }
    }
}
