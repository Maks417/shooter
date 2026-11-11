using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Shooter.Contracts;

namespace Shooter.Contracts.Tests
{
    // Test fixture codec only. This is deliberately neither a transport nor a content binary format.
    internal static class FixtureJson
    {
        public static JsonDocument Load(string name) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
        public static Float3 Vector(JsonElement value) => new Float3(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
        public static ulong Id(JsonElement value)
        {
            string text = value.GetString() ?? throw new ArgumentException("ID is null");
            ulong id = ulong.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
            if (id.ToString(CultureInfo.InvariantCulture) != text) throw new ArgumentException("Noncanonical ID");
            return id;
        }
        public static InputCommand Command(JsonElement v)
        {
            var names = new HashSet<string>(new[] { "entityId", "clientTick", "sequence", "moveX", "moveY", "aimYaw", "aimPitch", "buttons", "selectedSlot" });
            foreach (var p in v.EnumerateObject()) if (!names.Remove(p.Name)) throw new ArgumentException("Unknown/duplicate command field");
            if (names.Count != 0) throw new ArgumentException("Missing command field");
            return new InputCommand(new EntityId(Id(v.GetProperty("entityId"))), v.GetProperty("clientTick").GetUInt32(), v.GetProperty("sequence").GetUInt32(),
                v.GetProperty("moveX").GetSingle(), v.GetProperty("moveY").GetSingle(), v.GetProperty("aimYaw").GetSingle(), v.GetProperty("aimPitch").GetSingle(),
                (InputButtons)v.GetProperty("buttons").GetUInt16(), v.GetProperty("selectedSlot").GetByte());
        }
        public static byte[] Write(InputCommand command)
        {
            using var stream = new MemoryStream(); using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject(); w.WriteString("entityId", command.EntityId.ToString()); w.WriteNumber("clientTick", command.ClientTick); w.WriteNumber("sequence", command.Sequence);
                w.WriteNumber("moveX", command.MoveX); w.WriteNumber("moveY", command.MoveY); w.WriteNumber("aimYaw", command.AimYaw); w.WriteNumber("aimPitch", command.AimPitch);
                w.WriteNumber("buttons", (ushort)command.Buttons); w.WriteNumber("selectedSlot", command.SelectedSlot); w.WriteEndObject();
            }
            return stream.ToArray();
        }
        public static WeaponState Weapon(JsonElement v) => new WeaponState(new DefinitionId(v.GetProperty("definitionId").GetString()!), (WeaponAction)v.GetProperty("action").GetInt32(),
            v.GetProperty("ammoInMagazine").GetUInt16(), v.GetProperty("reserve").GetUInt16(), v.GetProperty("nextAllowedFireTick").GetUInt32(),
            v.GetProperty("phaseStartTick").GetUInt32(), v.GetProperty("phaseEndTick").GetUInt32(), v.GetProperty("shotIndex").GetUInt32(), v.GetProperty("previousFireHeld").GetBoolean());
        public static object WriteWeapon(WeaponState w) => new
        {
            definitionId = w.DefinitionId.ToString(), action = (int)w.Action, ammoInMagazine = w.AmmoInMagazine, reserve = w.Reserve,
            nextAllowedFireTick = w.NextAllowedFireTick, phaseStartTick = w.PhaseStartTick, phaseEndTick = w.PhaseEndTick, shotIndex = w.ShotIndex, previousFireHeld = w.PreviousFireHeld
        };
        private static float[] V(Float3 v) => new[] { v.X, v.Y, v.Z };
        public static SimulationSnapshot Snapshot(JsonElement v)
        {
            if (v.GetProperty("schemaVersion").GetUInt32() != 1) throw new ArgumentException("Unsupported snapshot schema");
            var e = v.GetProperty("eventHighWater"); ulong session = Id(e.GetProperty("authoritySessionId"));
            EventId highWater;
            if (session == 0)
            {
                if (e.GetProperty("tick").GetUInt32() != 0 || Id(e.GetProperty("entityId")) != 0 || e.GetProperty("ordinal").GetUInt16() != 0) throw new ArgumentException("Invalid sentinel");
                highWater = default;
            }
            else highWater = new EventId(session, e.GetProperty("tick").GetUInt32(), new EntityId(Id(e.GetProperty("entityId"))), e.GetProperty("ordinal").GetUInt16());
            return new SimulationSnapshot(v.GetProperty("tick").GetUInt32(), v.GetProperty("worldRevision").GetUInt32(), (MatchState)v.GetProperty("matchState").GetInt32(), highWater,
                v.GetProperty("entities").EnumerateArray().Select(a => new EntitySnapshot(new EntityId(Id(a.GetProperty("id"))), Vector(a.GetProperty("position")), a.GetProperty("yaw").GetSingle(),
                    Vector(a.GetProperty("velocity")), (Stance)a.GetProperty("stance").GetInt32(), a.GetProperty("health").GetUInt16(), a.GetProperty("team").GetByte(), a.GetProperty("activeSlot").GetByte(),
                    Weapon(a.GetProperty("weapon")), a.GetProperty("locomotionDistanceMeters").GetSingle(), a.GetProperty("aimPitch").GetSingle(), a.GetProperty("gameplayPoseVersion").GetUInt32())).ToArray(),
                v.GetProperty("hitVolumes").EnumerateArray().Select(a => new HitVolume(new EntityId(Id(a.GetProperty("entityId"))), a.GetProperty("regionId").GetUInt16(),
                    new Capsule(Vector(a.GetProperty("capsule").GetProperty("bottomSphereCenter")), Vector(a.GetProperty("capsule").GetProperty("topSphereCenter")), a.GetProperty("capsule").GetProperty("radius").GetSingle()), a.GetProperty("tick").GetUInt32())).ToArray());
        }
        public static JsonElement WriteSnapshot(SimulationSnapshot s) => JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1, tick = s.Tick, worldRevision = s.WorldRevision, matchState = (int)s.MatchState,
            eventHighWater = new { authoritySessionId = s.EventHighWater.AuthoritySessionId.ToString(CultureInfo.InvariantCulture), tick = s.EventHighWater.Tick, entityId = s.EventHighWater.EntityId.ToString(), ordinal = s.EventHighWater.Ordinal },
            entities = s.Entities.Select(e => new { id = e.Id.ToString(), position = V(e.Position), yaw = e.Yaw, velocity = V(e.Velocity), stance = (int)e.Stance, health = e.Health, team = e.Team, activeSlot = e.ActiveSlot,
                weapon = WriteWeapon(e.Weapon), locomotionDistanceMeters = e.LocomotionDistanceMeters, aimPitch = e.AimPitch, gameplayPoseVersion = e.GameplayPoseVersion }),
            hitVolumes = s.HitVolumes.Select(h => new { entityId = h.EntityId.ToString(), regionId = h.RegionId,
                capsule = new { bottomSphereCenter = V(h.Capsule.BottomSphereCenter), topSphereCenter = V(h.Capsule.TopSphereCenter), radius = h.Capsule.Radius }, tick = h.Tick })
        });
        public static PlayerPredictionState Prediction(JsonElement v)
        {
            if (v.GetProperty("schemaVersion").GetUInt32() != ContractVersion.PredictionSchema) throw new ArgumentException("Unsupported prediction schema");
            return new PlayerPredictionState(
            v.GetProperty("schemaVersion").GetUInt32(), v.GetProperty("tick").GetUInt32(), new EntityId(Id(v.GetProperty("actorId"))), v.GetProperty("worldRevision").GetUInt32(), v.GetProperty("collisionRevision").GetUInt32(), new ContentId(v.GetProperty("definitionHash").GetString()!),
            Vector(v.GetProperty("position")), Vector(v.GetProperty("velocity")), v.GetProperty("aimYaw").GetSingle(), v.GetProperty("aimPitch").GetSingle(), v.GetProperty("grounded").GetBoolean(),
            Id(v.GetProperty("groundSurfaceId")) == 0 ? default : new SurfaceId(Id(v.GetProperty("groundSurfaceId"))), Vector(v.GetProperty("groundNormal")), (Stance)v.GetProperty("stance").GetInt32(), v.GetProperty("capsuleHeight").GetSingle(),
            v.GetProperty("lastGroundedTick").GetUInt32(), v.GetProperty("jumpConsumedSequence").GetUInt32(), v.GetProperty("activeSlot").GetByte(), v.GetProperty("desiredSlot").GetByte(), v.GetProperty("weaponSlots").EnumerateArray().Select(Weapon).ToArray(),
            v.GetProperty("recoilYaw").GetSingle(), v.GetProperty("recoilPitch").GetSingle(), v.GetProperty("recoilYawVelocity").GetSingle(), v.GetProperty("recoilPitchVelocity").GetSingle(), v.GetProperty("locomotionDistanceMeters").GetSingle(),
            v.GetProperty("lastProcessedSequence").GetUInt32(), (InputButtons)v.GetProperty("lastButtons").GetUInt16(), v.GetProperty("lastValidCommandTick").GetUInt32(), v.GetProperty("alive").GetBoolean(), v.GetProperty("health").GetUInt16());
        }
        public static JsonElement WritePrediction(PlayerPredictionState p) => JsonSerializer.SerializeToElement(new
        {
            schemaVersion = p.SchemaVersion, tick = p.Tick, actorId = p.ActorId.ToString(), worldRevision = p.WorldRevision, collisionRevision = p.CollisionRevision, definitionHash = p.DefinitionHash.ToString(),
            position = V(p.Position), velocity = V(p.Velocity), aimYaw = p.AimYaw, aimPitch = p.AimPitch, grounded = p.Grounded, groundSurfaceId = p.GroundSurfaceId.ToString(), groundNormal = V(p.GroundNormal), stance = (int)p.Stance,
            capsuleHeight = p.CapsuleHeight, lastGroundedTick = p.LastGroundedTick, jumpConsumedSequence = p.JumpConsumedSequence, activeSlot = p.ActiveSlot, desiredSlot = p.DesiredSlot, weaponSlots = p.WeaponSlots.Select(WriteWeapon),
            recoilYaw = p.RecoilYaw, recoilPitch = p.RecoilPitch, recoilYawVelocity = p.RecoilYawVelocity, recoilPitchVelocity = p.RecoilPitchVelocity, locomotionDistanceMeters = p.LocomotionDistanceMeters,
            lastProcessedSequence = p.LastProcessedSequence, lastButtons = (ushort)p.LastButtons, lastValidCommandTick = p.LastValidCommandTick, alive = p.Alive, health = p.Health
        });
    }
}
