#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>
    /// Reviewed product TDM measurement identities (ADR-037 / T063 / ADR-101). Mirrors docs/game-fixtures/tdm-workloads.json;
    /// do not invent map substitutions for compound-only historical rows.
    /// </summary>
    public static class ProductTdmWorkloads
    {
        public const string Compound5v5 = "TDM-5v5-v1";
        public const string Compound8v8 = "TDM-8v8-v1";
        public const string Compound10v10 = "TDM-10v10-v1";
        public const string Compound15v15 = "TDM-15v15-v1";
        public const string Depot5v5 = "TDM-5v5-depot-v1";
        public const string Depot8v8 = "TDM-8v8-depot-v1";
        public const string Depot10v10 = "TDM-10v10-depot-v1";
        public const string Depot15v15 = "TDM-15v15-depot-v1";
        public const string Oil5v5 = "TDM-5v5-oil-v1";
        public const string Oil8v8 = "TDM-8v8-oil-v1";
        public const string Oil10v10 = "TDM-10v10-oil-v1";
        public const string Oil15v15 = "TDM-15v15-oil-v1";
        public const string SoakCompound15v15 = "TDM-soak-15v15-compound-v1";
        public const string SoakDepot15v15 = "TDM-soak-15v15-depot-v1";
        public const string SoakOil15v15 = "TDM-soak-15v15-oil-v1";

        public readonly struct Definition
        {
            public string Id { get; }
            public MapId MapId { get; }
            public byte TeamSize { get; }
            public int Humans { get; }
            public int Bots { get; }
            public int Actors { get; }
            public ulong Seed { get; }
            public int WarmupTicks { get; }
            public int CaptureTicks { get; }
            public bool Soak { get; }
            public string[] RequiredCounters { get; }

            public Definition(
                string id,
                MapId mapId,
                byte teamSize,
                int humans,
                int bots,
                int actors,
                ulong seed,
                int warmupTicks,
                int captureTicks,
                bool soak,
                string[] requiredCounters)
            {
                Id = id;
                MapId = mapId;
                TeamSize = teamSize;
                Humans = humans;
                Bots = bots;
                Actors = actors;
                Seed = seed;
                WarmupTicks = warmupTicks;
                CaptureTicks = captureTicks;
                Soak = soak;
                RequiredCounters = requiredCounters;
            }
        }

        private static readonly string[] MeasurementCounters =
        {
            "team1Kills", "team2Kills", "activeTicks", "respawns", "aiRays", "pathRequests",
            "shots", "damageEvents", "leaseAcquires", "querySamples", "contentBindings", "renderFrames"
        };

        private static readonly Definition[] All =
        {
            new Definition(Compound5v5, WellKnownMaps.TrainingCompound, 5, 1, 9, 10, 460072030UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Compound8v8, WellKnownMaps.TrainingCompound, 8, 1, 15, 16, 460072030UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Compound10v10, WellKnownMaps.TrainingCompound, 10, 1, 19, 20, 460072030UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Compound15v15, WellKnownMaps.TrainingCompound, 15, 1, 29, 30, 460072030UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Depot5v5, WellKnownMaps.DepotYard, 5, 1, 9, 10, 460072031UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Depot8v8, WellKnownMaps.DepotYard, 8, 1, 15, 16, 460072031UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Depot10v10, WellKnownMaps.DepotYard, 10, 1, 19, 20, 460072031UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Depot15v15, WellKnownMaps.DepotYard, 15, 1, 29, 30, 460072031UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Oil5v5, WellKnownMaps.OilDepot, 5, 1, 9, 10, 460072032UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Oil8v8, WellKnownMaps.OilDepot, 8, 1, 15, 16, 460072032UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Oil10v10, WellKnownMaps.OilDepot, 10, 1, 19, 20, 460072032UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Oil15v15, WellKnownMaps.OilDepot, 15, 1, 29, 30, 460072032UL, 3600, 10800, false, MeasurementCounters),
            new Definition(SoakCompound15v15, WellKnownMaps.TrainingCompound, 15, 1, 29, 30, 460072030UL, 0, 108000, true, MeasurementCounters),
            new Definition(SoakDepot15v15, WellKnownMaps.DepotYard, 15, 1, 29, 30, 460072031UL, 0, 108000, true, MeasurementCounters),
            new Definition(SoakOil15v15, WellKnownMaps.OilDepot, 15, 1, 29, 30, 460072032UL, 0, 108000, true, MeasurementCounters)
        };

        public static bool IsProductWorkload(string? workloadId)
        {
            if (string.IsNullOrEmpty(workloadId)) return false;
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i].Id, workloadId, StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool TryGet(string workloadId, out Definition definition)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (!string.Equals(All[i].Id, workloadId, StringComparison.Ordinal)) continue;
                definition = All[i];
                return true;
            }
            definition = default;
            return false;
        }

        public static Definition Require(string workloadId)
        {
            if (!TryGet(workloadId, out var definition))
                throw new ArgumentException("Unsupported product TDM workload: " + workloadId);
            return definition;
        }
    }
}
