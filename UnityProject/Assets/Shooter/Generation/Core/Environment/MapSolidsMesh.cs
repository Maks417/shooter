#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// T079-A: packaged map solids split by material role (structure/concrete, metal/cover, trim).
    /// Submesh MaterialSlot values match <see cref="MapSolidsMaterialRole"/>; empty roles are omitted.
    /// </summary>
    public enum MapSolidsMaterialRole : ushort
    {
        StructureConcrete = 0,
        MetalCover = 1,
        Trim = 2
    }

    /// <summary>Builds the combined map-solids mesh with metric UVs and role submeshes.</summary>
    public static class MapSolidsMesh
    {
        /// <summary>
        /// Classifies a collision box into a packaged material role without mutating gameplay identity.
        /// Non-gameplay architecture → trim; tagged/heuristic cover → metal; otherwise structure.
        /// </summary>
        public static ushort Classify(CollisionBox box, ISet<ulong>? coverStableIds = null)
        {
            if (!box.Gameplay) return (ushort)MapSolidsMaterialRole.Trim;
            if (box.SurfaceTag == ArenaBoxBuilder.TagCover) return (ushort)MapSolidsMaterialRole.MetalCover;
            if (coverStableIds != null && coverStableIds.Contains(box.StableId))
                return (ushort)MapSolidsMaterialRole.MetalCover;
            if (LooksLikeMetalCover(box)) return (ushort)MapSolidsMaterialRole.MetalCover;
            return (ushort)MapSolidsMaterialRole.StructureConcrete;
        }

        /// <summary>Same cover-size heuristic ProductMapView uses for metal overlays (presentation parity).</summary>
        public static bool LooksLikeMetalCover(CollisionBox box)
        {
            return box.Size.Y <= 1.6f && box.Size.Y >= 0.8f
                && box.Size.X <= 4f && box.Size.Z <= 4f
                && box.Size.X >= 0.6f && box.Size.Z >= 0.6f;
        }

        public static HashSet<ulong> CoverStableIds(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var ids = new HashSet<ulong>();
            for (int i = 0; i < plan.Covers.Count; i++)
                ids.Add(plan.Covers[i].StableId);
            return ids;
        }

        /// <summary>
        /// Emits every collision box (gameplay + visual) so roofs/trim remain visible.
        /// Collision SGAF still filters gameplay-only elsewhere. UVs stay metric via PrimitiveGeometry.Box.
        /// </summary>
        public static MeshData Build(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.CollisionBoxes.Count == 0)
                return PrimitiveGeometry.Box(new Float3(1f, 1f, 1f), 0f, 1);

            var coverIds = CoverStableIds(plan);
            var assembly = new MeshAssembly();
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                CollisionBox box = plan.CollisionBoxes[i];
                ushort slot = Classify(box, coverIds);
                var local = PrimitiveGeometry.Box(box.Size, 0f, 1);
                assembly.Add(local, box.Center, Quat.Identity, slot);
            }
            return assembly.Build();
        }
    }
}
