#nullable enable
using System;
using Shooter.Contracts;
namespace Shooter.Generation
{
    public static class ArtifactBounds
    {
        public static void Validate(Aabb bounds, ContentKind kind, ReadOnlySpan<byte> body)
        {
            if (kind == ContentKind.Mesh) foreach (var point in GeometryCodec.DecodeMesh(body).Positions) InBounds(bounds, point);
            if (kind == ContentKind.Collision) ValidateCollisionBounds(bounds, GeometryCodec.DecodeCollision(body));
        }
        private static void InBounds(Aabb bounds, Float3 point)
        {
            const double tolerance = 0.0001;
            if (point.X < bounds.Min.X - tolerance || point.X > bounds.Max.X + tolerance || point.Y < bounds.Min.Y - tolerance || point.Y > bounds.Max.Y + tolerance || point.Z < bounds.Min.Z - tolerance || point.Z > bounds.Max.Z + tolerance)
                throw new FormatException("Manifest bounds do not contain geometry.");
        }
        private static void ValidateCollisionBounds(Aabb bounds, CollisionData collision)
        {
            foreach (var tagged in collision.Capsules)
            {
                var c = tagged.Shape;
                foreach (var p in new[] { c.BottomSphereCenter, c.TopSphereCenter })
                {
                    InBounds(bounds, new Float3(p.X - c.Radius, p.Y - c.Radius, p.Z - c.Radius));
                    InBounds(bounds, new Float3(p.X + c.Radius, p.Y + c.Radius, p.Z + c.Radius));
                }
            }
            foreach (var tagged in collision.Boxes)
            {
                var box = tagged.Shape; var q = box.Rotation;
                for (int i = 0; i < 8; i++)
                {
                    float x = box.Size.X * ((i & 1) == 0 ? -.5f : .5f), y = box.Size.Y * ((i & 2) == 0 ? -.5f : .5f), z = box.Size.Z * ((i & 4) == 0 ? -.5f : .5f);
                    float tx = 2 * (q.Y * z - q.Z * y), ty = 2 * (q.Z * x - q.X * z), tz = 2 * (q.X * y - q.Y * x);
                    InBounds(bounds, new Float3(box.Center.X + x + q.W * tx + q.Y * tz - q.Z * ty, box.Center.Y + y + q.W * ty + q.Z * tx - q.X * tz, box.Center.Z + z + q.W * tz + q.X * ty - q.Y * tx));
                }
            }
            foreach (var tagged in collision.HeightGrids)
            {
                var grid = tagged.Shape;
                for (int z = 0; z < grid.Depth; z++) for (int x = 0; x < grid.Width; x++)
                    InBounds(bounds, new Float3(grid.Origin.X + x * grid.SpacingMeters, grid.Origin.Y + grid.Heights[z * grid.Width + x], grid.Origin.Z + z * grid.SpacingMeters));
            }
        }
    }
}
