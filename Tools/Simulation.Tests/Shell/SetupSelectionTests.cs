#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Shell
{
    public sealed class SetupSelectionTests
    {
        [Fact]
        public void Build_request_uses_currently_displayed_map_and_size()
        {
            var catalog = new FakeCatalog();
            var setup = new SetupSelection(catalog, WellKnownMaps.TrainingCompound, 5);
            Assert.Equal("Training Compound", setup.SelectedDisplayName);
            setup.CycleMap(1);
            setup.CycleTeamSize(1);
            var request = setup.TryBuildRequest(out var error);
            Assert.Equal("", error);
            Assert.NotNull(request);
            Assert.Equal(WellKnownMaps.DepotYard, request!.MapId);
            Assert.Equal((byte)6, request.TeamSize);
            Assert.Equal(460072031UL, request.Seed);
        }

        private sealed class FakeCatalog : IMapCatalog
        {
            private readonly Dictionary<string, MapCatalogEntry> entries = new Dictionary<string, MapCatalogEntry>
            {
                [WellKnownMaps.TrainingCompound.Value] = Entry(WellKnownMaps.TrainingCompound, "Training Compound", "compound", 460072030UL),
                [WellKnownMaps.DepotYard.Value] = Entry(WellKnownMaps.DepotYard, "Depot Yard", "depot", 460072031UL)
            };

            public int Count => entries.Count;

            public bool TryGet(MapId mapId, out MapCatalogEntry entry) => entries.TryGetValue(mapId.Value, out entry!);

            public bool TryGetGameplay(MapId mapId, out MapGameplayDefinition definition)
            {
                definition = default!;
                return false;
            }

            public GameOperationResult RejectUnknown(MapId mapId, ModeId modeId, byte teamSize)
            {
                if (!TryGet(mapId, out var entry)) return new GameOperationResult(GameErrorCode.InvalidRequest, "unknown-map", "Unknown map.");
                bool modeOk = false;
                for (int i = 0; i < entry.SupportedModes.Count; i++) if (entry.SupportedModes[i] == modeId) modeOk = true;
                if (!modeOk) return new GameOperationResult(GameErrorCode.InvalidRequest, "unknown-mode", "Unsupported mode.");
                bool sizeOk = false;
                for (int i = 0; i < entry.SupportedTeamSizes.Count; i++) if (entry.SupportedTeamSizes[i] == teamSize) sizeOk = true;
                if (!sizeOk) return new GameOperationResult(GameErrorCode.InvalidRequest, "bad-team-size", "Unsupported team size.");
                return GameOperationResult.Ok;
            }

            private static MapCatalogEntry Entry(MapId id, string name, string desc, ulong seed)
            {
                var zero = new ContentId(new byte[32]);
                return new MapCatalogEntry(
                    id, name, desc,
                    new[] { WellKnownModes.TeamDeathmatch },
                    new byte[] { 5, 6 },
                    seed, "map.test.1",
                    zero, zero, zero, zero);
            }
        }
    }
}
