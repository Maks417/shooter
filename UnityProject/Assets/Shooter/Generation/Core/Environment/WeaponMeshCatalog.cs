#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// One packaged weapon mesh set, keyed by the visual profile <c>MeshAssetKey</c> (ADR-078). Recipe-backed guns come
    /// from <c>geometry.weapon</c> and carry a magazine and cycle part; built-in props (knife, grenade) do not.
    /// </summary>
    public sealed class WeaponMeshSource
    {
        private WeaponMeshSource(string meshKey, WeaponRecipe? recipe)
        {
            if (string.IsNullOrEmpty(meshKey)) throw new ArgumentException("Mesh key required.", nameof(meshKey));
            MeshKey = meshKey;
            Recipe = recipe;
        }

        public string MeshKey { get; }

        /// <summary>Gun recipe; null for a built-in loadout prop.</summary>
        public WeaponRecipe? Recipe { get; }

        public bool IsProp => Recipe == null;
        public bool HasMagazineCycle => Recipe != null;

        /// <summary>SGAF generator fingerprint the package stores this source's meshes under.</summary>
        public ContentId Fingerprint => IsProp ? WeaponMeshCatalog.PropFingerprint
            : GeneratorFingerprint.Compute(WeaponRecipe.GeneratorId, Recipe!.Version);

        internal static WeaponMeshSource FromRecipe(string meshKey, string recipeJson) =>
            new WeaponMeshSource(meshKey, WeaponRecipe.Parse(Encoding.ASCII.GetBytes(recipeJson)));

        internal static WeaponMeshSource Prop(string meshKey)
        {
            if (meshKey != "knife" && meshKey != "grenade")
                throw new ArgumentOutOfRangeException(nameof(meshKey), meshKey, "No built-in prop geometry for this key.");
            return new WeaponMeshSource(meshKey, null);
        }

        /// <summary>Compile-time geometry only (package builder, editor, tests); shipping players load packaged bytes.</summary>
        public MeshData BuildPropLod(int lod)
        {
            if (!IsProp) throw new InvalidOperationException("Not a prop mesh source: " + MeshKey);
            return MeshKey == "knife" ? LoadoutPropGeometry.BuildKnife(lod) : LoadoutPropGeometry.BuildGrenade(lod);
        }
    }

    /// <summary>
    /// Weapon meshes published in the shared weapon package (ADR-078). Rows are generated from
    /// <c>Content/Weapons/*.weapon.json</c>; paths follow <c>mesh-&lt;key&gt;-lod&lt;n&gt;.bin</c>,
    /// <c>mesh-&lt;key&gt;-magazine.bin</c> and <c>mesh-&lt;key&gt;-cycle.bin</c>.
    /// </summary>
    public static partial class WeaponMeshCatalog
    {
        public const int LodCount = 4;
        public const string PropGeneratorId = "geometry.loadout-prop";
        public const string PropGeneratorVersion = "1.0.0";

        private static readonly WeaponMeshSource[] Sources = CreateGeneratedSources();
        private static readonly Dictionary<string, WeaponMeshSource> ByKey = Index(Sources);

        public static ContentId GunFingerprint => GeneratorFingerprint.Compute(WeaponRecipe.GeneratorId, WeaponRecipe.GeneratorVersion);
        public static ContentId PropFingerprint => GeneratorFingerprint.Compute(PropGeneratorId, PropGeneratorVersion);

        public static IReadOnlyList<WeaponMeshSource> All => Sources;

        public static bool TryGet(string meshKey, out WeaponMeshSource source)
        {
            source = null!;
            return meshKey != null && ByKey.TryGetValue(meshKey, out source!);
        }

        public static WeaponMeshSource Require(string meshKey)
        {
            if (!TryGet(meshKey, out var source))
                throw new ArgumentOutOfRangeException(nameof(meshKey), meshKey, "Unknown weapon mesh asset key.");
            return source;
        }

        public static string LodPath(string meshKey, int lod)
        {
            if (lod < 0 || lod >= LodCount) throw new ArgumentOutOfRangeException(nameof(lod));
            return "mesh-" + Require(meshKey).MeshKey + "-lod" + lod + ".bin";
        }

        public static string[] LodPaths(string meshKey)
        {
            var paths = new string[LodCount];
            for (int lod = 0; lod < LodCount; lod++) paths[lod] = LodPath(meshKey, lod);
            return paths;
        }

        public static string? MagazinePath(string meshKey) =>
            Require(meshKey).HasMagazineCycle ? "mesh-" + meshKey + "-magazine.bin" : null;

        public static string? CyclePath(string meshKey) =>
            Require(meshKey).HasMagazineCycle ? "mesh-" + meshKey + "-cycle.bin" : null;

        /// <summary>Every payload path the weapon package must hold, in catalog order.</summary>
        public static IReadOnlyList<string> RequiredPaths()
        {
            var list = new List<string>(Sources.Length * (LodCount + 2));
            foreach (var source in Sources)
            {
                list.AddRange(LodPaths(source.MeshKey));
                if (source.HasMagazineCycle)
                {
                    list.Add(MagazinePath(source.MeshKey)!);
                    list.Add(CyclePath(source.MeshKey)!);
                }
            }
            return list;
        }

        private static Dictionary<string, WeaponMeshSource> Index(WeaponMeshSource[] sources)
        {
            var byKey = new Dictionary<string, WeaponMeshSource>(StringComparer.Ordinal);
            foreach (var source in sources) byKey.Add(source.MeshKey, source);
            return byKey;
        }
    }
}
