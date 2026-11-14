#nullable enable
using System;
using System.IO;
using System.Text;
using Shooter.Game.Contracts;
using Shooter.Game.Settings;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class UserSettingsTests
    {
        [Fact]
        public void Defaults_Match_Shell_Schema2()
        {
            var s = UserSettings.Defaults;
            Assert.Equal(2u, s.Schema);
            Assert.Equal(1f, s.MasterVolume);
            Assert.Equal(1f, s.EffectsVolume);
            Assert.Equal(0.7f, s.UiVolume);
            Assert.Equal(0, s.QualityTier);
            Assert.Equal(1280, s.ResolutionWidth);
            Assert.Equal(720, s.ResolutionHeight);
            Assert.Equal(DisplayMode.Windowed, s.DisplayMode);
            Assert.False(s.VSync);
            Assert.Equal(60, s.FpsCap);
            Assert.Equal(80f, s.HipFovDegrees);
            Assert.Equal(0.10f, s.MouseSensitivity);
            Assert.Equal(1f, s.UiScale);
            Assert.Equal(15, s.Bindings.Count);
            Assert.Equal(GameContractVersion.SettingsSchema, 2u);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LegacyBindingsGainSlideWithoutStealingCustomizedF(bool customizedF)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "user-settings.valid.json");
            string legacy = File.ReadAllText(path);
            if (customizedF) legacy = legacy.Replace("Key.R", "Key.F");
            Assert.True(UserSettingsJson.TryDecodeText(legacy, out var settings, out string diagnostic), diagnostic);
            Assert.Contains(settings!.Bindings, b => b.Action == "Slide" && b.Control == (customizedF ? "Unbound" : "Key.F"));
            Assert.Contains(settings.Bindings, b => b.Action == "Reload" && b.Control == (customizedF ? "Key.F" : "Key.R"));
            Assert.True(UserSettingsJson.TryDecode(UserSettingsJson.Encode(settings), out var again, out diagnostic), diagnostic);
            Assert.Equal(settings.Bindings.Count, again!.Bindings.Count);
            var editor = new InputBindingEditor(again.Bindings);
            Assert.True(editor.TryBeginListen("Slide"));
            Assert.True(editor.TryOfferControl("Key.Space"));
            Assert.Equal("Jump", editor.ConflictAction);
            Assert.True(editor.TrySwapConflict());
            Assert.Contains(editor.Working, b => b.Action == "Slide" && b.Control == "Key.Space");
        }

        [Fact]
        public void SlideSettingsFixtureRoundtripsWithSprintChordBindings()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "user-settings-slide.valid.json");
            Assert.True(UserSettingsJson.TryDecode(File.ReadAllBytes(path), out var settings, out string diagnostic), diagnostic);
            Assert.Contains(settings!.Bindings, b => b.Action == "Slide" && b.Control == "Key.F");
            Assert.Contains(settings.Bindings, b => b.Action == "Sprint" && b.Control == "Key.LeftShift");
        }

        [Fact]
        public void Json_Roundtrip_Valid_Fixture()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "user-settings.valid.json");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(UserSettingsJson.TryDecode(bytes, out var settings, out string diagnostic), diagnostic);
            Assert.NotNull(settings);
            byte[] encoded = UserSettingsJson.Encode(settings!);
            Assert.True(encoded.Length <= GameContractVersion.MaxSettingsFileBytes);
            Assert.True(UserSettingsJson.TryDecode(encoded, out var again, out diagnostic), diagnostic);
            Assert.Equal(settings!.EffectsVolume, again!.EffectsVolume);
            Assert.Equal(settings.HipFovDegrees, again.HipFovDegrees);
            Assert.Equal(settings.Bindings.Count, again.Bindings.Count);
        }

        [Fact]
        public void Legacy_Schema1_Migrates_And_Clamps()
        {
            const string legacy = "schema=1;master=1;sfx=0.8;music=0.5;quality=1;mouse=1;invertY=0;subtitles=1;uiScale=0.9";
            Assert.True(UserSettingsJson.TryDecodeText(legacy, out var settings, out string diagnostic), diagnostic);
            Assert.NotNull(settings);
            Assert.Equal(2u, settings!.Schema);
            Assert.Equal(0.8f, settings.EffectsVolume);
            Assert.Equal(0.5f, settings.UiVolume);
            Assert.Equal(1f, settings.MouseSensitivity);
            Assert.Equal(1f, settings.UiScale);
            Assert.True(settings.Subtitles);
            Assert.Equal(1, settings.QualityTier);
        }

        [Fact]
        public void Rejects_NaN_Oversized_And_Newer()
        {
            Assert.Throws<ArgumentException>(() => new UserSettings(
                2, float.NaN, 1f, 0.7f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings()));

            var oversized = new string('x', GameContractVersion.MaxSettingsFileBytes + 8);
            Assert.False(UserSettingsJson.TryDecodeText("{\"schema\":2,\"pad\":\"" + oversized + "\"}", out _, out string diag));
            Assert.Equal("oversized", diag);

            Assert.False(UserSettingsJson.TryDecodeText("{\"schema\":9,\"schemaId\":\"x\"}", out _, out diag));
            Assert.StartsWith("newer-", diag);
        }

        [Fact]
        public void FileStore_Corrupt_Quarantine_Newer_Preserve_LastGood()
        {
            string dir = Path.Combine(Path.GetTempPath(), "shooter-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.json");
            try
            {
                var store = new FileUserSettingsStore(path);
                Assert.True(store.Save(UserSettings.Defaults).Succeeded);
                Assert.True(File.Exists(path + ".last-good"));

                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("{not-json"));
                var corrupt = store.Load(out var recovered);
                Assert.Equal(GameErrorCode.StorageError, corrupt.Error);
                Assert.Equal(2u, recovered.Schema);
                Assert.NotEmpty(Directory.GetFiles(dir, "settings.json.corrupt-*"));

                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("{\"schema\":9}"));
                var newer = store.Load(out _);
                Assert.Equal(GameErrorCode.UnsupportedVersion, newer.Error);
                Assert.True(File.Exists(path));
                Assert.Contains("\"schema\":9", File.ReadAllText(path), StringComparison.Ordinal);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public void FileStore_Atomic_Replace_Preserves_LastGood_On_Interrupt_Pattern()
        {
            string dir = Path.Combine(Path.GetTempPath(), "shooter-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.json");
            try
            {
                var store = new FileUserSettingsStore(path);
                Assert.True(store.Save(UserSettings.Defaults).Succeeded);
                var muted = new UserSettings(
                    2, 0f, 0f, 0f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                    UserSettings.DefaultBindings());
                Assert.True(store.Save(muted).Succeeded);

                // Simulate interrupted replace: primary missing, last-good present.
                File.Delete(path);
                Assert.True(File.Exists(path + ".last-good"));
                Assert.True(store.Load(out var loaded).Succeeded);
                Assert.Equal(0f, loaded.MasterVolume);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public void Editor_Apply_Cancel_Defaults_And_Display_Confirm_Clock()
        {
            var store = new MemoryUserSettingsStore();
            var applier = new MemoryClientSettingsApplier();
            var editor = new UserSettingsEditor(UserSettings.Defaults);

            var draft = new UserSettings(
                2, 0.5f, 0.4f, 0.3f, 1, 1920, 1080, DisplayMode.Fullscreen, true, 120, 90f, 0.2f, true, 1.25f, true, true, 1.2f, true,
                UserSettings.DefaultBindings());
            editor.SetDraft(draft);
            Assert.True(editor.IsDirty);
            Assert.True(editor.Apply(store, applier).Succeeded);
            Assert.True(applier.HasPendingDisplayChange);
            Assert.False(editor.IsDirty);
            // Restart rollback: disk keeps confirmed display until Keep.
            Assert.True(store.Load(out var pendingDisk).Succeeded);
            Assert.Equal(1280, pendingDisk.ResolutionWidth);
            Assert.Equal(720, pendingDisk.ResolutionHeight);
            Assert.Equal(0.5f, pendingDisk.MasterVolume);
            Assert.Equal(1920, editor.Applied.ResolutionWidth);

            editor.SetDraft(UserSettings.Defaults);
            editor.Cancel();
            Assert.Equal(0.5f, editor.Draft.MasterVolume);

            editor.RestoreDefaults();
            Assert.Equal(UserSettings.Defaults.MasterVolume, editor.Draft.MasterVolume);
            Assert.True(editor.IsDirty);

            var clock = new DisplayChangeConfirmClock();
            clock.Begin();
            Assert.True(clock.Pending);
            Assert.True(clock.Tick(1f, focusLost: true, out bool revert));
            Assert.True(revert);
            Assert.True(editor.RevertDisplay(applier).Succeeded);
            Assert.Equal(1, applier.RevertCount);

            store.FailNextSave = true;
            editor.SetDraft(new UserSettings(
                2, 0.2f, 1f, 0.7f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings()));
            var saveFail = editor.Apply(store, applier);
            Assert.Equal(GameErrorCode.StorageError, saveFail.Error);
            Assert.Equal(0.2f, editor.Applied.MasterVolume);
        }

        [Fact]
        public void Editor_ConfirmDisplay_Persists_New_Surface()
        {
            var store = new MemoryUserSettingsStore();
            var applier = new MemoryClientSettingsApplier();
            var editor = new UserSettingsEditor(UserSettings.Defaults);
            editor.SetDraft(new UserSettings(
                2, 1f, 1f, 0.7f, 0, 1920, 1080, DisplayMode.Borderless, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings()));
            Assert.True(editor.Apply(store, applier).Succeeded);
            Assert.True(applier.HasPendingDisplayChange);
            Assert.True(editor.ConfirmDisplay(store, applier).Succeeded);
            Assert.False(applier.HasPendingDisplayChange);
            Assert.True(store.Load(out var kept).Succeeded);
            Assert.Equal(1920, kept.ResolutionWidth);
            Assert.Equal(1080, kept.ResolutionHeight);
            Assert.Equal(DisplayMode.Borderless, kept.DisplayMode);
        }

        [Fact]
        public void BindingEditor_Apply_Persist_Conflict_Swap_And_Cancel()
        {
            var editor = new InputBindingEditor(UserSettings.DefaultBindings());
            Assert.False(editor.TryBeginListen("Pause"));
            Assert.True(editor.TryBeginListen("Reload"));
            Assert.True(editor.IsListening);

            // Offer a free control.
            Assert.True(editor.TryOfferControl("Key.G"));
            Assert.False(editor.IsListening);
            Assert.Contains(editor.Working, b => b.Action == "Reload" && b.Control == "Key.G");

            // Conflict with Fire (Mouse.Left): require swap/cancel.
            Assert.True(editor.TryBeginListen("Jump"));
            Assert.True(editor.TryOfferControl("Mouse.Left"));
            Assert.True(editor.HasConflict);
            Assert.Equal("Fire", editor.ConflictAction);
            Assert.True(editor.TryCancelConflict());
            Assert.False(editor.HasConflict);
            Assert.True(editor.IsListening);

            Assert.True(editor.TryOfferControl("Mouse.Left"));
            Assert.True(editor.TrySwapConflict());
            Assert.False(editor.IsListening);
            Assert.Contains(editor.Working, b => b.Action == "Jump" && b.Control == "Mouse.Left");
            Assert.Contains(editor.Working, b => b.Action == "Fire" && b.Control == "Key.Space");

            // Escape cannot steal Pause.
            Assert.True(editor.TryBeginListen("Crouch"));
            Assert.False(editor.TryOfferControl(UserSettings.ReservedEscapeControl));
            editor.CancelListen();

            var store = new MemoryUserSettingsStore();
            var applier = new MemoryClientSettingsApplier();
            var settingsEditor = new UserSettingsEditor(UserSettings.Defaults);
            settingsEditor.SetDraft(editor.WithBindings(UserSettings.Defaults));
            Assert.True(settingsEditor.Apply(store, applier).Succeeded);
            Assert.True(store.Load(out var loaded).Succeeded);
            Assert.Contains(loaded.Bindings, b => b.Action == "Reload" && b.Control == "Key.G");
            Assert.Contains(loaded.Bindings, b => b.Action == "Jump" && b.Control == "Mouse.Left");
            Assert.Contains(loaded.Bindings, b => b.Action == "Pause" && b.Control == UserSettings.ReservedEscapeControl);
        }

        [Fact]
        public void MemoryStore_Corrupt_And_Newer_Still_Covered()
        {
            var store = new MemoryUserSettingsStore { CorruptOnLoad = true };
            Assert.False(store.Load(out _).Succeeded);
            store.CorruptOnLoad = false;
            store.NewerSchemaOnLoad = 9;
            Assert.Equal(GameErrorCode.UnsupportedVersion, store.Load(out _).Error);
            store.NewerSchemaOnLoad = null;
            Assert.True(store.Save(UserSettings.Defaults).Succeeded);
            Assert.True(store.Load(out var loaded).Succeeded);
            Assert.Equal(2u, loaded.Schema);
        }

        [Fact]
        public void Bindings_Reject_Duplicate_And_Stolen_Escape()
        {
            var bindings = UserSettings.DefaultBindings();
            var list = new InputBinding[bindings.Count];
            for (int i = 0; i < bindings.Count; i++) list[i] = bindings[i];
            list[5] = new InputBinding("Fire", "Key.R");
            Assert.Throws<ArgumentException>(() => new UserSettings(
                2, 1f, 1f, 0.7f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false, list));

            list[5] = new InputBinding("Fire", "Key.Escape");
            list[13] = new InputBinding("Pause", "Key.Escape");
            Assert.Throws<ArgumentException>(() => new UserSettings(
                2, 1f, 1f, 0.7f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false, list));
        }
    }
}
