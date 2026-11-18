#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// ADR-107 Armory file (<c>shooter-armory.json</c> beside the settings file), with the settings store's rules:
    /// atomic temp-then-replace writes, a <c>.last-good</c> copy, quarantine of corrupt files and never overwriting a
    /// newer-schema document. Load always yields a usable profile (defaults when nothing valid exists).
    /// </summary>
    public sealed class FileArmoryStore : IArmoryStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly string path;
        private readonly string lastGoodPath;
        private readonly string tempPath;
        private bool newerOnDisk;

        public FileArmoryStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            this.path = path;
            lastGoodPath = path + ".last-good";
            tempPath = path + ".tmp";
        }

        public string Path => path;

        public GameOperationResult Load(out ArmoryProfile profile)
        {
            profile = ArmoryProfile.Defaults;
            newerOnDisk = false;
            try
            {
                if (!File.Exists(path))
                {
                    if (File.Exists(lastGoodPath) && ArmoryJson.TryDecode(File.ReadAllBytes(lastGoodPath), out var lastGood, out _))
                        profile = lastGood!;
                    return GameOperationResult.Ok;
                }
                byte[] bytes = File.ReadAllBytes(path);
                if (ArmoryJson.TryDecode(bytes, out var parsed, out string diagnostic))
                {
                    profile = parsed!;
                    return GameOperationResult.Ok;
                }
                if (diagnostic.StartsWith("newer-", StringComparison.Ordinal))
                {
                    newerOnDisk = true;
                    return new GameOperationResult(GameErrorCode.UnsupportedVersion, diagnostic, "Armory file is from a newer version.");
                }
                Quarantine(bytes, diagnostic);
                if (File.Exists(lastGoodPath) && ArmoryJson.TryDecode(File.ReadAllBytes(lastGoodPath), out var recovered, out _))
                {
                    profile = recovered!;
                    return new GameOperationResult(GameErrorCode.StorageError, "corrupt-recovered-last-good", "Armory was reset from last good.");
                }
                return new GameOperationResult(GameErrorCode.StorageError, "corrupt-armory", "Armory was reset.");
            }
            catch (IOException) { return new GameOperationResult(GameErrorCode.StorageError, "armory-io", "Armory was reset."); }
            catch (UnauthorizedAccessException) { return new GameOperationResult(GameErrorCode.StorageError, "armory-io", "Armory was reset."); }
        }

        public GameOperationResult Save(ArmoryProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (newerOnDisk || IsNewerOnDisk())
                return new GameOperationResult(GameErrorCode.UnsupportedVersion, "newer-armory-preserved", "Armory was not saved over a newer version.");
            try
            {
                byte[] bytes = ArmoryJson.Encode(profile);
                string? dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(tempPath, bytes);
                using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    stream.Flush(true);
                if (File.Exists(path))
                {
                    File.Copy(path, lastGoodPath, overwrite: true);
                    File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
                }
                else
                    File.Move(tempPath, path);
                File.Copy(path, lastGoodPath, overwrite: true);
                return GameOperationResult.Ok;
            }
            catch (IOException) { TryDeleteTemp(); return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save the Armory."); }
            catch (UnauthorizedAccessException) { TryDeleteTemp(); return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save the Armory."); }
        }

        private bool IsNewerOnDisk()
        {
            try
            {
                return File.Exists(path) && !ArmoryJson.TryDecode(File.ReadAllBytes(path), out _, out string diagnostic)
                    && diagnostic.StartsWith("newer-", StringComparison.Ordinal);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private void Quarantine(byte[] bytes, string diagnostic)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
                File.WriteAllBytes(path + ".corrupt-" + stamp, bytes);
                File.WriteAllText(path + ".corrupt-" + stamp + ".txt", diagnostic ?? "corrupt", Utf8NoBom);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void TryDeleteTemp()
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
