#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;

namespace Shooter.Content
{
    public sealed class CacheReadException : IOException
    {
        public ErrorCode Error { get; }
        public CacheReadException(ErrorCode error, string message) : base(message) { Error = error; }
    }

    /// <summary>Read-only verified byte cache. Does not repair, delete, generate or create Unity objects.</summary>
    public class CacheFiles
    {
        public string Root { get; }
        public ContentId ExpectedToolchainHash { get; }
        public CacheFiles(string root, ContentId expectedToolchainHash)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A cache root is required.", nameof(root));
            Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Root.Length == 0 || Root == Path.GetPathRoot(Root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                throw new ArgumentException("A filesystem root cannot be a content cache.", nameof(root));
            ExpectedToolchainHash = expectedToolchainHash;
            AssertSafePath(Root);
        }

        public string ContentDirectory(ContentId id)
        {
            string hex = id.ToString();
            string path = Path.Combine(Root, hex.Substring(0, 2), hex);
            AssertSafePath(path);
            return path;
        }

        public virtual ContentManifest ReadManifest(ContentId id)
        {
            byte[] bytes = ReadBounded(Path.Combine(ContentDirectory(id), "manifest.json"), CanonicalJson.MaxBytes);
            ContentManifest manifest;
            try
            {
                var root = JsonValue.Parse(bytes).Object;
                if (root.TryGetValue("manifestSchema", out var schema) && schema.String != ContentManifest.Schema)
                    throw new CacheReadException(ErrorCode.UnsupportedVersion, "Unsupported content manifest schema.");
                manifest = ContentManifest.Decode(bytes);
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is OverflowException)
            {
                throw new CacheReadException(ErrorCode.CorruptContent, "Invalid content manifest: " + Safe(error.Message));
            }
            if (manifest.ContentId != id) throw new CacheReadException(ErrorCode.CorruptContent, "Manifest content identity differs from requested directory.");
            if (manifest.ToolchainHash != ExpectedToolchainHash) throw new CacheReadException(ErrorCode.UnsupportedVersion, "Content requires a different toolchain.");
            return manifest;
        }

        public virtual byte[] ReadArtifact(ContentManifest manifest, ManifestArtifact artifact)
        {
            if (manifest == null || artifact == null) throw new ArgumentNullException("manifest/artifact");
            if (manifest.ToolchainHash != ExpectedToolchainHash) throw new CacheReadException(ErrorCode.UnsupportedVersion, "Content requires a different toolchain.");
            if (!manifest.Artifacts.Contains(artifact)) throw new CacheReadException(ErrorCode.InvalidInput, "Artifact does not belong to manifest.");
            byte[] bytes = ReadBounded(Path.Combine(ContentDirectory(manifest.ContentId), artifact.Path), ContractVersion.MaxArtifactBytes, artifact.Bytes);
            if (ContentIdentity.Hash(bytes) != artifact.Sha256) throw new CacheReadException(ErrorCode.CorruptContent, "Artifact file hash mismatch.");
            if (bytes.Length >= ArtifactCodec.HeaderBytes && U32(bytes, 0) == 0x46414753 && (U32(bytes, 4) != 1 || U32(bytes, 12) != 0))
                throw new CacheReadException(ErrorCode.UnsupportedVersion, "Unsupported artifact envelope version/compression.");
            try
            {
                byte[] body = ArtifactCodec.Decode(bytes, artifact.Kind, manifest.GeneratorHash, manifest.ToolchainHash);
                ArtifactCodec.ValidateTyped(artifact.Kind, body);
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is OverflowException || error is EndOfStreamException)
            {
                throw new CacheReadException(ErrorCode.CorruptContent, "Invalid content artifact: " + Safe(error.Message));
            }
            return bytes;
        }

        public void VerifyContent(ContentManifest manifest) { ReadAll(manifest); }

