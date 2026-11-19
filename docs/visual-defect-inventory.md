# Product visual defect inventory (T062 / FIX11)

Status 2026-11-17: [ADR-095](decisions.md#adr-095--accepted-structural-weapon-from-eye-shooting-posture-p05-reopening) resolves the **numeric** P05 gate (D01–D03) with the structural shooting posture, now the product default; **visual acceptance stays FAIL until a named visual review**. This page records the product visual defect inventory against the client presentation path; it does not accept P05.

Authoritative stop: [ADR-024](decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations). Historical residuals: [soldier baseline](soldier-baseline.md#third-bounded-clearance-candidate). Repair task: T062. Exit gate: [FIX11](acceptance.md).

## Immutable gate

| Field | Value |
|---|---|
| Gate | P05 |
| Visual acceptance | **FAIL** |
| ADR | ADR-024 stop; ADR-094 reopening (failed); ADR-095 structural posture (numeric PASS) |
| Further clearance/parameter search | Not needed for the numeric gate after ADR-095; any further change needs a new owner decision |
| Soldier identity under review | `soldier.loft` 1.0.3 kit with `soldier.motion.v7.locomotion` (product default); historical 1.0.2 / baseline motion non-accepted |
| FIX11 software disposition | ToolingReady |
| REL03 | Open until named review attaches captures with defects resolved/unresolved |

Do not delete failed M1 artifacts, do not relabel FAIL as PASS, and do not inherit historical waivers for new gates.

## Named defects

| ID | Category | Status | Summary |
|---|---|---|---|
| P05-D01 | clearance | NumericallyResolvedPendingReview | Historical ~2.18 mm left forearm/strap overlap at down-aim reload 0.45; ADR-095 v6 minimum +3.0 mm |
| P05-D02 | alignment | NumericallyResolvedPendingReview | Historical eye/sight-line residual (~6.49 cm neutral; ~12.70 cm reload); ADR-095 v6 residual 0 mm |
| P05-D03 | clearance | NumericallyResolvedPendingReview | Historical trigger-arm/torso penetration; ADR-095 v6 right-arm minimum +3.0 mm |
| PROD-D01 | readability | PendingCapture | Team-identifiable living soldiers in product player (both maps) |
| PROD-D02 | lod | PendingCapture | Required LOD / grip / foot / clearance tolerances in live poses |
| PROD-D03 | ui | PendingReview | 720p HUD/shell readability at 100–150% UI scale |
| VD-D01–D20 | presentation | SoftFixedPendingReview | Software fixes from T079–T083 and ADR-084–094 / ADR-103 (materials, atmosphere, FX, menu, environment, hands, SDF UI and icons, motion, HUD, noise materials, surface detail, decals, particles, soldier kit, loading, flicker/scope/damage). Each needs player review at 720p/150% and on the reference machine; see the ADR for scope. |
| VD-D06 | reference | PendingOwner | Exact Bodycam/Warface comparison frames not yet selected |

A VD-D row closes only with evidence and owner/technical review ([visual direction](visual-direction.md)); a software merge alone does not resolve it.

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

## ADR-095 structural shooting posture

[ADR-095](decisions.md#adr-095--accepted-structural-weapon-from-eye-shooting-posture-p05-reopening) replaced head-only correction (the ADR-094 three-candidate search failed) with a structural posture: right clavicle shooting posture with a reload drop, the right eye placed on the sight line by construction with the neck solved to it, clearance-aware elbows solved after the head, and an outward support-hand reload bow. `soldier.motion.v7.locomotion` measures minimum arm clearance +3.0 mm, eye/sight residual 0 mm, grip below 1e-7 m and stock error 1.2e-7 m on the unchanged gate. It is the product default. Pistol sockets still show a −4.3 mm left-arm residual at reload phase 0.91 (outside the rifle gate). Evidence: `Artifacts/visual-design/adr-095/`.

No further clearance or generator search is authorized without a new owner decision. Do not delete failed artifacts or relabel FAIL as PASS.
