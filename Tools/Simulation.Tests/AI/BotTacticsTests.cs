#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.AI;
using Xunit;

namespace Shooter.Simulation.Tests.AI
{
    /// <summary>T082 combat behaviour: aim model, trigger discipline, strafing, stimuli, cover, callouts, determinism.</summary>
    public sealed class BotTacticsTests
    {
        /// <summary>Static axis-aligned boxes; <see cref="BlockAll"/> occludes every ray (one-tick dropouts).</summary>
        private sealed class BoxWorld : IWorldQueries
        {
            private readonly List<(Float3 Min, Float3 Max)> boxes = new List<(Float3, Float3)>();
            public bool BlockAll { get; set; }
            public int Rays { get; private set; }
            public BoxWorld Add(Float3 min, Float3 max) { boxes.Add((min, max)); return this; }
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
            public RayHit Raycast(Float3 origin, Float3 dir, float maxMeters, QueryFilter filter)
            {
                Rays++;
                float best = float.MaxValue;
                if (BlockAll) best = Math.Min(1f, maxMeters * 0.5f);
                foreach (var (min, max) in boxes)
                    if (Slab(origin, dir, min, max, out float t) && t <= maxMeters && t < best) best = t;
                if (best == float.MaxValue) return new RayHit(QueryStatus.NoHit);
                var p = new Float3(origin.X + dir.X * best, origin.Y + dir.Y * best, origin.Z + dir.Z * best);
                return new RayHit(QueryStatus.Hit, new QueryHit(best, p, new Float3(0, 1, 0), default, new SurfaceId(1), 7, 1, 1));
            }

            private static bool Slab(Float3 o, Float3 d, Float3 min, Float3 max, out float tEnter)
            {
                float t0 = 0, t1 = float.MaxValue;
                tEnter = 0;
                float[] os = { o.X, o.Y, o.Z }, ds = { d.X, d.Y, d.Z }, mins = { min.X, min.Y, min.Z }, maxs = { max.X, max.Y, max.Z };
                for (int a = 0; a < 3; a++)
                {
                    if (Math.Abs(ds[a]) < 1e-8f)
                    {
                        if (os[a] < mins[a] || os[a] > maxs[a]) return false;
                        continue;
                    }
                    float ta = (mins[a] - os[a]) / ds[a], tb = (maxs[a] - os[a]) / ds[a];
                    if (ta > tb) (ta, tb) = (tb, ta);
                    t0 = Math.Max(t0, ta);
                    t1 = Math.Min(t1, tb);
                    if (t0 > t1) return false;
                }
                tEnter = t0;
                return true;
            }
        }

        /// <summary>Straight-line nav: one corner at the goal; records every request.</summary>
        private sealed class LineNav : INavigation
        {
            private ulong next;
            private readonly Dictionary<ulong, Float3> goals = new Dictionary<ulong, Float3>();
            public List<Float3> Requested { get; } = new List<Float3>();
            public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
            {
                Requested.Add(destination);
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

        private static SenseTarget EnemyAt(float x, float z, ushort health = 100) =>
            new SenseTarget(Enemy, new Float3(x, CombatAiDirector.StandingAimHeight, z), team: 2, alive: true, health: health);

        private static bool Fires(InputCommand c) => (c.Buttons & InputButtons.FireHeld) != 0;

        [Fact]
        public void Aim_turns_at_a_bounded_rate_onto_an_off_axis_target()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            double a = 40 * Math.PI / 180;
            var enemies = new[] { EnemyAt((float)(15 * Math.Sin(a)), (float)(15 * Math.Cos(a))) };
            var cmds = new InputCommand[1];
            ai.PlanEnemies(1, enemies, 1, cmds, 0);
            float firstTurn = Math.Abs(cmds[0].AimYaw);
            Assert.True(firstTurn > 0 && firstTurn < 0.05f, "single-tick turn " + firstTurn);
            float maxStep = 0, prev = cmds[0].AimYaw;
            for (uint t = 2; t <= 45; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                maxStep = Math.Max(maxStep, Math.Abs(cmds[0].AimYaw - prev));
                prev = cmds[0].AimYaw;
            }
            Assert.True(maxStep <= AiTuning.CombatTurnRate * AiTuning.TickSeconds + 1e-4f, "step " + maxStep);
            Assert.InRange(cmds[0].AimYaw, (float)a - 0.12f, (float)a + 0.12f);
            Assert.True(bot.HasVisibleTarget);
        }

