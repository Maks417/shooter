#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Content.Maps
{
    public sealed class StaticMapCatalog : IMapCatalog
    {
        private readonly Dictionary<string, MapCatalogEntry> entries = new Dictionary<string, MapCatalogEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapGameplayDefinition> gameplay = new Dictionary<string, MapGameplayDefinition>(StringComparer.Ordinal);

        public StaticMapCatalog(IReadOnlyList<MapCatalogEntry> catalog, IReadOnlyList<MapGameplayDefinition> maps)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            foreach (var entry in catalog) entries[entry.MapId.Value] = entry;
            foreach (var map in maps) gameplay[map.MapId.Value] = map;
        }

        public int Count => entries.Count;

        public bool TryGet(MapId mapId, out MapCatalogEntry entry) => entries.TryGetValue(mapId.Value, out entry!);

        public bool TryGetGameplay(MapId mapId, out MapGameplayDefinition definition) => gameplay.TryGetValue(mapId.Value, out definition!);

        public GameOperationResult RejectUnknown(MapId mapId, ModeId modeId, byte teamSize)
        {
            if (!TryGet(mapId, out var entry))
                return new GameOperationResult(GameErrorCode.InvalidRequest, "unknown-map", "Unknown map.");
            bool modeOk = false;
            for (int i = 0; i < entry.SupportedModes.Count; i++) if (entry.SupportedModes[i] == modeId) { modeOk = true; break; }
            if (!modeOk) return new GameOperationResult(GameErrorCode.InvalidRequest, "unknown-mode", "Unsupported mode.");
            bool sizeOk = false;
            for (int i = 0; i < entry.SupportedTeamSizes.Count; i++) if (entry.SupportedTeamSizes[i] == teamSize) { sizeOk = true; break; }
            if (!sizeOk) return new GameOperationResult(GameErrorCode.InvalidRequest, "bad-team-size", "Unsupported team size.");
            return GameOperationResult.Ok;
        }

        public static ContentId Hash(string label)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return new ContentId(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(label)));
        }

        public static StaticMapCatalog CreateReleaseSeed()
        {
            var modes = new[] { WellKnownModes.TeamDeathmatch };
            var sizes = new byte[] { 5, 6 };
            var compoundHash = Hash("map.training-compound.gameplay.v1");
            var depotHash = Hash("map.depot-yard.gameplay.v1");
            var low = Hash("visual.low.v1");
            var bal = Hash("visual.balanced.v1");
            var high = Hash("visual.high.v1");
            var catalog = new[]
            {
                new MapCatalogEntry(WellKnownMaps.TrainingCompound, "Training Compound", "Bounded compound for offline TDM.", modes, sizes, 460072030UL, "compound.v1", compoundHash, low, bal, high),
                new MapCatalogEntry(WellKnownMaps.DepotYard, "Depot Yard", "Distinct depot grammar for offline TDM.", modes, sizes, 460072031UL, "depot.v1", depotHash, low, bal, high)
            };
            var bounds = new Aabb(new Float3(-120f, 0f, -120f), new Float3(120f, 40f, 120f));
            var compoundSpawns = new[]
            {
                new SpawnSlot(1, 1, new Float3(-40f, 0f, -40f), 0.5f),
                new SpawnSlot(2, 1, new Float3(-35f, 0f, -40f), 0.5f),
                new SpawnSlot(3, 1, new Float3(-30f, 0f, -40f), 0.5f),
                new SpawnSlot(4, 1, new Float3(-25f, 0f, -40f), 0.5f),
                new SpawnSlot(5, 1, new Float3(-20f, 0f, -40f), 0.5f),
                new SpawnSlot(6, 1, new Float3(-15f, 0f, -40f), 0.5f),
                new SpawnSlot(11, 2, new Float3(40f, 0f, 40f), -2.5f),
                new SpawnSlot(12, 2, new Float3(35f, 0f, 40f), -2.5f),
                new SpawnSlot(13, 2, new Float3(30f, 0f, 40f), -2.5f),
                new SpawnSlot(14, 2, new Float3(25f, 0f, 40f), -2.5f),
                new SpawnSlot(15, 2, new Float3(20f, 0f, 40f), -2.5f),
                new SpawnSlot(16, 2, new Float3(15f, 0f, 40f), -2.5f)
            };
            var depotSpawns = new[]
            {
                new SpawnSlot(101, 1, new Float3(-50f, 0f, 0f), 0f),
                new SpawnSlot(102, 1, new Float3(-45f, 0f, 5f), 0f),
                new SpawnSlot(103, 1, new Float3(-45f, 0f, -5f), 0f),
                new SpawnSlot(104, 1, new Float3(-40f, 0f, 8f), 0f),
                new SpawnSlot(105, 1, new Float3(-40f, 0f, -8f), 0f),
                new SpawnSlot(106, 1, new Float3(-35f, 0f, 0f), 0f),
                new SpawnSlot(201, 2, new Float3(50f, 0f, 0f), 3.14f),
                new SpawnSlot(202, 2, new Float3(45f, 0f, 5f), 3.14f),
                new SpawnSlot(203, 2, new Float3(45f, 0f, -5f), 3.14f),
                new SpawnSlot(204, 2, new Float3(40f, 0f, 8f), 3.14f),
                new SpawnSlot(205, 2, new Float3(40f, 0f, -8f), 3.14f),
                new SpawnSlot(206, 2, new Float3(35f, 0f, 0f), 3.14f)
            };
            var patrol = new[] { new Float3(0f, 0f, 0f), new Float3(20f, 0f, 20f) };
            var combat = new[] { new Float3(0f, 0f, 10f), new Float3(-10f, 0f, 0f) };
            var maps = new[]
            {
                new MapGameplayDefinition(WellKnownMaps.TrainingCompound, 1, compoundHash, 1, Hash("nav.compound.v1"), bounds, compoundSpawns, patrol, combat),
                new MapGameplayDefinition(WellKnownMaps.DepotYard, 1, depotHash, 1, Hash("nav.depot.v1"), bounds, depotSpawns, patrol, combat)
            };
            return new StaticMapCatalog(catalog, maps);
        }
    }
}
