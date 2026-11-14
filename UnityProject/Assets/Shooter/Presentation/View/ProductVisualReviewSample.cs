#nullable enable
using System.Globalization;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>One product-pose overlay sample for T062 capture inventory. Read-only presentation evidence.</summary>
    public readonly struct ProductVisualReviewSample
    {
        public ulong EntityId { get; }
        public byte Team { get; }
        public bool Alive { get; }
        public bool Corpse { get; }
        public float ArmBodyClearanceMeters { get; }
        public float EyeSightLineErrorMeters { get; }
        public float LeftGripErrorMeters { get; }
        public float RightGripErrorMeters { get; }
        public float PlantSlideMeters { get; }
        public float StockErrorMeters { get; }
        public float ReloadPhase { get; }
        public float AimPitchRadians { get; }
        public Float3 Feet { get; }
        public Float3 RightEye { get; }
        public Float3 WeaponPosition { get; }
        public Float3 SightWorld { get; }
        public uint CompletedTick { get; }
        public string MapId { get; }
        public string Profile { get; }

        public ProductVisualReviewSample(
            ulong entityId,
            byte team,
            bool alive,
            bool corpse,
            float armBodyClearanceMeters,
            float eyeSightLineErrorMeters,
            float leftGripErrorMeters,
            float rightGripErrorMeters,
            float plantSlideMeters,
            float stockErrorMeters,
            float reloadPhase,
            float aimPitchRadians,
            Float3 feet,
            Float3 rightEye,
            Float3 weaponPosition,
            Float3 sightWorld,
            uint completedTick,
            string mapId,
            string profile)
        {
            EntityId = entityId;
            Team = team;
            Alive = alive;
            Corpse = corpse;
            ArmBodyClearanceMeters = armBodyClearanceMeters;
            EyeSightLineErrorMeters = eyeSightLineErrorMeters;
            LeftGripErrorMeters = leftGripErrorMeters;
            RightGripErrorMeters = rightGripErrorMeters;
            PlantSlideMeters = plantSlideMeters;
            StockErrorMeters = stockErrorMeters;
            ReloadPhase = reloadPhase;
            AimPitchRadians = aimPitchRadians;
            Feet = feet;
            RightEye = rightEye;
            WeaponPosition = weaponPosition;
            SightWorld = sightWorld;
            CompletedTick = completedTick;
            MapId = mapId ?? "";
            Profile = profile ?? "";
        }

        /// <summary>Flags proxy clearance/eye residuals that reproduce the named P05 FAIL conditions.</summary>
        public bool ReproducesForearmStrapProxy => ArmBodyClearanceMeters < 0f;
        public bool ReproducesEyeSightResidual => EyeSightLineErrorMeters > 0.05f;

        public string ToJsonObject()
        {
            var sb = new StringBuilder(512);
            bool first = true;
            sb.Append('{');
            Field(sb, ref first, "entityId", EntityId.ToString(CultureInfo.InvariantCulture));
            Field(sb, ref first, "team", Team.ToString(CultureInfo.InvariantCulture));
            Field(sb, ref first, "alive", Alive ? "true" : "false");
            Field(sb, ref first, "corpse", Corpse ? "true" : "false");
            Number(sb, ref first, "armBodyClearanceMeters", ArmBodyClearanceMeters);
            Number(sb, ref first, "eyeSightLineErrorMeters", EyeSightLineErrorMeters);
            Number(sb, ref first, "leftGripErrorMeters", LeftGripErrorMeters);
            Number(sb, ref first, "rightGripErrorMeters", RightGripErrorMeters);
            Number(sb, ref first, "plantSlideMeters", PlantSlideMeters);
            Number(sb, ref first, "stockErrorMeters", StockErrorMeters);
            Number(sb, ref first, "reloadPhase", ReloadPhase);
            Number(sb, ref first, "aimPitchRadians", AimPitchRadians);
            Vec(sb, ref first, "feet", Feet);
            Vec(sb, ref first, "rightEye", RightEye);
            Vec(sb, ref first, "weaponPosition", WeaponPosition);
            Vec(sb, ref first, "sightWorld", SightWorld);
            Field(sb, ref first, "completedTick", CompletedTick.ToString(CultureInfo.InvariantCulture));
            Field(sb, ref first, "mapId", Quote(MapId));
            Field(sb, ref first, "profile", Quote(Profile));
            Field(sb, ref first, "reproducesForearmStrapProxy", ReproducesForearmStrapProxy ? "true" : "false");
            Field(sb, ref first, "reproducesEyeSightResidual", ReproducesEyeSightResidual ? "true" : "false");
            Field(sb, ref first, "p05VisualAcceptance", Quote(ProductVisualDefectCatalog.VisualAcceptance));
            Field(sb, ref first, "fourthClearanceSearchAuthorized", "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static void Field(StringBuilder sb, ref bool first, string key, string value)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(key).Append('"').Append(':').Append(value);
        }

        private static void Number(StringBuilder sb, ref bool first, string key, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) Field(sb, ref first, key, "null");
            else Field(sb, ref first, key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Vec(StringBuilder sb, ref bool first, string key, Float3 v)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(key).Append('"').Append(':');
            sb.Append("{\"x\":").Append(v.X.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(",\"y\":").Append(v.Y.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(",\"z\":").Append(v.Z.ToString("R", CultureInfo.InvariantCulture)).Append('}');
        }

        private static string Quote(string value) =>
            "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
