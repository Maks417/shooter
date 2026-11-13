#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    public sealed class CompoundValidationReport
    {
        public ulong Seed { get; }
        public bool Passed { get; }
        public float BorderErrorMeters { get; }
        public float MaxOpeningErrorMeters { get; }
        public bool SpawnObjectiveConnected { get; }
        public bool BotsReachable { get; }
        public bool SpawnLosBlocked { get; }
        public ContentId CollisionHash { get; }
        public ContentId NavHash { get; }
        public string Diagnostic { get; }

        public CompoundValidationReport(
            ulong seed, bool passed, float borderErrorMeters, float maxOpeningErrorMeters,
            bool spawnObjectiveConnected, bool botsReachable, bool spawnLosBlocked,
            ContentId collisionHash, ContentId navHash, string diagnostic)
        {
            Seed = seed; Passed = passed; BorderErrorMeters = borderErrorMeters;
            MaxOpeningErrorMeters = maxOpeningErrorMeters; SpawnObjectiveConnected = spawnObjectiveConnected;
            BotsReachable = botsReachable; SpawnLosBlocked = spawnLosBlocked;
            CollisionHash = collisionHash; NavHash = navHash; Diagnostic = diagnostic ?? "";
        }
    }

    public static class CompoundValidation
    {
        public static CompoundValidationReport Validate(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            float border = TerrainHeight.MaxNeighborBorderError(plan.Seed);
            float opening = MeasureOpeningError(plan);
            bool connected = plan.RouteCorners.Count > 0;
            bool bots = true;
            var tmp = new List<Float3>();
            for (int i = 0; i < plan.BotSpawns.Count; i++)
            {
                if (!CompoundConnectivity.TryFindPath(plan.CollisionBoxes, plan.PlayerSpawn, plan.BotSpawns[i], tmp, CompoundConnectivity.RequiredClearance))
                { bots = false; break; }
            }
            bool losOk = true;
            float eye = plan.PlayerSpawn.Y + 1.65f;
            var playerEye = new Float3(plan.PlayerSpawn.X, eye, plan.PlayerSpawn.Z);
            for (int i = 0; i < plan.BotSpawns.Count; i++)
            {
                var botEye = new Float3(plan.BotSpawns[i].X, plan.BotSpawns[i].Y + 1.65f, plan.BotSpawns[i].Z);
                if (CompoundConnectivity.HasLineOfSight(plan.CollisionBoxes, playerEye, botEye, eye))
                { losOk = false; break; }
            }
            bool passed = border <= 1e-4f && opening <= 0.01f && connected && bots && losOk;
            string diag = passed ? "ok" :
                "border=" + border.ToString("R", CultureInfo.InvariantCulture) +
                " opening=" + opening.ToString("R", CultureInfo.InvariantCulture) +
                " connected=" + connected + " bots=" + bots + " losBlocked=" + losOk;
            return new CompoundValidationReport(plan.Seed, passed, border, opening, connected, bots, losOk, plan.CollisionHash, plan.NavHash, diag);
        }

        public static CompoundValidationReport[] ValidateCanonical()
        {
            var reports = new CompoundValidationReport[CompoundPlanner.CanonicalSeeds.Length];
            for (int i = 0; i < reports.Length; i++)
            {
                var plan = CompoundPlanner.Generate(CompoundPlanner.CanonicalSeeds[i]);
                reports[i] = Validate(plan);
            }
            return reports;
        }

        private static float MeasureOpeningError(CompoundPlan plan)
        {
            // Entrances align with collision within 1 cm. Compare against structural wall boxes only
            // (height ≥ 5.5 m) so props/cover cannot masquerade as opening misalignment.
            var walls = new System.Collections.Generic.List<CollisionBox>();
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
                if (plan.CollisionBoxes[i].Gameplay && plan.CollisionBoxes[i].Size.Y >= 5.5f)
                    walls.Add(plan.CollisionBoxes[i]);

            float max = 0;
            const float wallHalfT = 0.15f;
            const float doorHalf = 1f;
            for (int i = 0; i < plan.Buildings.Count; i++)
            {
                var b = plan.Buildings[i];
                float x = b.Center.X;
                float hz = b.SizeZ * 0.5f;
                float[] zFaces = { b.Center.Z + hz, b.Center.Z - hz };
                for (int d = 0; d < zFaces.Length; d++)
                {
                    float zFace = zFaces[d];
                    float zApproach = zFace + (d == 0 ? (wallHalfT + 0.05f) : -(wallHalfT + 0.05f));
                    if (CompoundConnectivity.IsBlocked(walls, x, zApproach, 0f))
                    { max = Math.Max(max, 0.02f); continue; }
                    if (!CompoundConnectivity.IsBlocked(walls, x + doorHalf, zFace, 0f) ||
                        !CompoundConnectivity.IsBlocked(walls, x - doorHalf, zFace, 0f))
                    { max = Math.Max(max, 0.02f); continue; }
                    if (CompoundConnectivity.IsBlocked(walls, x, zFace, 0f))
                    { max = Math.Max(max, 0.02f); continue; }
                }
            }
            return max;
        }
    }
}
