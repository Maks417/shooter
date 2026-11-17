#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Icons
{
    /// <summary>One packed icon: pixel rectangle in the atlas (row 0 = top) and its span in cells.</summary>
    public readonly struct IconAtlasEntry
    {
        public IconAtlasEntry(string name, int x, int y, int width, int height)
        {
            Name = name; X = x; Y = y; Width = width; Height = height;
        }

        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public float Aspect => Width / (float)Height;
    }

    /// <summary>Weapon silhouette input: packaged LOD0 plus detachable parts, keyed by visual mesh key.</summary>
    public sealed class WeaponSilhouetteSource
    {
        public WeaponSilhouetteSource(string meshKey, int spanCells, IReadOnlyList<MeshData> meshes)
        {
            if (string.IsNullOrEmpty(meshKey)) throw new ArgumentException("Mesh key required.", nameof(meshKey));
            if (spanCells < 1 || spanCells > 8) throw new ArgumentOutOfRangeException(nameof(spanCells));
            MeshKey = meshKey;
            SpanCells = spanCells;
            Meshes = meshes ?? throw new ArgumentNullException(nameof(meshes));
        }

        public string MeshKey { get; }
        public int SpanCells { get; }
        public IReadOnlyList<MeshData> Meshes { get; }

        /// <summary>Atlas name for a weapon mesh key.</summary>
        public static string IconName(string meshKey) => "weapon." + meshKey;

        /// <summary>Cell span by mesh key: long guns and knife 3, pistol 2, grenade 1.</summary>
        public static int SpanFor(string meshKey) => meshKey switch
        {
            "grenade" => 1,
            "pistol" or "g17" or "golden-eagle" => 2,
            _ => 3
        };
    }

    /// <summary>
    /// ADR-085 single-channel signed-distance atlas. 64 px cells, 8 px spread, 1024 px wide; value 128 is the edge,
    /// higher is inside. Built in memory from code recipes and packaged meshes; never shipped as an image file.
    /// </summary>
    public sealed class IconAtlas
    {
        public const int CellPixels = 64;
        public const int SpreadPixels = 8;
        public const int AtlasWidth = 1024;
        private const int Columns = AtlasWidth / CellPixels;

        private readonly Dictionary<string, IconAtlasEntry> entries;

        private IconAtlas(int height, byte[] pixels, Dictionary<string, IconAtlasEntry> entries, IReadOnlyList<string> order)
        {
            Height = height;
            Pixels = pixels;
            this.entries = entries;
            Names = order;
        }

        public int Width => AtlasWidth;
        public int Height { get; }
        /// <summary>R8 pixels, row 0 at the top.</summary>
        public byte[] Pixels { get; }
        public IReadOnlyList<string> Names { get; }

        public bool TryGet(string name, out IconAtlasEntry entry) => entries.TryGetValue(name, out entry);

        public static byte Encode(double distancePixels)
        {
            double v = 0.5 - distancePixels / (2.0 * SpreadPixels);
            if (v <= 0) return 0;
            if (v >= 1) return 255;
            return (byte)Math.Floor(v * 255.0 + 0.5);
        }

        /// <summary>Build from recipes (library order) then weapon silhouettes (given order).</summary>
        public static IconAtlas Build(IReadOnlyList<IconRecipe> recipes, IReadOnlyList<WeaponSilhouetteSource>? weapons = null)
        {
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            var spans = new List<(string Name, int Span)>();
            foreach (var r in recipes) spans.Add((r.Name, r.SpanCells));
            if (weapons != null)
                foreach (var w in weapons) spans.Add((WeaponSilhouetteSource.IconName(w.MeshKey), w.SpanCells));

            // Row-major packing; an entry never wraps across rows.
            var placed = new List<(int Col, int Row)>();
            int col = 0, row = 0;
            foreach (var s in spans)
            {
                if (s.Span > Columns) throw new InvalidOperationException("Icon wider than the atlas: " + s.Name);
                if (col + s.Span > Columns) { col = 0; row++; }
                placed.Add((col, row));
                col += s.Span;
            }
            int rows = row + 1;
            int height = rows * CellPixels;
            var pixels = new byte[AtlasWidth * height];
            var map = new Dictionary<string, IconAtlasEntry>(StringComparer.Ordinal);
            var order = new List<string>();

            for (int i = 0; i < spans.Count; i++)
            {
                var (name, span) = spans[i];
                if (map.ContainsKey(name)) throw new InvalidOperationException("Duplicate icon name: " + name);
                int x0 = placed[i].Col * CellPixels, y0 = placed[i].Row * CellPixels, w = span * CellPixels;
                var entry = new IconAtlasEntry(name, x0, y0, w, CellPixels);
                map.Add(name, entry);
                order.Add(name);
                if (i < recipes.Count)
                {
                    var recipe = recipes[i];
                    double unitsPerPixel = 1000.0 / CellPixels;
                    for (int y = 0; y < CellPixels; y++)
                        for (int x = 0; x < w; x++)
                        {
                            double d = recipe.Distance((x + 0.5) * unitsPerPixel, (y + 0.5) * unitsPerPixel) / unitsPerPixel;
                            pixels[(y0 + y) * AtlasWidth + x0 + x] = Encode(d);
                        }
                }
                else
                {
                    var source = weapons![i - recipes.Count];
                    double[] d = MeshSilhouette.SignedDistance(source.Meshes, w, CellPixels, SpreadPixels / 2 + 2);
                    for (int y = 0; y < CellPixels; y++)
                        for (int x = 0; x < w; x++)
                            pixels[(y0 + y) * AtlasWidth + x0 + x] = Encode(d[y * w + x]);
                }
            }
            return new IconAtlas(height, pixels, map, order);
        }
    }
}
