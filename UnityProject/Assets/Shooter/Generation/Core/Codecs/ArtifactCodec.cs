#nullable enable
using System;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    /// <summary>Field-by-field SGAF v1 envelope; hashes verified before typed body decoding.</summary>
    public static class ArtifactCodec
    {
        public const int HeaderBytes = 116;
        public const int MaxBodyBytes = ContractVersion.MaxArtifactBytes - HeaderBytes;
        private static void Kind(ContentKind kind)
        { if (kind < ContentKind.Mesh || kind > ContentKind.Navigation) throw new ArgumentException("Unknown artifact kind.", nameof(kind)); }
        public static byte[] Encode(ContentKind kind, ReadOnlySpan<byte> body, ContentId generatorHash, ContentId toolchainHash)
        {
            Kind(kind); BodyWriting.Size(body.Length);
            using var stream = new MemoryStream(HeaderBytes + body.Length);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(new byte[] {83, 71, 65, 70}); writer.Write(1U); writer.Write((uint)kind); writer.Write(0U); writer.Write((uint)body.Length);
            writer.Write(ContentIdentity.Hash(body).ToBytes()); writer.Write(generatorHash.ToBytes()); writer.Write(toolchainHash.ToBytes()); writer.Write(body.ToArray());
            return stream.ToArray();
        }
        public static byte[] Decode(ReadOnlySpan<byte> file, ContentKind expectedKind, ContentId generatorHash, ContentId toolchainHash)
        {
            Kind(expectedKind);
            if (file.Length < HeaderBytes || file.Length > ContractVersion.MaxArtifactBytes) throw new FormatException("Artifact envelope size.");
            // Reader's body-size limit excludes the header; slice only the fixed header here.
            var reader = new BodyReader(file.Slice(0, HeaderBytes));
            BodyWriting.Valid(reader.U32() == 0x46414753, "Artifact magic.");
            BodyWriting.Valid(reader.U32() == 1, "Unsupported artifact version.");
            BodyWriting.Valid(reader.U32() == (uint)expectedKind, "Artifact kind mismatch.");
            BodyWriting.Valid(reader.U32() == 0, "Unsupported artifact compression.");
            uint count = reader.U32();
            BodyWriting.Valid(count <= MaxBodyBytes && count == file.Length - HeaderBytes, "Artifact body length mismatch.");
            ContentId bodyHash = new ContentId(reader.Bytes(32));
            BodyWriting.Valid(new ContentId(reader.Bytes(32)) == generatorHash, "Artifact generator fingerprint mismatch.");
            BodyWriting.Valid(new ContentId(reader.Bytes(32)) == toolchainHash, "Artifact toolchain mismatch.");
            ReadOnlySpan<byte> body = file.Slice(HeaderBytes);
            BodyWriting.Valid(ContentIdentity.Hash(body) == bodyHash, "Artifact body hash mismatch.");
            return body.ToArray();
        }
        public static void ValidateTyped(ContentKind kind, ReadOnlySpan<byte> body)
        {
            switch (kind)
            {
                case ContentKind.Mesh: GeometryCodec.DecodeMesh(body); break;
                case ContentKind.Skeleton: GeometryCodec.DecodeSkeleton(body); break;
                case ContentKind.Material: GeometryCodec.DecodeMaterial(body); break;
                case ContentKind.Collision: GeometryCodec.DecodeCollision(body); break;
                case ContentKind.Navigation: GeometryCodec.DecodeNavigation(body); break;
                default: throw new ArgumentException("Unknown artifact kind.", nameof(kind));
            }
        }
    }
}
