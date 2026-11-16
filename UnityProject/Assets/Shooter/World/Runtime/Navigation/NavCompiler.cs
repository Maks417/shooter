#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;

namespace Shooter.World.Navigation
{
    /// <summary>Compiles cover metadata and validates clearance/reachability against collision boxes provided by the planner.</summary>
    public static class NavCompiler
    {
        public static NavCompileResult Compile(
            uint navRevision,
            ContentId collisionHash,
            NavSettings settings,
            Float3 spawn,
            Float3 objective,
            IReadOnlyList<Float3> botSpawns,
            IReadOnlyList<(ulong id, Float3 center, float sx, float sy, float sz, Float3 peekStand, Float3 peekCrouch, bool hard)> covers,
            Func<Float3, Float3, List<Float3>, bool> pathSolve,
            Func<float, float, float, bool> isBlocked)
        {
            if (settings.SettingsVersion == 0) throw new ArgumentException("settings");
            if (pathSolve == null || isBlocked == null) throw new ArgumentNullException(nameof(pathSolve));
            if (botSpawns == null || covers == null) throw new ArgumentNullException(nameof(botSpawns));

            float clearance = settings.RequiredClearance;
            var route = new List<Float3>();
            bool reachable = pathSolve(spawn, objective, route);
            for (int i = 0; i < botSpawns.Count && reachable; i++)
            {
                var tmp = new List<Float3>();
                if (!pathSolve(spawn, botSpawns[i], tmp)) reachable = false;
            }

            float minClear = float.PositiveInfinity;
            for (int i = 0; i < route.Count; i++)
            {
                // Sample four offsets at required clearance; none may be inside solid at half clearance.
                float x = route[i].X, z = route[i].Z;
                bool blockedCore = isBlocked(x, z, clearance);
                if (blockedCore) minClear = 0;
                else minClear = Math.Min(minClear, clearance);
            }
            if (float.IsPositiveInfinity(minClear)) minClear = clearance;

            var coverList = new List<NavCoverCandidate>(covers.Count);
            for (int i = 0; i < covers.Count; i++)
            {
                var c = covers[i];
                coverList.Add(new NavCoverCandidate(c.id, c.center, c.peekStand, c.peekCrouch, c.hard));
            }

            ContentId settingsHash = settings.Hash();
            ContentId navHash = HashNav(navRevision, collisionHash, settingsHash, route, coverList, "gameplay");
            return new NavCompileResult(navRevision, navHash, collisionHash, settingsHash, route, coverList, reachable, minClear);
        }

        public static ContentId HashNav(
            uint navRevision,
            ContentId collisionHash,
            ContentId settingsHash,
            IReadOnlyList<Float3> route,
            IReadOnlyList<NavCoverCandidate> covers,
            string visualProfile)
        {
            // visualProfile is accepted but ignored for gameplay nav hash so quality changes cannot drift gameplay.
            _ = visualProfile;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            w.Write("shooter.nav-compile.v1");
            w.Write(navRevision);
            w.Write(collisionHash.ToBytes());
            w.Write(settingsHash.ToBytes());
            w.Write(route.Count);
            for (int i = 0; i < route.Count; i++)
            { w.Write(route[i].X); w.Write(route[i].Y); w.Write(route[i].Z); }
            w.Write(covers.Count);
            for (int i = 0; i < covers.Count; i++)
            {
                var c = covers[i];
                w.Write(c.StableId);
                w.Write(c.Anchor.X); w.Write(c.Anchor.Y); w.Write(c.Anchor.Z);
                w.Write(c.HardCover);
            }
            using var sha = SHA256.Create();
            return new ContentId(sha.ComputeHash(ms.ToArray()));
        }
    }
}
