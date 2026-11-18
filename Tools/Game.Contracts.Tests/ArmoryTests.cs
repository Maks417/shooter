#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Game.Settings;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Session;
using Shooter.World.Runtime;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    /// <summary>ADR-107 Armory file and the human's pinned loadout in the offline session.</summary>
    public sealed class ArmoryTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "shooter-armory-" + Guid.NewGuid().ToString("N"));
        private string FilePath => Path.Combine(dir, "shooter-armory.json");

        public ArmoryTests() => Directory.CreateDirectory(dir);
        public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

        private static ArmoryProfile Sample() => new ArmoryProfile("weapons/ak47", "weapons/golden-eagle",
            new Dictionary<string, string> { ["weapons/m16"] = "sights/collimator", ["weapons/ak47"] = "sights/scope2" });

        [Fact]
        public void Json_round_trips_with_sorted_sight_keys()
        {
            byte[] bytes = ArmoryJson.Encode(Sample());
            Assert.Equal("{\"schema\":1,\"schemaId\":\"shooter.armory.v1\",\"primary\":\"weapons/ak47\",\"sidearm\":\"weapons/golden-eagle\","
                + "\"sights\":{\"weapons/ak47\":\"sights/scope2\",\"weapons/m16\":\"sights/collimator\"}}", Encoding.UTF8.GetString(bytes));
            Assert.True(ArmoryJson.TryDecode(bytes, out var decoded, out _));
            Assert.True(Sample().SameAs(decoded));
        }

        [Fact]
        public void Fixtures_decode_and_the_invalid_one_is_rejected()
        {
            string root = RepoRoot();
            Assert.True(ArmoryJson.TryDecode(File.ReadAllBytes(Path.Combine(root, "docs", "armory-fixtures", "armory.valid.json")), out var valid, out string ok), ok);
            Assert.Equal("weapons/m16", valid!.Primary);
            Assert.False(ArmoryJson.TryDecode(File.ReadAllBytes(Path.Combine(root, "docs", "armory-fixtures", "armory.invalid-sight.json")), out _, out string bad));
            Assert.Equal("bad-sights", bad);
        }

        [Theory]
        [InlineData("", "empty")]
        [InlineData("{\"schema\":1,\"schemaId\":\"shooter.armory.v1\",\"primary\":\"M16\",\"sidearm\":\"weapons/g17\"}", "bad-weapon-id")]
        [InlineData("{\"schema\":1,\"schemaId\":\"other\",\"primary\":\"weapons/m16\",\"sidearm\":\"weapons/g17\"}", "unsupported-schema")]
        [InlineData("{\"schema\":2,\"schemaId\":\"shooter.armory.v2\"}", "newer-2")]
        [InlineData("{\"schema\":1,\"schemaId\":\"shooter.armory.v1\",\"primary\":\"weapons/m16\",\"sidearm\":\"weapons/g17\",\"sights\":\"x\"}", "bad-sights")]
        public void Decoder_rejects_bad_documents(string text, string diagnostic)
        {
            Assert.False(ArmoryJson.TryDecode(Encoding.UTF8.GetBytes(text), out _, out string actual));
            Assert.Equal(diagnostic, actual);
        }

        [Fact]
        public void Unknown_top_level_keys_are_ignored()
        {
            Assert.True(ArmoryJson.TryDecode(Encoding.UTF8.GetBytes(
                "{\"schema\":1,\"schemaId\":\"shooter.armory.v1\",\"primary\":\"weapons/m16\",\"sidearm\":\"weapons/g17\",\"future\":true}"), out var profile, out _));
            Assert.Empty(profile!.Sights);
        }

        [Fact]
        public void File_store_saves_atomically_and_recovers_from_corruption()
        {
            var store = new FileArmoryStore(FilePath);
            Assert.True(store.Load(out var initial).Succeeded);
            Assert.True(ArmoryProfile.Defaults.SameAs(initial));
            Assert.True(store.Save(Sample()).Succeeded);
            Assert.True(File.Exists(FilePath + ".last-good"));
            Assert.False(File.Exists(FilePath + ".tmp"));
            Assert.True(new FileArmoryStore(FilePath).Load(out var loaded).Succeeded);
            Assert.True(Sample().SameAs(loaded));

            File.WriteAllText(FilePath, "{ not json");
            var result = new FileArmoryStore(FilePath).Load(out var recovered);
            Assert.False(result.Succeeded);
            Assert.Equal("corrupt-recovered-last-good", result.Diagnostic);
            Assert.True(Sample().SameAs(recovered));
            Assert.NotEmpty(Directory.GetFiles(dir, "shooter-armory.json.corrupt-*"));
        }

        [Fact]
        public void File_store_never_overwrites_a_newer_document()
        {
            const string newer = "{\"schema\":9,\"schemaId\":\"shooter.armory.v9\"}";
            File.WriteAllText(FilePath, newer);
            var store = new FileArmoryStore(FilePath);
            Assert.Equal(GameErrorCode.UnsupportedVersion, store.Load(out var profile).Error);
            Assert.True(ArmoryProfile.Defaults.SameAs(profile));
            Assert.False(store.Save(Sample()).Succeeded);
            Assert.Equal(newer, File.ReadAllText(FilePath));
        }

        [Fact]
        public void Armory_catalog_offers_only_spawning_named_guns()
        {
            Assert.Contains(new DefinitionId("weapons/m16"), ArmoryCatalog.Primaries);
            Assert.DoesNotContain(WeaponLoadoutPolicy.DefaultPrimaryId, ArmoryCatalog.Primaries);
            Assert.DoesNotContain(WeaponLoadoutPolicy.PistolId, ArmoryCatalog.Sidearms);
            Assert.Equal(new[] { new DefinitionId("weapons/g17"), new DefinitionId("weapons/golden-eagle") }, ArmoryCatalog.Sidearms);
            Assert.True(ArmoryCatalog.TryGetStats(new DefinitionId("weapons/ak47"), out int damage, out int magazine, out int rpm, out string mode));
            Assert.Equal(36, damage);
            Assert.Equal(30, magazine);
            Assert.Equal(600, rpm);
            Assert.Equal("AUTO", mode);
        }

        [Fact]
        public async Task Session_spawns_and_respawns_the_human_with_the_armory_loadout_while_bots_stay_random()
        {
            var catalog = StaticMapCatalog.CreateReleaseSeed();
            using var session = new OfflineGameSession(catalog,
                (_, __) => new FakeMapRuntime(new PreMatchCountdownTests.FloorWorld(), new PreMatchCountdownTests.NoNav()),
                allowFakeMapRuntime: true);
            session.UseArmoryLoadout(new DefinitionId("weapons/ak47"), SightCatalog.Collimator, new DefinitionId("weapons/golden-eagle"));
            var request = new SessionRequest(WellKnownMaps.TrainingCompound, WellKnownModes.TeamDeathmatch, 5, 460072030UL);
            Assert.True((await session.BeginLoadAsync(request, CancellationToken.None)).Succeeded);
            Assert.True(session.Start().Succeeded);
            var lifecycle = Assert.IsType<HarnessActorLifecycle>(session.Lifecycle);
            AssertHuman(session, lifecycle);

            // Every bot carries a catalog gun with a sight it may mount.
            var snap = session.TryAcquireSnapshot()!;
            int botsChecked = 0;
            for (int i = 0; i < snap.Participants.Count; i++)
            {
                var p = snap.Participants[i];
                if (p.IsHuman) continue;
                Assert.True(lifecycle.Simulation.TryGetWeapon(p.EntityId, 0, out var primary));
                Assert.True(session.TryGetPrimarySight(p.EntityId, out var sight));
                Assert.True(SightCatalog.IsAllowed(primary.DefinitionId, sight));
                botsChecked++;
            }
            Assert.Equal(9, botsChecked);

            // A respawned human life carries the same loadout.
            var first = session.HumanEntityId;
            lifecycle.DespawnLife(first);
            var next = lifecycle.SpawnLife(new ParticipantId(1), new LifeId(2), default, 0, 1);
            Assert.True(lifecycle.Simulation.TryGetWeapon(next, 0, out var again));
            Assert.Equal(new DefinitionId("weapons/ak47"), again.DefinitionId);
            Assert.True(session.TryGetPrimarySight(next, out var sightAgain));
            Assert.Equal(SightCatalog.Collimator, sightAgain);
        }

        private static void AssertHuman(OfflineGameSession session, HarnessActorLifecycle lifecycle)
        {
            Assert.True(lifecycle.Simulation.TryGetWeapon(session.HumanEntityId, 0, out var primary));
            Assert.True(lifecycle.Simulation.TryGetWeapon(session.HumanEntityId, 1, out var sidearm));
            Assert.True(session.TryGetPrimarySight(session.HumanEntityId, out var sight));
            Assert.Equal(new DefinitionId("weapons/ak47"), primary.DefinitionId);
            Assert.Equal(new DefinitionId("weapons/golden-eagle"), sidearm.DefinitionId);
            Assert.Equal(SightCatalog.Collimator, sight);
        }

        private static string RepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "toolchain-lock.json"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }
}
