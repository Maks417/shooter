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
            WellKnownMaps.DepotYard
        };

        private static readonly byte[] TeamSizes = { 5, 6 };

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

            teamSizeIndex = preferredTeamSize == 6 ? 1 : 0;
        }

        public int MapCount => maps.Length;
        public MapId SelectedMapId => maps[mapIndex];
        public byte SelectedTeamSize => TeamSizes[teamSizeIndex];
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

        public void CycleTeamSize(int delta) => teamSizeIndex = Mod(teamSizeIndex + delta, TeamSizes.Length);

        public SessionRequest? TryBuildRequest(out string error)
        {
            error = "";
            if (!catalog.TryGet(SelectedMapId, out var entry))
            {
                error = "Selected map is missing from the catalog.";
                return null;
            }

            var request = new SessionRequest(SelectedMapId, WellKnownModes.TeamDeathmatch, SelectedTeamSize, entry.RecipeSeed);
            var reject = catalog.RejectUnknown(request.MapId, request.ModeId, request.TeamSize);
            if (!reject.Succeeded)
            {
                error = reject.UserFacing;
                return null;
            }

            return request;
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
