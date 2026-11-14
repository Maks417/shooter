#nullable enable
namespace Shooter.Game.Session
{
    /// <summary>Accumulated product-match counters for T063 / FIX12 capture analysis. Never invents missing values.</summary>
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

        public void Reset()
        {
            Team1Kills = Team2Kills = ActiveTicks = Respawns = AiRays = PathRequests = 0;
            Shots = DamageEvents = LeaseAcquires = QuerySamples = ContentBindings = RenderFrames = 0;
            Actors = 0;
            MapId = Seed = ContentManifestHash = PackageRootHash = WorkloadId = Profile = "";
            TeamSize = 0;
        }
    }
}
