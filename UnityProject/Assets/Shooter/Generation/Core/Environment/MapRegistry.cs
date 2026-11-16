#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// One registered map (ADR-083), generated from <c>Content/Maps/&lt;id&gt;.map.json</c>. A map's planner is either a
    /// data <see cref="ArenaLayoutDocument"/> or a named C# planner for layouts that still need code.
    /// </summary>
    public sealed class MapDefinition
    {
        private readonly string? layoutJson;
        private readonly Lazy<ArenaLayoutDocument>? layout;

        internal MapDefinition(string id, int order, string displayName, string description, ulong seed, string recipeGenerator,
            string planner, string? layoutJson, string skyPreset, string groundSurface)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Map id required.", nameof(id));
            Id = id; Order = order; DisplayName = displayName; Description = description; Seed = seed;
            RecipeGenerator = recipeGenerator; Planner = planner; SkyPreset = skyPreset; GroundSurface = groundSurface;
            this.layoutJson = layoutJson;
            if (planner == "layout")
            {
                if (string.IsNullOrEmpty(layoutJson)) throw new ArgumentException("Layout planner needs a layout document.", nameof(layoutJson));
                layout = new Lazy<ArenaLayoutDocument>(() => ArenaLayoutDocument.Parse(layoutJson!), true);
            }
            else if (!planner.StartsWith("code:", StringComparison.Ordinal))
                throw new ArgumentException("Planner must be layout or code:<name>.", nameof(planner));
        }

        public string Id { get; }
        public int Order { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public ulong Seed { get; }
        /// <summary><c>map.compound</c>/<c>map.depot</c>/<c>map.oil</c> (frozen per-map recipes) or <c>map.arena</c>.</summary>
        public string RecipeGenerator { get; }
        public string RecipeSchema => MapRecipe.SchemaForGenerator(RecipeGenerator);
        /// <summary><c>layout</c> or <c>code:&lt;name&gt;</c>.</summary>
        public string Planner { get; }
        /// <summary>Presentation sky preset: <c>default</c>, <c>compound</c>, <c>dusty-yard</c> or <c>petroleum</c>.</summary>
        public string SkyPreset { get; }
        /// <summary>Presentation yard surface: <c>soil</c> or <c>asphalt</c>.</summary>
        public string GroundSurface { get; }
        public bool HasLayoutDocument => layout != null;
        public ArenaLayoutDocument LayoutDocument => layout?.Value ?? throw new InvalidOperationException(Id + " uses a code planner.");

        public CompoundPlan Generate()
        {
            if (layout != null) return layout.Value.Generate(Seed);
            string name = Planner.Substring("code:".Length);
            if (!MapRegistry.TryGetCodePlanner(name, out var planner))
                throw new InvalidOperationException("Unknown code planner " + name + " for " + Id + ".");
            return planner(Seed);
        }
    }

    /// <summary>
    /// The map registry (ADR-083). Rows are generated from <c>Content/Maps/*.map.json</c> by
    /// <c>Tools/generate_map_registry.py</c>; recipe identity, planner dispatch, release catalogs and presentation hints
    /// all read it instead of per-map switches.
    /// </summary>
    public static partial class MapRegistry
    {
        private static readonly Dictionary<string, Func<ulong, CompoundPlan>> CodePlanners =
            new Dictionary<string, Func<ulong, CompoundPlan>>(StringComparer.Ordinal)
            {
                ["depot-arena"] = DepotArenaPlanner.Generate,
                ["oil-arena"] = OilArenaPlanner.Generate
            };

        private static readonly MapDefinition[] Definitions = CreateGeneratedDefinitions();
        private static readonly Dictionary<string, MapDefinition> ById = Index(Definitions);
        private static readonly string[] OrderedIds = Ids(Definitions);

        public static IReadOnlyList<MapDefinition> All => Definitions;
        /// <summary>Every registered map id in catalog order.</summary>
        public static IReadOnlyList<string> MapIds => OrderedIds;

        public static bool TryGet(string? mapId, out MapDefinition definition)
        {
            definition = null!;
            return mapId != null && ById.TryGetValue(mapId, out definition!);
        }

        public static MapDefinition Require(string mapId)
        {
            if (mapId == null) throw new ArgumentNullException(nameof(mapId));
            if (!TryGet(mapId, out var definition)) throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            return definition;
        }

        internal static bool TryGetCodePlanner(string name, out Func<ulong, CompoundPlan> planner) =>
            CodePlanners.TryGetValue(name, out planner!);

        public static IReadOnlyCollection<string> CodePlannerNames => CodePlanners.Keys;

        private static Dictionary<string, MapDefinition> Index(MapDefinition[] definitions)
        {
            var map = new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
            foreach (var definition in definitions) map.Add(definition.Id, definition);
            return map;
        }

        private static string[] Ids(MapDefinition[] definitions)
        {
            var ids = new string[definitions.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = definitions[i].Id;
            return ids;
        }
    }
}
