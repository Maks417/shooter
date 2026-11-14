# Playable release planning — actual validation record

## Historical record notice — 2026-11-14

The dated results below are retained unchanged for ad4544f. Current source is 59316d3: Game-v1 is frozen with ADR-036/037 extensions and T052–T064 remediation is committed. The [current audit](product-state-audit.md) records Contracts 72, Simulation 101 and Game 38 PASS; Generation 310/311 FAIL and provenance FAIL on 19 missing metadata entries. The earlier NU1004 failure is repaired. Follow [T066–T071](next-playable-build.md) and T062/T065 acceptance, rather than the historical next-action paragraph below.

Date: 2026-11-13. Reviewed code/source HEAD: `ad4544f941d3848e53252db00790a170bdfdc960`, with this documentation working tree dirty during checks. Changes are repository docs plus AGENTS.md only; no Unity source, generated payload, golden, package/toolchain lock, C0 contract, schema or historical acceptance report was changed. This record validates planning delivery and existing regressions, not M8–M11 implementation.

## Executed checks

| Check | Actual outcome | Evidence / scope |
|---|---|---|
| `python docs/fixtures/verify_fixtures.py` | PASS, 35 assertions / 17 JSON files | Existing specification fixtures/subset validator; not game runtime |
| `python docs/verify_docs.py --refresh-manifest`, then normal verify | PASS | Intentional doc inventory refresh, links/anchors/index/fences/hash integrity; delivery report `verification-report.json`; no gameplay result |
| `Tools/Test-Contracts.ps1 -Offline` | PASS, 71/71, zero skipped | `Artifacts/m0-foundation/ad4544f941d3848e53252db00790a170bdfdc960/20261113T210812849Z-782fdce1/` |
| `Tools/Test-Generation.ps1 -Offline` | PASS, 298/298, zero skipped | `Artifacts/m1-procedural-feasibility/ad4544f941d3848e53252db00790a170bdfdc960/20261113T210830471Z-370a9c06/` |
| `Tools/Test-Simulation.ps1 -Offline` | PASS, 61/61, zero skipped | `Artifacts/m2-combat-harness/ad4544f941d3848e53252db00790a170bdfdc960/20261113T210830475Z-540d04c5/` |
| Frozen C0/content/feature registries | PASS in wrappers above | Source/fixture/format guards ran before tests; unchanged versions |
| `Tools/Test-SourceProvenance.ps1` | **FAIL**, 52 findings | Preserved snapshot `Artifacts/planning-review-20261113/source-provenance.json`; 51 missing metadata, one unapproved Assets Markdown file |
| `git diff --check` | PASS | Documentation whitespace check; no game test |

Initial sandboxed wrapper attempts stopped at restore (zero tests); raw logs remain in their timestamped Artifacts directories. Serial MSBuild restored successfully, but sandboxed test-host connection failed/was cancelled. Required wrappers were rerun outside the process sandbox and produced the actual passing counts above. Earlier failed/aborted attempts were retained and were not overwritten as PASS. Offline advisory lookup remains PendingNetwork.

## Baseline defect disposition

The provenance failures affect existing M3–M7 source additions: missing directory/source `.meta` files under Environment, AI, Match, World navigation/streaming, HUD/FX/audio, Networking and Bootstrap/Release; plus `UnityProject/Assets/Shooter/Tests/EditMode/View/ProceduralViewModelEditMode.md`, which is an unapproved asset-source extension. The failure means the audited source layout is not release-ready. It does not prove prohibited visual art was imported. No asset or metadata repair was made in this documentation-only task.

T035 / [RUN00](milestones/m8-runtime-composition/validation-acceptance.md) now owns the repair and repeat source/GUID/reference/assembly audit before new feature dispatch. Preserve established GUIDs, use approved generation/import workflow for missing metadata, move prose outside Assets where appropriate, and validate provenance from its real generator. Do not weaken the allowlist, edit caches or simply mark the guard waived. Original milestone waivers and P05 FAIL are unchanged.

## Not executed or accepted here

The local configured locked editor executable exists; this inspection did not run Unity import, license/module verification, EditMode/PlayMode, client/server build, device/UI/audio review, full map/game compilation, standalone match, visual sign-off or hardware benchmark. Those are future M8–M11 evidence, with previous historical limits retained. Documentation changes do not require regenerating game content or claiming new runtime results. New Game-v1/map/team-match/UI/release suites remain **NotRun / not implemented** until the assigned tasks create and execute them.

No agents were dispatched, no worker leases changed, and coordination.json's false readiness was preserved. The next action is to assign the M8 integrator, repair RUN00 and freeze Game-v1 before bounded multi-agent feature implementation. [Ordered roadmap](playable-release-plan.md) · [Readiness review](readiness-review.md) · [Documentation index](index.md).
