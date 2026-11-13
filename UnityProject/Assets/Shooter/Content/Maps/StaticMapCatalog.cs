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
            var low = Hash("visual.low.v1");
            var bal = Hash("visual.balanced.v1");
            var high = Hash("visual.high.v1");
            // Gameplay definitions come from the frozen compound planner so spawn/nav match collision.
            var compoundGameplay = CompoundCatalogFactory.BuildGameplay(WellKnownMaps.TrainingCompound, 460072030UL);
            var depotGameplay = CompoundCatalogFactory.BuildGameplay(WellKnownMaps.DepotYard, 460072031UL);
            var catalog = new[]
            {
                new MapCatalogEntry(WellKnownMaps.TrainingCompound, "Training Compound", "Bounded compound for offline TDM.", modes, sizes, 460072030UL, "compound.v1", compoundGameplay.CollisionHash, low, bal, high),
                new MapCatalogEntry(WellKnownMaps.DepotYard, "Depot Yard", "Distinct depot grammar for offline TDM.", modes, sizes, 460072031UL, "depot.v1", depotGameplay.CollisionHash, low, bal, high)
            };
            return new StaticMapCatalog(catalog, new[] { compoundGameplay, depotGameplay });
        }
    }
}