        internal Dictionary<ManifestArtifact, FrozenList<byte>> ReadAll(ContentManifest manifest)
        {
            var payloads = new Dictionary<ManifestArtifact, FrozenList<byte>>();
            long total = 0; int vertices = 0, triangles = 0, submeshes = 0, bones = 0;
            foreach (var artifact in manifest.Artifacts)
            {
                total += artifact.Bytes;
                if (total > 256L * 1024 * 1024) throw new CacheReadException(ErrorCode.LimitExceeded, "Content byte limit exceeded.");
                byte[] file = ReadArtifact(manifest, artifact);
                byte[] body = ArtifactCodec.Decode(file, artifact.Kind, manifest.GeneratorHash, manifest.ToolchainHash);
                if (artifact.Kind == ContentKind.Mesh)
                {
                    var mesh = GeometryCodec.DecodeMesh(body);
                    vertices = checked(vertices + mesh.Positions.Count); triangles = checked(triangles + mesh.Indices.Count / 3); submeshes = checked(submeshes + mesh.Submeshes.Count);
                    ValidateBounds(manifest, artifact, body);
                }
                else if (artifact.Kind == ContentKind.Skeleton) bones = checked(bones + GeometryCodec.DecodeSkeleton(body).Joints.Count);
                else if (artifact.Kind == ContentKind.Collision) ValidateBounds(manifest, artifact, body);
                payloads.Add(artifact, new FrozenList<byte>(file, ContractVersion.MaxArtifactBytes));
            }
            if (vertices != manifest.VertexCount || triangles != manifest.TriangleCount || submeshes != manifest.SubmeshCount || bones != manifest.BoneCount)
                throw new CacheReadException(ErrorCode.CorruptContent, "Manifest geometry counts do not match payloads.");
            return payloads;
        }

        public void AssertSafePath(string fullPath)
        {
            string path = Path.GetFullPath(fullPath);
            if (!path.Equals(Root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new CacheReadException(ErrorCode.CorruptContent, "Path escapes content cache.");
            // Also inspect ancestors above Root; a cache rooted below a junction must reject.
            for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            {
                try
                {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new CacheReadException(ErrorCode.CorruptContent, "Reparse points are not permitted in content paths.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                string name = Path.GetFileName(current);
                if (name.Length != 0 && (name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal) || name.Contains(":")))
                    throw new CacheReadException(ErrorCode.CorruptContent, "Ambiguous content path component.");
            }
        }

        public void CreateDirectories(string directory)
        {
            string path = Path.GetFullPath(directory);
            AssertSafePath(path);
            Directory.CreateDirectory(path);
            AssertSafePath(path);
        }

        private byte[] ReadBounded(string path, int maxBytes, int? expectedBytes = null)
        {
            AssertSafePath(path);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            AssertSafePath(path);
            if (file.Length <= 0 || file.Length > maxBytes) throw new CacheReadException(ErrorCode.LimitExceeded, "Content file byte limit exceeded.");
            if (expectedBytes.HasValue && file.Length != expectedBytes.Value) throw new CacheReadException(ErrorCode.CorruptContent, "Content file length differs from manifest.");
            var bytes = new byte[(int)file.Length]; int offset = 0;
            while (offset < bytes.Length)
            {
                int read = file.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw new CacheReadException(ErrorCode.CorruptContent, "Content file truncated during read.");
                offset += read;
            }
            if (file.ReadByte() != -1) throw new CacheReadException(ErrorCode.CorruptContent, "Content file grew during read.");
            return bytes;
        }

        private static string Safe(string message) => message.Length <= 256 ? message : message.Substring(0, 256);
        private static void ValidateBounds(ContentManifest manifest, ManifestArtifact artifact, byte[] body)
        {
            try { ArtifactBounds.Validate(manifest.Bounds, artifact.Kind, body); }
            catch (FormatException error) { throw new CacheReadException(ErrorCode.CorruptContent, error.Message); }
        }
        private static uint U32(byte[] bytes, int offset) => (uint)bytes[offset] | (uint)bytes[offset + 1] << 8 | (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24;
    }
}
