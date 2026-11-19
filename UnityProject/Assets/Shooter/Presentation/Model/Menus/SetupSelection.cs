#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Presentation.Menus
{
    /// <summary>Catalog-backed map/size selection. Start builds the request currently displayed — never a stale prefs surrogate.</summary>
    public sealed class SetupSelection
    {
        private static readonly MapId[] DefaultMaps =
        {
            WellKnownMaps.TrainingCompound,
            WellKnownMaps.DepotYard,
            WellKnownMaps.OilDepot
        };

        /// <summary>ADR-101 product team sizes (per side).</summary>
        private static readonly byte[] TeamSizes = { 5, 8, 10, 15 };

        private readonly IMapCatalog catalog;
        private readonly MapId[] maps;
        private int mapIndex;
        private int teamSizeIndex;

        public SetupSelection(IMapCatalog catalog, MapId? preferredMap = null, byte preferredTeamSize = 5)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            maps = FilterAvailable(catalog, DefaultMaps);
            if (maps.Length == 0)
                throw new InvalidOperationException("No packaged maps available in catalog.");

            mapIndex = 0;
            if (preferredMap.HasValue)
            {
                for (int i = 0; i < maps.Length; i++)
                    if (maps[i].Equals(preferredMap.Value)) { mapIndex = i; break; }
            }

            teamSizeIndex = IndexOfTeamSize(preferredTeamSize);
        }

        public int MapCount => maps.Length;
        public MapId SelectedMapId => maps[mapIndex];
        public byte SelectedTeamSize => TeamSizes[teamSizeIndex];
        /// <summary>ADR-122: the mode the next request uses (the first registered mode until a mode picker exists).</summary>
        public ModeRules SelectedMode { get; set; } = ModeRules.All[0];
        public bool HasContent => maps.Length > 0;

        public string SelectedDisplayName
        {
            get
            {
                if (!catalog.TryGet(SelectedMapId, out var entry)) return SelectedMapId.Value;
                return entry.DisplayName;
            }
        }

        public string SelectedDescription
        {
            get
            {
                if (!catalog.TryGet(SelectedMapId, out var entry)) return "";
                return entry.Description;
            }
        }

        public void CycleMap(int delta)
        {
            if (maps.Length == 0) return;
            mapIndex = Mod(mapIndex + delta, maps.Length);
        }

        public void SelectMap(MapId mapId)
        {
            for (int i = 0; i < maps.Length; i++)
            {
                if (!maps[i].Equals(mapId)) continue;
                mapIndex = i;
                return;
            }
        }

        public void CycleTeamSize(int delta) => teamSizeIndex = Mod(teamSizeIndex + delta, TeamSizes.Length);

        public void SelectTeamSize(byte teamSize)
        {
            int index = IndexOfTeamSize(teamSize);
            if (index >= 0) teamSizeIndex = index;
        }

        public SessionRequest? TryBuildRequest(out string error)
        {
            error = "";
            if (!catalog.TryGet(SelectedMapId, out var entry))
            {
                error = "Selected map is missing from the catalog.";
                return null;
            }

            var request = new SessionRequest(SelectedMapId, SelectedMode.Id, SelectedTeamSize, entry.RecipeSeed);
            var reject = catalog.RejectUnknown(request.MapId, request.ModeId, request.TeamSize);
            if (!reject.Succeeded)
            {
                error = reject.UserFacing;
                return null;
            }

            return request;
        }

        private static int IndexOfTeamSize(byte teamSize)
        {
            for (int i = 0; i < TeamSizes.Length; i++)
                if (TeamSizes[i] == teamSize) return i;
            return 0;
        }

        private static MapId[] FilterAvailable(IMapCatalog catalog, MapId[] candidates)
        {
            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
                if (catalog.TryGet(candidates[i], out _)) count++;
            var maps = new MapId[count];
            int w = 0;
            for (int i = 0; i < candidates.Length; i++)
                if (catalog.TryGet(candidates[i], out _)) maps[w++] = candidates[i];
            return maps;
        }

        private static int Mod(int value, int modulus)
        {
            int m = value % modulus;
            return m < 0 ? m + modulus : m;
        }
    }
}
