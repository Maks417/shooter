#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-083 arena layout documents: a map's planner written as data. The document names the planner identity, the
    /// <see cref="ArenaLayout"/> and an ordered list of kit operations, each a direct call on <see cref="ArenaBoxBuilder"/>
    /// in the same order a C# planner would make it (stable ids follow call order). Every length is an integer number of
    /// millimetres, converted with an exact int → float division, so documents parse to identical floats in every runtime.
    /// Unknown operations, fields or values are rejected.
    /// </summary>
    public sealed class ArenaLayoutDocument
    {
        public string GeneratorId { get; }
        public string GeneratorVersion { get; }
        public ulong StableIdBase { get; }
        public ArenaLayout Layout { get; }
        private readonly IReadOnlyList<IReadOnlyDictionary<string, MapJsonValue>> ops;

        private ArenaLayoutDocument(string generatorId, string generatorVersion, ulong idBase, ArenaLayout layout,
            IReadOnlyList<IReadOnlyDictionary<string, MapJsonValue>> ops)
        {
            GeneratorId = generatorId; GeneratorVersion = generatorVersion; StableIdBase = idBase; Layout = layout; this.ops = ops;
        }

        public int OperationCount => ops.Count;

        public static ArenaLayoutDocument Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Layout document required.", nameof(json));
            var root = MapJsonValue.Parse(Encoding.UTF8.GetBytes(json)).Object;
            Exact(root, "layout", new[] { "planner", "arena", "ops" });
            var planner = root["planner"].Object;
            Exact(planner, "layout.planner", new[] { "id", "version", "idBase" });
            var arena = root["arena"].Object;
            Exact(arena, "layout.arena", new[] { "halfExtentX", "halfExtentZ", "staging", "team1Anchor", "team1Exits", "lanePoints", "symmetry" });
            var staging = arena["staging"].Array;
            if (staging.Count != 4) throw new FormatException("layout.arena.staging must be [minX, maxX, minZ, maxZ].");
            string symmetry = arena["symmetry"].String;
            if (symmetry != "MirrorZ" && symmetry != "Rotate180") throw new FormatException("layout.arena.symmetry must be MirrorZ or Rotate180.");
            var layout = new ArenaLayout(
                M(arena["halfExtentX"]), M(arena["halfExtentZ"]),
                M(staging[0]), M(staging[1]), M(staging[2]), M(staging[3]),
                Point(arena["team1Anchor"]), Points(arena["team1Exits"]), Points(arena["lanePoints"]),
                symmetry == "Rotate180" ? ArenaSymmetry.Rotate180 : ArenaSymmetry.MirrorZ);
            int idBase = planner["idBase"].Int32;
            if (idBase <= 0) throw new FormatException("layout.planner.idBase must be positive.");
            var list = new List<IReadOnlyDictionary<string, MapJsonValue>>();
            foreach (var op in root["ops"].Array) list.Add(op.Object);
            var document = new ArenaLayoutDocument(planner["id"].String, planner["version"].String, (ulong)idBase, layout, list);
            document.Run(new ArenaBoxBuilder((ulong)idBase, layout.Symmetry)); // validate every op once
            return document;
        }

        public CompoundPlan Generate(ulong seed)
        {
            var builder = new ArenaBoxBuilder(StableIdBase, Layout.Symmetry);
            Run(builder);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, builder);
        }

        private void Run(ArenaBoxBuilder b)
        {
            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                string where = "layout.ops[" + i + "]";
                if (!op.ContainsKey("op")) throw new FormatException(where + ": missing op.");
                string name = op["op"].String;
                switch (name)
                {
                    case "perimeter":
                        Exact(op, where, new[] { "op", "height", "thickness" });
                        b.Perimeter(Layout, M(op["height"]), M(op["thickness"]));
                        break;
                    case "shell":
                        Exact(op, where, new[] { "op", "x", "z", "sizeX", "sizeZ", "thickness", "height", "doors", "roof" }, "mirror");
                        b.Shell(M(op["x"]), M(op["z"]), M(op["sizeX"]), M(op["sizeZ"]), M(op["thickness"]), M(op["height"]),
                            Doors(op["doors"]), op["roof"].Boolean, Mirror(op));
                        break;
                    case "cover":
                        Exact(op, where, new[] { "op", "x", "z", "sizeX", "sizeY", "sizeZ", "threat" }, "mirror");
                        b.AddCover(M(op["x"]), M(op["z"]), M(op["sizeX"]), M(op["sizeY"]), M(op["sizeZ"]), Point(op["threat"]), Mirror(op));
                        break;
                    case "vehicle":
                        Exact(op, where, new[] { "op", "kind", "x", "z", "length", "width", "height", "alongX", "threat" }, "facing", "mirror");
                        b.AddVehicle(Kind(op["kind"]), M(op["x"]), M(op["z"]), M(op["length"]), M(op["width"]), M(op["height"]),
                            op["alongX"].Boolean, Point(op["threat"]), Facing(op), Mirror(op));
                        break;
                    case "feature":
                        Exact(op, where, new[] { "op", "kind", "x", "z", "sizeX", "sizeY", "sizeZ", "facing" }, "mirror", "baseY");
                        b.RecordFeature(Kind(op["kind"]), M(op["x"]), M(op["z"]), M(op["sizeX"]), M(op["sizeY"]), M(op["sizeZ"]),
                            Facing(op), Mirror(op), op.ContainsKey("baseY") ? M(op["baseY"]) : 0f);
                        break;
                    case "sandbags":
                        Exact(op, where, new[] { "op", "x", "z", "length", "alongX", "threat" }, "mirror", "height");
                        if (op.ContainsKey("height"))
                            b.AddSandbagWall(M(op["x"]), M(op["z"]), M(op["length"]), op["alongX"].Boolean, Point(op["threat"]), Mirror(op), M(op["height"]));
                        else
                            b.AddSandbagWall(M(op["x"]), M(op["z"]), M(op["length"]), op["alongX"].Boolean, Point(op["threat"]), Mirror(op));
                        break;
                    case "fence":
                        Exact(op, where, new[] { "op", "x", "z", "length", "alongX" }, "mirror", "height");
                        if (op.ContainsKey("height"))
                            b.AddFenceRun(M(op["x"]), M(op["z"]), M(op["length"]), op["alongX"].Boolean, Mirror(op), M(op["height"]));
                        else
                            b.AddFenceRun(M(op["x"]), M(op["z"]), M(op["length"]), op["alongX"].Boolean, Mirror(op));
                        break;
                    case "grounded":
                        Exact(op, where, new[] { "op", "x", "z", "sizeX", "sizeZ", "height" }, "tag", "mirror");
                        b.AddGrounded(M(op["x"]), M(op["z"]), M(op["sizeX"]), M(op["sizeZ"]), M(op["height"]), Tag(op, ArenaBoxBuilder.TagSolid), Mirror(op));
                        break;
                    case "box":
                        Exact(op, where, new[] { "op", "center", "size" }, "tag", "mirror", "gameplay");
                        b.Add(Point(op["center"]), Point(op["size"]), Tag(op, ArenaBoxBuilder.TagSolid), Mirror(op),
                            !op.ContainsKey("gameplay") || op["gameplay"].Boolean);
                        break;
                    case "wallX":
                        Exact(op, where, new[] { "op", "z", "from", "to", "thickness", "height" }, "gaps", "tag", "mirror");
                        b.WallAlongX(M(op["z"]), M(op["from"]), M(op["to"]), M(op["thickness"]), M(op["height"]), Gaps(op),
                            Tag(op, ArenaBoxBuilder.TagSolid), Mirror(op));
                        break;
                    case "wallZ":
                        Exact(op, where, new[] { "op", "x", "from", "to", "thickness", "height" }, "gaps", "tag", "mirror");
                        b.WallAlongZ(M(op["x"]), M(op["from"]), M(op["to"]), M(op["thickness"]), M(op["height"]), Gaps(op),
                            Tag(op, ArenaBoxBuilder.TagSolid), Mirror(op));
                        break;
                    case "visualDetail":
                        Exact(op, where, new[] { "op", "set" });
                        switch (op["set"].String)
                        {
                            case "compound": ArenaVisualDetail.AppendCompound(b, Layout); break;
                            case "depot": ArenaVisualDetail.AppendDepot(b, Layout); break;
                            case "oil": ArenaVisualDetail.AppendOil(b, Layout); break;
                            default: throw new FormatException(where + ": unknown visual detail set " + op["set"].String + ".");
                        }
                        break;
                    default:
                        throw new FormatException(where + ": unknown op " + name + ".");
                }
            }
        }

        /// <summary>Millimetres → metres with an exact int → float division (identical in every runtime).</summary>
        private static float M(MapJsonValue value)
        {
            int mm = value.Int32;
            if (mm < -1_000_000 || mm > 1_000_000) throw new FormatException("Layout length out of range: " + mm + " mm.");
            return mm / 1000f;
        }

        private static Float3 Point(MapJsonValue value)
        {
            var a = value.Array;
            if (a.Count != 3) throw new FormatException("Layout point must be [x, y, z] in millimetres.");
            return new Float3(M(a[0]), M(a[1]), M(a[2]));
        }

        private static IReadOnlyList<Float3> Points(MapJsonValue value)
        {
            var list = new List<Float3>();
            foreach (var p in value.Array) list.Add(Point(p));
            return list;
        }

        private static IReadOnlyList<ArenaDoor> Doors(MapJsonValue value)
        {
            var list = new List<ArenaDoor>();
            foreach (var d in value.Array)
            {
                var door = d.Object;
                Exact(door, "door", new[] { "side", "offset", "width" });
                if (!Enum.TryParse(door["side"].String, false, out ArenaSide side) || !Enum.IsDefined(typeof(ArenaSide), side))
                    throw new FormatException("Unknown door side " + door["side"].String + ".");
                list.Add(new ArenaDoor(side, M(door["offset"]), M(door["width"])));
            }
            return list;
        }

        private static IReadOnlyList<ArenaGap>? Gaps(IReadOnlyDictionary<string, MapJsonValue> op)
        {
            if (!op.ContainsKey("gaps")) return null;
            var list = new List<ArenaGap>();
            foreach (var g in op["gaps"].Array)
            {
                var gap = g.Object;
                Exact(gap, "gap", new[] { "center", "width" });
                list.Add(new ArenaGap(M(gap["center"]), M(gap["width"])));
            }
            return list;
        }

        private static ArenaFeatureKind Kind(MapJsonValue value)
        {
            if (!Enum.TryParse(value.String, false, out ArenaFeatureKind kind) || !Enum.IsDefined(typeof(ArenaFeatureKind), kind))
                throw new FormatException("Unknown feature kind " + value.String + ".");
            return kind;
        }

        private static float Facing(IReadOnlyDictionary<string, MapJsonValue> op)
        {
            if (!op.ContainsKey("facing")) return 1f;
            int facing = op["facing"].Int32;
            if (facing != 1 && facing != -1) throw new FormatException("facing must be 1 or -1.");
            return facing;
        }

        private static bool Mirror(IReadOnlyDictionary<string, MapJsonValue> op) => !op.ContainsKey("mirror") || op["mirror"].Boolean;

        private static ushort Tag(IReadOnlyDictionary<string, MapJsonValue> op, ushort fallback)
        {
            if (!op.ContainsKey("tag")) return fallback;
            return op["tag"].String switch
            {
                "solid" => ArenaBoxBuilder.TagSolid,
                "cover" => ArenaBoxBuilder.TagCover,
                _ => throw new FormatException("tag must be solid or cover.")
            };
        }

        private static void Exact(IReadOnlyDictionary<string, MapJsonValue> values, string where, string[] required, params string[] optional)
        {
            foreach (string key in required)
                if (!values.ContainsKey(key)) throw new FormatException(where + ": missing " + key + ".");
            foreach (string key in values.Keys)
                if (Array.IndexOf(required, key) < 0 && Array.IndexOf(optional, key) < 0)
                    throw new FormatException(where + ": unknown field " + key + ".");
        }
    }
}
