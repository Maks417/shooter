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
        Trim = 2,
        DepotBlue = 3,
        OxidePaint = 4,
        Foundation = 5,
        /// <summary>Oil-depot white tank enamel (OilDepotDressing).</summary>
        TankWhite = 6,
        /// <summary>Oil-depot safety-yellow rails, ladders and gantry steel.</summary>
        SafetyYellow = 7
    }

    /// <summary>Builds the combined map-solids mesh with metric UVs and role submeshes.</summary>
    public static class MapSolidsMesh
    {
        public const int MaxVisualVertices = 180000;

        public static bool IsCargoContainer(CollisionBox box) => box.Gameplay &&
            Math.Abs(box.Size.X - 6f) < .01f && Math.Abs(box.Size.Z - 2.5f) < .01f &&
            (Math.Abs(box.Size.Y - 2.6f) < .01f || Math.Abs(box.Size.Y - 5.2f) < .01f);

        /// <summary>
        /// Classifies a collision box into a packaged material role without mutating gameplay identity.
        /// Cover proxies / TagCover / cover-size heuristic → metal; thin non-gameplay architecture → trim;
        /// roofs and bulk structure (gameplay or visual) → concrete.
        /// </summary>
        public static ushort Classify(CollisionBox box, ISet<ulong>? coverStableIds = null)
        {
            if (IsCargoContainer(box))
                return (ushort)(Math.Abs(box.Center.X) < 25f ? MapSolidsMaterialRole.DepotBlue : MapSolidsMaterialRole.OxidePaint);
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
        public static MeshData Build(CompoundPlan plan) => Build(plan, null);

        /// <summary>
        /// Map-aware overload: arena features (vehicles, rolling stock, sandbags, fences) are drawn by
        /// <see cref="ArenaFeatureDressing"/> instead of their envelope box; <c>oil-depot</c> also replaces its square
        /// tank drums with round shells and appends <see cref="OilDepotDressing"/>. Collision boxes are never changed.
        /// </summary>
        public static MeshData Build(CompoundPlan plan, string? mapId)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            bool oil = string.Equals(mapId, OilDepotDressing.MapId, StringComparison.Ordinal) && plan.Arena != null;
            if (plan.CollisionBoxes.Count == 0)
                return PrimitiveGeometry.Box(new Float3(1f, 1f, 1f), 0f, 1);

            var coverIds = CoverStableIds(plan);
            var assembly = new MeshAssembly();
            for (int i = 0; i < plan.CollisionBoxes.Count; i++)
            {
                CollisionBox box = plan.CollisionBoxes[i];
                if (oil && OilDepotDressing.ReplacedByDressing(box)) continue;
                if (ArenaFeatureDressing.ReplacedByDressing(box, plan.Features)) continue;
                ushort slot = Classify(box, coverIds);
                // Small physical edge radii catch directional light instead of a razor-sharp blockout edge.
                float minimum = Math.Min(box.Size.X, Math.Min(box.Size.Y, box.Size.Z));
                float bevel = box.Gameplay && minimum >= .3f ? .015f : 0f;
                var local = PrimitiveGeometry.Box(box.Size, bevel, 1);
                assembly.Add(local, box.Center, Quat.Identity, slot);
                if (IsCargoContainer(box)) AddCargoDetails(assembly, box);
                else if (box.Gameplay && box.Size.Y >= 2.35f &&
                    Math.Min(box.Size.X, box.Size.Z) <= 2.01f && Math.Max(box.Size.X, box.Size.Z) >= 3f)
                    AddWallDetails(assembly, box);
            }
            ArenaFeatureDressing.Append(assembly, plan.Features);
            if (oil) OilDepotDressing.Append(assembly, plan.Arena!);
            if (assembly.VertexCount > MaxVisualVertices)
                throw new InvalidOperationException("Architectural mesh exceeds the reviewed visual vertex budget.");
            return assembly.Build();
        }

        private static void Box(MeshAssembly mesh, Float3 center, Float3 size, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Box(size), center, Quat.Identity, (ushort)role);

        private static void AddWallDetails(MeshAssembly mesh, CollisionBox box)
        {
            bool alongX = box.Size.X >= box.Size.Z;
            float length = alongX ? box.Size.X : box.Size.Z;
            float thickness = alongX ? box.Size.Z : box.Size.X;
            float foot = box.Center.Y - box.Size.Y * .5f;
            // Contacting bands extend only centimetres from the authoritative wall, never across openings.
            Float3 Dimensions(float run, float height, float depth) => alongX ? new Float3(run, height, depth) : new Float3(depth, height, run);
            Float3 At(float run, float height) => alongX ? new Float3(box.Center.X + run, height, box.Center.Z) : new Float3(box.Center.X, height, box.Center.Z + run);
            Box(mesh, At(0, foot + .22f), Dimensions(length, .44f, thickness + .024f), MapSolidsMaterialRole.Foundation);
            Box(mesh, At(0, foot + box.Size.Y - .065f), Dimensions(length, .13f, thickness + .04f), MapSolidsMaterialRole.Trim);
            int panels = Math.Max(1, (int)Math.Ceiling(length / 3.2f));
            for (int i = 1; i < panels; i++)
            {
                float run = -length * .5f + length * i / panels;
                // Narrow expansion joints break long blank walls at believable precast-panel intervals.
                Box(mesh, At(run, foot + box.Size.Y * .5f), Dimensions(.025f, box.Size.Y - .24f, thickness + .008f), MapSolidsMaterialRole.Foundation);
            }
            // Wall-end protection stays within the original silhouette in both horizontal axes.
            foreach (float end in new[] { -1f, 1f })
                Box(mesh, At(end * (length * .5f - .055f), foot + box.Size.Y * .5f), Dimensions(.11f, box.Size.Y - .13f, thickness + .025f), MapSolidsMaterialRole.Trim);
        }

        private static void AddCargoDetails(MeshAssembly mesh, CollisionBox box)
        {
            var c = box.Center;
            float foot = c.Y - box.Size.Y * .5f;
            int tiers = box.Size.Y > 3f ? 2 : 1;
            for (int tier = 0; tier < tiers; tier++)
            {
                float y = foot + tier * 2.6f;
                foreach (float side in new[] { -1f, 1f })
                {
                    // Long corrugated steel sides, with clear top/bottom perimeter rails.
                    for (int rib = 0; rib < 11; rib++)
                        Box(mesh, new Float3(c.X - 2.7f + rib * .54f, y + 1.3f, c.Z + side * 1.26f),
                            new Float3(.055f, 2.32f, .035f), MapSolidsMaterialRole.MetalCover);
                    foreach (float height in new[] { .09f, 2.51f })
                        Box(mesh, new Float3(c.X, y + height, c.Z + side * 1.26f), new Float3(5.94f, .13f, .05f), MapSolidsMaterialRole.MetalCover);
                    // Door seam and locking bars on the short ends.
                    Box(mesh, new Float3(c.X + side * 3.007f, y + 1.3f, c.Z), new Float3(.025f, 2.4f, .035f), MapSolidsMaterialRole.Foundation);
                    foreach (float door in new[] { -.62f, .62f })
                    {
                        Box(mesh, new Float3(c.X + side * 3.018f, y + 1.3f, c.Z + door), new Float3(.04f, 2.24f, .045f), MapSolidsMaterialRole.MetalCover);
                        Box(mesh, new Float3(c.X + side * 3.028f, y + 1.04f, c.Z + door + .12f), new Float3(.045f, .055f, .28f), MapSolidsMaterialRole.MetalCover);
                    }
                }
            }
        }
    }
}
