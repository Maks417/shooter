#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.Snapshots
{
    public readonly struct RemotePose
    {
        public EntityId Id { get; }
        public Float3 Position { get; }
        public float Yaw { get; }
        public float AimPitch { get; }
        public Float3 Velocity { get; }
        public RemotePose(EntityId id, Float3 position, float yaw, float aimPitch, Float3 velocity)
        {
            Id = id; Position = position; Yaw = yaw; AimPitch = aimPitch; Velocity = velocity;
        }
    }

    /// <summary>20 Hz snapshots with 50–150 ms interpolation buffer and ≤100 ms extrapolation then hold.</summary>
    public sealed class RemoteInterpolationBuffer
    {
        public const double MinBufferSeconds = 0.050;
        public const double DefaultBufferSeconds = 0.100;
        public const double MaxBufferSeconds = 0.150;
        public const double MaxExtrapolationSeconds = 0.100;
        public const int MaxQueuedSnapshots = 64;

        private readonly List<(double time, RemotePose pose)> samples = new List<(double, RemotePose)>();
        private double bufferSeconds = DefaultBufferSeconds;
        private double lastSampleTime;
        private RemotePose lastPose;
        private bool hasPose;
        private int dropped;

        public int Dropped => dropped;
        public double BufferSeconds => bufferSeconds;
        public int Count => samples.Count;

        public void Push(double serverTimeSeconds, RemotePose pose)
        {
            if (double.IsNaN(serverTimeSeconds) || double.IsInfinity(serverTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(serverTimeSeconds));
            if (samples.Count >= MaxQueuedSnapshots)
            {
                samples.RemoveAt(0);
                dropped++;
            }
            samples.Add((serverTimeSeconds, pose));
            samples.Sort((a, b) => a.time.CompareTo(b.time));
            lastSampleTime = serverTimeSeconds;
            lastPose = pose;
            hasPose = true;
            AdaptBuffer();
        }

        public bool TrySample(double renderTimeSeconds, out RemotePose pose)
        {
            pose = default;
            if (!hasPose) return false;
            double target = renderTimeSeconds - bufferSeconds;
            if (samples.Count == 0)
            {
                pose = HoldOrExtrapolate(target);
                return true;
            }

            // Find surrounding samples.
            int hi = 0;
            while (hi < samples.Count && samples[hi].time < target) hi++;
            if (hi == 0)
            {
                pose = samples[0].pose;
                return true;
            }
            if (hi >= samples.Count)
            {
                pose = HoldOrExtrapolate(target);
                return true;
            }

            var (t0, a) = samples[hi - 1];
            var (t1, b) = samples[hi];
            double span = t1 - t0;
            float alpha = span <= 1e-9 ? 0f : (float)((target - t0) / span);
            if (alpha < 0) alpha = 0;
            if (alpha > 1) alpha = 1;
            pose = Lerp(a, b, alpha);
            return true;
        }

        private RemotePose HoldOrExtrapolate(double target)
        {
            double ahead = target - lastSampleTime;
            if (ahead <= 0 || ahead > MaxExtrapolationSeconds)
                return lastPose;
            float dt = (float)ahead;
            var pos = new Float3(
                lastPose.Position.X + lastPose.Velocity.X * dt,
                lastPose.Position.Y + lastPose.Velocity.Y * dt,
                lastPose.Position.Z + lastPose.Velocity.Z * dt);
            return new RemotePose(lastPose.Id, pos, lastPose.Yaw, lastPose.AimPitch, lastPose.Velocity);
        }

        private void AdaptBuffer()
        {
            if (samples.Count < 2) return;
            double gap = samples[samples.Count - 1].time - samples[samples.Count - 2].time;
            if (gap > 0.08) bufferSeconds = Math.Min(MaxBufferSeconds, bufferSeconds + 0.01);
            else if (gap < 0.04) bufferSeconds = Math.Max(MinBufferSeconds, bufferSeconds - 0.005);
        }

        private static RemotePose Lerp(RemotePose a, RemotePose b, float t)
        {
            return new RemotePose(
                b.Id,
                new Float3(a.Position.X + (b.Position.X - a.Position.X) * t, a.Position.Y + (b.Position.Y - a.Position.Y) * t, a.Position.Z + (b.Position.Z - a.Position.Z) * t),
                a.Yaw + (b.Yaw - a.Yaw) * t,
                a.AimPitch + (b.AimPitch - a.AimPitch) * t,
                new Float3(a.Velocity.X + (b.Velocity.X - a.Velocity.X) * t, a.Velocity.Y + (b.Velocity.Y - a.Velocity.Y) * t, a.Velocity.Z + (b.Velocity.Z - a.Velocity.Z) * t));
        }
    }

    public sealed class DeltaBaselineStore
    {
        public const int MaxBaselines = 8;
        private readonly Dictionary<uint, SimulationSnapshot> baselines = new Dictionary<uint, SimulationSnapshot>();
        private readonly Queue<uint> order = new Queue<uint>();
        private uint lastAck;
        private double nextResyncAllowed;
        public int ResyncRequests { get; private set; }

        public void Store(SimulationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (baselines.ContainsKey(snapshot.Tick)) return;
            if (order.Count >= MaxBaselines)
            {
                uint old = order.Dequeue();
                baselines.Remove(old);
            }
            baselines[snapshot.Tick] = snapshot;
            order.Enqueue(snapshot.Tick);
        }

        public bool TryGet(uint tick, out SimulationSnapshot? snapshot) => baselines.TryGetValue(tick, out snapshot);

        public void Ack(uint tick) => lastAck = tick;

        public uint LastAck => lastAck;

        public bool TryRequestResync(double nowSeconds, out bool allowed)
        {
            ResyncRequests++;
            if (nowSeconds < nextResyncAllowed) { allowed = false; return true; }
            nextResyncAllowed = nowSeconds + 1.0;
            allowed = true;
            return true;
        }
    }
}
