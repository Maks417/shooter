#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-074 mesh-only dressing that turns arena feature envelopes (<see cref="CompoundPlan.Features"/>) into readable
    /// objects: trucks, tankers, semi-trailers, boxcars, flatcars with tarped loads, sandbag walls, tarp fences, booths,
    /// a flag on the parade plinth and pipe racks. Vehicles, rolling stock, sandbags and fences are drawn instead of their
    /// raw collision box and stay inside its envelope (gaps under chassis are closed by dark skirts so nothing visible
    /// opens where collision blocks). Booths, the plinth and pipe racks keep their box and gain trim on and above it.
    /// Collision, navigation and package identity never change. Only the oil-only kinds (tanker barrel, pipe rack) use the
    /// petroleum accent roles <see cref="MapSolidsMaterialRole.TankWhite"/>/<see cref="MapSolidsMaterialRole.SafetyYellow"/>,
    /// so Training Compound and Depot Yard keep their own palettes.
    /// </summary>
    public static class ArenaFeatureDressing
    {
        private const float S = 0.70710678f;
        private static readonly Quat YUp = new Quat(-S, 0f, 0f, S);
        private static readonly Quat AlongX = new Quat(0f, S, 0f, S);
        private static readonly Quat AlongZ = Quat.Identity;

        /// <summary>True for feature kinds whose collision box is drawn by this dressing instead of as a plain box.</summary>
        public static bool ReplacesBox(ArenaFeatureKind kind)
            => kind == ArenaFeatureKind.Truck || kind == ArenaFeatureKind.Tanker || kind == ArenaFeatureKind.Trailer ||
               kind == ArenaFeatureKind.Boxcar || kind == ArenaFeatureKind.Flatcar || kind == ArenaFeatureKind.Cargo ||
               kind == ArenaFeatureKind.Sandbags || kind == ArenaFeatureKind.TarpFence;

        /// <summary>True when <paramref name="box"/> is the envelope of a feature this dressing draws itself.</summary>
        public static bool ReplacedByDressing(CollisionBox box, IReadOnlyList<ArenaFeature> features)
        {
            if (!box.Gameplay) return false;
            for (int i = 0; i < features.Count; i++)
                if (ReplacesBox(features[i].Kind) && features[i].Matches(box)) return true;
            return false;
        }

        public static void Append(MeshAssembly mesh, IReadOnlyList<ArenaFeature> features)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (features == null) throw new ArgumentNullException(nameof(features));
            for (int i = 0; i < features.Count; i++)
            {
                var f = features[i];
                switch (f.Kind)
                {
                    case ArenaFeatureKind.Truck: AddTruck(mesh, f, tanker: false); break;
                    case ArenaFeatureKind.Tanker: AddTruck(mesh, f, tanker: true); break;
                    case ArenaFeatureKind.Trailer: AddTrailer(mesh, f); break;
                    case ArenaFeatureKind.Boxcar: AddBoxcar(mesh, f); break;
                    case ArenaFeatureKind.Flatcar: AddFlatcar(mesh, f); break;
                    case ArenaFeatureKind.Cargo: AddCargo(mesh, f); break;
                    case ArenaFeatureKind.Sandbags: AddSandbags(mesh, f); break;
                    case ArenaFeatureKind.TarpFence: AddTarpFence(mesh, f); break;
                    case ArenaFeatureKind.Booth: AddBooth(mesh, f); break;
                    case ArenaFeatureKind.Flagpole: AddFlag(mesh, f); break;
                    case ArenaFeatureKind.PipeRack: AddPipeRack(mesh, f); break;
                }
            }
        }

        // ---- local frame helpers: u = along the long axis (toward the front when Facing = +1), v = across, y = up ----

        private readonly struct Frame
        {
            public readonly ArenaFeature F;
            public readonly float Foot;
            public Frame(ArenaFeature f) { F = f; Foot = f.Center.Y - f.Size.Y * 0.5f; }
            public float HalfL => F.Length * 0.5f;
            public float HalfW => F.Width * 0.5f;
            public float Top => Foot + F.Size.Y;
            public Float3 At(float u, float y, float v)
            {
                u *= F.Facing;
                return F.AlongX ? new Float3(F.Center.X + u, y, F.Center.Z + v) : new Float3(F.Center.X + v, y, F.Center.Z + u);
            }
            public Float3 Dim(float lu, float ly, float lv) => F.AlongX ? new Float3(lu, ly, lv) : new Float3(lv, ly, lu);
            /// <summary>Rotation for a cylinder whose axis runs across the vehicle (axles).</summary>
            public Quat Across => F.AlongX ? AlongZ : AlongX;
            /// <summary>Rotation for a cylinder whose axis runs along the vehicle (tanker barrels).</summary>
            public Quat Along => F.AlongX ? AlongX : AlongZ;
        }

        private static void Box(MeshAssembly mesh, Frame fr, float u, float y0, float y1, float v, float lu, float lv, MapSolidsMaterialRole role)
            => mesh.Add(PrimitiveGeometry.Box(fr.Dim(lu, y1 - y0, lv)), fr.At(u, (y0 + y1) * 0.5f, v), Quat.Identity, (ushort)role);

        private static void Axle(MeshAssembly mesh, Frame fr, float u, float radius, float width)
            => mesh.Add(PrimitiveGeometry.Cylinder(radius, width, 14), fr.At(u, fr.Foot + radius, 0f), fr.Across, (ushort)MapSolidsMaterialRole.Foundation);

        // ---- vehicles ----

        private static void AddTruck(MeshAssembly mesh, ArenaFeature f, bool tanker)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            const float wheel = 0.5f;
            float cabLen = Math.Min(2.2f, L * 0.3f);
            float frontU = fr.HalfL - cabLen * 0.5f;
            // Dark skirts close the under-chassis gap inside the envelope.
            Box(mesh, fr, 0f, foot + 0.18f, foot + 0.95f, 0f, L - 0.3f, W - 0.5f, MapSolidsMaterialRole.Foundation);
            Box(mesh, fr, 0f, foot + 0.95f, foot + 1.2f, 0f, L, W * 0.92f, MapSolidsMaterialRole.MetalCover);
            Axle(mesh, fr, fr.HalfL - 1.1f, wheel, W);
            Axle(mesh, fr, -fr.HalfL + 2.6f, wheel, W);
            Axle(mesh, fr, -fr.HalfL + 1.3f, wheel, W);
            // Cab with windscreen, roof and bumper.
            float cabTop = foot + H * 0.92f;
            Box(mesh, fr, frontU, foot + 1.2f, cabTop, 0f, cabLen, W, MapSolidsMaterialRole.OxidePaint);
            Box(mesh, fr, fr.HalfL - 0.03f, foot + H * 0.55f, cabTop - 0.2f, 0f, 0.06f, W * 0.82f, MapSolidsMaterialRole.Foundation);
            Box(mesh, fr, fr.HalfL - 0.12f, foot + 0.45f, foot + 0.8f, 0f, 0.24f, W, MapSolidsMaterialRole.MetalCover);
            Box(mesh, fr, frontU, cabTop, foot + H, 0f, cabLen * 0.8f, W * 0.8f, MapSolidsMaterialRole.Foundation);
            float bedLen = L - cabLen - 0.25f;
            float bedU = -fr.HalfL + bedLen * 0.5f;
            if (tanker)
            {
                float r = Math.Min(W * 0.5f, (H - 1.2f) * 0.5f) - 0.02f;
                mesh.Add(PrimitiveGeometry.Cylinder(r, bedLen, 24), fr.At(bedU, foot + 1.2f + r, 0f), fr.Along, (ushort)MapSolidsMaterialRole.TankWhite);
                for (float u = bedU - bedLen * 0.5f + 0.6f; u < bedU + bedLen * 0.5f; u += 1.6f)
                    mesh.Add(PrimitiveGeometry.Cylinder(r + 0.03f, 0.08f, 24), fr.At(u, foot + 1.2f + r, 0f), fr.Along, (ushort)MapSolidsMaterialRole.Foundation);
                Box(mesh, fr, bedU, foot + 1.2f + 2f * r, foot + 1.25f + 2f * r, 0f, bedLen * 0.7f, 0.5f, MapSolidsMaterialRole.SafetyYellow);
            }
            else
            {
                // Canvas-covered cargo bed with hoops.
                Box(mesh, fr, bedU, foot + 1.2f, foot + 1.75f, 0f, bedLen, W, MapSolidsMaterialRole.OxidePaint);
                Box(mesh, fr, bedU, foot + 1.75f, foot + H - 0.05f, 0f, bedLen - 0.1f, W - 0.1f, MapSolidsMaterialRole.DepotBlue);
                for (float u = bedU - bedLen * 0.5f + 0.4f; u < bedU + bedLen * 0.5f; u += 1.4f)
                    Box(mesh, fr, u, foot + 1.75f, foot + H, 0f, 0.08f, W, MapSolidsMaterialRole.Trim);
            }
        }

        private static void AddTrailer(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            Box(mesh, fr, 0f, foot + 0.15f, foot + 1.0f, 0f, L - 0.2f, W - 0.5f, MapSolidsMaterialRole.Foundation);
            Axle(mesh, fr, -fr.HalfL + 1.0f, 0.45f, W);
            Axle(mesh, fr, -fr.HalfL + 2.1f, 0.45f, W);
            Box(mesh, fr, fr.HalfL - 1.5f, foot, foot + 1.0f, 0f, 0.2f, W - 0.6f, MapSolidsMaterialRole.MetalCover);
            Box(mesh, fr, 0f, foot + 1.0f, foot + H, 0f, L, W, MapSolidsMaterialRole.DepotBlue);
            // Corrugated side ribs, rear doors and a top rail.
            for (float u = -fr.HalfL + 0.6f; u < fr.HalfL - 0.3f; u += 0.9f)
            {
                Box(mesh, fr, u, foot + 1.05f, foot + H - 0.05f, fr.HalfW + 0.015f, 0.06f, 0.03f, MapSolidsMaterialRole.Trim);
                Box(mesh, fr, u, foot + 1.05f, foot + H - 0.05f, -fr.HalfW - 0.015f, 0.06f, 0.03f, MapSolidsMaterialRole.Trim);
            }
            Box(mesh, fr, -fr.HalfL - 0.015f, foot + 1.05f, foot + H - 0.05f, 0f, 0.03f, 0.05f, MapSolidsMaterialRole.Foundation);
            Box(mesh, fr, 0f, foot + H - 0.1f, foot + H, 0f, L + 0.02f, W + 0.02f, MapSolidsMaterialRole.OxidePaint);
        }

        private static void AddBogies(MeshAssembly mesh, Frame fr, float W)
        {
            foreach (float end in new[] { -1f, 1f })
            {
                float u = end * (fr.HalfL - 2.0f);
                Box(mesh, fr, u, fr.Foot + 0.2f, fr.Foot + 0.95f, 0f, 2.6f, W - 0.4f, MapSolidsMaterialRole.Foundation);
                Axle(mesh, fr, u - 0.75f, 0.42f, W - 0.3f);
                Axle(mesh, fr, u + 0.75f, 0.42f, W - 0.3f);
            }
            // Couplers on both ends (inside the envelope).
            Box(mesh, fr, fr.HalfL - 0.15f, fr.Foot + 0.7f, fr.Foot + 0.95f, 0f, 0.3f, 0.35f, MapSolidsMaterialRole.MetalCover);
            Box(mesh, fr, -fr.HalfL + 0.15f, fr.Foot + 0.7f, fr.Foot + 0.95f, 0f, 0.3f, 0.35f, MapSolidsMaterialRole.MetalCover);
        }

        private static void AddBoxcar(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            // Dark under-frame closes the gap between the bogies.
            Box(mesh, fr, 0f, foot + 0.15f, foot + 1.0f, 0f, L - 0.6f, W - 0.6f, MapSolidsMaterialRole.Foundation);
            AddBogies(mesh, fr, W);
            Box(mesh, fr, 0f, foot + 1.0f, foot + H - 0.15f, 0f, L - 0.6f, W, MapSolidsMaterialRole.OxidePaint);
            Box(mesh, fr, 0f, foot + H - 0.15f, foot + H, 0f, L - 0.5f, W - 0.2f, MapSolidsMaterialRole.Foundation);
            // Sliding door, door rails and vertical ribs on both sides.
            foreach (float side in new[] { -1f, 1f })
            {
                float v = side * (fr.HalfW + 0.02f);
                Box(mesh, fr, 0f, foot + 1.1f, foot + H - 0.35f, v, 2.8f, 0.04f, MapSolidsMaterialRole.DepotBlue);
                Box(mesh, fr, 0f, foot + H - 0.32f, foot + H - 0.24f, v, 6.2f, 0.06f, MapSolidsMaterialRole.MetalCover);
                for (float u = -fr.HalfL + 0.9f; u < fr.HalfL - 0.6f; u += 1.1f)
                {
                    if (Math.Abs(u) < 1.6f) continue;
                    Box(mesh, fr, u, foot + 1.05f, foot + H - 0.2f, v, 0.07f, 0.03f, MapSolidsMaterialRole.Trim);
                }
            }
        }

        private static void AddFlatcar(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            Box(mesh, fr, 0f, foot + 0.15f, foot + H - 0.3f, 0f, L - 0.6f, W - 0.6f, MapSolidsMaterialRole.Foundation);
            AddBogies(mesh, fr, W);
            Box(mesh, fr, 0f, foot + H - 0.3f, foot + H, 0f, L, W, MapSolidsMaterialRole.MetalCover);
            // Stake pockets along the deck edge.
            for (float u = -fr.HalfL + 0.8f; u < fr.HalfL; u += 1.6f)
            {
                Box(mesh, fr, u, foot + H - 0.3f, foot + H, fr.HalfW - 0.06f, 0.12f, 0.12f, MapSolidsMaterialRole.OxidePaint);
                Box(mesh, fr, u, foot + H - 0.3f, foot + H, -fr.HalfW + 0.06f, 0.12f, 0.12f, MapSolidsMaterialRole.OxidePaint);
            }
        }

        private static void AddCargo(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            Box(mesh, fr, 0f, foot, foot + H - 0.2f, 0f, L, W, MapSolidsMaterialRole.DepotBlue);
            Box(mesh, fr, 0f, foot + H - 0.2f, foot + H, 0f, L - 0.3f, W - 0.3f, MapSolidsMaterialRole.DepotBlue);
            // Ratchet straps over the tarp.
            for (float u = -fr.HalfL + 0.7f; u < fr.HalfL; u += 1.2f)
                Box(mesh, fr, u, foot, foot + H + 0.01f, 0f, 0.07f, W + 0.02f, MapSolidsMaterialRole.OxidePaint);
        }

        // ---- field works ----

        private static void AddSandbags(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            const float bagH = 0.22f;
            int courses = Math.Max(1, (int)Math.Round(H / bagH));
            float ch = H / courses;
            for (int c = 0; c < courses; c++)
            {
                // Running bond: odd courses start with a half bag; bags taper slightly toward the top course.
                const float bagL = 0.62f;
                float taper = 1f - 0.06f * c / Math.Max(1, courses - 1);
                float a = -fr.HalfL;
                float next = a + (c % 2 == 0 ? bagL : bagL * 0.5f);
                while (a < fr.HalfL - 0.05f)
                {
                    float b = Math.Min(next, fr.HalfL);
                    Box(mesh, fr, (a + b) * 0.5f, foot + c * ch, foot + (c + 1) * ch - 0.015f, 0f, Math.Max(0.05f, b - a - 0.04f), W * taper, MapSolidsMaterialRole.Trim);
                    a = b;
                    next = a + bagL;
                }
            }
        }

        private static void AddTarpFence(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, H = f.Size.Y, foot = fr.Foot;
            int posts = Math.Max(2, (int)Math.Ceiling(L / 2.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float u = -fr.HalfL + 0.05f + (L - 0.1f) * i / (posts - 1);
                Box(mesh, fr, u, foot, foot + H, 0f, 0.1f, f.Width, MapSolidsMaterialRole.MetalCover);
            }
            Box(mesh, fr, 0f, foot + 0.12f, foot + H - 0.1f, 0f, L - 0.12f, f.Width * 0.4f, MapSolidsMaterialRole.DepotBlue);
            Box(mesh, fr, 0f, foot + H - 0.06f, foot + H, 0f, L, f.Width * 0.6f, MapSolidsMaterialRole.MetalCover);
        }

        // ---- trimmed boxes (raw box still drawn) ----

        private static void AddBooth(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, W = f.Width, H = f.Size.Y, foot = fr.Foot;
            // Overhanging roof, window band on all four faces and a door slab on the front.
            Box(mesh, fr, 0f, foot + H, foot + H + 0.14f, 0f, L + 0.4f, W + 0.4f, MapSolidsMaterialRole.Foundation);
            float y0 = foot + H * 0.5f, y1 = foot + H * 0.78f;
            Box(mesh, fr, 0f, y0, y1, fr.HalfW + 0.01f, L * 0.7f, 0.03f, MapSolidsMaterialRole.Foundation);
            Box(mesh, fr, 0f, y0, y1, -fr.HalfW - 0.01f, L * 0.7f, 0.03f, MapSolidsMaterialRole.Foundation);
            Box(mesh, fr, fr.HalfL + 0.01f, y0, y1, 0f, 0.03f, W * 0.6f, MapSolidsMaterialRole.Foundation);
            Box(mesh, fr, -fr.HalfL - 0.01f, foot, foot + Math.Min(2.1f, H - 0.3f), 0f, 0.03f, Math.Min(0.9f, W * 0.5f), MapSolidsMaterialRole.Trim);
        }

        private static void AddFlag(MeshAssembly mesh, ArenaFeature f)
        {
            float top = f.Center.Y + f.Size.Y * 0.5f;
            // Pole on the plinth, finial and a still flag panel (above head height, visual only).
            mesh.Add(PrimitiveGeometry.Cylinder(0.07f, 8f, 12), new Float3(f.Center.X, top + 4f, f.Center.Z), YUp, (ushort)MapSolidsMaterialRole.MetalCover);
            mesh.Add(PrimitiveGeometry.Cylinder(0.14f, 0.2f, 12), new Float3(f.Center.X, top + 8.1f, f.Center.Z), YUp, (ushort)MapSolidsMaterialRole.Trim);
            mesh.Add(PrimitiveGeometry.Box(new Float3(2.2f, 1.3f, 0.03f)), new Float3(f.Center.X + 1.15f, top + 7.25f, f.Center.Z), Quat.Identity, (ushort)MapSolidsMaterialRole.DepotBlue);
            // Plinth cap and steps band.
            mesh.Add(PrimitiveGeometry.Box(new Float3(f.Size.X + 0.2f, 0.1f, f.Size.Z + 0.2f)), new Float3(f.Center.X, top + 0.05f, f.Center.Z), Quat.Identity, (ushort)MapSolidsMaterialRole.Trim);
        }

        private static void AddPipeRack(MeshAssembly mesh, ArenaFeature f)
        {
            var fr = new Frame(f);
            float L = f.Length, H = f.Size.Y, foot = fr.Foot;
            // Pipes riding on the clad rack top, with bent caps every 3 m.
            float[] offsets = { -0.35f, 0f, 0.35f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float r = i == 1 ? 0.2f : 0.14f;
                mesh.Add(PrimitiveGeometry.Cylinder(r, L + 0.6f, 12), fr.At(0f, foot + H + r + 0.05f, offsets[i]), fr.Along,
                    (ushort)(i == 1 ? MapSolidsMaterialRole.TankWhite : MapSolidsMaterialRole.OxidePaint));
            }
            for (float u = -fr.HalfL; u <= fr.HalfL + 0.01f; u += Math.Max(1f, L / Math.Max(1, (int)Math.Round(L / 3f))))
                Box(mesh, fr, u, foot + H, foot + H + 0.12f, 0f, 0.2f, f.Width + 0.3f, MapSolidsMaterialRole.SafetyYellow);
        }
    }
}
