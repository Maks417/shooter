#nullable enable
namespace Shooter.Game.Session
{
    /// <summary>
    /// Product-match counters for FIX12 / T070 capture analysis.
    /// Never invents missing values: absent bindings stay 0; analyzers reject incomplete captures.
    /// </summary>
    /// <remarks>
    /// Counter semantics (T070):
    /// respawns — successful life replacements installed by ScheduleDueRespawns (entity/life change), not net alive growth.
    /// querySamples — IWorldQueries invocations (Raycast + SweepCapsule + Overlap) through the session adapter; distinct from aiRays / pathRequests.
    /// contentBindings — verified package roots actually bound (gameplay catalog entry, then visuals when the host binds them).
    /// WorkloadId — set by the measurement host after BeginLoadAsync; re-applied on every load/replay.
    /// Cumulative* fields survive per-match ResetForNewMatch so soak captures retain cross-match totals.
    /// </remarks>
    public sealed class ProductMeasurementTelemetry
    {
        public long Team1Kills;
        public long Team2Kills;
        public long ActiveTicks;
        public long Respawns;
        public long AiRays;
        public long PathRequests;
        public long Shots;
        public long DamageEvents;
        public long LeaseAcquires;
        public long QuerySamples;
        public long ContentBindings;
        public long RenderFrames;
        public int Actors;
        public string MapId = "";
        public string Seed = "";
        public string ContentManifestHash = "";
        public string PackageRootHash = "";
        public byte TeamSize;
        public string WorkloadId = "";
        public string Profile = "";

        public ulong SessionEpoch;
        public int MatchIndex;
        public int MatchesCompleted;
        public long CumulativeActiveTicks;
        public long CumulativeRespawns;
        public long CumulativeQuerySamples;
        public long CumulativeContentBindings;

        public void ResetForNewMatch()
        {
            Team1Kills = Team2Kills = ActiveTicks = Respawns = AiRays = PathRequests = 0;
            Shots = DamageEvents = LeaseAcquires = QuerySamples = ContentBindings = RenderFrames = 0;
            Actors = 0;
            MapId = Seed = ContentManifestHash = PackageRootHash = Profile = "";
            TeamSize = 0;
        }

        public void Reset()
        {
            ResetForNewMatch();
            WorkloadId = "";
            SessionEpoch = 0;
            MatchIndex = 0;
            MatchesCompleted = 0;
            CumulativeActiveTicks = CumulativeRespawns = CumulativeQuerySamples = CumulativeContentBindings = 0;
        }

        public void FoldMatchIntoCumulative()
        {
            CumulativeActiveTicks += ActiveTicks;
            CumulativeRespawns += Respawns;
            CumulativeQuerySamples += QuerySamples;
            CumulativeContentBindings += ContentBindings;
            MatchesCompleted++;
        }

        public void RecordContentBinding()
        {
            ContentBindings++;
        }

        public long PublishedCumulativeActiveTicks => CumulativeActiveTicks + ActiveTicks;
        public long PublishedCumulativeRespawns => CumulativeRespawns + Respawns;
        public long PublishedCumulativeQuerySamples => CumulativeQuerySamples + QuerySamples;
        public long PublishedCumulativeContentBindings => CumulativeContentBindings + ContentBindings;
    }
}
