#nullable enable
using Shooter.Game.Contracts;

namespace Shooter.Game.Settings
{
    public sealed class MemoryUserSettingsStore : IUserSettingsStore
    {
        private UserSettings current = UserSettings.Defaults;
        public bool FailNextSave { get; set; }
        public bool CorruptOnLoad { get; set; }
        public uint? NewerSchemaOnLoad { get; set; }

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
            current = settings ?? throw new System.ArgumentNullException(nameof(settings));
            return GameOperationResult.Ok;
        }
    }
}
