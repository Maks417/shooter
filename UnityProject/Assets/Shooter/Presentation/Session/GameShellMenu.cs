#nullable enable
using Shooter.Game.Contracts;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// Compatibility entry for the T059 product shell. Prefer <see cref="ProductShellHost"/>.
    /// PlayerPrefs/CLI values are setup defaults only — they do not start a match.
    /// </summary>
    public static class GameShellMenu
    {
        public static ProductShellHost Create(IProductShellBackend backend, string preferredMapId, byte preferredTeamSize)
            => ProductShellHost.Create(backend, preferredMapId, preferredTeamSize);

        public static SessionRequest? TryReadPreferredRequest(IMapCatalog catalog)
        {
            string map = PlayerPrefs.GetString("shooter.map", "training-compound");
            int size = PlayerPrefs.GetInt("shooter.teamSize", 5);
            if (size != 5 && size != 6) size = 5;
            var mapId = new MapId(map);
            if (!catalog.TryGet(mapId, out var entry)) return null;
            return new SessionRequest(mapId, WellKnownModes.TeamDeathmatch, (byte)size, entry.RecipeSeed);
        }
    }
}
