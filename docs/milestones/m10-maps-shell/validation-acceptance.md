# M10 — Two maps, menus and settings — validation and acceptance

Status: **NOT RUN** for this new scope. Scenario IDs below are required checks to implement and run, not claims of existing test coverage. Each result records source SHA/dirty state, toolchain/contract versions, map/seed/content hashes, profile, roster, workload, duration and exact artifact paths.

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

On a fresh Unity checkout run ./Tools/Configure-Unity.ps1 with the locked installed EditorPath first. Then ./Tools/Test-Unity.ps1 -Mode EditMode and -Mode PlayMode; ./Tools/Build.ps1 -Target Client and -Target Server; ./Tools/Smoke.ps1 -BuildRunDirectory <actual-build-run>. Existing Smoke is the diagnostic ready/tick check and must remain distinct from new real-game acceptance. Proposed Compile-GameContent.ps1, Test-Maps.ps1, Test-Game.ps1 and game benchmark/package commands require implementation, documented parameters, nonzero failure propagation and fresh reports before use as gates. Do not claim today's B01-only capture path supports full team workloads.

Retain raw XML/TRX/logs, versioned fixture/manifest/provenance reports, real player captures and machine/counter/thermal evidence where required. Use PASS only for measured checks, FAIL for actual failures, Pending/PendingHardware for unavailable execution, and NotRun for work not attempted. Screenshots/pure loops/docs hashes cannot replace actual Unity/player/performance or named human review. Detailed historical gates remain in their original handoffs.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m9-team-deathmatch/index.md) · [Next pack](../m11-playable-release/index.md).
