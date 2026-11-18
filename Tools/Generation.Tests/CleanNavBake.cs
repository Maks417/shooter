using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Generation.Environment;

/// <summary>
/// Test stand-in for a correct native triangulation: a ground-level grid of shared-vertex cells that keeps only cells
/// clear of every solid box inflated by the agent radius. It passes <see cref="NavCollisionAudit"/> by construction
/// (unlike the coarse software grid, whose 8–12 m cells run under walls).
/// </summary>
internal static class CleanNavBake
{
    public const float Radius = 0.32f, Height = 1.8f, Step = 0.25f;

    public static UnityNavMeshBakeCodec.Bake Build(IReadOnlyList<CollisionBox> boxes, float halfExtent, float cellSize = 1f, float radius = Radius)
    {
        var solid = new List<CollisionBox>();
        foreach (var b in boxes)
            if (NavCollisionAudit.Blocks(b, 0f, Height, Step)) solid.Add(b);
        int cells = (int)(halfExtent * 2f / cellSize);
        int stride = cells + 1;
        var index = new Dictionary<int, uint>();
        var vertices = new List<Float3>();
        var polys = new List<UnityNavMeshBakeCodec.Polygon>();
        uint Vertex(int x, int z)
        {
            int key = z * stride + x;
            if (index.TryGetValue(key, out uint i)) return i;
            i = (uint)vertices.Count;
            vertices.Add(new Float3(-halfExtent + x * cellSize, 0f, -halfExtent + z * cellSize));
            index.Add(key, i);
            return i;
        }
        for (int z = 0; z < cells; z++)
        for (int x = 0; x < cells; x++)
        {
            float x0 = -halfExtent + x * cellSize, x1 = x0 + cellSize;
            float z0 = -halfExtent + z * cellSize, z1 = z0 + cellSize;
            bool blocked = false;
            foreach (var b in solid)
            {
                float hx = b.Size.X * 0.5f + radius, hz = b.Size.Z * 0.5f + radius;
                if (x1 > b.Center.X - hx && x0 < b.Center.X + hx && z1 > b.Center.Z - hz && z0 < b.Center.Z + hz) { blocked = true; break; }
            }
            if (blocked) continue;
            polys.Add(new UnityNavMeshBakeCodec.Polygon(new[] { Vertex(x, z), Vertex(x, z + 1), Vertex(x + 1, z + 1), Vertex(x + 1, z) }, 0, 0));
        }
        return new UnityNavMeshBakeCodec.Bake(vertices, polys, new Float3(-halfExtent, -1f, -halfExtent), new Float3(halfExtent, 4f, halfExtent));
    }

    /// <summary>The 0.3.22 defect: one floor polygon across the whole map, walkable inside every wall.</summary>
    public static UnityNavMeshBakeCodec.Bake LeakyFloor(float halfExtent, float y = 0.02f)
    {
        var vertices = new List<Float3>
        {
            new Float3(-halfExtent, y, -halfExtent), new Float3(-halfExtent, y, halfExtent),
            new Float3(halfExtent, y, halfExtent), new Float3(halfExtent, y, -halfExtent)
        };
        var polys = new List<UnityNavMeshBakeCodec.Polygon> { new UnityNavMeshBakeCodec.Polygon(new uint[] { 0, 1, 2, 3 }, 0, 0) };
        return new UnityNavMeshBakeCodec.Bake(vertices, polys, new Float3(-halfExtent, -1f, -halfExtent), new Float3(halfExtent, 4f, halfExtent));
    }
}
