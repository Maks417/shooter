#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Immutable compact-TDM arena descriptor (T076). The playable rectangle is centred at the origin,
    /// team 1 stages at -Z and team 2 is the mirror image across z = 0.
    /// </summary>
    public sealed class ArenaLayout
    {
        public float HalfExtentX { get; }
        public float HalfExtentZ { get; }
        /// <summary>Team-1 staging rectangle (team 2 is its mirror across z = 0).</summary>
        public float StagingMinX { get; }
        public float StagingMaxX { get; }
        public float StagingMinZ { get; }
        public float StagingMaxZ { get; }
        public Float3 Team1Anchor { get; }
        public Float3 Team2Anchor { get; }
        public IReadOnlyList<Float3> Team1Exits { get; }
        public IReadOnlyList<Float3> Team2Exits { get; }
        public IReadOnlyList<Float3> LanePoints { get; }
        public Float3 Center => new Float3(0f, 0f, 0f);

        public ArenaLayout(
            float halfExtentX,
            float halfExtentZ,
            float stagingMinX,
            float stagingMaxX,
            float stagingMinZ,
            float stagingMaxZ,
            Float3 team1Anchor,
            IReadOnlyList<Float3> team1Exits,
            IReadOnlyList<Float3> lanePoints)
        {
            if (team1Exits == null) throw new ArgumentNullException(nameof(team1Exits));
            if (lanePoints == null) throw new ArgumentNullException(nameof(lanePoints));
            if (halfExtentX <= 0f || halfExtentZ <= 0f) throw new ArgumentOutOfRangeException(nameof(halfExtentX));
            if (stagingMinX >= stagingMaxX || stagingMinZ >= stagingMaxZ) throw new ArgumentException("Staging rectangle is empty.");
            if (stagingMaxZ >= 0f) throw new ArgumentException("Team-1 staging must lie entirely at -Z.");
            HalfExtentX = halfExtentX; HalfExtentZ = halfExtentZ;
            StagingMinX = stagingMinX; StagingMaxX = stagingMaxX; StagingMinZ = stagingMinZ; StagingMaxZ = stagingMaxZ;
            Team1Anchor = team1Anchor;
            Team2Anchor = MirrorZ(team1Anchor);
            var t1 = new List<Float3>(team1Exits);
            var t2 = new List<Float3>(t1.Count);
            for (int i = 0; i < t1.Count; i++) t2.Add(MirrorZ(t1[i]));
            Team1Exits = t1; Team2Exits = t2;
            LanePoints = new List<Float3>(lanePoints);
        }

        public static Float3 MirrorZ(Float3 p) => new Float3(p.X, p.Y, -p.Z);

        public Float3 Anchor(int team) => team == 1 ? Team1Anchor : Team2Anchor;

        public IReadOnlyList<Float3> Exits(int team) => team == 1 ? Team1Exits : Team2Exits;

        /// <summary>Team-specific staging rectangle bounds.</summary>
        public (float minX, float maxX, float minZ, float maxZ) StagingRect(int team)
            => team == 1
                ? (StagingMinX, StagingMaxX, StagingMinZ, StagingMaxZ)
                : (StagingMinX, StagingMaxX, -StagingMaxZ, -StagingMinZ);

        public bool StagingContains(int team, float x, float z)
        {
            var r = StagingRect(team);
            return x >= r.minX && x <= r.maxX && z >= r.minZ && z <= r.maxZ;
        }

        public bool InPlayable(float x, float z) => Math.Abs(x) <= HalfExtentX && Math.Abs(z) <= HalfExtentZ;
    }
}
