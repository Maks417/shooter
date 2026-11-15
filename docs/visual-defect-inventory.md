# Product visual defect inventory (T062 / FIX11)

Status: **tooling + inventory only**, 2026-11-14. This page records the honest product visual defect inventory against the integrated T056 client presentation path. It does **not** accept P05, does **not** authorize a fourth clearance/parameter search, and does **not** change generator/recipe sources.

Authoritative stop: [ADR-024](decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations). Historical residuals: [soldier baseline](soldier-baseline.md#third-bounded-clearance-candidate). Repair task: [T062](release-remediation-plan.md). Exit gate: [FIX11](release-remediation-validation.md).

## Immutable gate

| Field | Value |
|---|---|
| Gate | P05 |
| Visual acceptance | **FAIL** |
| ADR | ADR-024 |
| Fourth clearance/parameter search | **Not authorized** |
| Soldier identity under review | `soldier.loft1.0.2` / `soldier.motion.spike.v5` (non-accepted diagnostic) |
| FIX11 software disposition | ToolingReady |
| REL03 | Open until named review attaches captures with defects resolved/unresolved |

Do not delete failed M1 artifacts, do not relabel FAIL as PASS, and do not inherit historical waivers for new gates.

## Named defects

| ID | Category | Status | Summary |
|---|---|---|---|
| P05-D01 | clearance | Unresolved | ~2.18 mm dense possible left forearm/strap overlap near down-aim reload phase 0.45 |
| P05-D02 | alignment | Unresolved | Eye/sight-line residual (~6.49 cm neutral; ~12.70 cm worst reload) |
| P05-D03 | clearance | Unresolved | Historical iteration-2 trigger-arm/torso penetration context |
| PROD-D01 | readability | PendingCapture | Team-identifiable living soldiers in product player (both maps) |
| PROD-D02 | lod | PendingCapture | Required LOD / grip / foot / clearance tolerances in live poses |
| PROD-D03 | ui | PendingReview | 720p HUD/shell readability at 100–150% UI scale |
| VD-D01 | materials | SoftFixedPendingReview | Product map now uploads packaged albedo `_BaseMap` (+ optional normal/mask when present); T075+ packages carry spatial albedo/normal/mask planes; T080 fixes scaled-box texture scale and card submesh retention |
| VD-D02 | atmosphere | SoftFixedPendingReview | Product sky/fog/probe defaults tuned for grounded daylight; owner/player review Pending |
| VD-D03 | fx | SoftFixedPendingReview | T079 denser analytic FX plus hit marker/damage flash; player review Pending |
| VD-D04 | menu | SoftFixedPendingReview | T079 BFV staging depth bands and stronger operation cards; interactive review Pending |
| VD-D05 | environment | SoftFixedPendingReview | T081 packaged wall articulation and painted cargo/door hardware, four/six material submeshes; Compound view observed, Depot close-up and named VD5 review Pending |
| VD-D07 | material scale / cards | SoftFixedPendingReview | T080 metric box UVs, consistent two-metre repeats, actual-bounds overview framing and material roles; source/tests are distinct from named player acceptance |
| VD-D08 | first-person hands | SoftFixedPendingReview | T083 packaged trigger/support gloves and sleeves; rifle observed, live pistol/reload/ADS and articulated fingers/full magazine exchange remain Pending |
| VD-D06 | reference | PendingOwner | Exact Bodycam/Warface comparison frames/timecodes not yet selected (BFV chat screenshots establish direction only) |

VD gaps are tracked for the [VD0–VD9](design-milestones.md) track and the [art brief](visual-art-brief.md). Closing a VD-D* row requires evidence and owner/technical review; software merges alone do not mark them resolved.

## Capture / overlay tooling

Presentation path (T056):

- `Presentation/View/ProductVisualDefectCatalog.cs` — named defects + immutable P05 FAIL guard
- `Presentation/View/ProductVisualReviewSample.cs` — pose overlay sample DTO
- `Presentation/View/ProductVisualInventoryBuilder.cs` — inventory JSON builder
- `Presentation/Rig/ProductActorPresentation.cs` — retains live pose metrics for overlay
- `Presentation/Rig/ProductVisualReviewOverlay.cs` — GL/HUD overlay + JSON sample writer
- `Presentation/Session/ProductSessionView.cs` — enables overlay via command line on the product view

Enable in the product client:

```text
-shooterVisualReview 1
-shooterVisualReviewOutput Artifacts/m8-runtime-composition/<commit>/visual-review-<run>/
```

Inventory package (no Benchmark / Package-Release ownership):

```powershell
./Tools/Inventory-ProductVisualDefects.ps1
./Tools/Inventory-ProductVisualDefects.ps1 -CaptureRoot <overlay-output-dir>
python Tools/test_product_visual_defect_inventory.py
```

Packages land under ignored `Artifacts/m8-runtime-composition/<commit>/visual-defect-inventory-<utc>/` with `defect-inventory.json`, `p05-status.json`, and `README.txt`.

## What this task does not do

- No fourth P05 clearance/generator/recipe search
- No generator, loft, motion, or content-format lock edits for art clearance
- No Benchmark* or Package-Release* ownership changes
- No REL03 / RUN05 PASS claim; unresolved required defects keep release visual acceptance open

[Release remediation plan](release-remediation-plan.md) · [Validation matrix](release-remediation-validation.md) · [M8 handoff](milestones/m8-runtime-composition/handoff.md) · [M11 handoff](milestones/m11-playable-release/handoff.md).
