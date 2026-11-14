# Playable offline release — current state and repair order

Reviewed 2026-11-14 at `f3d995497d88376d171c86b4088ae37fa8f289f0`. M0–M7 retain their historical owner-waived closures. M8–M11 code and historical software closeouts exist, but the [source audit](product-state-audit.md) leaves M8–M10 integration incomplete and M11 release acceptance open. **Later same day:** T052–T064 software remediation is on a dirty uncommitted working tree; [T065 handoff](milestones/m11-playable-release/handoff.md) records the acceptance checklist and operator runbook without claiming REL01–REL06 PASS. This page supersedes the old 2026-11-13 next-development narrative; [planning validation](planning-validation.md) retains its original results.

## Required product

A source-matched Windows x64 archive launches offline into a menu. Select Training Compound or Depot Yard, Team Deathmatch, and 5v5 or 6v6. One human joins Team 1; bots fill the other nine/eleven slots. Teams race to 150 enemy kills or a 15-minute active-play deadline; equal scores at deadline draw without overtime. Results include team/player stats, replay and return to menu. Audio/video/control/accessibility preferences persist and apply to real devices.

[Vision](vision.md), [team rules](features/team-deathmatch.md), [maps](features/playable-maps.md) and [shell](features/game-shell.md) retain exact requirements. Compound Clear remains diagnostic, remote networking is not required, T025/public networking remain deferred, and procedural-only art rules remain unchanged. Native1280×720 Low30 on the reference machine is mandatory; 60 FPS remains separate stretch.

## Reusable foundation and actual gaps

| Area | Existing code/evidence | Remaining product gap |
|---|---|---|
| Contracts/regression | Game-v1 frozen ADR-034; 298 Generation, 61 Simulation and 15 Game tests passed at audited source | Required Contracts wrapper fails NU1004 before tests; green fake/reducer cases omit real integration |
| Product Bootstrap | OfflineGameSession default; real collider/path installation, initial 9/11 bots, camera/input/text; historical IL2CPP client/server builds and READY logs | Pause treated as fatal Busy; actors not retired/rebound across respawns; no product map/soldier/weapon renderers or audio-device output |
| Team match/AI | Pure score/deadline/draw reducer and eleven-bot capacity | Teammates enter sensing candidates, nav corners discarded, reload/life-reset behavior incomplete; safe spawn occupancy/pressure/timeout policy missing |
| Maps/content | Separate CompoundPlanner and DepotPlanner, map catalog and initial spawns | Compile-GameContent runs tests rather than publishes dependencies; visual roots are label hashes, maps regenerate at runtime, six slots/team versus required twelve |
| Shell/settings | Minimal GameShellMenu, ProductSessionView and file store | Match auto-starts; Start only saves next-launch preferences; full tables/replay/menu/settings/device flows absent |
| Delivery/measurement | Package-Release copy/inventory wrapper; historical package at dirty 03fa6dd source | No clean-current-source independent archive/rollback evidence; collector supports B01, not real TDM; P05 FAIL, full player matrix, reference performance and soak open |

Paths and exact observed failures are centralized in [audit A01–A13](product-state-audit.md), not duplicated in milestone reference folders. Source provenance now passes; the earlier 51-metadata/one-Markdown entry blocker was repaired. RUN00 repair and T035 freeze must not be restarted merely because the original planning pages described them as future work.

## Dependency-ordered implementation

[Tasks T052–T065](release-remediation-plan.md) give ownership, permitted path boundaries, compatibility decisions and exact outputs; [FIX01–FIX14](release-remediation-validation.md) define acceptance. Original T035–T051 and RUN/TDM/MAP/UI/SET/REL IDs remain in place.

| Order | Bounded tasks | Reviewable result |
|---|---|---|
| 1 | T052 regression baseline, T053 actor lifecycle, T054 pause/input | Locked suites execute; first/repeated 6v6 respawn works; pause/focus never kills session or consumes duplicate input |
| 2 | T055 verified compiled content, T056 visible presentation | One real generated map with visible actors/weapons and coherent lifecycle in the player; then both approved maps/profiles |
| 3 | T057 team AI, T058 safe spawns/routes | Enemy-only sensed combat, actual corner following/reload and bounded fair 9/11-bot behavior; safe distinct maps |
| 4 | T059 shell, T060 settings, T061 event audio | Menu owns loading/play/pause/results/replay; all required preferences apply/persist; synthesized output is audible |
| 5 | T062 scoped visual review, T063 TDM measurement, T064 clean archive | Explicit P05 disposition, actual product capture tooling and source-matched independent package/rollback |
| 6 | T065 final acceptance | Full match matrix, named visual/UI review, native720p reference captures, soaks and REL01–REL06 handoff — **checklist/runbook ready; gates NotClaimed** |

These are dependencies, not dates or active worker assignments. One integrator owns shared contracts/Bootstrap/settings/locks/workloads/coordination. Game-v1 is frozen; missing settings/workload/content coverage must follow reviewed ADR/fixture migration before dependent edits. Parallelism requires explicit authorization and separate ownership/worktrees. The docs update creates no leases and does not alter historical coordination entries.

## Acceptance boundary

Historical CLOSED_SOFTWARE and package PASS labels remain history, not technical completion. READY proves initialization only; diagnostic Smoke selects floor/box, pure tests do not prove device behavior, and Package-Release tooling PASS does not prove a source-matched release (`releaseAcceptance=NotClaimed`). Both maps must ship all verified collision/nav/visual/weapon/rig/shader/font dependencies and run without Unity, developer cache, internet or a server process. No release-player map compilation or silent diagnostic fallback.

P05 remains FAIL under ADR-024. T062 can triage/capture live defects, but a fourth clearance/parameter search still needs an explicit owner scope decision; this documentation task does not provide it. New generated identities require review and retained failed evidence. Unavailable reviewers/devices/counters/reference hardware leave required gates Pending/PendingHardware, without inheriting M0–M7 waivers.

**T065 next human/hardware path** (see [M11 handoff](milestones/m11-playable-release/handoff.md)): Compile-GameContent → clean Client build (`sourceDirty=false`) → Package-Release → extract/launch → named reviews → match matrix → Benchmark-Product-Matrix/Soak → regression wrappers. Do not claim REL01–REL06 PASS until those measured results exist on one clean candidate. The large dirty working tree has not been committed unless the owner requests it.

The release matrix remains [M11 REL01–REL06](milestones/m11-playable-release/validation-acceptance.md), supplemented by FIX01–FIX14: both maps/sizes, score and unshortened timeout matches, results/replay/settings, actual product workloads and per-map soaks. Implementation handoffs attach exact source/toolchain/content/workload/evidence identities and all real limitations, commit completed work to main when requested and preserve evidence before temporary-workspace cleanup.