        [Fact]
        public void Aim_error_starts_wide_recovers_and_varies_between_bots()
        {
            double early = 0, late = 0;
            var lateValues = new HashSet<float>();
            for (ulong id = 1; id <= 24; id++)
            {
                var board = new AiBlackboard { Self = new EntityId(id), AcquiredTick = 1000 };
                AimController.ErrorOffset(board, 1000, 20f, 0, out float y0, out float p0);
                AimController.ErrorOffset(board, 1090, 20f, 0, out float y1, out float p1);
                early += Math.Sqrt(y0 * y0 + p0 * p0);
                double l = Math.Sqrt(y1 * y1 + p1 * p1);
                late += l;
                lateValues.Add((float)l);
            }
            Assert.True(early > late * 3, $"early {early} late {late}");
            Assert.True(lateValues.Count > 12, "error should be a distribution, not a constant");
            Assert.True(late / 24 > 0.001, "settled aim is never perfect");
        }

        [Fact]
        public void Bursts_only_start_when_aligned_and_mid_range_fire_pauses()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(8f, 18f) };
            var cmds = new InputCommand[1];
            int fireTicks = 0, quietTicks = 0, bursts = 0;
            bool firing = false, seenFire = false;
            for (uint t = 1; t <= 300; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                bool f = Fires(cmds[0]);
                if (f && bot.BurstStartTick == t)
                    Assert.True(bot.AimOffsetRadians <= AimController.FireTolerance(bot.TargetDistance) + 1e-5f);
                if (f && !firing) bursts++;
                firing = f;
                if (f) { fireTicks++; seenFire = true; }
                else if (seenFire) quietTicks++;
            }
            Assert.True(bursts >= 3, "bursts " + bursts);
            Assert.True(fireTicks > 0 && quietTicks > 0);
            Assert.True(ai.Counters.BurstsStarted >= 3);
        }

