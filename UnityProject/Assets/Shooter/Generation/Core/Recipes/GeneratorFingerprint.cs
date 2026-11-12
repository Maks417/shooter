#nullable enable
using System;
using System.IO;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Generation
{
    public static class GeneratorFingerprint
    {
        public static ContentId Compute(string id, string version)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            Write(writer, id); Write(writer, version);
            return ContentIdentity.Hash(stream.ToArray());
        }
        private static void Write(BinaryWriter writer, string value)
        {
            if (value == null || value.Length == 0 || value.Length > 4096) throw new ArgumentException("Invalid generator identity.");
            foreach (char ch in value) if (ch < 32 || ch > 126) throw new ArgumentException("Generator identity must be printable ASCII.");
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((uint)bytes.Length); writer.Write(bytes);
        }
    }
}
