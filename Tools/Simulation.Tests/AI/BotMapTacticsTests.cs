#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>ADR-112: authored map cover, flanking instead of piling on, team-aware destinations and smooth tracking.</summary>
    public sealed class BotMapTacticsTests
    {
        private sealed class OpenWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
        }

        private sealed class LineNav : INavigation
        {
            private ulong next;
            private readonly Dictionary<ulong, Float3> goals = new Dictionary<ulong, Float3>();
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                var id = ++next;
                goals[id] = destination;
                return new PathTicket(new NavTicketId(id), actor, navRevision);
            }
            public void Cancel(PathTicket ticket) => goals.Remove(ticket.Id.Value);
            public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
            {
                if (!goals.TryGetValue(ticket.Id.Value, out var goal)) return new PathResult(PathStatus.Cancelled, 0, 0, ErrorCode.Cancelled);
                goals.Remove(ticket.Id.Value);
                callerOwned[0] = goal;
                return new PathResult(PathStatus.Complete, 1, 1, ErrorCode.None);
            }
        }

        private static readonly EntityId Enemy = new EntityId(99);

        private static SenseTarget At(float x, float z) =>
            new SenseTarget(Enemy, new Float3(x, CombatAiDirector.StandingAimHeight, z), team: 2);

        [Fact]
        public void Hide_spot_is_on_the_face_away_from_the_threat_and_slides_toward_the_bot()
        {
            var crate = new MapCover(7, new Float3(0, 0.5f, 0), new Float3(2, 1, 1), false);
            var fromSouth = CombatAiDirector.HideSpot(crate, new Float3(0, 0, -10), new Float3(5, 0, 3));
            Assert.Equal(1.1f, fromSouth.Z, 3);   // half depth 0.5 + 0.6 stand-off, on the north face
            Assert.Equal(0.6f, fromSouth.X, 3);   // slid toward the bot, inset 0.4 from the end
            Assert.Equal(0f, fromSouth.Y, 3);     // on the ground the box stands on

            var fromWest = CombatAiDirector.HideSpot(crate, new Float3(-10, 0, 0), new Float3(5, 0, 3));
            Assert.Equal(1.6f, fromWest.X, 3);    // east face
            // A long wall seen at 45 degrees is hidden behind its length, not its end.
            var wall = new MapCover(8, new Float3(0, 1, 0), new Float3(8, 2, 0.4f), true);
            var diagonal = CombatAiDirector.HideSpot(wall, new Float3(-6, 0, -6), new Float3(0, 0, 3));
            Assert.Equal(0.8f, diagonal.Z, 3);
        }

        [Fact]
        public void Engaged_bot_moves_to_map_cover_on_the_side_away_from_its_target()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var near = new MapCover(11, new Float3(3, 0.55f, 4), new Float3(2, 1.1f, 1), false);
            var far = new MapCover(12, new Float3(40, 0.55f, 4), new Float3(2, 1.1f, 1), false);
            var ahead = new MapCover(13, new Float3(0, 0.55f, 27), new Float3(2, 1.1f, 1), false); // next to the enemy
            ai.SetMapCovers(new[] { far, ahead, near });
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            var enemies = new[] { At(0, 30) };
            bool engaged = false;
            for (uint t = 1; t <= 400 && bot.State != AiState.SeekCover; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                engaged |= bot.State == AiState.Engage;
            }
            Assert.True(engaged);
            Assert.Equal(AiState.SeekCover, bot.State);
            Assert.Equal(11UL, bot.CoverReservation);
            Assert.True(bot.CoverPoint.HasValue);
            Assert.True(bot.CoverPoint!.Value.Z < near.Center.Z, "cover spot must be on the far side from the enemy");
            Assert.True(bot.CoverLow);
            Assert.Equal(1, ai.Counters.MapCoverClaims);
            Assert.Equal(1, ai.Counters.CoverRepositions);
        }

        [Fact]
        public void Low_cover_alternates_ducking_and_peeking()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var crate = new MapCover(11, new Float3(0, 0.55f, 2), new Float3(2, 1.1f, 1), false);
            ai.SetMapCovers(new[] { crate });
            var spot = CombatAiDirector.HideSpot(crate, new Float3(0, 0, 30), new Float3(0, 0, 0));
            var bot = ai.RegisterBot(new EntityId(1), spot, 0, team: 1);
            var cmds = new InputCommand[1];
            bool crouched = false, stood = false;
            // A bot already settled in its claimed low cover, still trading shots with an enemy to the north.
            Assert.True(ai.CoverBoard.TryClaim(11, bot.Self));
            bot.LastSeenEnemy = new Float3(0, 1.15f, 30);
            bot.CoverPoint = spot; bot.CoverReservation = 11; bot.CoverLow = true;
            bot.InCover = true; bot.InCoverSinceTick = 2;
            bot.State = AiState.SeekCover;
            for (uint t = 2; t < 2 + AiTuning.CoverPeekCycleTicks; t++)
            {
                bot.LastSeenTick = t; // still trading shots: the cover is held
                ai.PlanEnemies(t, null, 0, cmds, 0);
                if (bot.State != AiState.SeekCover) break;
                bool c = (cmds[0].Buttons & InputButtons.CrouchHeld) != 0;
                crouched |= c; stood |= !c;
            }
            Assert.True(crouched, "never ducked behind low cover");
            Assert.True(stood, "never rose to peek over low cover");
        }

        [Fact]
        public void Crowded_contact_sends_extra_bots_round_a_flank_not_down_the_same_line()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var contact = new Float3(20, 1.5f, 10);
            var ring = new List<Float3>();
            for (int k = 0; k < 12; k++)
            {
                double a = k * Math.PI / 6;
                ring.Add(new Float3(20 + 16 * (float)Math.Cos(a), 0, 10 + 16 * (float)Math.Sin(a)));
            }
            ai.SetNavigationDestinations(null, ring);
            var bots = new List<AiBlackboard>();
            for (ulong id = 1; id <= 5; id++) bots.Add(ai.RegisterBot(new EntityId(id), new Float3(id, 0, 0), 0, team: 1));
            var cmds = new InputCommand[5];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            ai.ObserveGunfire(Enemy, contact);

            int investigating = 0;
            var flanks = new List<Float3>();
            foreach (var b in bots)
            {
                if (b.State == AiState.Investigate) investigating++;
                if (!b.FlankGoal.HasValue) continue;
                Assert.Equal(AiState.Reposition, b.State);
                var f = b.FlankGoal.Value;
                // Beside the contact relative to the bot's approach (45-135 degrees), not on the straight line.
                float ax = b.Feet.X - contact.X, az = b.Feet.Z - contact.Z, al = MathF.Sqrt(ax * ax + az * az);
                float fx = f.X - contact.X, fz = f.Z - contact.Z, fl = MathF.Sqrt(fx * fx + fz * fz);
                float cos = (ax * fx + az * fz) / (al * fl);
                Assert.InRange(cos, -0.71f, 0.71f);
                flanks.Add(f);
            }
            Assert.Equal(AiTuning.InvestigateCrowdLimit, investigating);
            Assert.True(flanks.Count >= 2, "extra bots should flank, got " + flanks.Count);
            for (int i = 0; i < flanks.Count; i++)
                for (int j = 0; j < i; j++)
                {
                    float dx = flanks[i].X - flanks[j].X, dz = flanks[i].Z - flanks[j].Z;
                    Assert.True(dx * dx + dz * dz > 1f, "two flankers share one flank point");
                }
            Assert.True(ai.Counters.FlankMoves >= 2);
        }

        [Fact]
        public void Destinations_in_the_own_spawn_are_avoided_and_lean_toward_the_enemy_side()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(1), new Float3(0, 0, 0), 0, team: 1);
            ai.RegisterBot(new EntityId(2), new Float3(0, 0, 120), 0, team: 2);
            // The bot has walked 40 m out; every candidate is the same 35 m leg away, one of them back home.
            bot.Feet = new Float3(0, 0, 40);
            ai.SetNavigationDestinations(new[] { new Float3(0, 0, 5), new Float3(0, 0, 75) }, null);
            var cmds = new InputCommand[2];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            Assert.True(bot.PatrolGoal.HasValue);
            Assert.Equal(75f, bot.PatrolGoal!.Value.Z, 3);
        }

        [Fact]
        public void Engaged_bots_keep_their_target_through_budget_starved_ticks()
        {
            var ai = new CombatAiDirector(new OpenWorld(), new LineNav(), 1u << 8);
            var bots = new List<AiBlackboard>();
            // 24 bots facing one enemy: the 16-ray budget cannot look for every bot every tick.
            for (int i = 0; i < 24; i++)
                bots.Add(ai.RegisterBot(new EntityId((ulong)(i + 1)), new Float3(-12 + i, 0, 0), 0, team: 1));
            var cmds = new InputCommand[24];
            var enemies = new[] { At(0, 25) };
            var engagedSince = new uint[24];
            int drops = 0;
            for (uint t = 1; t <= 240; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                for (int i = 0; i < bots.Count; i++)
                {
                    var b = bots[i];
                    if (b.State != AiState.Engage) { engagedSince[i] = 0; continue; }
                    if (engagedSince[i] == 0) { engagedSince[i] = t; continue; }
                    if (!b.HasVisibleTarget) drops++;
                }
            }
            Assert.True(ai.Counters.VisibleHolds > 0, "the budget was never short; the test does not exercise the hold");
            Assert.Equal(0, drops);
        }
    }
}
