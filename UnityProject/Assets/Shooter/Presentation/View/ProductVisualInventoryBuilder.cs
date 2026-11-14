#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Builds the durable T062 product visual defect inventory JSON. Never claims P05 PASS or authorizes
    /// a fourth clearance search.
    /// </summary>
    public static class ProductVisualInventoryBuilder
    {
        public static string Build(
            string sourceCommit,
            bool sourceDirty,
            string inventoryRoot,
            IReadOnlyList<string>? historicalArtifactPointers = null,
            IReadOnlyList<string>? captureSamplePaths = null)
        {
            ProductVisualDefectCatalog.ValidateImmutableP05Fail();
            if (string.IsNullOrWhiteSpace(sourceCommit)) throw new ArgumentException("sourceCommit required.", nameof(sourceCommit));
            inventoryRoot ??= "";

            var sb = new StringBuilder(4096);
            bool first = true;
            sb.Append('{');
            Field(sb, ref first, "schemaId", Quote(ProductVisualDefectCatalog.SchemaId));
            Field(sb, ref first, "taskId", Quote("T062"));
            Field(sb, ref first, "fixId", Quote("FIX11"));
            Field(sb, ref first, "gateId", Quote(ProductVisualDefectCatalog.GateId));
            Field(sb, ref first, "adrId", Quote(ProductVisualDefectCatalog.AdrId));
            Field(sb, ref first, "visualAcceptance", Quote(ProductVisualDefectCatalog.VisualAcceptance));
            Field(sb, ref first, "fourthClearanceSearchAuthorized", "false");
            Field(sb, ref first, "soldierRecipeIdentity", Quote(ProductVisualDefectCatalog.SoldierRecipeIdentity));
            Field(sb, ref first, "motionIdentity", Quote(ProductVisualDefectCatalog.MotionIdentity));
            Field(sb, ref first, "sourceCommit", Quote(sourceCommit));
            Field(sb, ref first, "sourceDirty", sourceDirty ? "true" : "false");
            Field(sb, ref first, "inventoryRoot", Quote(inventoryRoot));
            Field(sb, ref first, "disposition", Quote("InventoryOnly"));
            Field(sb, ref first, "fix11Status", Quote("ToolingReady"));
            Field(sb, ref first, "rel03Status", Quote("Open"));
            Field(sb, ref first, "note", Quote(
                "Initial capture/overlay tooling and honest defect inventory only. P05 remains FAIL under ADR-024. No fourth clearance/parameter search. Generator/recipe sources unchanged."));

            if (!first) sb.Append(',');
            sb.Append("\"defects\":[");
            var defects = ProductVisualDefectCatalog.Defects;
            for (int i = 0; i < defects.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var d = defects[i];
                bool df = true;
                sb.Append('{');
                Field(sb, ref df, "id", Quote(d.Id));
                Field(sb, ref df, "category", Quote(d.Category));
                Field(sb, ref df, "title", Quote(d.Title));
                Field(sb, ref df, "description", Quote(d.Description));
                Field(sb, ref df, "historicalEvidence", Quote(d.HistoricalEvidence));
                Field(sb, ref df, "status", Quote(d.Status));
                Field(sb, ref df, "requiredFor", Quote(d.RequiredFor));
                sb.Append('}');
            }
            sb.Append(']');
            first = false;

            sb.Append(",\"historicalArtifactPointers\":[");
            WriteStringArray(sb, historicalArtifactPointers);
            sb.Append(']');

            sb.Append(",\"captureSamplePaths\":[");
            WriteStringArray(sb, captureSamplePaths);
            sb.Append(']');

            sb.Append(",\"overlay\":{");
            bool of = true;
            Field(sb, ref of, "runtimeType", Quote("Shooter.Presentation.Rig.ProductVisualReviewOverlay"));
            Field(sb, ref of, "enableFlag", Quote("-shooterVisualReview"));
            Field(sb, ref of, "outputFlag", Quote("-shooterVisualReviewOutput"));
            Field(sb, ref of, "presentationPath", Quote("ProductSessionView / ProductActorPresentation"));
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        private static void Field(StringBuilder sb, ref bool first, string key, string value)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(key).Append('"').Append(':').Append(value);
        }

        private static void WriteStringArray(StringBuilder sb, IReadOnlyList<string>? values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Quote(values[i] ?? ""));
            }
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
