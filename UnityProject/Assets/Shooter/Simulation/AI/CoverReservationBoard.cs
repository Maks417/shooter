#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    /// <summary>Two-phase cover claim/release keyed by entity ID.</summary>
    public sealed class CoverReservationBoard
    {
        private readonly Dictionary<ulong, EntityId> claims = new Dictionary<ulong, EntityId>();

        public int ActiveClaims => claims.Count;

        public bool TryClaim(ulong coverId, EntityId actor)
        {
            if (coverId == 0 || !actor.IsValid) throw new ArgumentOutOfRangeException(nameof(coverId));
            if (claims.TryGetValue(coverId, out var owner))
                return owner == actor;
            claims.Add(coverId, actor);
            return true;
        }

        public bool Release(ulong coverId, EntityId actor)
        {
            if (!claims.TryGetValue(coverId, out var owner)) return false;
            if (owner != actor) return false;
            claims.Remove(coverId);
            return true;
        }

        public int ReleaseAll(EntityId actor)
        {
            var keys = new List<ulong>();
            foreach (var kv in claims) if (kv.Value == actor) keys.Add(kv.Key);
            for (int i = 0; i < keys.Count; i++) claims.Remove(keys[i]);
            return keys.Count;
        }

        public bool IsClaimed(ulong coverId) => claims.ContainsKey(coverId);

        public bool TryGetOwner(ulong coverId, out EntityId owner) => claims.TryGetValue(coverId, out owner!);
    }
}
