#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Pure read-only reconcile of product actor/corpse view handles against authority.
    /// Does not mutate simulation state; Presentation owns the resulting spawn/despawn lists.
    /// </summary>
    public sealed class ProductActorBinder
    {
        public const int MaxCosmeticCorpses = 4;

        private readonly HashSet<ulong> live = new HashSet<ulong>();
        private readonly List<ulong> corpses = new List<ulong>(MaxCosmeticCorpses);
        private readonly HashSet<ulong> seenLive = new HashSet<ulong>();
        private readonly HashSet<ulong> seenCorpse = new HashSet<ulong>();
        private readonly List<EntityId> spawnScratch = new List<EntityId>(16);
        private readonly List<EntityId> despawnScratch = new List<EntityId>(16);
        private readonly List<EntityId> corpseScratch = new List<EntityId>(MaxCosmeticCorpses);

        public int LiveHandleCount => live.Count;
        public int CorpseHandleCount => corpses.Count;

        public void Clear()
        {
            live.Clear();
            corpses.Clear();
            seenLive.Clear();
            seenCorpse.Clear();
        }

        /// <summary>
        /// Reconcile living participants and cosmetic corpses. Outputs are cleared then filled.
        /// Life replacement is expressed as despawn(old)+spawn(new) when EntityIds differ across frames.
        /// </summary>
        public void Reconcile(
            IReadOnlyList<ActorLifecycleState> living,
            IReadOnlyList<ActorLifecycleState> cosmeticCorpses,
            List<EntityId> spawnLive,
            List<EntityId> despawn,
            List<EntityId> ensureCorpse)
        {
            if (living == null) throw new ArgumentNullException(nameof(living));
            if (cosmeticCorpses == null) throw new ArgumentNullException(nameof(cosmeticCorpses));
            if (spawnLive == null) throw new ArgumentNullException(nameof(spawnLive));
            if (despawn == null) throw new ArgumentNullException(nameof(despawn));
            if (ensureCorpse == null) throw new ArgumentNullException(nameof(ensureCorpse));

            spawnLive.Clear();
            despawn.Clear();
            ensureCorpse.Clear();
            seenLive.Clear();
            seenCorpse.Clear();

            for (int i = 0; i < living.Count; i++)
            {
                var state = living[i];
                if (!state.EntityId.IsValid || !state.Alive) continue;
                ulong id = state.EntityId.Value;
                seenLive.Add(id);
                if (live.Add(id))
                {
                    // A fresh life must not remain on the corpse list.
                    RemoveCorpse(id);
                    spawnLive.Add(state.EntityId);
                }
            }

            int corpseLimit = Math.Min(MaxCosmeticCorpses, cosmeticCorpses.Count);
            for (int i = 0; i < corpseLimit; i++)
            {
                var state = cosmeticCorpses[i];
                if (!state.EntityId.IsValid) continue;
                ulong id = state.EntityId.Value;
                if (seenLive.Contains(id)) continue;
                seenCorpse.Add(id);
                if (live.Remove(id))
                {
                    // Living handle becomes a corpse view; do not despawn renderer.
                }
                if (!ContainsCorpse(id))
                {
                    corpses.Add(id);
                    ensureCorpse.Add(state.EntityId);
                }
            }

            while (corpses.Count > MaxCosmeticCorpses)
            {
                ulong oldest = corpses[0];
                corpses.RemoveAt(0);
                if (!seenCorpse.Contains(oldest) && !seenLive.Contains(oldest))
                    despawn.Add(new EntityId(oldest));
            }

            spawnScratch.Clear();
            foreach (ulong id in live)
            {
                if (!seenLive.Contains(id))
                    spawnScratch.Add(new EntityId(id));
            }
            for (int i = 0; i < spawnScratch.Count; i++)
            {
                live.Remove(spawnScratch[i].Value);
                despawn.Add(spawnScratch[i]);
            }

            corpseScratch.Clear();
            for (int i = corpses.Count - 1; i >= 0; i--)
            {
                ulong id = corpses[i];
                if (seenCorpse.Contains(id) || seenLive.Contains(id)) continue;
                corpses.RemoveAt(i);
                corpseScratch.Add(new EntityId(id));
            }
            for (int i = 0; i < corpseScratch.Count; i++)
                despawn.Add(corpseScratch[i]);
        }

        private bool ContainsCorpse(ulong id)
        {
            for (int i = 0; i < corpses.Count; i++)
                if (corpses[i] == id) return true;
            return false;
        }

        private void RemoveCorpse(ulong id)
        {
            for (int i = corpses.Count - 1; i >= 0; i--)
                if (corpses[i] == id) corpses.RemoveAt(i);
        }
    }
}
