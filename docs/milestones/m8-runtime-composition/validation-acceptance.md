# M8 — Runtime composition and Game-v1 foundation — validation and acceptance

Status: **NOT RUN** for this new scope. Scenario IDs below are required checks to implement and run, not claims of existing test coverage. Each result records source SHA/dirty state, toolchain/contract versions, map/seed/content hashes, profile, roster, workload, duration and exact artifact paths.

| ID | Scenario | Required result |
|---|---|---|
| RUN01 | Pure conformance | New Game-v1 project builds without Unity; invalid request/version/count, pause clock, leased data, stale load/stop and life mapping fixtures pass; old pure suites and frozen locks stay valid. |
| RUN00 | Existing source baseline | Repair the 51 missing metadata entries and one unapproved Assets Markdown source recorded by the planning audit; required source provenance, GUID/reference and assembly/client-server boundary checks pass before feature dispatch; no blanket allowlist exception. |
| RUN02 | Real map adapter | Release player installs compound collision/navigation, rejects missing/corrupt package, routes bots around a wall with real path corners, and fails safely on invalid spawn; fake/delegate-only navigation cannot pass. |
| RUN03 | Actual local gameplay | Named reviewer uses keyboard/mouse and rifle/pistol on the generated map with eight bots; camera/rig/health/ammo/hit feedback and synthesized audio reflect real authority; no empty-command/floor-box product substitute. |
| RUN04 | Lifecycle and regression | Pause/focus loss freezes authority, resume clears input/debt; cancel/late completion cannot cross sessions. Ten start/stop/replay cycles release resources; existing client/server diagnostic builds/smoke and server stripping pass. |
| RUN05 | Evidence and risk | Retain first real runtime captures/logs/manifests, actual workload/counter report and P05 defect list; hardware timing and subjective soldier acceptance remain distinct. Missing Unity keeps M8 exit Pending, even if pure tasks merge. |

## Commands and evidence

Existing commands from repository root: python docs/fixtures/verify_fixtures.py; python docs/verify_docs.py; ./Tools/Test-Contracts.ps1 -Offline; ./Tools/Test-Generation.ps1 -Offline; ./Tools/Test-Simulation.ps1 -Offline; ./Tools/Test-SourceProvenance.ps1. Offline advisory lookup is PendingNetwork, separate from execution results. Add the new product fixtures/projects to wrapper coverage through integrator and prove nonzero execution, rather than assuming wildcard inclusion.

On a fresh Unity checkout run ./Tools/Configure-Unity.ps1 with the locked installed EditorPath first. Then ./Tools/Test-Unity.ps1 -Mode EditMode and -Mode PlayMode; ./Tools/Build.ps1 -Target Client and -Target Server; ./Tools/Smoke.ps1 -BuildRunDirectory <actual-build-run>. Existing Smoke is the diagnostic ready/tick check and must remain distinct from new real-game acceptance. Proposed Compile-GameContent.ps1, Test-Maps.ps1, Test-Game.ps1 and game benchmark/package commands require implementation, documented parameters, nonzero failure propagation and fresh reports before use as gates. Do not claim today's B01-only capture path supports full team workloads.

Retain raw XML/TRX/logs, versioned fixture/manifest/provenance reports, real player captures and machine/counter/thermal evidence where required. Use PASS only for measured checks, FAIL for actual failures, Pending/PendingHardware for unavailable execution, and NotRun for work not attempted. Screenshots/pure loops/docs hashes cannot replace actual Unity/player/performance or named human review. Detailed historical gates remain in their original handoffs.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).
