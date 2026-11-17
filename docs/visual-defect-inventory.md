# Product visual defect inventory (T062 / FIX11)

Status 2026-11-17: [ADR-095](decisions.md#adr-095--accepted-structural-weapon-from-eye-shooting-posture-p05-reopening) resolves the **numeric** P05 gate (D01–D03) with the structural shooting posture, now the product default; **visual acceptance stays FAIL until a named visual review** (see [ADR-095 section](#adr-095-structural-shooting-posture)). This page records the product visual defect inventory against the client presentation path; it does not accept P05.

Authoritative stop: [ADR-024](decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations). Historical residuals: [soldier baseline](soldier-baseline.md#third-bounded-clearance-candidate). Repair task: [T062](archive/release-remediation-plan.md). Exit gate: [FIX11](release-remediation-validation.md).

## Immutable gate

| Field | Value |
|---|---|
| Gate | P05 |
| Visual acceptance | **FAIL** |
| ADR | ADR-024 stop; ADR-094 reopening (failed); ADR-095 structural posture (numeric PASS) |
| Further clearance/parameter search | Not needed for the numeric gate after ADR-095; any further change needs a new owner decision |
| Soldier identity under review | `soldier.loft` 1.0.3 kit with `soldier.motion.v6.shooting` (product default); historical 1.0.2 / baseline motion non-accepted |
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
| VD-D01 | materials | SoftFixedPendingReview | Product map now uploads packaged albedo `_BaseMap` (+ optional normal/mask when present); T075+ packages carry spatial albedo/normal/mask planes; T080 fixes scaled-box texture scale and card submesh retention |
| VD-D02 | atmosphere | SoftFixedPendingReview | Product sky/fog/probe defaults tuned for grounded daylight; owner/player review Pending |
| VD-D03 | fx | SoftFixedPendingReview | T079 denser analytic FX plus hit marker/damage flash; player review Pending |
| VD-D04 | menu | SoftFixedPendingReview | T079 BFV staging depth bands and stronger operation cards; interactive review Pending |
| VD-D05 | environment | SoftFixedPendingReview | T081 packaged wall articulation and painted cargo/door hardware, four/six material submeshes; Compound view observed, Depot close-up and named VD5 review Pending |
| VD-D07 | material scale / cards | SoftFixedPendingReview | T080 metric box UVs, consistent two-metre repeats, actual-bounds overview framing and material roles; source/tests are distinct from named player acceptance |
| VD-D08 | first-person hands | SoftFixedPendingReview | T083 packaged trigger/support gloves and sleeves; rifle observed, live pistol/reload/ADS and articulated fingers/full magazine exchange remain Pending |
| VD-D09 | ui craft | SoftFixedPendingReview | ADR-084 SDF UI layer: anti-aliased rounded plates/buttons/cards, gradients, shadows, focus glow, eased emphasis, pill sliders/switch toggles, procedural kill medal; menu/setup/settings observed in player, combat HUD/ribbon/loading and 720p/150% review Pending |
| VD-D10 | icons | SoftFixedPendingReview | ADR-085 generated SDF icon atlas: mesh-traced weapon silhouettes, faction emblems, category glyphs, crown; scoreboard/settings/ammo plate observed in player; kill-feed during kills, results and 150% review Pending |
| VD-D11 | motion | SoftFixedPendingReview | ADR-086 animated debrief (banner sweep, score count-up, award medals, staggered cards), screen enter veil/rise, menu camera drift/push-in/parallax; preview and menu observed in player, real end-of-match debrief and reduced-motion pass Pending |
| VD-D12 | combat HUD craft | SoftFixedPendingReview | ADR-087 first tranche: SDF damage/reload arcs, bounded round glyphs, rotated minimap shapes, entering team-plated elimination rows and headshot pop; gameplay/150%/reference review Pending |
| VD-D13 | noise materials | SoftFixedPendingReview | ADR-088 new versioned periodic roles and per-map material selection; paired byte proofs, render/reference review remains required |
| VD-D14 | surface detail | SoftFixedPendingReview | ADR-089 AO grime, height dirt, triplanar detail and macro shading; Low diagnostic/reference review required |
| VD-D15 | decals | SoftFixedPendingReview | ADR-090 packaged numeric atlas/layout paint and 64 impact marks; projected higher tiers / Low quads; runtime review Pending |
| VD-D16 | atmosphere | SoftFixedPendingReview | ADR-091 clouded sky, sun presets, reflected environment, height fog, map LUT, actor AO/contact shadows; transition review Pending |
| VD-D17 | particle FX | SoftFixedPendingReview | ADR-092/093 five numeric flipbooks, bounded particle adaptation, weapon/impact/explosion/movement cues; live/reduced-flash/reference review Pending |
| VD-D18 | soldier kit | SoftFixedPendingReview | ADR-094 1.0.3 contoured carrier/pouches, camo, bands/patches, collision support and per-profile grips; P05 correction failed and stays disabled by default |
| VD-D19 | insertion/menu craft | SoftFixedPendingReview | ADR-087 blueprint, tips, wordmark/cards/keycaps, sliced loading with stale-epoch fencing; 720p/150% and indivisible upload review Pending |
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

[Release remediation plan](archive/release-remediation-plan.md) · [Validation matrix](release-remediation-validation.md) · [M8 handoff](milestones/m8-runtime-composition/handoff.md) · [M11 handoff](milestones/m11-playable-release/handoff.md).


## ADR-094 bounded correction disposition

The full roadmap owner assignment reopened the ADR-024 stop only for three measured candidates / 90 minutes. All three failed the unchanged conservative arm-clearance gate; the search stopped at the candidate limit. Candidate 3 closes the numeric eye line but retains -11.46 mm worst arm clearance and is available only by diagnostic CLI. The product keeps the baseline solver (-1.94 mm fresh clearance / 127.03 mm worst eye-line residual); P05-D01–D03 remain unresolved. Raw failed candidates and source snapshots are retained under `Artifacts/visual-design/adr-094/`; [roadmap disposition](visual-roadmap.md#bounded-p05-disposition).

## ADR-095 structural shooting posture

[ADR-095](decisions.md#adr-095--accepted-structural-weapon-from-eye-shooting-posture-p05-reopening) replaced head-only correction with a structural posture: right clavicle shooting posture (with a reload drop/protraction), the right eye placed on the sight line by construction with the neck solved to it, clearance-aware elbows solved after the head, and an outward support-hand reload bow. On the unchanged gate envelope and proxy, `soldier.motion.v6.shooting` measures minimum arm clearance +3.0 mm, eye/sight residual 0 mm, grip below 1e-7 m and stock error 1.2e-7 m. It is now the product default; `-shooterP05Candidate` is retired. P05-D01..D03 are numerically resolved only. P05 visual acceptance stays **FAIL** until a named visual review. Pistol sockets still show a -4.3 mm left-arm residual at reload phase 0.91 (outside the rifle gate). Evidence is in `Artifacts/visual-design/adr-095/`.
