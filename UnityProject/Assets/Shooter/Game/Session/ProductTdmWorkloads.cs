#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>
    /// Reviewed product TDM measurement identities (ADR-037 / T063). Mirrors docs/game-fixtures/tdm-workloads.json;
    /// do not invent map substitutions for compound-only historical rows.
    /// </summary>
    public static class ProductTdmWorkloads
    {
        public const string Compound5v5 = "TDM-5v5-v1";
        public const string Compound6v6 = "TDM-6v6-v1";
        public const string Depot5v5 = "TDM-5v5-depot-v1";
        public const string Depot6v6 = "TDM-6v6-depot-v1";
        public const string Oil5v5 = "TDM-5v5-oil-v1";
        public const string Oil6v6 = "TDM-6v6-oil-v1";
        public const string SoakCompound6v6 = "TDM-soak-6v6-compound-v1";
        public const string SoakDepot6v6 = "TDM-soak-6v6-depot-v1";
        public const string SoakOil6v6 = "TDM-soak-6v6-oil-v1";

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
            new Definition(Compound6v6, WellKnownMaps.TrainingCompound, 6, 1, 11, 12, 460072030UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Depot5v5, WellKnownMaps.DepotYard, 5, 1, 9, 10, 460072031UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Depot6v6, WellKnownMaps.DepotYard, 6, 1, 11, 12, 460072031UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Oil5v5, WellKnownMaps.OilDepot, 5, 1, 9, 10, 460072032UL, 3600, 10800, false, MeasurementCounters),
            new Definition(Oil6v6, WellKnownMaps.OilDepot, 6, 1, 11, 12, 460072032UL, 3600, 10800, false, MeasurementCounters),
            new Definition(SoakCompound6v6, WellKnownMaps.TrainingCompound, 6, 1, 11, 12, 460072030UL, 0, 108000, true, MeasurementCounters),
            new Definition(SoakDepot6v6, WellKnownMaps.DepotYard, 6, 1, 11, 12, 460072031UL, 0, 108000, true, MeasurementCounters),
            new Definition(SoakOil6v6, WellKnownMaps.OilDepot, 6, 1, 11, 12, 460072032UL, 0, 108000, true, MeasurementCounters)
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
