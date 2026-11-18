#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// ADR-108 career file (<c>shooter-career.json</c> beside the settings and Armory files), with the settings store's rules:
    /// atomic temp-then-replace writes, a <c>.last-good</c> copy, quarantine of corrupt files and never overwriting a
    /// newer-schema document. Load always yields a usable profile (defaults when nothing valid exists).
    /// </summary>
    public sealed class FileCareerStore : ICareerStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly string path;
        private readonly string lastGoodPath;
        private readonly string tempPath;
        private bool newerOnDisk;
        // A career that could not be read is never overwritten with an empty one (progress is not reconstructible).
        private bool unreadable;

        public FileCareerStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            this.path = path;
            lastGoodPath = path + ".last-good";
            tempPath = path + ".tmp";
        }

        public string Path => path;

        public GameOperationResult Load(out CareerProfile profile)
        {
            profile = CareerProfile.Empty;
            newerOnDisk = false;
            unreadable = false;
            try
            {
                if (!File.Exists(path))
                {
                    if (File.Exists(lastGoodPath) && CareerJson.TryDecode(File.ReadAllBytes(lastGoodPath), out var lastGood, out _))
                        profile = lastGood!;
                    return GameOperationResult.Ok;
                }
                byte[] bytes = File.ReadAllBytes(path);
                if (CareerJson.TryDecode(bytes, out var parsed, out string diagnostic))
                {
                    profile = parsed!;
                    return GameOperationResult.Ok;
                }
                if (diagnostic.StartsWith("newer-", StringComparison.Ordinal))
                {
                    newerOnDisk = true;
                    return new GameOperationResult(GameErrorCode.UnsupportedVersion, diagnostic, "Career file is from a newer version.");
                }
                Quarantine(bytes, diagnostic);
                if (File.Exists(lastGoodPath) && CareerJson.TryDecode(File.ReadAllBytes(lastGoodPath), out var recovered, out _))
                {
                    profile = recovered!;
                    return new GameOperationResult(GameErrorCode.StorageError, "corrupt-recovered-last-good", "Career was restored from the last good copy.");
                }
                return new GameOperationResult(GameErrorCode.StorageError, "corrupt-career", "Career was reset.");
            }
            catch (IOException) { unreadable = true; return new GameOperationResult(GameErrorCode.StorageError, "career-io", "Career could not be read."); }
            catch (UnauthorizedAccessException) { unreadable = true; return new GameOperationResult(GameErrorCode.StorageError, "career-io", "Career could not be read."); }
        }

        public GameOperationResult Save(CareerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (unreadable)
                return new GameOperationResult(GameErrorCode.StorageError, "unreadable-career-preserved", "Career was not saved over an unreadable file.");
            if (newerOnDisk || IsNewerOnDisk())
                return new GameOperationResult(GameErrorCode.UnsupportedVersion, "newer-career-preserved", "Career was not saved over a newer version.");
            try
            {
                byte[] bytes = CareerJson.Encode(profile);
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
            catch (IOException) { TryDeleteTemp(); return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save the career."); }
            catch (UnauthorizedAccessException) { TryDeleteTemp(); return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save the career."); }
        }

        private bool IsNewerOnDisk()
        {
            try
            {
                return File.Exists(path) && !CareerJson.TryDecode(File.ReadAllBytes(path), out _, out string diagnostic)
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
