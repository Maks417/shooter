#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    /// <summary>
    /// Atomic schema-2 JSON settings store (ADR-036): ≤64 KiB, last-good, quarantine,
    /// newer-schema preservation, nonblocking read-only errors.
    /// </summary>
    public sealed class FileUserSettingsStore : IUserSettingsStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly string path;
        private readonly string lastGoodPath;
        private readonly string tempPath;

        public FileUserSettingsStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            this.path = path;
            lastGoodPath = path + ".last-good";
            tempPath = path + ".tmp";
        }

        public string Path => path;

        public GameOperationResult Load(out UserSettings settings)
        {
            settings = UserSettings.Defaults;
            try
            {
                if (!File.Exists(path))
                {
                    if (File.Exists(lastGoodPath))
                        return LoadBytes(File.ReadAllBytes(lastGoodPath), preserveNewerFile: false, out settings);
                    return GameOperationResult.Ok;
                }

                byte[] bytes = File.ReadAllBytes(path);
                var result = LoadBytes(bytes, preserveNewerFile: true, out settings);
                if (result.Error == GameErrorCode.UnsupportedVersion)
                    return result;
                if (!result.Succeeded)
                {
                    Quarantine(bytes, result.Diagnostic);
                    if (File.Exists(lastGoodPath))
                    {
                        var recovered = LoadBytes(File.ReadAllBytes(lastGoodPath), preserveNewerFile: false, out settings);
                        if (recovered.Succeeded)
                            return new GameOperationResult(GameErrorCode.StorageError, "corrupt-recovered-last-good", "Settings were reset from last good.");
                    }
                    settings = UserSettings.Defaults;
                    return new GameOperationResult(GameErrorCode.StorageError, "corrupt-settings", "Settings were reset.");
                }
                return GameOperationResult.Ok;
            }
            catch (IOException)
            {
                settings = UserSettings.Defaults;
                return new GameOperationResult(GameErrorCode.StorageError, "settings-io", "Settings were reset.");
            }
            catch (UnauthorizedAccessException)
            {
                settings = UserSettings.Defaults;
                return new GameOperationResult(GameErrorCode.StorageError, "settings-io", "Settings were reset.");
            }
        }

        public GameOperationResult Save(UserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            try
            {
                byte[] bytes = UserSettingsJson.Encode(settings);
                string? dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllBytes(tempPath, bytes);
                using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    stream.Flush(true);

                if (File.Exists(path))
                {
                    // Preserve previous good before replace; then mirror the committed file to last-good.
                    File.Copy(path, lastGoodPath, overwrite: true);
                    File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
                File.Copy(path, lastGoodPath, overwrite: true);
                return GameOperationResult.Ok;
            }
            catch (IOException)
            {
                TryDeleteTemp();
                return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save settings.");
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteTemp();
                return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save settings.");
            }
            catch (ArgumentException ex)
            {
                TryDeleteTemp();
                return new GameOperationResult(GameErrorCode.InvalidRequest, ex.Message, "Settings were not saved.");
            }
        }

        private GameOperationResult LoadBytes(byte[] bytes, bool preserveNewerFile, out UserSettings settings)
        {
            settings = UserSettings.Defaults;
            if (!UserSettingsJson.TryDecode(bytes, out var parsed, out string diagnostic))
            {
                if (diagnostic.StartsWith("newer-", StringComparison.Ordinal))
                {
                    // Never overwrite a newer on-disk document.
                    _ = preserveNewerFile;
                    return new GameOperationResult(GameErrorCode.UnsupportedVersion, diagnostic, "Settings version unsupported.");
                }
                return new GameOperationResult(GameErrorCode.StorageError, diagnostic, "Settings were reset.");
            }
            settings = parsed ?? UserSettings.Defaults;
            return GameOperationResult.Ok;
        }

        private void Quarantine(byte[] bytes, string diagnostic)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
                string quarantine = path + ".corrupt-" + stamp;
                File.WriteAllBytes(quarantine, bytes);
                string note = path + ".corrupt-" + stamp + ".txt";
                File.WriteAllText(note, diagnostic ?? "corrupt", Utf8NoBom);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Quarantine is best-effort; load still returns defaults.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
