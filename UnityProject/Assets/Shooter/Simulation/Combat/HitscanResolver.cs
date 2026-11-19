#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Simulation.Combat
{
    public readonly struct HitscanRequest
    {
        public EntityId Shooter { get; }
        public Float3 Eye { get; }
        public Float3 Muzzle { get; }
        public Float3 UnitAim { get; }
        public WeaponDefinition Definition { get; }
        public uint ShotIndex { get; }
        public bool Ads { get; }
        public ulong Seed { get; }
        public uint CollisionRevision { get; }
        /// <summary>ADR-118: which pellet of a multi-pellet shot this ray is (0 for single-projectile guns).</summary>
        public byte PelletIndex { get; }
        public HitscanRequest(EntityId shooter, Float3 eye, Float3 muzzle, Float3 unitAim, WeaponDefinition definition, uint shotIndex, bool ads, ulong seed, uint collisionRevision, byte pelletIndex = 0)
        {
            Shooter = shooter; Eye = eye; Muzzle = muzzle; UnitAim = unitAim; Definition = definition; ShotIndex = shotIndex; Ads = ads; Seed = seed; CollisionRevision = collisionRevision;
            PelletIndex = pelletIndex;
        }
    }

    public readonly struct HitscanResult
    {
        public bool BlockedAtMuzzle { get; }
        public bool Fired { get; }
        public Float3 Origin { get; }
        public Float3 UnitDirection { get; }
        public bool Impacted { get; }
        public QueryHit Impact { get; }
        public HitscanResult(bool blockedAtMuzzle, bool fired, Float3 origin, Float3 unitDirection, bool impacted, QueryHit impact)
        {
            BlockedAtMuzzle = blockedAtMuzzle; Fired = fired; Origin = origin; UnitDirection = unitDirection; Impacted = impacted; Impact = impact;
        }
    }

    /// <summary>ADR-112: the eye (crosshair) ray resolves the hit against world and hostile actor capsules up to 300 m;
    /// a muzzle inside or behind a wall rejects the shot; the muzzle is the tracer origin only.</summary>
    public sealed class HitscanResolver
    {
        public const float MaxMeters = 300f;
        private readonly QueryFilter coverFilter;

        public HitscanResolver(uint worldSolidMask, uint bulletMask)
        {
            coverFilter = new QueryFilter(worldSolidMask, default, false);
            _ = bulletMask;
        }

        public HitscanResult Resolve(HitscanRequest request, IWorldQueries world, IReadOnlyList<HitVolume>? volumes = null, IReadOnlyList<EntityId>? ignore = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            float spread = request.Ads ? request.Definition.AdsSpreadRadians : request.Definition.HipSpreadRadians;
            ulong u = SpreadSampling.MixShot(request.Seed, request.ShotIndex, 1);
            ulong v = SpreadSampling.MixShot(request.Seed, request.ShotIndex, 2);
            Float3 dir = SpreadSampling.SampleDirection(request.UnitAim, spread, u, v);
            // ADR-118: a multi-pellet shot scatters each pellet inside the pellet cone around the aimed direction, from
            // its own channels (16 + 2p, 17 + 2p). Single-projectile guns never take this path, so they stay bit-identical.
            if (request.Definition.PelletCount > 1)
                dir = PelletDirection(dir, request.Definition.PelletSpreadRadians, request.Seed, request.ShotIndex, request.PelletIndex);

            // Eye→muzzle segment through static cover => block. The gameplay muzzle can sit inside or behind
            // a wall when the player is pressed against it; a ray that starts inside a collider is not reported
            // by the physics backend, so the segment from the (outside) eye is tested instead.
            Float3 eyeToMuzzle = new Float3(request.Muzzle.X - request.Eye.X, request.Muzzle.Y - request.Eye.Y, request.Muzzle.Z - request.Eye.Z);
            double eyeMuzzleLen2 = eyeToMuzzle.LengthSquared;
            if (eyeMuzzleLen2 > 1e-10)
            {
                float eyeMuzzleLen = (float)Math.Sqrt(eyeMuzzleLen2);
                Float3 eyeMuzzleDir = new Float3(eyeToMuzzle.X / eyeMuzzleLen, eyeToMuzzle.Y / eyeMuzzleLen, eyeToMuzzle.Z / eyeMuzzleLen);
                var eyeMuzzleProbe = world.Raycast(request.Eye, eyeMuzzleDir, eyeMuzzleLen, coverFilter);
                if (eyeMuzzleProbe.Status == QueryStatus.Hit && eyeMuzzleProbe.Hit.DistanceMeters < eyeMuzzleLen - 1e-4f)
                    return new HitscanResult(true, false, request.Muzzle, dir, false, default);
            }

            // Muzzle inside static cover => block (overlap at muzzle point approximated by short ray).
            var muzzleProbe = world.Raycast(request.Muzzle, dir, 0.02f, coverFilter);
            if (muzzleProbe.Status == QueryStatus.Hit && muzzleProbe.Hit.DistanceMeters <= 0.001f)
                return new HitscanResult(true, false, request.Muzzle, dir, false, default);

            float maxRange = request.Definition.MaxRangeMeters > 0f
                ? Math.Min(MaxMeters, request.Definition.MaxRangeMeters)
                : MaxMeters;

            // ADR-112: the crosshair is the truth. The eye ray alone decides what the shot hits: the nearest of the
            // static world and every hostile actor capsule along it. The muzzle only gates the gun being pushed into
            // a wall (above) and is the visible origin of the tracer. Before ADR-112 the bullet flew from the muzzle
            // toward whatever the eye ray met behind the target, so a centred head shot against open sky passed
            // about 0.22 m off the 0.12 m head sphere.
            var worldHit = world.Raycast(request.Eye, dir, maxRange, coverFilter);
            float limit = worldHit.Status == QueryStatus.Hit ? worldHit.Hit.DistanceMeters : maxRange;

            QueryHit best = default; bool any = false;
            if (volumes != null)
            {
                for (int i = 0; i < volumes.Count; i++)
                {
                    var vol = volumes[i];
                    if (vol.EntityId.Equals(request.Shooter)) continue;
                    if (IsIgnored(ignore, vol.EntityId)) continue;
                    if (!RayCapsule(request.Eye, dir, limit, vol.Capsule, out float t, out Float3 point, out Float3 normal)) continue;
                    var hit = new QueryHit(t, point, normal, vol.EntityId, default, vol.EntityId.Value * 10 + vol.RegionId, vol.RegionId, request.CollisionRevision);
                    if (!any || QueryOrder.CompareClosest(hit, best) < 0) { best = hit; any = true; }
                }
            }

            QueryHit impact;
            bool impacted;
            Float3 targetPoint;
            if (any)
            {
                impact = best; impacted = true; targetPoint = best.Point;
            }
            else if (worldHit.Status == QueryStatus.Hit)
            {
                impact = worldHit.Hit; impacted = true; targetPoint = worldHit.Hit.Point;
            }
            else
            {
                impact = default; impacted = false;
                targetPoint = new Float3(request.Eye.X + dir.X * maxRange, request.Eye.Y + dir.Y * maxRange, request.Eye.Z + dir.Z * maxRange);
            }

            Float3 toTarget = new Float3(targetPoint.X - request.Muzzle.X, targetPoint.Y - request.Muzzle.Y, targetPoint.Z - request.Muzzle.Z);
            double len2 = toTarget.LengthSquared;
            Float3 tracerDir = dir;
            if (len2 > 1e-10)
            {
                float len = (float)Math.Sqrt(len2);
                tracerDir = new Float3(toTarget.X / len, toTarget.Y / len, toTarget.Z / len);
            }
            return new HitscanResult(false, true, request.Muzzle, tracerDir, impacted, impact);
        }

        /// <summary>ADR-118 pellet direction inside a cone of <paramref name="pelletSpreadRadians"/> around <paramref name="centre"/>.</summary>
        public static Float3 PelletDirection(Float3 centre, float pelletSpreadRadians, ulong seed, uint shotIndex, byte pelletIndex)
        {
            byte channel = (byte)(16 + 2 * pelletIndex);
            ulong pu = SpreadSampling.MixShot(seed, shotIndex, channel);
            ulong pv = SpreadSampling.MixShot(seed, shotIndex, (byte)(channel + 1));
            return SpreadSampling.SampleDirection(centre, pelletSpreadRadians, pu, pv);
        }

        private static bool IsIgnored(IReadOnlyList<EntityId>? ignore, EntityId id)
        {
            if (ignore == null) return false;
            for (int i = 0; i < ignore.Count; i++)
                if (ignore[i].Equals(id)) return true;
            return false;
        }

        /// <summary>Ray vs capsule (finite cylinder plus hemispherical caps). Nearest entry t in [0, max].</summary>
        public static bool RayCapsule(Float3 origin, Float3 dir, float max, Capsule capsule, out float t, out Float3 point, out Float3 normal)
        {
            t = 0; point = default; normal = new Float3(0, 1, 0);
            bool hit = false; float best = float.MaxValue; Float3 bestPoint = default; Float3 bestNormal = new Float3(0, 1, 0);
            Float3 a = capsule.BottomSphereCenter, b = capsule.TopSphereCenter;
            float r = capsule.Radius;
            double ax = b.X - a.X, ay = b.Y - a.Y, az = b.Z - a.Z;
            double length = Math.Sqrt(ax * ax + ay * ay + az * az);
            if (length > 1e-6)
            {
                double ux = ax / length, uy = ay / length, uz = az / length;
                double mx = origin.X - a.X, my = origin.Y - a.Y, mz = origin.Z - a.Z;
                double mu = mx * ux + my * uy + mz * uz;
                double du = dir.X * ux + dir.Y * uy + dir.Z * uz;
                double ox = mx - mu * ux, oy = my - mu * uy, oz = mz - mu * uz;
                double dx = dir.X - du * ux, dy = dir.Y - du * uy, dz = dir.Z - du * uz;
                double qa = dx * dx + dy * dy + dz * dz;
                if (qa > 1e-12)
                {
                    double qb = ox * dx + oy * dy + oz * dz;
                    double qc = ox * ox + oy * oy + oz * oz - (double)r * r;
                    double disc = qb * qb - qa * qc;
                    if (disc >= 0)
                    {
                        double t0 = (-qb - Math.Sqrt(disc)) / qa;
                        if (t0 >= 0 && t0 <= max && t0 < best)
                        {
                            double s = mu + t0 * du;
                            if (s >= 0 && s <= length)
                            {
                                float tf = (float)t0;
                                bestPoint = new Float3(origin.X + dir.X * tf, origin.Y + dir.Y * tf, origin.Z + dir.Z * tf);
                                double nx = bestPoint.X - (a.X + ux * s), ny = bestPoint.Y - (a.Y + uy * s), nz = bestPoint.Z - (a.Z + uz * s);
                                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                                bestNormal = nl < 1e-9 ? new Float3(0, 1, 0) : new Float3((float)(nx / nl), (float)(ny / nl), (float)(nz / nl));
                                best = tf; hit = true;
                            }
                        }
                    }
                }
            }
            TrySphere(origin, dir, max, a, r, ref hit, ref best, ref bestPoint, ref bestNormal);
            TrySphere(origin, dir, max, b, r, ref hit, ref best, ref bestPoint, ref bestNormal);
            if (!hit) return false;
            t = best; point = bestPoint; normal = bestNormal; return true;
        }

        private static void TrySphere(Float3 origin, Float3 dir, float max, Float3 center, float radius, ref bool hit, ref float best, ref Float3 bestPoint, ref Float3 bestNormal)
        {
            Float3 oc = new Float3(origin.X - center.X, origin.Y - center.Y, origin.Z - center.Z);
            float b = oc.X * dir.X + oc.Y * dir.Y + oc.Z * dir.Z;
            float c = oc.X * oc.X + oc.Y * oc.Y + oc.Z * oc.Z - radius * radius;
            float disc = b * b - c;
            if (disc < 0) return;
            float s = (float)Math.Sqrt(disc);
            float t0 = -b - s;
            if (t0 < 0 || t0 > max) return;
            if (t0 >= best) return;
            hit = true; best = t0;
            bestPoint = new Float3(origin.X + dir.X * t0, origin.Y + dir.Y * t0, origin.Z + dir.Z * t0);
            Float3 n = new Float3(bestPoint.X - center.X, bestPoint.Y - center.Y, bestPoint.Z - center.Z);
            float len = (float)Math.Sqrt(n.LengthSquared);
            bestNormal = len < 1e-6f ? new Float3(0, 1, 0) : new Float3(n.X / len, n.Y / len, n.Z / len);
        }
    }
}