        [Fact]
        public void Close_range_fire_is_sustained()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(0f, 7f) };
            var cmds = new InputCommand[1];
            int fire = 0, total = 0;
            bool started = false;
            for (uint t = 1; t <= 240; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0);
                started |= Fires(cmds[0]);
                if (!started) continue;
                total++;
                if (Fires(cmds[0])) fire++;
            }
            Assert.True(total > 100);
            Assert.True(fire > total * 0.75, $"{fire}/{total}");
        }

        [Fact]
        public void Strafe_reverses_when_blocked()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(0f, 15f) };
            var cmds = new InputCommand[1];
            int left = 0, right = 0;
            for (uint t = 1; t <= 120; t++)
            {
                ai.PlanEnemies(t, enemies, 1, cmds, 0); // feet never move: every strafe leg is "blocked"
                if (cmds[0].MoveX > 0.9f) right++;
                if (cmds[0].MoveX < -0.9f) left++;
            }
            Assert.True(left > 10 && right > 10, $"left {left} right {right}");
        }

        [Fact]
        public void Damage_from_an_unseen_attacker_turns_the_bot_and_investigates()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            ai.ObserveDamage(bot.Self, Enemy, new Float3(0, 1.3f, -20));
            Assert.Equal(AiState.Investigate, bot.State);
            Assert.Equal(HeardClass.Damage, bot.Heard);
            Assert.True(Math.Abs(bot.LastHeard!.Value.Z + 20) <= AiTuning.DamageSourceNoiseMeters + 1e-4f);
            for (uint t = 2; t <= 40; t++) ai.PlanEnemies(t, null, 0, cmds, 0);
            Assert.True(Math.Abs(cmds[0].AimYaw) > 2.6f, "yaw " + cmds[0].AimYaw);
            Assert.True(ai.Counters.DamageReactions == 1);
        }

        [Fact]
        public void Damage_while_fighting_the_attacker_keeps_engaging_with_a_flinch()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(0, 15f) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 50; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            Assert.Equal(AiState.Engage, bot.State);
            ai.ObserveDamage(bot.Self, Enemy, new Float3(0, 1.3f, 15f));
            Assert.Equal(AiState.Engage, bot.State);
            Assert.True(bot.FlinchUntilTick > 50);
            AimController.ErrorOffset(bot, 51, 15f, 0, out float fy, out float fp);
            var calm = new AiBlackboard { Self = bot.Self, AcquiredTick = bot.AcquiredTick };
            AimController.ErrorOffset(calm, 51, 15f, 0, out float cy, out float cp);
            Assert.True(fy * fy + fp * fp > cy * cy + cp * cp);
        }

        [Fact]
        public void Gunfire_inside_hearing_radius_is_investigated_and_far_gunfire_is_ignored()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var near = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var far = ai.RegisterBot(new EntityId(5), new Float3(200, 0, 0), 0, team: 1);
            var cmds = new InputCommand[2];
            ai.PlanEnemies(1, null, 0, cmds, 0);
            ai.ObserveGunfire(Enemy, new Float3(20, 1.5f, 10));
            Assert.Equal(AiState.Investigate, near.State);
            Assert.Equal(HeardClass.Gunfire, near.Heard);
            Assert.Equal(AiState.Patrol, far.State);
            // Distance-attenuated estimate: within 8 % of the 22.4 m range on each axis.
            var heard = near.LastHeard!.Value;
            float bound = MathF.Sqrt(20 * 20 + 10 * 10) * AiTuning.GunfireNoisePerMeter + 1e-4f;
            Assert.True(Math.Abs(heard.X - 20) <= bound && Math.Abs(heard.Z - 10) <= bound);
            Assert.Equal(1, ai.Counters.StimuliHeard);
        }

        [Fact]
        public void Stimulus_replaces_an_active_patrol_route()
        {
            var nav = new LineNav();
            var ai = new CombatAiDirector(new BoxWorld(), nav, 1u << 8);
            ai.SetNavigationDestinations(new[] { new Float3(0, 0, 50) }, null);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 40; t++) ai.PlanEnemies(t, null, 0, cmds, 0);
            Assert.True(bot.PathCornerIndex < bot.PathCornerCount, "patrol route should still be active");
            ai.ObserveGunfire(Enemy, new Float3(-20, 1.5f, 0));
            for (uint t = 41; t <= 44; t++) ai.PlanEnemies(t, null, 0, cmds, 0);
            Assert.Equal(AiState.Investigate, bot.State);
            Assert.True(nav.Requested.Count >= 2);
            Assert.True(nav.Requested[nav.Requested.Count - 1].X < -15f);
        }

        [Fact]
        public void Low_health_losing_trade_retreats_to_a_probed_hidden_point()
        {
            // Threat 20 m north; a low box 2 m south hides a crouched bot 4 m south from the threat.
            var world = new BoxWorld().Add(new Float3(-1, 0, -2.5f), new Float3(1, 1.0f, -1.5f));
            var nav = new LineNav();
            var ai = new CombatAiDirector(world, nav, 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            bot.Health = 20;
            var enemies = new[] { EnemyAt(0, 20f, health: 100) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 60 && bot.CoverPoint == null; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            Assert.True(ai.Counters.Retreats >= 1);
            Assert.NotNull(bot.CoverPoint);
            Assert.Equal(-4f, bot.CoverPoint!.Value.Z, 3);
            Assert.True(ai.CoverBoard.IsClaimed(bot.CoverReservation!.Value));
            for (uint t = 61; t <= 64; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            Assert.Contains(nav.Requested, p => Math.Abs(p.Z + 4f) < 1e-3f);
            Assert.Equal(AiState.SeekCover, bot.State);

            // Death releases the dynamic cover claim.
            ulong claim = bot.CoverReservation!.Value;
            ai.NotifyDeath(bot.Self);
            Assert.False(ai.CoverBoard.IsClaimed(claim));
        }

        [Fact]
        public void Cover_probes_share_the_sixteen_ray_budget_with_eleven_bots()
        {
            var world = new BoxWorld();
            var ai = new CombatAiDirector(world, new LineNav(), 1u << 8);
            var enemies = new SenseTarget[11];
            for (int i = 0; i < 11; i++)
            {
                ai.RegisterBot(new EntityId((ulong)(i + 1)), new Float3(i * 3, 0, 0), 0, team: 1);
                enemies[i] = new SenseTarget(new EntityId((ulong)(100 + i)), new Float3(i * 3, 1.3f, 20), 2);
            }
            var cmds = new InputCommand[11];
            int probeRays = 0;
            for (uint t = 1; t <= 120; t++)
            {
                for (int i = 0; i < 11; i++)
                {
                    var b = ai.GetBoard(i);
                    if (!b.CoverProbeActive) CoverFinder.Start(b, new Float3(b.Feet.X, 1.3f, 20));
                }
                ai.PlanEnemies(t, enemies, enemies.Length, cmds, 0);
                Assert.True(ai.Counters.VisionRaysThisTick <= PerceptionSystem.MaxVisionRaysPerTick);
                // T090: strafe footing uses unbudgeted ground probes; the AI vision/cover/hearing budget is separate.
                Assert.True(ai.AiRaysUsedThisTick <= PerceptionSystem.MaxVisionRaysPerTick);
                for (int i = 0; i < 11; i++)
                    Assert.True(t - ai.GetBoard(i).LastSenseOpportunityTick < 12);
            }
            probeRays = ai.Counters.CoverProbeRays;
            Assert.True(probeRays > 0);
        }

        [Fact]
        public void Tactical_reload_waits_two_seconds_after_contact()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(0, 15f) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 40; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            uint lastSeen = bot.LastSeenTick;
            bot.MagAmmo = 8;
            uint reloadTick = 0;
            for (uint t = 41; t <= 400 && reloadTick == 0; t++)
            {
                ai.PlanEnemies(t, null, 0, cmds, 0);
                if (bot.State == AiState.Reload) reloadTick = t;
            }
            Assert.True(reloadTick > lastSeen + AiTuning.CombatMemoryTicks, $"reload {reloadTick} last seen {lastSeen}");
            Assert.NotEqual(InputButtons.None, cmds[0].Buttons & InputButtons.ReloadPressed);
            Assert.Equal(1, ai.Counters.TacticalReloads);
        }

        [Fact]
        public void First_sighting_calls_out_to_nearby_calm_allies()
        {
            var ai = new CombatAiDirector(new BoxWorld(), new LineNav(), 1u << 8);
            var spotter = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var ally = ai.RegisterBot(new EntityId(5), new Float3(5, 0, 0), 3.0f, team: 1); // facing away
            var stranger = ai.RegisterBot(new EntityId(6), new Float3(5, 0, -60), 3.0f, team: 1); // out of radius
            var enemies = new[] { EnemyAt(0, 20f) };
            var cmds = new InputCommand[3];
            ai.PlanEnemies(1, enemies, 1, cmds, 0);
            ai.PlanEnemies(2, enemies, 1, cmds, 0);
            Assert.True(spotter.HasVisibleTarget);
            Assert.Equal(AiState.Investigate, ally.State);
            Assert.Equal(HeardClass.Callout, ally.Heard);
            Assert.Equal(enemies[0].Point.Z, ally.LastHeard!.Value.Z);
            Assert.NotEqual(AiState.Investigate, stranger.State);
            Assert.True(ai.Counters.Callouts >= 1);
        }

        [Fact]
        public void One_tick_dropout_keeps_reaction_and_accuracy_state()
        {
            var world = new BoxWorld();
            var ai = new CombatAiDirector(world, new LineNav(), 1u << 8);
            var bot = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
            var enemies = new[] { EnemyAt(0, 15f) };
            var cmds = new InputCommand[1];
            for (uint t = 1; t <= 5; t++) ai.PlanEnemies(t, enemies, 1, cmds, 0);
            uint ready = bot.ReactionReadyTick, acquired = bot.AcquiredTick;
            world.BlockAll = true;
            ai.PlanEnemies(6, enemies, 1, cmds, 0);
            Assert.False(bot.HasVisibleTarget);
            Assert.False(Fires(cmds[0]));
            world.BlockAll = false;
            ai.PlanEnemies(7, enemies, 1, cmds, 0);
            Assert.True(bot.HasVisibleTarget);
            Assert.Equal(ready, bot.ReactionReadyTick);
            Assert.Equal(acquired, bot.AcquiredTick);
        }

        [Fact]
        public void Identical_inputs_produce_identical_command_streams()
        {
            static List<InputCommand> Run()
            {
                var world = new BoxWorld().Add(new Float3(4, 0, 4), new Float3(6, 2, 6));
                var ai = new CombatAiDirector(world, new LineNav(), 1u << 8);
                ai.SetNavigationDestinations(new[] { new Float3(0, 0, 30), new Float3(10, 0, 10) }, new[] { new Float3(5, 0, 20) });
                var a = ai.RegisterBot(new EntityId(4), new Float3(0, 0, 0), 0, team: 1);
                var b = ai.RegisterBot(new EntityId(7), new Float3(3, 0, 0), 0.5f, team: 1);
                var cmds = new InputCommand[2];
                var log = new List<InputCommand>();
                for (uint t = 1; t <= 400; t++)
                {
                    float z = 25f + 5f * (float)Math.Sin(t * 0.03);
                    var enemies = new[] { EnemyAt(2f * (float)Math.Cos(t * 0.05), z, (ushort)(100 - t / 10)) };
                    if (t == 120) ai.ObserveDamage(a.Self, Enemy, enemies[0].Point);
                    if (t == 200) ai.ObserveGunfire(Enemy, new Float3(-10, 1.5f, 5));
                    int n = ai.PlanEnemies(t, enemies, 1, cmds, 0);
                    for (int i = 0; i < n; i++) log.Add(cmds[i]);
                    // Feed the planned move back as a crude motor so positions evolve.
                    a.Feet = new Float3(a.Feet.X + cmds[0].MoveX * 0.05f, 0, a.Feet.Z + cmds[0].MoveY * 0.05f);
                    b.MagAmmo = Math.Max(0, b.MagAmmo - ((cmds[1].Buttons & InputButtons.FireHeld) != 0 ? 1 : 0));
                }
                return log;
            }
            var first = Run();
            var second = Run();
            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].EntityId, second[i].EntityId);
                Assert.Equal(first[i].MoveX, second[i].MoveX);
                Assert.Equal(first[i].MoveY, second[i].MoveY);
                Assert.Equal(first[i].AimYaw, second[i].AimYaw);
                Assert.Equal(first[i].AimPitch, second[i].AimPitch);
                Assert.Equal(first[i].Buttons, second[i].Buttons);
            }
        }
    }
}
