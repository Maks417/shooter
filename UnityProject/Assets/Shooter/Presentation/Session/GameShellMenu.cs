#nullable enable
using Shooter.Content.Maps;
using Shooter.Game.Contracts;
using Shooter.Game.Settings;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>M10 minimal launch shell: map/mode/team-size selection before OfflineGameSession start.</summary>
    public sealed class GameShellMenu : MonoBehaviour
    {
        private int mapIndex;
        private int teamSizeIndex;
        private readonly string[] maps = { "training-compound", "depot-yard" };
        private readonly byte[] teamSizes = { 5, 6 };
        private string status = "Select map and Start Match";
        private IUserSettingsStore? store;
        private UserSettings settings = UserSettings.Defaults;

        public static GameShellMenu Create()
        {
            return new GameObject("Game shell").AddComponent<GameShellMenu>();
        }

        private void Start()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "shooter-user-settings-v1.txt");
            store = new FileUserSettingsStore(path);
            store.Load(out settings);
            QualitySettings.SetQualityLevel(Mathf.Clamp(settings.QualityTier, 0, QualitySettings.names.Length - 1), true);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(40, 40, 520, 320), "Shooter — Offline Team Deathmatch");
            GUI.Label(new Rect(60, 80, 480, 24), "Map: " + maps[mapIndex]);
            if (GUI.Button(new Rect(60, 110, 140, 28), "Cycle Map"))
                mapIndex = (mapIndex + 1) % maps.Length;
            GUI.Label(new Rect(60, 150, 480, 24), "Team size: " + teamSizes[teamSizeIndex] + "v" + teamSizes[teamSizeIndex]);
            if (GUI.Button(new Rect(60, 180, 140, 28), "Cycle Size"))
                teamSizeIndex = (teamSizeIndex + 1) % teamSizes.Length;
            if (GUI.Button(new Rect(60, 230, 200, 36), "Start Match"))
            {
                status = "Launch args: -shooterMap " + maps[mapIndex] + " -shooterTeamSize " + teamSizes[teamSizeIndex];
                // Product Bootstrap already auto-starts; shell records selection for relaunch / future menu-owned composition.
                PlayerPrefs.SetString("shooter.map", maps[mapIndex]);
                PlayerPrefs.SetInt("shooter.teamSize", teamSizes[teamSizeIndex]);
                PlayerPrefs.Save();
            }
            if (GUI.Button(new Rect(280, 230, 200, 36), "Save Settings"))
            {
                if (store != null)
                {
                    var result = store.Save(settings);
                    status = result.Succeeded ? "Settings saved" : result.UserFacing;
                }
            }
            GUI.Label(new Rect(60, 290, 480, 40), status);
        }

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
