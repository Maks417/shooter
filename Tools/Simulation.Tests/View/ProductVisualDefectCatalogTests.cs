#nullable enable
using System.Text.Json;
using Shooter.Contracts;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.View
{
    public sealed class ProductVisualDefectCatalogTests
    {
        [Fact]
        public void Catalog_KeepsP05FailAndBlocksFourthSearch()
        {
            Assert.Equal("FAIL", ProductVisualDefectCatalog.VisualAcceptance);
            Assert.False(ProductVisualDefectCatalog.FourthClearanceSearchAuthorized);
            ProductVisualDefectCatalog.ValidateImmutableP05Fail();
            Assert.Contains(ProductVisualDefectCatalog.Defects, d => d.Id == "P05-D01");
            Assert.Contains(ProductVisualDefectCatalog.Defects, d => d.Id == "P05-D02");
        }

        [Fact]
        public void InventoryBuilder_EmitsValidJsonWithFailRetained()
        {
            string json = ProductVisualInventoryBuilder.Build(
                "abc123",
                true,
                "Artifacts/m8-runtime-composition/abc123/visual-defect-inventory",
                new[] { "docs/soldier-baseline.md" },
                new[] { "sample-tick-30.json" });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("FAIL", root.GetProperty("visualAcceptance").GetString());
            Assert.False(root.GetProperty("fourthClearanceSearchAuthorized").GetBoolean());
            Assert.Equal("InventoryOnly", root.GetProperty("disposition").GetString());
            Assert.Equal("ToolingReady", root.GetProperty("fix11Status").GetString());
            Assert.Equal("Open", root.GetProperty("rel03Status").GetString());
            Assert.True(root.GetProperty("defects").GetArrayLength() >= 3);
        }

        [Fact]
        public void ReviewSample_FlagsHistoricalProxyDefects()
        {
            var penetrating = new ProductVisualReviewSample(
                1, 1, true, false,
                armBodyClearanceMeters: -0.00218f,
                eyeSightLineErrorMeters: 0.0649f,
                leftGripErrorMeters: 0.001f,
                rightGripErrorMeters: 0.001f,
                plantSlideMeters: 0.01f,
                stockErrorMeters: 0.01f,
                reloadPhase: 0.45f,
                aimPitchRadians: -0.4f,
                feet: new Float3(0, 0, 0),
                rightEye: new Float3(0, 1.6f, 0),
                weaponPosition: new Float3(0.2f, 1.4f, 0.3f),
                sightWorld: new Float3(0.2f, 1.45f, 0.55f),
                completedTick: 30,
                mapId: "training-compound",
                profile: "Low");
            Assert.True(penetrating.ReproducesForearmStrapProxy);
            Assert.True(penetrating.ReproducesEyeSightResidual);
            string json = penetrating.ToJsonObject();
            Assert.Contains("\"p05VisualAcceptance\":\"FAIL\"", json);
            Assert.Contains("\"fourthClearanceSearchAuthorized\":false", json);
        }
    }
}
