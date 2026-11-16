#nullable enable

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-074 compact TDM arena for <c>training-compound</c> ("garrison square", docs/features/tdm-map-layouts.md Map A).
    /// Point-symmetric (<see cref="ArenaSymmetry.Rotate180"/>): team 1 plays from -Z and team 2 is the 180° rotation.
    /// The centre is an open parade square with a low flag plinth, not a building. Four corner blocks form a pinwheel
    /// around it: for each team the left route runs through a barracks interior, the right route down a truck-lined
    /// street, and the middle route through the motor-pool garage and the guard-hut arch. Every blocking box is a
    /// readable object (building wall, vehicle, sandbags, tarp fence); there are no free-standing separator walls.
    /// </summary>
    public static class CompoundArenaPlanner
    {
        public const string GeneratorId = "environment.compound.arena";
        public const string GeneratorVersion = "3.0.0";
        public const ulong StableIdBase = 10000;

        /// <summary>
        /// ADR-083: the layout and every kit operation now live in <c>Content/Maps/training-compound.map.json</c>
        /// (an <see cref="ArenaLayoutDocument"/>); this class is a thin wrapper kept for existing callers.
        /// </summary>
        private static ArenaLayoutDocument Document => MapRegistry.Require("training-compound").LayoutDocument;

        public static ArenaLayout Layout => Document.Layout;

        public static CompoundPlan Generate(ulong seed) => Document.Generate(seed);
    }
}
