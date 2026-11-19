#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>T195: <see cref="IArmoryCatalogView"/> over <see cref="ArmoryCatalog"/> for consumers without a Simulation reference.</summary>
    public sealed class ArmoryCatalogView : IArmoryCatalogView
    {
        public static readonly ArmoryCatalogView Instance = new ArmoryCatalogView();
        private ArmoryCatalogView() { }

        public IReadOnlyList<DefinitionId> Primaries => ArmoryCatalog.Primaries;
        public IReadOnlyList<DefinitionId> Sidearms => ArmoryCatalog.Sidearms;
        public bool TryGetStats(DefinitionId weapon, out int damage, out int pellets, out int magazine, out int roundsPerMinute, out string fireMode) =>
            ArmoryCatalog.TryGetStats(weapon, out damage, out pellets, out magazine, out roundsPerMinute, out fireMode);
        public float BaseHipSpreadRadians(DefinitionId weapon) => OfflineGameSession.HipSpreadRadians(weapon);
    }
}
