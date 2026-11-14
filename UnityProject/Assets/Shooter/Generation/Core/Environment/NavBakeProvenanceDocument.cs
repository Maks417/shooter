#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Sidecar (beside index.json/catalog.json, never inside frozen package bytes) recording which navigation
    /// provider produced every published package so release packaging can require native content (ADR-035/038/T067).
    /// </summary>
    public static class NavBakeProvenanceDocument
    {
        public const string Schema = "shooter.nav-bake-provenance.v1";
        public const string FileName = "nav-provenance.json";

        public static string SidecarPath(string cacheRoot) => Path.Combine(Path.GetFullPath(cacheRoot), FileName);

        public static byte[] BuildBytes(IReadOnlyList<MapPackageBuild> packages, string? toolIdentity)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            var entries = new List<object?>(packages.Count);
            bool allNative = packages.Count > 0;
            for (int i = 0; i < packages.Count; i++)
            {
                var p = packages[i];
                bool native = NavBakePublication.IsNativeProvenance(p.NavBakeProvenance);
                if (!native) allNative = false;
                entries.Add(new Dictionary<string, object?>
                {
                    ["mapId"] = p.MapId,
                    ["visualProfile"] = p.VisualProfile,
                    ["packageRootSha256"] = p.PackageRootHash.ToString(),
                    ["navigationSha256"] = p.NavigationHash.ToString(),
                    ["navSettingsSha256"] = p.NavSettingsHash.ToString(),
                    ["seed"] = p.Seed.ToString(CultureInfo.InvariantCulture),
                    ["navBakeProvenance"] = p.NavBakeProvenance,
                    ["native"] = native
                });
            }
            return MapDocumentJson.EncodeObject(new Dictionary<string, object?>
            {
                ["provenanceSchema"] = Schema,
                ["allNative"] = allNative,
                ["nativeToolIdentity"] = allNative ? (toolIdentity ?? "") : "",
                ["packages"] = entries
            });
        }

        public static void WriteAtomic(string cacheRoot, IReadOnlyList<MapPackageBuild> packages, string? toolIdentity)
        {
            string root = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(root);
            byte[] bytes = BuildBytes(packages, toolIdentity);
            string path = SidecarPath(root);
            string stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
            if (File.Exists(path)) File.Replace(stage, path, null);
            else File.Move(stage, path);
        }

        /// <summary>
        /// Validates the sidecar against the published packages on disk. When <paramref name="requireNative"/> is set,
        /// every package must be native-provenance, record an editor identity, and match its stored navigation hash.
        /// </summary>
        public static bool TryValidate(string cacheRoot, bool requireNative, out int packageCount, out bool allNative, out string diagnostic)
        {
            packageCount = 0;
            allNative = false;
            diagnostic = "missing";
            string path = SidecarPath(cacheRoot);
            if (!File.Exists(path)) return false;
            try
            {
                var root = MapJsonValue.Parse(File.ReadAllBytes(path)).Object;
                if (root["provenanceSchema"].String != Schema) { diagnostic = "provenance-schema-mismatch"; return false; }
                var entries = root["packages"].Array;
                if (entries.Count < 1) { diagnostic = "provenance-empty"; return false; }
                bool every = true;
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i].Object;
                    string rootHash = e["packageRootSha256"].String;
                    string prov = e["navBakeProvenance"].String;
                    bool claimedNative = e["native"].Boolean;
                    bool actualNative = NavBakePublication.IsNativeProvenance(prov);
                    if (claimedNative != actualNative) { diagnostic = "native-flag-mismatch:" + rootHash; return false; }
                    if (!actualNative) every = false;
                    var id = new ContentId(rootHash);
                    string dir = MapPackagePublisher.PackageDirectory(cacheRoot, id);
                    if (!MapPackagePublisher.TryOpenPublished(dir, id, out var published, out string open))
                    {
                        diagnostic = "package-open-failed:" + open;
                        return false;
                    }
                    if (ContentIdentity.Hash(published.NavigationSgaf).ToString() != e["navigationSha256"].String)
                    {
                        diagnostic = "navigation-hash-mismatch:" + rootHash;
                        return false;
                    }
                    packageCount++;
                }
                allNative = every;
                if (root["allNative"].Boolean != every) { diagnostic = "all-native-flag-mismatch"; return false; }
                if (requireNative)
                {
                    if (!every) { diagnostic = "software-navigation-not-release-native"; return false; }
                    if (string.IsNullOrWhiteSpace(root["nativeToolIdentity"].String)) { diagnostic = "native-tool-identity-missing"; return false; }
                }
                diagnostic = "ok";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = error.Message.Length <= 200 ? error.Message : error.Message.Substring(0, 200);
                return false;
            }
        }
    }
}
