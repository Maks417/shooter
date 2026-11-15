#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    /// <summary>
    /// T082 / ADR-051 runtime cover probes. Product map packages carry no cover proxies, so a bot that
    /// wants cover samples nearby points biased away from its threat and keeps the first one the threat
    /// cannot see (ray from the threat's eye to a crouched chest at the point is blocked). Probe rays come
    /// from the same global 16-ray AI budget, only after vision has had its share, and at most
    /// <see cref="AiTuning.MaxCoverProbeRaysPerTick"/> per tick. Reachability is left to the normal path request.
    /// </summary>
    public static class CoverFinder
    {
        public const ulong DynamicCoverIdFlag = 0x8000_0000_0000_0000UL;

        public static int CandidateCount => AiTuning.CoverProbeAngles.Length;

        public static void Start(AiBlackboard board, Float3 threat)
        {
            board.CoverProbeActive = true;
            board.CoverProbeFailed = false;
            board.CoverProbeIndex = 0;
            board.CoverProbeOrigin = board.Feet;
            board.CoverThreat = threat;
        }

        public static Float3 Candidate(Float3 origin, Float3 threat, int index)
        {
            float ax = origin.X - threat.X, az = origin.Z - threat.Z;
            float len = MathF.Sqrt(ax * ax + az * az);
            if (len < 1e-3f) { ax = 0; az = 1; }
            else { ax /= len; az /= len; }
            float rad = AiTuning.CoverProbeAngles[index] * (MathF.PI / 180f);
            float c = MathF.Cos(rad), s = MathF.Sin(rad);
            float dx = ax * c - az * s, dz = ax * s + az * c;
            float r = AiTuning.CoverProbeRadii[index];
            return new Float3(origin.X + dx * r, origin.Y, origin.Z + dz * r);
        }

        public static ulong CoverIdFor(Float3 point)
        {
            uint qx = unchecked((uint)(int)MathF.Floor(point.X));
            uint qz = unchecked((uint)(int)MathF.Floor(point.Z));
            return DynamicCoverIdFlag | ((ulong)(qx & 0x7FFF_FFFF) << 32) | qz;
        }

        /// <summary>
        /// Advance active probes round-robin with leftover budget. Returns rays used.
        /// </summary>
        public static int Run(
            AiBlackboard[] boards, int botCount, uint tick, PerceptionSystem perception,
            CoverReservationBoard reservations, AiCounters counters)
        {
            int used = 0;
            if (botCount == 0) return 0;
            int first = (int)(tick % (uint)botCount);
            for (int turn = 0; turn < botCount; turn++)
            {
                var board = boards[(first + turn) % botCount];
                if (!board.Alive || board.State == AiState.Dead || !board.CoverProbeActive) continue;
                while (board.CoverProbeActive)
                {
                    if (board.CoverProbeIndex >= CandidateCount)
                    {
                        board.CoverProbeActive = false;
                        board.CoverProbeFailed = true;
                        counters.CoverProbesFailed++;
                        break;
                    }
                    if (used >= AiTuning.MaxCoverProbeRaysPerTick) return used;
                    var point = Candidate(board.CoverProbeOrigin, board.CoverThreat, board.CoverProbeIndex);
                    var eye = new Float3(board.CoverThreat.X, board.CoverThreat.Y + AiTuning.ThreatEyeLift, board.CoverThreat.Z);
                    var chest = new Float3(point.X, point.Y + AiTuning.CoverChestHeight, point.Z);
                    float dx = chest.X - eye.X, dy = chest.Y - eye.Y, dz = chest.Z - eye.Z;
                    float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (len < 0.5f) { board.CoverProbeIndex++; continue; }
                    var dir = new Float3(dx / len, dy / len, dz / len);
                    if (!perception.TryBudgetedRay(eye, dir, len, board.Self, out var hit)) return used;
                    used++;
                    counters.CoverProbeRays++;
                    board.CoverProbeIndex++;
                    bool hidden = hit.Status == QueryStatus.Hit && hit.Hit.DistanceMeters < len - 0.3f;
                    if (!hidden) continue;
                    ulong id = CoverIdFor(point);
                    if (!reservations.TryClaim(id, board.Self)) continue;
                    board.CoverReservation = id;
                    board.CoverPoint = point;
                    board.CoverProbeActive = false;
                    counters.CoverClaims++;
                }
            }
            return used;
        }
    }
}
