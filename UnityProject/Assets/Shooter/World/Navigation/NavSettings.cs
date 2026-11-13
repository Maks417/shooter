#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;

namespace Shooter.World.Navigation
{
    /// <summary>Locked agent nav settings. Gameplay hashes ignore visual quality profile.</summary>
    public readonly struct NavSettings
    {
        public float AgentRadius { get; }
        public float AgentHeight { get; }
        public float MaxStep { get; }
        public float MaxSlopeDegrees { get; }
        public float ClearanceMargin { get; }
        public uint SettingsVersion { get; }

        public NavSettings(float agentRadius, float agentHeight, float maxStep, float maxSlopeDegrees, float clearanceMargin, uint settingsVersion = 1)
        {
            if (agentRadius <= 0 || agentHeight <= 0 || maxStep < 0 || maxSlopeDegrees <= 0 || maxSlopeDegrees > 89 || clearanceMargin < 0 || settingsVersion == 0)
                throw new ArgumentOutOfRangeException(nameof(agentRadius));
            AgentRadius = agentRadius; AgentHeight = agentHeight; MaxStep = maxStep;
            MaxSlopeDegrees = maxSlopeDegrees; ClearanceMargin = clearanceMargin; SettingsVersion = settingsVersion;
        }

        public static NavSettings SliceDefault => new NavSettings(0.32f, 1.8f, 0.25f, 45f, 0.05f, 1);

        public float RequiredClearance => AgentRadius + ClearanceMargin;

        public ContentId Hash()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write("shooter.nav-settings.v1");
            w.Write(SettingsVersion);
            w.Write(AgentRadius); w.Write(AgentHeight); w.Write(MaxStep); w.Write(MaxSlopeDegrees); w.Write(ClearanceMargin);
            using var sha = SHA256.Create();
            return new ContentId(sha.ComputeHash(ms.ToArray()));
        }
    }

    public readonly struct NavCoverCandidate
    {
        public ulong StableId { get; }
        public Float3 StandPeek { get; }
        public Float3 CrouchPeek { get; }
        public Float3 Anchor { get; }
        public bool HardCover { get; }
        public NavCoverCandidate(ulong stableId, Float3 anchor, Float3 standPeek, Float3 crouchPeek, bool hardCover)
        {
            if (stableId == 0) throw new ArgumentOutOfRangeException(nameof(stableId));
            StableId = stableId; Anchor = anchor; StandPeek = standPeek; CrouchPeek = crouchPeek; HardCover = hardCover;
        }
    }

    public sealed class NavCompileResult
    {
        public uint NavRevision { get; }
        public ContentId NavHash { get; }
        public ContentId CollisionHash { get; }
        public ContentId SettingsHash { get; }
        public IReadOnlyList<Float3> MandatoryRoute { get; }
        public IReadOnlyList<NavCoverCandidate> Covers { get; }
        public bool AllObjectivesReachable { get; }
        public float MinClearanceMeters { get; }

        public NavCompileResult(
            uint navRevision,
            ContentId navHash,
            ContentId collisionHash,
            ContentId settingsHash,
            IReadOnlyList<Float3> mandatoryRoute,
            IReadOnlyList<NavCoverCandidate> covers,
            bool allObjectivesReachable,
            float minClearanceMeters)
        {
            NavRevision = navRevision; NavHash = navHash; CollisionHash = collisionHash; SettingsHash = settingsHash;
            MandatoryRoute = mandatoryRoute ?? throw new ArgumentNullException(nameof(mandatoryRoute));
            Covers = covers ?? throw new ArgumentNullException(nameof(covers));
            AllObjectivesReachable = allObjectivesReachable; MinClearanceMeters = minClearanceMeters;
        }
    }
}
