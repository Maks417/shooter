#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Networking.Codec
{
    public static class SnapshotCodec
    {
        public static ReadOnlyMemory<byte> EncodeSnapshot(SimulationSnapshot snapshot, uint baselineTick, uint lastProcessedSequence)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var w = new WireWriter(1200);
            w.WriteHeader(WireMessageKind.Snapshot);
            w.WriteUInt32(snapshot.Tick);
            w.WriteUInt32(baselineTick);
            w.WriteUInt32(snapshot.WorldRevision);
            w.WriteByte((byte)snapshot.MatchState);
            w.WriteUInt32(lastProcessedSequence);
            WriteEventId(w, snapshot.EventHighWater);
            w.WriteByte((byte)snapshot.Entities.Count);
            for (int i = 0; i < snapshot.Entities.Count; i++)
                WriteEntity(w, snapshot.Entities[i]);
            // Hit volumes are N3 history; M5 remote snapshots carry entity poses only within 1200 bound.
            return w.Memory.ToArray();
        }

        public static bool TryDecodeSnapshot(ReadOnlySpan<byte> payload, out SimulationSnapshot? snapshot, out uint baselineTick, out uint lastProcessedSequence, out string rejectReason)
        {
            snapshot = null; baselineTick = 0; lastProcessedSequence = 0; rejectReason = "";
            try
            {
                var r = new WireReader(payload);
                if (r.ReadHeader() != WireMessageKind.Snapshot) { rejectReason = "kind"; return false; }
                uint tick = r.ReadUInt32();
                baselineTick = r.ReadUInt32();
                uint worldRevision = r.ReadUInt32();
                var matchState = (MatchState)r.ReadByte();
                lastProcessedSequence = r.ReadUInt32();
                var highWater = ReadEventId(ref r);
                int entityCount = r.ReadByte();
                if (entityCount > ContractVersion.MaxActors) { rejectReason = "entities"; return false; }
                var entities = new List<EntitySnapshot>(entityCount);
                for (int i = 0; i < entityCount; i++)
                    entities.Add(ReadEntity(ref r));
                if (r.Remaining != 0) { rejectReason = "trailing"; return false; }
                snapshot = new SimulationSnapshot(tick, worldRevision, matchState, highWater, entities, Array.Empty<HitVolume>());
                return true;
            }
            catch (Exception ex)
            {
                rejectReason = ex.Message;
                return false;
            }
        }

        public static ReadOnlyMemory<byte> EncodeAck(uint baselineTick, uint receivedTick)
        {
            var w = new WireWriter();
            w.WriteHeader(WireMessageKind.SnapshotAck);
            w.WriteUInt32(baselineTick);
            w.WriteUInt32(receivedTick);
            return w.Memory.ToArray();
        }

        private static void WriteEntity(WireWriter w, EntitySnapshot e)
        {
            w.WriteUInt64(e.Id.Value);
            w.WriteFloat3(e.Position);
            w.WriteFloat(e.Yaw);
            w.WriteFloat3(e.Velocity);
            w.WriteByte((byte)e.Stance);
            w.WriteUInt16(e.Health);
            w.WriteByte(e.Team);
            w.WriteByte(e.ActiveSlot);
            WriteWeapon(w, e.Weapon);
            w.WriteFloat(e.LocomotionDistanceMeters);
            w.WriteFloat(e.AimPitch);
            w.WriteUInt32(e.GameplayPoseVersion);
        }

        private static EntitySnapshot ReadEntity(ref WireReader r)
        {
            var id = new EntityId(r.ReadUInt64());
            var pos = r.ReadFloat3();
            float yaw = r.ReadFloat();
            var vel = r.ReadFloat3();
            var stance = (Stance)r.ReadByte();
            ushort health = r.ReadUInt16();
            byte team = r.ReadByte();
            byte slot = r.ReadByte();
            var weapon = ReadWeapon(ref r);
            float loco = r.ReadFloat();
            float pitch = r.ReadFloat();
            uint pose = r.ReadUInt32();
            return new EntitySnapshot(id, pos, yaw, vel, stance, health, team, slot, weapon, loco, pitch, pose);
        }

        private static void WriteWeapon(WireWriter w, WeaponState weapon)
        {
            w.WriteString(weapon.DefinitionId.ToString(), 64);
            w.WriteByte((byte)weapon.Action);
            w.WriteUInt16(weapon.AmmoInMagazine);
            w.WriteUInt16(weapon.Reserve);
            w.WriteUInt32(weapon.NextAllowedFireTick);
            w.WriteUInt32(weapon.PhaseStartTick);
            w.WriteUInt32(weapon.PhaseEndTick);
            w.WriteUInt32(weapon.ShotIndex);
            w.WriteByte(weapon.PreviousFireHeld ? (byte)1 : (byte)0);
        }

        private static WeaponState ReadWeapon(ref WireReader r)
        {
            var def = new DefinitionId(r.ReadString(64));
            var action = (WeaponAction)r.ReadByte();
            ushort ammo = r.ReadUInt16();
            ushort reserve = r.ReadUInt16();
            uint nextFire = r.ReadUInt32();
            uint phaseStart = r.ReadUInt32();
            uint phaseEnd = r.ReadUInt32();
            uint shot = r.ReadUInt32();
            bool prev = r.ReadByte() != 0;
            return new WeaponState(def, action, ammo, reserve, nextFire, phaseStart, phaseEnd, shot, prev);
        }

        private static void WriteEventId(WireWriter w, EventId id)
        {
            w.WriteUInt64(id.AuthoritySessionId);
            w.WriteUInt32(id.Tick);
            w.WriteUInt64(id.EntityId.IsValid ? id.EntityId.Value : 0UL);
            w.WriteUInt16(id.Ordinal);
        }

        private static EventId ReadEventId(ref WireReader r)
        {
            ulong session = r.ReadUInt64();
            uint tick = r.ReadUInt32();
            ulong entity = r.ReadUInt64();
            ushort ordinal = r.ReadUInt16();
            if (session == 0 && tick == 0 && entity == 0 && ordinal == 0) return default;
            return new EventId(session, tick, new EntityId(entity), ordinal);
        }
    }
}
