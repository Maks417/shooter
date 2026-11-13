#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.Codec
{
    public readonly struct MatchManifest
    {
        public string ProtocolVersion { get; }
        public string BuildCompatibility { get; }
        public ContentId RecipeHash { get; }
        public ContentId GameplayHash { get; }
        public ulong MapSeed { get; }
        public uint TickRate { get; }
        public uint SnapshotHz { get; }

        public MatchManifest(string protocolVersion, string buildCompatibility, ContentId recipeHash, ContentId gameplayHash, ulong mapSeed, uint tickRate = 60, uint snapshotHz = 20)
        {
            if (string.IsNullOrEmpty(protocolVersion) || protocolVersion.Length > 16) throw new ArgumentException(nameof(protocolVersion));
            if (string.IsNullOrEmpty(buildCompatibility) || buildCompatibility.Length > 64) throw new ArgumentException(nameof(buildCompatibility));
            if (tickRate == 0 || snapshotHz == 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            ProtocolVersion = protocolVersion;
            BuildCompatibility = buildCompatibility;
            RecipeHash = recipeHash;
            GameplayHash = gameplayHash;
            MapSeed = mapSeed;
            TickRate = tickRate;
            SnapshotHz = snapshotHz;
        }
    }

    public static class SessionCodec
    {
        public static ReadOnlyMemory<byte> EncodeHandshake(MatchManifest manifest, PeerId peer, EntityId ownedActor, ulong sessionToken)
        {
            var w = new WireWriter();
            w.WriteHeader(WireMessageKind.Handshake);
            w.WriteString(manifest.ProtocolVersion, 16);
            w.WriteString(manifest.BuildCompatibility, 64);
            w.WriteContentId(manifest.RecipeHash);
            w.WriteContentId(manifest.GameplayHash);
            w.WriteUInt64(manifest.MapSeed);
            w.WriteUInt32(manifest.TickRate);
            w.WriteUInt32(manifest.SnapshotHz);
            w.WriteUInt64(peer.Value);
            w.WriteUInt64(ownedActor.Value);
            w.WriteUInt64(sessionToken);
            return w.Memory.ToArray();
        }

        public static bool TryDecodeHandshake(ReadOnlySpan<byte> payload, out MatchManifest manifest, out PeerId peer, out EntityId ownedActor, out ulong sessionToken, out string rejectReason)
        {
            manifest = default; peer = default; ownedActor = default; sessionToken = 0; rejectReason = "";
            try
            {
                var r = new WireReader(payload);
                if (r.ReadHeader() != WireMessageKind.Handshake) { rejectReason = "kind"; return false; }
                string protocol = r.ReadString(16);
                string build = r.ReadString(64);
                var recipe = r.ReadContentId();
                var gameplay = r.ReadContentId();
                ulong seed = r.ReadUInt64();
                uint tickRate = r.ReadUInt32();
                uint snapshotHz = r.ReadUInt32();
                peer = new PeerId(r.ReadUInt64());
                ownedActor = new EntityId(r.ReadUInt64());
                sessionToken = r.ReadUInt64();
                if (r.Remaining != 0) { rejectReason = "trailing"; return false; }
                manifest = new MatchManifest(protocol, build, recipe, gameplay, seed, tickRate, snapshotHz);
                return true;
            }
            catch (Exception ex)
            {
                rejectReason = ex.Message;
                return false;
            }
        }

        public static ReadOnlyMemory<byte> EncodeReject(string reason)
        {
            var w = new WireWriter();
            w.WriteHeader(WireMessageKind.HandshakeReject);
            w.WriteString(reason ?? "reject", 64);
            return w.Memory.ToArray();
        }

        public static ReadOnlyMemory<byte> EncodeCommandBatch(EntityId actor, uint baseTick, IReadOnlyList<InputCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (commands.Count == 0 || commands.Count > 8) throw new ArgumentOutOfRangeException(nameof(commands));
            var w = new WireWriter();
            w.WriteHeader(WireMessageKind.CommandBatch);
            w.WriteUInt64(actor.Value);
            w.WriteUInt32(baseTick);
            w.WriteByte((byte)commands.Count);
            for (int i = 0; i < commands.Count; i++)
            {
                var c = commands[i];
                if (c.EntityId != actor) throw new ArgumentException("command ownership");
                w.WriteUInt32(c.ClientTick);
                w.WriteUInt32(c.Sequence);
                w.WriteFloat(c.MoveX);
                w.WriteFloat(c.MoveY);
                w.WriteFloat(c.AimYaw);
                w.WriteFloat(c.AimPitch);
                w.WriteUInt16((ushort)c.Buttons);
                w.WriteByte(c.SelectedSlot);
            }
            return w.Memory.ToArray();
        }

        public static bool TryDecodeCommandBatch(ReadOnlySpan<byte> payload, List<InputCommand> destination, out string rejectReason)
        {
            destination.Clear();
            rejectReason = "";
            try
            {
                var r = new WireReader(payload);
                if (r.ReadHeader() != WireMessageKind.CommandBatch) { rejectReason = "kind"; return false; }
                var actor = new EntityId(r.ReadUInt64());
                r.ReadUInt32(); // baseTick retained for redundancy windows
                int count = r.ReadByte();
                if (count == 0 || count > 8) { rejectReason = "count"; return false; }
                for (int i = 0; i < count; i++)
                {
                    uint tick = r.ReadUInt32();
                    uint seq = r.ReadUInt32();
                    float mx = r.ReadFloat();
                    float my = r.ReadFloat();
                    float yaw = r.ReadFloat();
                    float pitch = r.ReadFloat();
                    var buttons = (InputButtons)r.ReadUInt16();
                    byte slot = r.ReadByte();
                    destination.Add(new InputCommand(actor, tick, seq, mx, my, yaw, pitch, buttons, slot));
                }
                if (r.Remaining != 0) { rejectReason = "trailing"; return false; }
                return true;
            }
            catch (Exception ex)
            {
                rejectReason = ex.Message;
                return false;
            }
        }
    }
}
