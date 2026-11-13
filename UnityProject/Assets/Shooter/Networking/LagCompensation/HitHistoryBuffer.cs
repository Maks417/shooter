#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.LagCompensation
{
    public readonly struct HistoricalActorSample
    {
        public EntityId EntityId { get; }
        public ushort RegionId { get; }
        public Capsule Capsule { get; }
        public bool AliveAtStart { get; }
        public bool AliveAtEnd { get; }
        public HistoricalActorSample(EntityId entityId, ushort regionId, Capsule capsule, bool aliveAtStart, bool aliveAtEnd)
        {
            if (!entityId.IsValid || regionId < 1 || regionId > 6 || !capsule.IsValid)
                throw new ArgumentException("historical sample");
            EntityId = entityId; RegionId = regionId; Capsule = capsule; AliveAtStart = aliveAtStart; AliveAtEnd = aliveAtEnd;
        }
    }

    public readonly struct HistoryFrame
    {
        public uint Tick { get; }
        public IReadOnlyList<HistoricalActorSample> Samples { get; }
        public HistoryFrame(uint tick, IReadOnlyList<HistoricalActorSample> samples)
        {
            Tick = tick;
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        }
    }

    /// <summary>16 samples covering ≥200 ms at 60 Hz for lag-compensated hit queries without moving live colliders.</summary>
    public sealed class HitHistoryBuffer
    {
        public const int Capacity = 16;
        public const int MaxRewindTicks = 12; // 200 ms at 60 Hz
        private readonly HistoryFrame?[] frames = new HistoryFrame?[Capacity];
        private int count;
        private int next;

        public int Count => count;

        public void Push(HistoryFrame frame)
        {
            frames[next] = frame;
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
        }

        public bool TryGetExact(uint tick, out HistoryFrame frame)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = (next - 1 - i + Capacity * 4) % Capacity;
                var f = frames[idx];
                if (f.HasValue && f.Value.Tick == tick) { frame = f.Value; return true; }
            }
            frame = default;
            return false;
        }

        /// <summary>Clamp shot time into [now-200ms, now]; reject impossible future.</summary>
        public bool TryApproveShotTick(uint serverNowTick, uint requestedTick, out uint approvedTick, out string rejectReason)
        {
            approvedTick = 0; rejectReason = "";
            if (Serial32.IsNewer(requestedTick, serverNowTick))
            {
                rejectReason = "future";
                return false;
            }
            uint oldest = serverNowTick >= MaxRewindTicks ? unchecked(serverNowTick - MaxRewindTicks) : 0u;
            if (serverNowTick >= MaxRewindTicks && Serial32.IsNewer(oldest, requestedTick))
            {
                rejectReason = "stale";
                return false;
            }
            approvedTick = requestedTick < oldest && serverNowTick >= MaxRewindTicks ? oldest : requestedTick;
            return true;
        }

        public bool TryQueryInterpolated(uint approvedTick, EntityId target, ushort regionId, out Capsule capsule, out bool eligible)
        {
            capsule = default; eligible = false;
            if (!TryGetExact(approvedTick, out var frame))
            {
                // Nearest older/newer pair for interpolation when exact tick missing.
                HistoryFrame? older = null, newer = null;
                for (int i = 0; i < count; i++)
                {
                    int idx = (next - 1 - i + Capacity * 4) % Capacity;
                    var f = frames[idx];
                    if (!f.HasValue) continue;
                    if (f.Value.Tick == approvedTick) { frame = f.Value; goto Found; }
                    if (!Serial32.IsNewer(f.Value.Tick, approvedTick))
                    {
                        if (!older.HasValue || Serial32.IsNewer(f.Value.Tick, older.Value.Tick)) older = f;
                    }
                    else if (!newer.HasValue || Serial32.IsNewer(newer.Value.Tick, f.Value.Tick)) newer = f;
                }
                if (!older.HasValue) return false;
                if (!newer.HasValue) { frame = older.Value; goto Found; }
                capsule = LerpCapsule(FindSample(older.Value, target, regionId, out var oOk, out var oElig),
                    FindSample(newer.Value, target, regionId, out var nOk, out var nElig), 0.5f);
                if (!oOk || !nOk) return false;
                eligible = oElig && nElig;
                return capsule.IsValid;
            }
        Found:
            var sample = FindSample(frame, target, regionId, out bool ok, out eligible);
            if (!ok) return false;
            capsule = sample;
            return true;
        }

        private static Capsule FindSample(HistoryFrame frame, EntityId target, ushort regionId, out bool ok, out bool eligible)
        {
            ok = false; eligible = false;
            foreach (var s in frame.Samples)
            {
                if (s.EntityId != target || s.RegionId != regionId) continue;
                ok = true;
                eligible = s.AliveAtStart; // rewind eligibility uses alive-at-start
                return s.Capsule;
            }
            return default;
        }

        private static Capsule LerpCapsule(Capsule a, Capsule b, float t)
        {
            if (!a.IsValid) return b;
            if (!b.IsValid) return a;
            Float3 L(Float3 x, Float3 y) => new Float3(x.X + (y.X - x.X) * t, x.Y + (y.Y - x.Y) * t, x.Z + (y.Z - x.Z) * t);
            return new Capsule(L(a.BottomSphereCenter, b.BottomSphereCenter), L(a.TopSphereCenter, b.TopSphereCenter), a.Radius + (b.Radius - a.Radius) * t);
        }
    }
}
