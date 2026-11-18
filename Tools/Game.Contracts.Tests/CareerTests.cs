#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>ADR-108 career ladder, award, file store, bot ranks and the human's buffed loadout.</summary>
    public sealed class CareerTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "shooter-career-" + Guid.NewGuid().ToString("N"));
        private string FilePath => Path.Combine(dir, "shooter-career.json");

        public CareerTests() => Directory.CreateDirectory(dir);
        public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

        // ------------------------------------------------------------ ladder

        [Fact]
        public void Ladder_has_twenty_ranks_on_the_quadratic_scale()
        {
            Assert.Equal(20, CareerLadder.Ranks.Count);
            for (int i = 0; i < CareerLadder.Ranks.Count; i++)
            {
                var rank = CareerLadder.Ranks[i];
                Assert.Equal(i + 1, rank.Index);
                Assert.Equal(1000L * ((long)rank.Index * rank.Index - 1), rank.Threshold);
                Assert.Equal("rank-" + rank.Index.ToString("00"), rank.IconName);
                if (i > 0) Assert.True(rank.Threshold > CareerLadder.Ranks[i - 1].Threshold);
            }
            Assert.Equal(0, CareerLadder.Get(1).Threshold);
            Assert.Equal(3000, CareerLadder.Get(2).Threshold);
            Assert.Equal(399000, CareerLadder.Get(20).Threshold);
        }

        [Fact]
        public void Reward_art_matches_the_ladder()
        {
            Assert.Equal(CareerLadder.RankCount, CareerRewardCatalog.All.Count);
            foreach (var rank in CareerLadder.Ranks)
            {
                var art = CareerRewardCatalog.For(rank);
                Assert.Equal(rank.Index, art.Rank);
                Assert.Equal(rank.RewardKind, art.Kind);
                Assert.False(string.IsNullOrEmpty(art.IconName));
                Assert.False(string.IsNullOrEmpty(art.Title));
                Assert.False(string.IsNullOrEmpty(art.Detail));
                if (rank.RewardKind == CareerRewardKind.Weapon) Assert.Equal("weapon." + rank.RewardId.Substring("weapons/".Length), art.IconName);
                if (rank.RewardKind == CareerRewardKind.Buff)
                {
                    Assert.StartsWith(art.FullTitle, rank.RewardLabel);
                    Assert.EndsWith(art.Detail, rank.RewardLabel, StringComparison.OrdinalIgnoreCase);
                    Assert.Equal(art.IconName, CareerRewardCatalog.BuffIcon(rank.RewardId));
                }
            }
            Assert.Equal("", CareerRewardCatalog.BuffIcon("buffs/unknown"));
        }

        [Fact]
        public void Every_rank_name_code_and_icon_is_unique()
        {
            var names = new HashSet<string>();
            var codes = new HashSet<string>();
            var icons = new HashSet<string>();
            foreach (var rank in CareerLadder.Ranks)
            {
                Assert.True(names.Add(rank.Name), rank.Name);
                Assert.True(codes.Add(rank.Code), rank.Code);
                Assert.True(icons.Add(rank.IconName), rank.IconName);
                Assert.False(string.IsNullOrEmpty(rank.RewardLabel));
            }
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(2999, 1)]
        [InlineData(3000, 2)]
        [InlineData(34999, 5)]
        [InlineData(35000, 6)]
        [InlineData(398999, 19)]
        [InlineData(399000, 20)]
        [InlineData(5000000, 20)]
        public void Rank_follows_career_points(long cp, int rank) => Assert.Equal(rank, CareerLadder.RankFor(cp));

        [Fact]
        public void Progress_runs_from_zero_to_one_inside_a_rank()
        {
            Assert.Equal(0f, CareerLadder.Progress01(3000));
            Assert.Equal(.5f, CareerLadder.Progress01(5500), 3);
            Assert.Equal(1f, CareerLadder.Progress01(399000));
            Assert.Null(CareerLadder.Next(20));
            Assert.Equal(2, CareerLadder.Next(1)!.Index);
        }

        [Fact]
        public void Rewards_name_real_catalog_guns_and_sights_and_every_optic_precedes_its_gun()
        {
            foreach (var rank in CareerLadder.Ranks)
            {
                if (rank.RewardKind == CareerRewardKind.Weapon)
                    Assert.True(PlayerLoadout.IsSelectable(new DefinitionId(rank.RewardId), WeaponCatalog.Require(new DefinitionId(rank.RewardId)).Family), rank.RewardId);
                if (rank.RewardKind == CareerRewardKind.Sight)
                    Assert.True(SightCatalog.IsSight(new DefinitionId(rank.RewardId)), rank.RewardId);
            }
            // Every reward gun's factory optic unlocks first; the starter M16 deliberately begins on iron sights.
            foreach (var gun in ArmoryCatalog.Primaries)
            {
                if (gun.ToString() == CareerLadder.StarterPrimary) continue;
                int gunRank = CareerLadder.UnlockRankFor(gun.ToString());
                int opticRank = CareerLadder.UnlockRankFor(SightCatalog.DefaultFor(gun).ToString());
                Assert.True(opticRank <= gunRank, gun + " arrives before its factory optic");
            }
            Assert.Equal(1, CareerLadder.UnlockRankFor(CareerLadder.StarterPrimary));
            Assert.Equal(1, CareerLadder.UnlockRankFor(CareerLadder.StarterSidearm));
            Assert.Equal(1, CareerLadder.UnlockRankFor(CareerLadder.StarterSight));
            Assert.Equal(15, CareerLadder.UnlockRankFor("weapons/mtac338"));
            Assert.Equal(14, CareerLadder.UnlockRankFor("sights/scope8"));
            Assert.True(CareerLadder.IsUnlocked("weapons/m16", 1));
            Assert.False(CareerLadder.IsUnlocked("weapons/ak47", 7));
            Assert.True(CareerLadder.IsUnlocked("weapons/ak47", 8));
        }

        [Fact]
        public void Every_armory_gun_and_mountable_sight_unlocks_somewhere_on_the_ladder()
        {
            var unlockable = new HashSet<string> { CareerLadder.StarterPrimary, CareerLadder.StarterSidearm, CareerLadder.StarterSight };
            foreach (var rank in CareerLadder.Ranks) if (rank.RewardId.Length > 0) unlockable.Add(rank.RewardId);
            foreach (var gun in ArmoryCatalog.Primaries) Assert.Contains(gun.ToString(), unlockable);
            foreach (var gun in ArmoryCatalog.Sidearms) Assert.Contains(gun.ToString(), unlockable);
            foreach (var options in SightCatalog.All)
                foreach (var sight in options.Allowed) Assert.Contains(sight.ToString(), unlockable);
        }

        [Fact]
        public void Buffs_stack_by_tier_and_respect_the_caps()
        {
            Assert.True(CareerLadder.BuffsFor(1).IsNone);
            Assert.True(CareerLadder.BuffsFor(2).IsNone);
            Assert.Equal(920, CareerLadder.BuffsFor(3).ReloadPerMille);
            var mid = CareerLadder.BuffsFor(12);
            Assert.Equal(new CareerBuffs(920, 920, 920, 1000, 1), mid);
            var top = CareerLadder.BuffsFor(20);
            Assert.Equal(new CareerBuffs(850, 850, 850, 800, 2), top);
            Assert.Equal(top, CareerLadder.BuffsFor(19));
            for (int r = 1; r <= 20; r++)
            {
                var b = CareerLadder.BuffsFor(r);
                Assert.InRange(b.RecoilPerMille, WeaponModifiers.MinScale, 1000);
                Assert.InRange(b.ReloadPerMille, WeaponModifiers.MinScale, 1000);
                Assert.InRange(b.AdsSpreadPerMille, WeaponModifiers.MinScale, 1000);
                Assert.InRange(b.EquipPerMille, WeaponModifiers.MinEquipScale, 1000);
                Assert.InRange(b.ExtraMagazines, 0, WeaponModifiers.MaxExtraMagazines);
            }
        }

        // ------------------------------------------------------------ award

        [Fact]
        public void Win_awards_score_plus_finish_plus_win_and_fills_the_service_record()
        {
            var award = CareerAward.Apply(CareerProfile.Empty, Results(MatchOutcome.Team1, humanTeam: 1, score: 1740, kills: 15, deaths: 8, ticks: 54000),
                new CareerMatchExtras(4, 3, 6))!;
            Assert.Equal(CareerMatchOutcome.Win, award.Outcome);
            Assert.Equal(1740 + 500 + 1000, award.Total);
            var p = award.After;
            Assert.Equal(3240, p.CareerPoints);
            Assert.Equal(1, p.Matches);
            Assert.Equal(1, p.Wins);
            Assert.Equal(15, p.Kills);
            Assert.Equal(8, p.Deaths);
            Assert.Equal(4, p.Assists);
            Assert.Equal(3, p.Headshots);
            Assert.Equal(1740, p.BestScore);
            Assert.Equal(6, p.BestStreak);
            Assert.Equal(54000, p.ActiveTicks);
            Assert.True(award.Promoted);
            Assert.Equal(2, award.RankAfter);
            Assert.Single(award.NewRanks);
        }

        [Fact]
        public void Loss_and_draw_use_the_human_team_and_skip_the_win_bonus()
        {
            var loss = CareerAward.Apply(CareerProfile.Empty, Results(MatchOutcome.Team1, humanTeam: 2, score: 900), default)!;
            Assert.Equal(CareerMatchOutcome.Loss, loss.Outcome);
            Assert.Equal(1400, loss.Total);
            Assert.Equal(1, loss.After.Losses);
            var draw = CareerAward.Apply(CareerProfile.Empty, Results(MatchOutcome.Draw, humanTeam: 1, score: 0), default)!;
            Assert.Equal(CareerMatchOutcome.Draw, draw.Outcome);
            Assert.Equal(1000, draw.Total);
            Assert.Equal(1, draw.After.Draws);
        }

        [Fact]
        public void Aborted_matches_and_results_without_a_human_earn_nothing()
        {
            Assert.Null(CareerAward.Apply(CareerProfile.Empty, Results(MatchOutcome.Team1, 1, 500, reason: FinishReason.Aborted), default));
            Assert.Null(CareerAward.Apply(CareerProfile.Empty, Results(MatchOutcome.Team1, 1, 500, human: false), default));
        }

        [Fact]
        public void A_big_match_can_jump_several_ranks_and_best_values_only_rise()
        {
            var start = new CareerProfile(careerPoints: 2900, matches: 3, bestScore: 9000, bestStreak: 12);
            var award = CareerAward.Apply(start, Results(MatchOutcome.Team1, 1, 6000), new CareerMatchExtras(0, 0, 4))!;
            Assert.Equal(1, award.RankBefore);
            Assert.Equal(3, award.RankAfter);
            Assert.Equal(new[] { 2, 3 }, new[] { award.NewRanks[0].Index, award.NewRanks[1].Index });
            Assert.Equal(9000, award.After.BestScore);
            Assert.Equal(12, award.After.BestStreak);
        }

        [Fact]
        public void Recorder_applies_each_finished_match_once()
        {
            var recorder = new CareerRecorder(CareerProfile.Empty);
            var results = Results(MatchOutcome.Team1, 1, 1000, sessionId: 1001);
            Assert.True(recorder.TryRecord(1, results, default));
            Assert.False(recorder.TryRecord(1, results, default));
            Assert.Equal(2500, recorder.Profile.CareerPoints);
            Assert.Equal(1001UL, recorder.LastAwardSessionId);
            // The next load epoch reuses session ids across app runs, so the epoch is part of the key.
            Assert.True(recorder.TryRecord(2, Results(MatchOutcome.Team2, 1, 0, sessionId: 1002), default));
            Assert.Equal(3000, recorder.Profile.CareerPoints);
            Assert.Equal(2, recorder.Profile.Matches);
        }

        // ------------------------------------------------------------ file

        [Fact]
        public void Json_round_trips_every_counter()
        {
            var profile = new CareerProfile(36500, 9, 5, 3, 1, 141, 98, 37, 22, 23500, 3820, 7, 432000);
            byte[] bytes = CareerJson.Encode(profile);
            Assert.Equal("{\"schema\":1,\"schemaId\":\"shooter.career.v1\",\"careerPoints\":36500,\"matches\":9,\"wins\":5,\"losses\":3,\"draws\":1,"
                + "\"kills\":141,\"deaths\":98,\"assists\":37,\"headshots\":22,\"scoreTotal\":23500,\"bestScore\":3820,\"bestStreak\":7,\"activeTicks\":432000}",
                Encoding.UTF8.GetString(bytes));
            Assert.True(CareerJson.TryDecode(bytes, out var decoded, out _));
            Assert.True(profile.SameAs(decoded));
        }

        [Fact]
        public void Fixtures_decode_and_the_invalid_one_is_rejected()
        {
            string root = RepoRoot();
            Assert.True(CareerJson.TryDecode(File.ReadAllBytes(Path.Combine(root, "docs", "career-fixtures", "career.valid.json")), out var valid, out string ok), ok);
            Assert.Equal(6, valid!.Rank);
            Assert.Equal(9, valid.Matches);
            Assert.False(CareerJson.TryDecode(File.ReadAllBytes(Path.Combine(root, "docs", "career-fixtures", "career.invalid-negative.json")), out _, out string bad));
            Assert.Equal("bad-careerPoints", bad);
        }

        [Theory]
        [InlineData("", "empty")]
        [InlineData("{\"schema\":1,\"schemaId\":\"other\"}", "unsupported-schema")]
        [InlineData("{\"schema\":2,\"schemaId\":\"shooter.career.v2\"}", "newer-2")]
        [InlineData("{\"schema\":1,\"schemaId\":\"shooter.career.v1\",\"kills\":1.5}", "bad-kills")]
        [InlineData("{\"schema\":1,\"schemaId\":\"shooter.career.v1\",\"matches\":\"3\"}", "bad-matches")]
        [InlineData("{\"schema\":1,\"schemaId\":\"shooter.career.v1\",\"careerPoints\":9999999999999999}", "bad-careerPoints")]
        public void Decoder_rejects_bad_documents(string text, string diagnostic)
        {
            Assert.False(CareerJson.TryDecode(Encoding.UTF8.GetBytes(text), out _, out string actual));
            Assert.Equal(diagnostic, actual);
        }

        [Fact]
        public void Missing_counters_read_as_zero_and_unknown_keys_are_ignored()
        {
            Assert.True(CareerJson.TryDecode(Encoding.UTF8.GetBytes("{\"schema\":1,\"schemaId\":\"shooter.career.v1\",\"kills\":4,\"future\":true}"), out var p, out _));
            Assert.Equal(4, p!.Kills);
            Assert.Equal(0, p.CareerPoints);
            Assert.Equal(1, p.Rank);
        }

        [Fact]
        public void File_store_saves_atomically_and_recovers_from_corruption()
        {
            var store = new FileCareerStore(FilePath);
            Assert.True(store.Load(out var initial).Succeeded);
            Assert.True(CareerProfile.Empty.SameAs(initial));
            var sample = new CareerProfile(careerPoints: 15000, matches: 4, kills: 60);
            Assert.True(store.Save(sample).Succeeded);
            Assert.True(File.Exists(FilePath + ".last-good"));
            Assert.False(File.Exists(FilePath + ".tmp"));
            Assert.True(new FileCareerStore(FilePath).Load(out var loaded).Succeeded);
            Assert.True(sample.SameAs(loaded));

            File.WriteAllText(FilePath, "{ not json");
            var result = new FileCareerStore(FilePath).Load(out var recovered);
            Assert.False(result.Succeeded);
            Assert.Equal("corrupt-recovered-last-good", result.Diagnostic);
            Assert.True(sample.SameAs(recovered));
            Assert.NotEmpty(Directory.GetFiles(dir, "shooter-career.json.corrupt-*"));
        }

        [Fact]
        public void File_store_never_overwrites_a_newer_document()
        {
            const string newer = "{\"schema\":4,\"schemaId\":\"shooter.career.v4\"}";
            File.WriteAllText(FilePath, newer);
            var store = new FileCareerStore(FilePath);
            Assert.Equal(GameErrorCode.UnsupportedVersion, store.Load(out var profile).Error);
            Assert.True(CareerProfile.Empty.SameAs(profile));
            Assert.False(store.Save(new CareerProfile(careerPoints: 1)).Succeeded);
            Assert.Equal(newer, File.ReadAllText(FilePath));
        }

        // ------------------------------------------------------------ bots

        [Fact]
        public void Bot_ranks_are_seeded_stable_and_near_the_player()
        {
            for (int player = 1; player <= 20; player++)
                for (uint bot = 2; bot <= 30; bot++)
                {
                    int rank = BotRanks.Resolve(bot, 460072030UL, player);
                    Assert.Equal(rank, BotRanks.Resolve(bot, 460072030UL, player));
                    Assert.InRange(rank, Math.Max(1, player - BotRanks.Spread), Math.Min(20, player + BotRanks.Spread));
                }
            var seen = new HashSet<int>();
            for (uint bot = 2; bot <= 30; bot++) seen.Add(BotRanks.Resolve(bot, 7UL, 10));
            Assert.True(seen.Count >= 4, "bot ranks should vary across a lobby");
        }

        // ------------------------------------------------------------ session

        [Fact]
        public async Task Career_buffs_reach_the_human_and_never_the_bots()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog,
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()),
                allowFakeMapRuntime: true);
            session.UseArmoryLoadout(new DefinitionId("weapons/m16"), new DefinitionId("sights/irons"), new DefinitionId("weapons/g17"),
                850, 850, 850, 800, 2);
            session.CareerRank = 18;
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle);
            Assert.True(lifecycle.Simulation.TryGetWeapon(session.HumanEntityId, 0, out var primary));
            Assert.True(lifecycle.Simulation.TryGetWeapon(session.HumanEntityId, 1, out var sidearm));
            Assert.Equal(90 + 2 * 30, primary.Reserve);
            Assert.Equal(51 + 2 * 17, sidearm.Reserve);
            Assert.True(lifecycle.TryGetActorState(session.HumanEntityId, out var humanState));
            Assert.Equal(150, humanState.Reserve);

            var snap = session.TryAcquireSnapshot()!;
            foreach (var p in snap.Participants)
            {
                if (p.IsHuman)
                {
                    Assert.Equal(18, session.DisplayRankOf(p.ParticipantId, true));
                    continue;
                }
                Assert.True(lifecycle.Simulation.TryGetWeapon(p.EntityId, 0, out var botGun));
                Assert.Equal(WeaponCatalog.RequireDefinition(botGun.DefinitionId).ReserveCapacity, botGun.Reserve);
                Assert.InRange(session.DisplayRankOf(p.ParticipantId, false), 15, 20);
            }
            Assert.Equal(new LocalMatchTally(0, 0, 0).Assists, session.LocalTally.Assists);
        }

        // ------------------------------------------------------------ helpers

        private static MatchResults Results(MatchOutcome outcome, byte humanTeam, uint score, uint kills = 0, uint deaths = 0, uint ticks = 6000,
            FinishReason reason = FinishReason.ScoreLimit, bool human = true, ulong sessionId = 1001)
        {
            var rows = new[]
            {
                new ParticipantState(new ParticipantId(1), "Player", humanTeam, human, new EntityId(1), new LifeId(1), true, kills, deaths, 0, score),
                new ParticipantState(new ParticipantId(2), "Nora", humanTeam == 1 ? (byte)2 : (byte)1, false, new EntityId(2), new LifeId(1), true, 3, 4, 0, 300),
            };
            return new MatchResults(new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL),
                WellKnownMaps.TrainingCompound, "catalog", "gameplay", "build", sessionId, 0, ticks, ticks, 150, 120, outcome, reason, rows);
        }

        private static string RepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "toolchain-lock.json"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }
}
