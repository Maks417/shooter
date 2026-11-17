#nullable enable
using System;
using System.Collections.Generic;

namespace Shooter.Presentation.View
{
    /// <summary>
    /// Named product visual defects for T062/FIX11 inventory. Historical P05 FAIL under ADR-024 is immutable;
    /// this catalog does not authorize clearance/parameter search or relabel FAIL as PASS.
    /// </summary>
    public static class ProductVisualDefectCatalog
    {
        public const string SchemaId = "shooter.product-visual-defect-inventory.v1";
        public const string GateId = "P05";
        public const string AdrId = "ADR-024";
        public const string VisualAcceptance = "FAIL";
        public const bool FourthClearanceSearchAuthorized = false;
        public const string SoldierRecipeIdentity = "soldier.loft1.0.2";
        public const string MotionIdentity = "soldier.motion.v6.shooting";

        public sealed class Defect
        {
            public string Id { get; }
            public string Category { get; }
            public string Title { get; }
            public string Description { get; }
            public string HistoricalEvidence { get; }
            public string Status { get; }
            public string RequiredFor { get; }

            public Defect(
                string id,
                string category,
                string title,
                string description,
                string historicalEvidence,
                string status,
                string requiredFor)
            {
                Id = id ?? throw new ArgumentNullException(nameof(id));
                Category = category ?? throw new ArgumentNullException(nameof(category));
                Title = title ?? throw new ArgumentNullException(nameof(title));
                Description = description ?? throw new ArgumentNullException(nameof(description));
                HistoricalEvidence = historicalEvidence ?? throw new ArgumentNullException(nameof(historicalEvidence));
                Status = status ?? throw new ArgumentNullException(nameof(status));
                RequiredFor = requiredFor ?? throw new ArgumentNullException(nameof(requiredFor));
            }
        }

        private static readonly Defect[] AllDefects =
        {
            new Defect(
                "P05-D01",
                "clearance",
                "Left forearm / chest-strap overlap",
                "Conservative clearance probe fails near down-aim reload phase 0.45 with approximately 2.18 mm dense possible left forearm/strap overlap on loft1.0.2/motion.spike.v3.",
                "docs/soldier-baseline.md third bounded clearance candidate; M1 captures under Artifacts/m1-procedural-feasibility (immutable). ADR-095 v6 shooting posture: unchanged conservative probe minimum +3.0 mm over the full gate envelope (outward support-hand reload bow). Visual review pending.",
                "NumericallyResolvedPendingReview",
                "RUN05, REL03, FIX11"),
            new Defect(
                "P05-D02",
                "alignment",
                "Eye / sight-line unresolved",
                "Neutral eye/sight-line separation is about 6.49 cm; worst reported reload residual about 12.70 cm. Grip/contact passes do not override this failure.",
                "docs/soldier-baseline.md; ADR-023 candidate review retained as FAIL. ADR-095 v6 shooting posture: eye/sight-line residual 0.000 mm over the full gate envelope (eye placed on the sight line by construction). Visual review pending.",
                "NumericallyResolvedPendingReview",
                "RUN05, REL03, FIX11"),
            new Defect(
                "P05-D03",
                "clearance",
                "Iteration-2 trigger-arm / torso penetration (historical)",
                "Neutral right shoulder/elbow/hand samples showed interior forearm points inside the loft ellipse and sleeve/plate intersection despite exact grip targets. Retained context for product pose triage; not accepted.",
                "ADR-023 retained iteration-2 defect; M1 continuation artifacts. ADR-095 v6 shooting posture: trigger-arm minimum clearance +3.0 mm over the full gate envelope. Visual review pending.",
                "NumericallyResolvedPendingReview",
                "RUN05, REL03, FIX11"),
            new Defect(
                "PROD-D01",
                "readability",
                "Team-identifiable living soldiers in product player",
                "Product captures must show distinct team materials on living actors for both maps. Wiring alone is not acceptance.",
                "FIX05/T056 pending product-player captures; triage owned with T062 overlays.",
                "PendingCapture",
                "FIX05, FIX11, REL03"),
            new Defect(
                "PROD-D02",
                "lod",
                "Required LOD / grip / foot / clearance tolerances in live poses",
                "Compare product poses against required LODs and grip/foot/clearance tolerances using overlay metrics; do not edit generator sources to chase clearance.",
                "FIX11 requires overlays on the integrated client; P05 FAIL remains.",
                "PendingCapture",
                "FIX11, REL03"),
            new Defect(
                "PROD-D03",
                "ui",
                "720p HUD / shell readability at 100–150% UI scale",
                "Named review of menu/HUD readability at native 720p. Separate from soldier clearance search.",
                "REL03 usability; shell owned by T059 with visual sign-off under T062/T065.",
                "PendingReview",
                "REL03, FIX11")
        };

        public static IReadOnlyList<Defect> Defects => AllDefects;

        public static void ValidateImmutableP05Fail()
        {
            if (!string.Equals(VisualAcceptance, "FAIL", StringComparison.Ordinal))
                throw new InvalidOperationException("P05 visual acceptance must remain FAIL under ADR-024.");
            if (FourthClearanceSearchAuthorized)
                throw new InvalidOperationException("Fourth P05 clearance/parameter search is not authorized.");
            for (int i = 0; i < AllDefects.Length; i++)
            {
                var defect = AllDefects[i];
                if (!defect.Id.StartsWith("P05-", StringComparison.Ordinal)) continue;
                if (string.Equals(defect.Status, "Resolved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(defect.Status, "PASS", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "P05 defect " + defect.Id + " cannot be marked Resolved/PASS without owner-authorized clearance work.");
                }
            }
        }
    }
}
