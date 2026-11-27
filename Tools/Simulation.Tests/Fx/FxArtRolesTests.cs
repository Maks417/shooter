#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Shooter.Contracts;
using Shooter.Generation.Environment;
using Shooter.Presentation.Fx;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    /// <summary>ADR-126 licensed effect flipbooks and burning wrecks (pure side).</summary>
    public sealed class FxArtRolesTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "toolchain-lock.json"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }

        private static string Art(params string[] parts) =>
            Path.Combine(new[] { RepoRoot(), "UnityProject", "Assets", "Shooter", "Art" }.Concat(parts).ToArray());

        private static FxArtSheet Sheet(FxArtRole role, int columns = 8, int rows = 8, float fps = 20f) =>
            new FxArtSheet(role, "FxArt/" + role, columns, rows, FxArtBlend.Alpha, fps, 1f, 0f, 0f);

        [Theory]
        [InlineData(FxKind.FirePuff, FxArtRole.FireBall)]
        [InlineData(FxKind.MuzzleFire, FxArtRole.FireBall)]
        [InlineData(FxKind.SmokeProxy, FxArtRole.Smoke)]
        [InlineData(FxKind.Dust, FxArtRole.Smoke)]
        [InlineData(FxKind.DustRing, FxArtRole.Smoke)]
        public void Fire_smoke_and_dust_draw_licensed_sheets(FxKind kind, FxArtRole role)
        {
            Assert.Equal(role, FxArtRoles.For(kind, 0));
            Assert.Equal(role, FxArtRoles.For(kind, 7));
        }

        [Theory]
        [InlineData(FxKind.MuzzleFlash)]
        [InlineData(FxKind.Tracer)]
        [InlineData(FxKind.ShellCasing)]
        [InlineData(FxKind.ImpactSpark)]
        [InlineData(FxKind.Debris)]
        [InlineData(FxKind.Sparks)]
        [InlineData(FxKind.Embers)]
        [InlineData(FxKind.Shockwave)]
        [InlineData(FxKind.BlastFlash)]
        public void Crisp_kinds_keep_their_generated_sheets(FxKind kind) => Assert.Null(FxArtRoles.For(kind, 3));

        [Fact]
        public void Two_fireballs_of_one_blast_use_both_explosion_sheets()
        {
            Assert.Equal(FxArtRole.BlastFireball, FxArtRoles.For(FxKind.Fireball, 4));
            Assert.Equal(FxArtRole.BlastFireballAlt, FxArtRoles.For(FxKind.Fireball, 5));
            Assert.False(FxArtRoles.Loops(FxArtRole.BlastFireball));
            Assert.True(FxArtRoles.Loops(FxArtRole.Smoke));
        }

        [Fact]
        public void Catalog_must_name_every_role_once()
        {
            var all = Enumerable.Range(0, (int)FxArtRole.Count).Select(r => Sheet((FxArtRole)r)).ToArray();
            Assert.Equal((int)FxArtRole.Count, FxArtRoles.RequireAll(all.Reverse()).Length);
            Assert.Throws<InvalidOperationException>(() => FxArtRoles.RequireAll(all.Skip(1)));
            Assert.Throws<InvalidOperationException>(() => FxArtRoles.RequireAll(all.Append(all[0])));
            Assert.Throws<InvalidOperationException>(() => FxArtRoles.ParseRole("smoke"));
            Assert.Throws<InvalidOperationException>(() => FxArtRoles.ParseRole("Count"));
            Assert.Throws<InvalidOperationException>(() => FxArtRoles.ParseBlend("Multiply"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Sheet(FxArtRole.Smoke, columns: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Sheet(FxArtRole.Smoke, rows: 17));
            Assert.Throws<ArgumentException>(() => new FxArtSheet(FxArtRole.Smoke, " ", 8, 8, FxArtBlend.Alpha, 20f, 1f, 0f, 0f));
        }

        [Fact]
        public void Playback_never_wraps_the_curve_and_explosions_play_once()
        {
            Assert.Equal((1, 1f), FxArtRoles.Playback(Sheet(FxArtRole.BlastFireball, 5, 5), AnalyticFxPool.FireballLifetime));
            var (cycles, end) = FxArtRoles.Playback(Sheet(FxArtRole.Smoke, 8, 8, 20f), AnalyticFxPool.SmokeProxyLifetime);
            Assert.Equal(1, cycles);
            Assert.InRange(end, 0.45f, 0.46f);
            (cycles, end) = FxArtRoles.Playback(Sheet(FxArtRole.SmokePlume, 16, 4, 24f), 7f);
            Assert.Equal(3, cycles);
            Assert.InRange(end, 0.874f, 0.876f);
            Assert.True(end * cycles * 64 / 7f > 23.9f, "24 frames per second");
        }

        [Fact]
        public void Shipped_catalog_names_pinned_cc0_power_of_two_sheets()
        {
            using var catalog = JsonDocument.Parse(File.ReadAllText(Art("Fx", "Resources", "FxArt", "fx-art.json")));
            Assert.Equal(FxArtRoles.SchemaVersion, catalog.RootElement.GetProperty("schemaVersion").GetInt32());
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Tools", "art-sources.json")));
            var licenses = manifest.RootElement.GetProperty("packs").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString()!, p => p.GetProperty("license").GetString()!);
            var pinned = manifest.RootElement.GetProperty("files").EnumerateArray().ToDictionary(f => f.GetProperty("path").GetString()!, f => f.GetProperty("pack").GetString()!);
            var sheets = new List<FxArtSheet>();
            foreach (var s in catalog.RootElement.GetProperty("sheets").EnumerateArray())
            {
                var sheet = new FxArtSheet(FxArtRoles.ParseRole(s.GetProperty("role").GetString()!), s.GetProperty("texture").GetString()!,
                    s.GetProperty("columns").GetInt32(), s.GetProperty("rows").GetInt32(), FxArtRoles.ParseBlend(s.GetProperty("blend").GetString()!),
                    s.GetProperty("framesPerSecond").GetSingle(), s.GetProperty("brightness").GetSingle(), s.GetProperty("lit").GetSingle(), s.GetProperty("emission").GetSingle());
                sheets.Add(sheet);
                var matches = Directory.GetFiles(Art("Fx"), Path.GetFileName(sheet.Resource) + ".png", SearchOption.AllDirectories)
                    .Where(f => f.Replace('\\', '/').EndsWith("/Resources/" + sheet.Resource + ".png", StringComparison.Ordinal)).ToArray();
                var file = Assert.Single(matches);
                string relative = Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');
                Assert.True(pinned.TryGetValue(relative, out var pack), relative + " is not pinned in Tools/art-sources.json");
                Assert.Equal("CC0-1.0", licenses[pack!]);
                Assert.EndsWith("_" + sheet.Columns + "x" + sheet.Rows, sheet.Resource);
                var header = File.ReadAllBytes(file).AsSpan(16, 8).ToArray();
                int width = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3], height = (header[4] << 24) | (header[5] << 16) | (header[6] << 8) | header[7];
                // The texture-sheet grid is cut in UV space (5x5 on 1024 is fine); sheets stay power of two and cells legible.
                Assert.Equal(0, width & (width - 1));
                Assert.Equal(0, height & (height - 1));
                Assert.True(width / sheet.Columns >= 64 && height / sheet.Rows >= 64, sheet.Resource + " cells too small");
            }
            Assert.Equal((int)FxArtRole.Count, FxArtRoles.RequireAll(sheets).Length);
        }

        [Fact]
        public void Muzzle_fire_ball_is_a_flash_and_reduced_flash_drops_it()
        {
            Assert.True(AnalyticFxPool.IsFlashing(FxKind.MuzzleFire));
            Assert.True(AnalyticFxPool.LifetimeFor(FxKind.MuzzleFire) < AnalyticFxPool.MuzzleFlashLifetime);
            var pool = new AnalyticFxPool();
            pool.SpawnGunshotFeedback(new Float3(0, 1, 0), new Float3(0, 0, 1), suppressMuzzleAndTracer: true);
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++) Assert.False(pool.Get(i).Active && pool.Get(i).Kind == FxKind.MuzzleFire);
        }

        private static AmbientFireSite[] Sites(string mapId) => ProductMapPlanner.Generate(mapId).Features
            .Where(f => f.Kind == ArenaFeatureKind.Truck || f.Kind == ArenaFeatureKind.Tanker || f.Kind == ArenaFeatureKind.Trailer || f.Kind == ArenaFeatureKind.Tank)
            .Select(f => new AmbientFireSite(f.Center, f.Size, f.Facing)).ToArray();

        [Fact]
        public void Burning_wrecks_are_stable_spaced_and_low_is_a_prefix_of_high()
        {
            int burning = 0;
            foreach (var map in MapRegistry.All)
            {
                var sites = Sites(map.Id);
                var high = AmbientFireAnchors.Pick(map.Id, sites, 2);
                var again = AmbientFireAnchors.Pick(map.Id, sites, 2);
                var low = AmbientFireAnchors.Pick(map.Id, sites, 0);
                Assert.Equal(high.Select(a => a.Site), again.Select(a => a.Site));
                Assert.True(high.Length <= AmbientFireAnchors.MaxFires);
                Assert.True(low.Length <= AmbientFireAnchors.MaxFiresLow);
                Assert.Equal(high.Take(low.Length).Select(a => a.Site), low.Select(a => a.Site));
                for (int a = 0; a < high.Length; a++)
                {
                    var site = sites[high[a].Site];
                    Assert.Equal(site.Center.Y + site.Size.Y * 0.5f, high[a].Base.Y, 3);
                    for (int b = a + 1; b < high.Length; b++)
                    {
                        float dx = high[a].Base.X - high[b].Base.X, dz = high[a].Base.Z - high[b].Base.Z;
                        Assert.True(dx * dx + dz * dz >= AmbientFireAnchors.MinSpacingMeters * AmbientFireAnchors.MinSpacingMeters, map.Id);
                    }
                }
                burning += high.Length;
            }
            Assert.True(burning > 0, "no registered map has a vehicle to burn");
        }

        [Fact]
        public void Anchor_sits_on_the_roof_toward_the_front()
        {
            var site = new AmbientFireSite(new Float3(10f, 1.5f, -4f), new Float3(8f, 3f, 2.5f), -1f);
            var anchor = AmbientFireAnchors.AnchorFor(site, 3);
            Assert.Equal(3f, anchor.Base.Y, 3);
            Assert.Equal(10f - 8f * 0.3f, anchor.Base.X, 3);
            Assert.Equal(-4f, anchor.Base.Z, 3);
            Assert.InRange(anchor.Scale, 1.2f, 2.6f);
            Assert.Empty(AmbientFireAnchors.Pick("any", Array.Empty<AmbientFireSite>(), 2));
        }
    }
}
