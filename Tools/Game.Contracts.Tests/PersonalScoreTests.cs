#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Simulation.TeamDeathmatch;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class PersonalScoreTests
    {
        [Fact]
        public void Enemy_kill_awards_exactly_one_hundred_personal_score()
        {
            var match = CreateMatch(out var roster);
            match.MarkPlaying(21, 0);
            var killer = Find(roster, 1);
            var victim = Find(roster, 2);
            match.ProcessDeath(Death(21, killer.EntityId, victim.EntityId, victim.LifeId, 1));
            var snap = match.AcquireSnapshot(21, 1);
            var killerRow = FindById(snap, killer.ParticipantId);
            Assert.Equal(1u, killerRow.Kills);
            Assert.Equal((uint)GameContractVersion.PersonalKillPoints, killerRow.Score);
            Assert.Equal(1u, snap.Team1Kills);
        }

        [Fact]
        public void Assist_awards_damage_points_without_kill_credit()
        {
            var match = CreateMatch(out var roster);
            match.MarkPlaying(22, 0);
            var assister = Find(roster, 1);
            var killer = FindOther(roster, 1, assister.ParticipantId);
            var victim = Find(roster, 2);
            match.AddScore(assister.EntityId, 65);
            match.ProcessDeath(Death(22, killer.EntityId, victim.EntityId, victim.LifeId, 1));
            var snap = match.AcquireSnapshot(22, 1);
            Assert.Equal(65u, FindById(snap, assister.ParticipantId).Score);
            Assert.Equal(0u, FindById(snap, assister.ParticipantId).Kills);
            Assert.Equal((uint)GameContractVersion.PersonalKillPoints, FindById(snap, killer.ParticipantId).Score);
            Assert.Equal(1u, FindById(snap, killer.ParticipantId).Kills);
            Assert.Equal(1u, snap.Team1Kills);
        }

        [Fact]
        public void Environmental_death_awards_damager_points_without_team_kill()
        {
            var match = CreateMatch(out var roster);
            match.MarkPlaying(23, 0);
            var damager = Find(roster, 1);
            var victim = Find(roster, 2);
            match.AddScore(damager.EntityId, 40);
            match.ProcessDeath(new DeathResolution(
                23,
                new EventId(23, 1, victim.EntityId, 1),
                victim.EntityId,
                victim.LifeId,
                null,
                1));
            var snap = match.AcquireSnapshot(23, 1);
            Assert.Equal(40u, FindById(snap, damager.ParticipantId).Score);
            Assert.Equal(0u, FindById(snap, damager.ParticipantId).Kills);
            Assert.Equal(0u, snap.Team1Kills);
            Assert.Equal(1u, FindById(snap, victim.ParticipantId).Deaths);
        }

        [Fact]
        public void Duplicate_death_does_not_double_kill_score()
        {
            var match = CreateMatch(out var roster);
            match.MarkPlaying(24, 0);
            var killer = Find(roster, 1);
            var victim = Find(roster, 2);
            var death = Death(24, killer.EntityId, victim.EntityId, victim.LifeId, 1);
            match.ProcessDeath(death);
            match.ProcessDeath(death);
            var snap = match.AcquireSnapshot(24, 1);
            Assert.Equal((uint)GameContractVersion.PersonalKillPoints, FindById(snap, killer.ParticipantId).Score);
            Assert.Equal(1u, FindById(snap, killer.ParticipantId).Kills);
            Assert.Equal(1u, snap.Team1Kills);
        }

        [Fact]
        public void Results_sort_by_score_then_kills()
        {
            var match = CreateMatch(out var roster);
            match.MarkPlaying(25, 0);
            var highScore = Find(roster, 1);
            var mid = FindOther(roster, 1, highScore.ParticipantId);
            var victimA = Find(roster, 2);
            var victimB = FindOther(roster, 2, victimA.ParticipantId);
            match.AddScore(highScore.EntityId, 250);
            match.ProcessDeath(Death(25, mid.EntityId, victimA.EntityId, victimA.LifeId, 1));
            match.ProcessDeath(Death(25, mid.EntityId, victimB.EntityId, victimB.LifeId, 2));
            for (uint i = 0; i < (uint)GameContractVersion.TimeLimitActiveTicks; i++)
                match.EvaluateEndOfTick(i, true);
            var results = match.TryAcquireResults();
            Assert.NotNull(results);
            Assert.Equal(250u, results!.FinalRows[0].Score);
            Assert.Equal(highScore.ParticipantId, results.FinalRows[0].ParticipantId);
            Assert.True(results.FinalRows[1].Score <= results.FinalRows[0].Score);
        }

        private static TeamDeathmatchMatch CreateMatch(out List<ParticipantState> roster)
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            Assert.True(catalog.TryGetGameplay(WellKnownMaps.TrainingCompound, out var map));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 1UL);
            roster = new List<ParticipantState>();
            uint id = 1;
            for (byte team = 1; team <= 2; team++)
            {
                for (int t = 0; t < 5; t++)
                {
                    roster.Add(new ParticipantState(
                        new ParticipantId(id),
                        "P" + id,
                        team,
                        team == 1 && t == 0,
                        new EntityId(id),
                        new LifeId(1),
                        true,
                        0,
                        0,
                        0));
                    id++;
                }
            }
            var match = new TeamDeathmatchMatch();
            match.ConfigureIdentities("catalog", "gameplay", "build");
            match.Begin(request, roster, map!);
            return match;
        }

        private static DeathResolution Death(ulong session, EntityId killer, EntityId victim, LifeId life, uint tick) =>
            new DeathResolution(session, new EventId(session, tick, victim, 1), victim, life, killer, tick);

        private static ParticipantState Find(List<ParticipantState> roster, byte team)
        {
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Team == team) return roster[i];
            throw new InvalidOperationException("team missing");
        }

        private static ParticipantState FindOther(List<ParticipantState> roster, byte team, ParticipantId skip)
        {
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Team == team && !roster[i].ParticipantId.Equals(skip)) return roster[i];
            throw new InvalidOperationException("other missing");
        }

        private static ParticipantState FindById(MatchViewSnapshot snap, ParticipantId id)
        {
            for (int i = 0; i < snap.Participants.Count; i++)
                if (snap.Participants[i].ParticipantId.Equals(id)) return snap.Participants[i];
            throw new InvalidOperationException("id missing");
        }
    }
}
