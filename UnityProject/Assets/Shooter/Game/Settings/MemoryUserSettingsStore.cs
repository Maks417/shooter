#nullable enable
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    public sealed class MemoryUserSettingsStore : IUserSettingsStore
    {
        private UserSettings current = UserSettings.Defaults;
        private byte[]? rawOverride;
        public bool FailNextSave { get; set; }
        public bool CorruptOnLoad { get; set; }
        public uint? NewerSchemaOnLoad { get; set; }
        public bool PreserveNewerRaw { get; private set; }

        public GameOperationResult Load(out UserSettings settings)
        {
            if (CorruptOnLoad)
            {
                settings = UserSettings.Defaults;
                return new GameOperationResult(GameErrorCode.StorageError, "corrupt-settings", "Settings were reset.");
            }
            if (NewerSchemaOnLoad is uint newer)
            {
                settings = UserSettings.Defaults;
                return new GameOperationResult(GameErrorCode.UnsupportedVersion, "settings-schema-" + newer, "Settings version unsupported.");
            }
            if (rawOverride != null)
            {
                if (!UserSettingsJson.TryDecode(rawOverride, out var parsed, out string diagnostic))
                {
                    settings = UserSettings.Defaults;
                    if (diagnostic.StartsWith("newer-", System.StringComparison.Ordinal))
                    {
                        PreserveNewerRaw = true;
                        return new GameOperationResult(GameErrorCode.UnsupportedVersion, diagnostic, "Settings version unsupported.");
                    }
                    PreserveNewerRaw = false;
                    return new GameOperationResult(GameErrorCode.StorageError, diagnostic, "Settings were reset.");
                }
                PreserveNewerRaw = false;
                settings = parsed ?? UserSettings.Defaults;
                current = settings;
                return GameOperationResult.Ok;
            }
            settings = current;
            return GameOperationResult.Ok;
        }

        public GameOperationResult Save(UserSettings settings)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                return new GameOperationResult(GameErrorCode.StorageError, "readonly-store", "Could not save settings.");
            }
            if (PreserveNewerRaw)
                return new GameOperationResult(GameErrorCode.UnsupportedVersion, "preserve-newer", "Settings version unsupported.");
            current = settings ?? throw new System.ArgumentNullException(nameof(settings));
            rawOverride = null;
            return GameOperationResult.Ok;
        }

        public void SeedRaw(byte[] bytes) => rawOverride = bytes;
        public void Seed(UserSettings settings) => current = settings ?? throw new System.ArgumentNullException(nameof(settings));
    }
}
