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
        /// Cover proxies / TagCover / cover-size heuristic → metal; thin non-gameplay architecture → trim;
        /// roofs and bulk structure (gameplay or visual) → concrete.
        /// </summary>
        public static ushort Classify(CollisionBox box, ISet<ulong>? coverStableIds = null)
        {
            if (box.SurfaceTag == ArenaBoxBuilder.TagCover) return (ushort)MapSolidsMaterialRole.MetalCover;
            if (coverStableIds != null && coverStableIds.Contains(box.StableId))
                return (ushort)MapSolidsMaterialRole.MetalCover;
            if (LooksLikeMetalCover(box)) return (ushort)MapSolidsMaterialRole.MetalCover;
            if (!box.Gameplay)
            {
                if (LooksLikeMetalProp(box)) return (ushort)MapSolidsMaterialRole.MetalCover;
                if (LooksLikeTrim(box)) return (ushort)MapSolidsMaterialRole.Trim;
                return (ushort)MapSolidsMaterialRole.StructureConcrete;
            }
            return (ushort)MapSolidsMaterialRole.StructureConcrete;
        }

        /// <summary>Same cover-size heuristic ProductMapView uses for metal overlays (presentation parity).</summary>
        public static bool LooksLikeMetalCover(CollisionBox box)
        {
            return box.Size.Y <= 1.6f && box.Size.Y >= 0.8f
                && box.Size.X <= 4f && box.Size.Z <= 4f
                && box.Size.X >= 0.6f && box.Size.Z >= 0.6f;
        }

        /// <summary>Thin rails, posts, and lamp stems that should read as metal on the baked solids mesh.</summary>
        public static bool LooksLikeMetalProp(CollisionBox box)
        {
            float minHoriz = Math.Min(box.Size.X, box.Size.Z);
            float maxHoriz = Math.Max(box.Size.X, box.Size.Z);
            // Tall thin posts / lamp stems.
            if (box.Size.Y >= 1.8f && minHoriz <= 0.35f && maxHoriz <= 0.45f) return true;
            // Low rails / corner castings / fence stubs (thin in one horizontal axis, short height).
            if (box.Size.Y <= 0.35f && minHoriz <= 0.22f && maxHoriz >= 0.4f) return true;
            return false;
        }

        /// <summary>Door/window frames, baseboards, curbs, and similar plaster-scale bands.</summary>
        public static bool LooksLikeTrim(CollisionBox box)
        {
            float minHoriz = Math.Min(box.Size.X, box.Size.Z);
            float maxHoriz = Math.Max(box.Size.X, box.Size.Z);
            if (box.Size.Y <= 0.45f && minHoriz <= 0.45f) return true;
            if (box.Size.Y <= 2.6f && minHoriz <= 0.18f && maxHoriz <= 4.5f) return true;
            return false;
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
