#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// T076 compact TDM arena for <c>training-compound</c> (docs/features/tdm-map-layouts.md, Map A).
    /// Team 1 plays from -Z, team 2 is the exact mirror image across z = 0. Left lane = firing range (long, capped
    /// sightline), middle = HQ building with plaza, right lane = barracks (close quarters, covered route).
    /// All gameplay boxes are emitted for z &lt; 0 (or symmetric about z = 0) and mirrored by the builder.
    /// </summary>
    public static class CompoundArenaPlanner
    {
        public const string GeneratorId = "environment.compound.arena";
        public const string GeneratorVersion = "2.0.0";
        public const ulong StableIdBase = 10000;

        private const float Thin = 0.4f;
        private const float WallFull = 3.0f;       // spawn wall, gate baffles
        private const float WallLane = 2.4f;       // lane separators, sheds, blocks
        private const float HalfCover = 1.1f;      // sandbags, planters, crates
        private const float GateX = 30f;
        private const float GateWidth = 4f;
        private const float SpawnWallZ = -37f;
        private const float InnerWallZ = -40.6f;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 44f, halfExtentZ: 52f,
            stagingMinX: -44f, stagingMaxX: 44f, stagingMinZ: -52f, stagingMaxZ: -38f,
            team1Anchor: new Float3(0f, 0f, -46f),
            team1Exits: new[] { new Float3(-GateX, 0f, SpawnWallZ), new Float3(0f, 0f, SpawnWallZ), new Float3(GateX, 0f, SpawnWallZ) },
            lanePoints: new[] { new Float3(-GateX, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(GateX, 0f, 0f) });

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);
            BuildStaging(b);
            BuildGates(b);
            BuildHeadquarters(b);
            BuildMidLane(b);
            BuildRangeLane(b);
            BuildSeparators(b);
            BuildBarracks(b);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }

        /// <summary>Threat point for cover on the team-1 half: straight toward the map centre line (peek points land on the own side).</summary>
        private static Float3 Toward(float x) => new Float3(x, 0f, 0f);

        private static void BuildStaging(ArenaBoxBuilder b)
        {
            // Spawn wall with one 4 m gate per lane.
            var gates = new[] { new ArenaGap(-GateX, GateWidth), new ArenaGap(0f, GateWidth), new ArenaGap(GateX, GateWidth) };
            b.WallAlongX(SpawnWallZ, -44f, 44f, Thin, WallFull, gates);

            // Dividers split the 88 m staging strip into three pockets (no long eye line along it). The passage
            // sits at the back wall so players leaving through a side gate are not in view of the centre pocket.
            var rear = new[] { new ArenaGap(-50f, 3f) };
            b.WallAlongZ(-15f, -52f, SpawnWallZ + 0.2f, Thin, WallLane, rear);
            // Right divider passage is offset from the left one so no eye line runs the whole strip.
            b.WallAlongZ(15f, -52f, SpawnWallZ + 0.2f, Thin, WallLane, new[] { new ArenaGap(-44.5f, 3f) });

            // Baffle wall 3.6 m behind the spawn wall: a 3 m corridor runs along the inside of the gates and the
            // team enters it only through passages offset from each gate. A player in or outside a gate sees a solid
            // 3 m wall, not the slots; only a narrow slanted fan reaches the staging floor.
            var inner = new[] { new ArenaGap(-26.5f, 3f), new ArenaGap(3.5f, 3f), new ArenaGap(26.5f, 3f) };
            b.WallAlongX(InnerWallZ, -44f, 44f, Thin, WallFull, inner);
        }

        private static void BuildGates(ArenaBoxBuilder b)
        {
            float[] gateXs = { -GateX, 0f, GateX };
            for (int i = 0; i < gateXs.Length; i++)
            {
                float g = gateXs[i];
                // Two half-cover crates in front of each gate.
                b.AddCover(g - 7f, -32.5f, 2.5f, HalfCover, 1.0f, Toward(g - 7f));
                b.AddCover(g + 7f, -32.5f, 2.5f, HalfCover, 1.0f, Toward(g + 7f));
            }
        }

        private static void BuildHeadquarters(ArenaBoxBuilder b)
        {
            // 16 x 12 shell, 3.5 m walls, four 2.4 m doors (N, S, E, W). The roof is non-gameplay.
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.North, 0f, 2.4f), new ArenaDoor(ArenaSide.South, 0f, 2.4f),
                new ArenaDoor(ArenaSide.East, 0f, 2.4f), new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            b.Shell(0f, 0f, 16f, 12f, Thin, 3.5f, doors, roof: true, mirror: false);
            // Two pillars on the E-W axis keep the centre (0,0) open but break the east-west eye line through the doors.
            b.AddGrounded(-3.5f, 0f, 1.2f, 1.2f, 3.5f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(3.5f, 0f, 1.2f, 1.2f, 3.5f, ArenaBoxBuilder.TagSolid, mirror: false);
        }

        private static void BuildMidLane(ArenaBoxBuilder b)
        {
            // Sheds break the north-south eye lines on the plaza flanks of the HQ.
            b.AddGrounded(-11.5f, -18f, 6f, 3f, WallLane);
            b.AddGrounded(11.5f, -18f, 6f, 3f, WallLane);
            // Axis block caps the N-S eye line through the HQ doors (path passes beside it).
            b.AddGrounded(0f, -21f, 3f, 1.5f, WallLane);
            // Plaza planters (half cover).
            b.AddCover(-12.5f, -9f, 4f, HalfCover, 1.2f, Toward(-12.5f));
            b.AddCover(12.5f, -9f, 4f, HalfCover, 1.2f, Toward(12.5f));
            b.AddCover(-6.5f, -25f, 4f, HalfCover, 1.2f, Toward(-6.5f));
            b.AddCover(6.5f, -25f, 4f, HalfCover, 1.2f, Toward(6.5f));
        }

        private static void BuildRangeLane(ArenaBoxBuilder b)
        {
            // Concrete blocks stagger across the lane so every column is capped within 60 m: B1 covers the outer
            // columns, B2 the inner ones, leaving 3+ m passages on alternating sides (a chicane).
            b.AddGrounded(-37.5f, -20f, 13f, 1.5f, WallLane);
            b.AddGrounded(-27.5f, -6.75f, 13f, 1.5f, WallLane);
            // Staggered sandbag rows roughly every 10 m, alternating sides.
            float[][] bags =
            {
                new[] { -39.5f, -30f }, new[] { -24.5f, -29f }, new[] { -27f, -24f },
                new[] { -39f, -13.5f }, new[] { -27f, -13.5f }, new[] { -38f, -3.5f }, new[] { -24f, -2.5f }
            };
            for (int i = 0; i < bags.Length; i++)
                b.AddCover(bags[i][0], bags[i][1], 5f, HalfCover, 0.8f, Toward(bags[i][0]));
        }

        private static void BuildSeparators(ArenaBoxBuilder b)
        {
            // Lane separators at x = +-18. One sally port per half at z = -18 (mirrored to +18). A U-shaped pocket on
            // the lane side is open only away from the centre, so crossing needs a detour: the nav grid cannot
            // collapse the three lane routes through the connectors, while players still rotate in a few steps.
            var port = new[] { new ArenaGap(-18f, 3f) };
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 18f;
                b.WallAlongZ(x, SpawnWallZ + 0.2f, 0f, Thin, WallLane, port);
                float endX = s * 22f;
                b.WallAlongX(-16.3f, Math.Min(x, endX), Math.Max(x, endX), Thin, WallLane);
                b.WallAlongZ(endX, -21.5f, -16.1f, Thin, WallLane);
            }
        }

        private static void BuildBarracks(ArenaBoxBuilder b)
        {
            // Two 12 x 22 buildings (z -30..-8 and the mirror). South end door x=29, north end door x=35, side door
            // toward mid at z=-19; partitions at z=-23.5 (door x=35) and z=-14.5 (door x=29) make a zig-zag of three
            // rooms, each with at least two exits.
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.South, -3f, 2.4f),
                new ArenaDoor(ArenaSide.North, 3f, 2.4f),
                new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            b.Shell(32.2f, -19f, 12f, 22f, Thin, 3.5f, doors, roof: true);
            b.WallAlongX(-23.5f, 26.4f, 38.0f, 0.3f, 3.0f, new[] { new ArenaGap(35.2f, 2.4f) });
            b.WallAlongX(-14.5f, 26.4f, 38.0f, 0.3f, 3.0f, new[] { new ArenaGap(29.2f, 2.4f) });
            // Interior furniture (low cover) in the long middle room.
            b.AddCover(32.2f, -19f, 4f, HalfCover, 1.0f, Toward(32.2f));

            // Courtyard: full cover plus half cover.
            b.AddCover(35.5f, -4.5f, 3f, 2.4f, 1.5f, Toward(35.5f));
            b.AddCover(29f, -3.5f, 3f, HalfCover, 1.0f, Toward(29f));
            // Alley sight breakers at the courtyard (lane stays >= 3 m wide beside them).
            b.AddGrounded(23.7f, 0f, 4.6f, 4f, WallLane, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(42.75f, 0f, 2.5f, 4f, WallLane, ArenaBoxBuilder.TagSolid, mirror: false);
            // East alley half cover.
            b.AddCover(41.5f, -13f, 2.5f, HalfCover, 1.0f, Toward(41.5f));
        }
    }
}
