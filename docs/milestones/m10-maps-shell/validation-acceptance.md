# M10 — Two maps, menus and settings — validation and acceptance

**Current disposition — 2026-11-14, T069 software:** M10 integration open for named UI/device gates. Schema-2 settings, editable applied bindings and accessibility consumers are in source; SET01–SET02 player/device evidence remain Pending. See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

Current coverage, 2026-11-14: partial historical pure/build/diagnostic evidence exists; complete product acceptance is open. Scenario IDs below remain required, not blanket NotRun or PASS claims. See [audit evidence](../../product-state-audit.md) and [FIX matrix](../../release-remediation-validation.md); repaired-code checks remain NotRun until implementation. Every run records exact source/build/toolchain/content/map/profile/roster/workload/duration and artifacts.

| ID | Scenario | Required result |
|---|---|---|
| MAP01 | Distinct approved catalog | Both fixed MapIds resolve to different topology/content, both support only TDM and team sizes 5/6; malformed/duplicate/unsupported IDs or dependencies reject before spawn; no arbitrary path input. |
| MAP02 | Real map safety | Each map ≥12 spawn slots/team, two staging exits, three inter-team routes including covered flank; no initial spawn shot; native nav/collision clearance, corner following, seams and spawn pressure pass; approved identities reproduce cold/warm. |
| MAP03 | Lifecycle and profile | 20 alternating map loads plus cancel/error/retry and stale completion cases; 100 traversal/stream cycles; bounded handles/residency; Low/Balanced/High gameplay identities and outcomes agree. |
| UI01 | Launch-to-results | Start shipped executable → map/mode/size selection → load → play → scores/timer → finish/results → replay/menu. Test both maps and sizes, pointer and keyboard, no click/fire leakage or duplicate Start. |
| UI02 | Readable tables/accessibility | Real player 720p at 100%/150% scale; team/name/Human-Bot/kills/deaths columns fit or scroll, local row visible, victory/defeat/draw and finish reason correct; focus/color-independent markers/contrast pass review. |
| SET01 | Persistence and safety | All fields/bindings roundtrip on relaunch; invalid/NaN/oversized/corrupt/newer schema and read-only disk recover correctly; atomic write interruption preserves last-good; Apply/Cancel/defaults behavior tested. |
| SET02 | Actual devices | Master/effects/UI gains and mute audible with existing voices; quality changes update actual visuals only; FOV/mouse/remapped keys applied; supported mode filtering and 15-second real-time revert work on timeout/focus loss/restart. |

## Commands and evidence

Existing commands from repository root: python docs/fixtures/verify_fixtures.py; python docs/verify_docs.py; ./Tools/Test-Contracts.ps1 -Offline; ./Tools/Test-Generation.ps1 -Offline; ./Tools/Test-Simulation.ps1 -Offline; ./Tools/Test-SourceProvenance.ps1. Offline advisory lookup is PendingNetwork, separate from execution results. Add the new product fixtures/projects to wrapper coverage through integrator and prove nonzero execution, rather than assuming wildcard inclusion.

On a fresh Unity checkout run ./Tools/Configure-Unity.ps1 with the locked installed EditorPath first. Then ./Tools/Test-Unity.ps1 -Mode EditMode and -Mode PlayMode; ./Tools/Build.ps1 -Target Client and -Target Server; ./Tools/Smoke.ps1 -BuildRunDirectory <actual-build-run>. Existing Smoke is the diagnostic ready/tick check and must remain distinct from new real-game acceptance. Compile-GameContent now publishes verified map packages; Test-Maps/Test-Game remain pure suites; Package-Release validates clean build/content identities and creates an archive with a launcher. Product TDM collector/matrix/soak commands exist under ADR-037, with native content and multi-match/counter gaps in T067/T070. Record actual parameters/counts/failures. None of these tooling results substitutes for player/REL evidence.

Retain raw XML/TRX/logs, versioned fixture/manifest/provenance reports, real player captures and machine/counter/thermal evidence where required. Use PASS only for measured checks, FAIL for actual failures, Pending/PendingHardware for unavailable execution, and NotRun for work not attempted. Screenshots/pure loops/docs hashes cannot replace actual Unity/player/performance or named human review. Detailed historical gates remain in their original handoffs.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m9-team-deathmatch/index.md) · [Next pack](../m11-playable-release/index.md).
