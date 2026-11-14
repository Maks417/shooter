# Offline release remediation — validation and evidence

Applies to [T052–T065](release-remediation-plan.md), derived from the [2026-11-14 audit](product-state-audit.md). Status (2026-11-14): FIX01–FIX13 **software** repairs exist on the dirty uncommitted working tree; player/device/hardware exits remain Pending/PendingHardware where noted in each row. FIX14 / REL01–REL06 stay **Open / NotClaimed** until the [M11 T065 operator runbook](milestones/m11-playable-release/handoff.md) is executed on one clean candidate — this docs task does not mark them PASS. Audit-time passing component suites are historical inputs, not acceptance of future changes. Existing [M8](milestones/m8-runtime-composition/validation-acceptance.md), [M9](milestones/m9-team-deathmatch/validation-acceptance.md), [M10](milestones/m10-maps-shell/validation-acceptance.md) and [M11](milestones/m11-playable-release/validation-acceptance.md) requirements remain mandatory.

## Evidence contract

Every run records action/test IDs, exact source commit and dirty state, actual build/source-tree hash, locked SDK/editor/URP/toolchain hash, C0/Game/content/N1 versions, map/recipe/seed/nav/collision/visual identities, profile/internal render size, roster/workload, duration, counts and artifact paths. Hardware acceptance additionally records reference inventory/adapter/driver, memory channel/power/thermal/process proof and available counters per [performance](performance.md). Unknown/missing values stay explicit. A source label alone does not prove a dirty-built binary matches main.

Use PASS only for the executed scope, FAIL for actual failed checks, NotRun for no execution, Pending for unavailable review/device, and PendingHardware for unavailable reference validation. Require nonzero expected case counts and zero skipped/aborted cases for a complete suite. Preserve raw TRX/XML/logs/captures/trace/sensor data, original failed attempts and prior manifests in immutable ignored Artifacts directories; commit summaries and durable pointers in the milestone handoff. Do not edit payloads, goldens, fixtures or thresholds to make a run pass.

## Repair-specific acceptance

| ID / task | Meaningful regression and required evidence | Original gates |
|---|---|---|
| FIX01 / T052 | Required locked/offline wrappers execute nonzero Contracts/Generation/Simulation/Game suites without NU1004; expected graph diff reviewed, SDK/dependency versions unchanged. Negative wrapper test rejects zero selected tests and propagates failure. Offline advisory lookup reported separately. | RUN01, REL05 |
| FIX02 / T053 | Real harness + session test: first 6v6 death reaches a successful fresh life after 180 active ticks; repeat at least 100 life replacements across both teams including the human. Active actor/hit/AI registry counts remain bounded at configured roster, new EntityIds never reused, stats retained, old-life commands/events rejected, human camera/input and bot commands use replacements. Verify simultaneous deaths, finish/abort cancellation, four-corpse/five-second cap and bounded retained state. Repeat in actual player; a fake lifecycle does not satisfy this check. | RUN03–04, TDM03–06 |
| FIX03 / T054 | Through Bootstrap, pause for 10 real seconds and focus loss: no fatal Busy handling, active ticks/AI/respawns stop, resume creates no debt/catch-up. Frame-rate/catch-up tests at 30/60/144 render FPS apply each mouse delta and button edge once; hold/release slot selection remains stable, ADS/aim limits and movement agree. UI clicks and focus return never fire/reload/jump inadvertently. | RUN04, TDM06, UI01, SET02 |
| FIX04 / T055 | Actual two-map/profile compiler outputs and complete manifests; cold/warm/forced byte reproduction, Map selection and invalid inputs checked. Verify hashes before installation; missing/corrupt/cancelled load releases partial handles and recovers. Release player loads packaged collision/nav/visuals without recipe compilation/dev paths. Native-world reachability/corners and server gameplay-only closure proved; no passing test filter mislabeled content compilation. | RUN02, MAP01–03, REL01, REL05 |
| FIX05 / T056 | Source-matched product-player captures on each map show visible terrain/cover, team-identifiable living soldiers, local/remote weapons and procedural motion; move/fire/reload/hit/death/respawn views follow authority. Check all profiles share gameplay outcomes, no forbidden art/fallback, no stale/despawned renderers/handles after 20 alternating loads and 100 traversal cycles. Captures identify build/profile/seed/tick; READY alone fails this evidence requirement. | RUN03–05, MAP03, REL03 |
| FIX06 / T057 | Both 9/11-bot rosters target only sensed opposite-team living actors; allied/occluded targets never generate invalid fire intent. Actual blocked direct route is solved by following corners. Authority-backed reload consumes real duration and replenishes through weapon rules; no blackboard ammo fabrication. Check state reset after respawn, persistent sequences, global 16 rays/2 new paths per tick, fair opportunities and bounded cancellation/cover/path resources. Include mixed-team player runs on both maps. | TDM04–05, REL02 |
| FIX07 / T058 | Each map has at least twelve unique valid slots per team, required staging exits/three routes/covered route and standing clearance; native nav/LOS/occupancy checks. Simultaneous respawns choose safe stable-ranked slots; all-unsafe defers at 30-tick cadence and terminates after 600 ticks from first failure without moving the deadline. Initial enemies cannot directly shoot staging spawns. Verify distinct depot topology and approved seed identity, profile-equivalent gameplay and named route/spawn review. | TDM03, MAP01–03 |
| FIX08 / T059 | Normal launch reaches main with no active match. Current map/size selection starts that match once; loading cancel/error is recoverable. Exercise pause/settings/restart/leave, live twelve-row scoreboard, final sorted table/outcome/reason, replay and menu. Repeat 20 alternating launches/replays with fresh epochs and bounded resources. At native720p/100% and 150% UI scale, pointer and keyboard focus/cursor/navigation work without leaked gameplay actions. **2026-11-14 software:** pure shell/scoreboard tests PASS (92 Simulation suite); player 20-launch soak and 720p UI review remain Pending; full-match acceptance waits on T057–T058. | UI01–02, REL02–03 |
| FIX09 / T060 | Reviewed version/migration for complete settings coverage; every normative field/binding roundtrips and changes the actual device/input/view. Test staged Apply/Cancel/defaults, finite/range/64KiB validation, corrupt quarantine/newer-file preservation, read-only IO and interrupted atomic replacement preserving last-good. Display change reverts after 15 real seconds, focus loss and relaunch without confirmation; gameplay hashes stay unchanged. Named audible/device verification cannot be replaced by a saved string. | SET01–02, REL03 |
| FIX10 / T061 | Real device-rate synthesized audio heard for product shot/impact/footstep/UI paths; event IDs prevent duplicates, old-session sounds stop. Test voice cap/limiter/attenuation and actual master/effects/UI gain/mute on existing voices. Callback uses no authority mutations or unsafe thread access; device loss warns and permits play. Attach named device review; pure waveform tests alone leave device acceptance Pending. | RUN03, SET02, REL03 |
| FIX11 / T062 | **2026-11-14 tooling:** product pose overlay + defect inventory landed (`ProductVisualReviewOverlay`, `Inventory-ProductVisualDefects.ps1`, [visual-defect-inventory.md](visual-defect-inventory.md)); P05 FAIL retained, fourth search not authorized. Remaining: actual product captures/pose overlays that reproduce and track named P05 defects with named review; original FAIL remains immutable. Any fourth clearance/source search still needs an explicit owner scope record before edits and reviewed generator/recipe migration. Unresolved required defects or absent approval/reviewer keep REL03 open. | RUN05, REL03 |
| FIX12 / T063 | **2026-11-14 software:** ADR-037 depot workloads + collector/analysis/wrappers landed; negative tests reject wrong map/roster/profile/build, missing counters and shortened windows (`python Tools/test_tdm_benchmark_analysis.py`). Product path records rendered TDM with respawn/AI/lease/query/content/render telemetry. **PendingHardware:** twelve captures (both maps × both sizes × three runs, 60s+180s, native1280×720 Low) and ≥30-minute normal-AI 6v6 soak/map. Commands: `Benchmark.ps1`/`Benchmark-Series.ps1`/`Benchmark-Product-Matrix.ps1`/`Benchmark-Soak.ps1` with TDM workloads after Compile-GameContent. Keep 60 FPS stretch and diagnostics separate. | TDM08, REL04 |
| FIX13 / T064 | **2026-11-14 software:** preflight rejects dirty/mismatched/absent build or incomplete content closure; identities come from `build-manifest.json` + map-package closure (not workspace HEAD); archive/SHA256/inventory/CONTROLS/LICENSES/KNOWN_ISSUES/rollback-pair; exclude policy drops Unity backup/BurstDebug/pdb/user files; clean extract + corrupt-content recovery message + previous-pair rollback rehearsal. Evidence: `Tools/test_package_release.ps1` (7 PASS), `ReleaseHardeningTests` (6 PASS), dirty historical build rejected. **Pending:** normal-menu player launch on both maps/sizes and REL01/REL06 acceptance on one clean candidate (T065). | REL01, REL05–06 |
| FIX14 / T065 | **2026-11-14 handoff:** acceptance checklist + operator runbook recorded in [M11 handoff](milestones/m11-playable-release/handoff.md). Software T052–T064 is in the dirty working tree; **REL01–REL06 NotClaimed**. Required next: one clean (`sourceDirty=false`) Client build → Package-Release → extract/launch → named reviews → eight full matches (both maps × both sizes × score/timeout) + draw fixtures → twelve reference captures + two soaks → provenance/regression/server isolation → archive/rollback inventory. P05 FAIL retained (ADR-024). Missing reviewer/machine/counters → Pending/PendingHardware, never invented PASS. Any required FAIL/Pending leaves release acceptance open. | REL01–06 |

FIX02's repeated lives and FIX08's load/replay cycles are fault-focused integration coverage. They supplement the original budgets and soak requirements; they do not permit synthetic hot loops or fake devices to replace real player/hardware evidence. A changed frozen workload or public DTO first follows ADR/migration, then tests the new reviewed version.

## Existing commands and their current scope

From repository root:

```powershell
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
python Tools/verify_contract_lock.py
python Tools/verify_content_format.py
python Tools/verify_feature_registry.py
python Tools/verify_game_contract_lock.py
./Tools/Test-SourceProvenance.ps1
./Tools/Test-Contracts.ps1 -Offline
./Tools/Test-Generation.ps1 -Offline
./Tools/Test-Simulation.ps1 -Offline
./Tools/Test-Game.ps1 -Offline
```

At the audit baseline, Test-Contracts fails locked restore; T052 repairs it before calling the suite green. Test-Game currently exercises pure contracts/reducer/fake-session scenarios; it is not a standalone-player acceptance wrapper. Test-Maps is a filtered pure test wrapper and does not prove complete map content/native navigation. Compile-GameContent and Package-Release received T055/T064 software repairs; their PASS reports still do not alone close player/hardware REL gates. T064 `Package-Release` result `PASS` is FIX13 tooling only (`releaseAcceptance=NotClaimed`, `playerLaunchEvidence=Pending`).

For fresh Unity project copies, use Configure-Unity with the locked installed editor first. Use existing Test-Unity EditMode/PlayMode, Build Client/Server and Smoke per [bootstrap runbook](bootstrap-runbook.md). Smoke intentionally selects the diagnostic path; it must remain separate from a product launch. Benchmark supports B01 and ADR-037 product TDM workloads (`TDM-5v5-v1`, `TDM-6v6-v1`, `TDM-5v5-depot-v1`, `TDM-6v6-depot-v1`, soak IDs) via `Tools/Benchmark.ps1` after `Compile-GameContent`; analysis uses `analyze_tdm_benchmark.py` (collector v4). Twelve-run matrix and soaks remain PendingHardware without reference proof.

Documentation changes use `python docs/verify_docs.py --refresh-manifest` only after intentional review, followed by normal verify and fixture checks. Documentation integrity hashes are never game tests.

## T065 operator command sequence (evidence only; no PASS)

```powershell
./Tools/Compile-GameContent.ps1
./Tools/Configure-Unity.ps1
./Tools/Build.ps1 -Target Client
./Tools/Build.ps1 -Target Server
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <clean-client-build-run> [-PreviousPackageDirectory <prior>]
# Extract package to a clean directory; launch Shooter.exe; record paths/hashes.
# Named reviews + REL02 match matrix on that packaged player (see M11 handoff).
./Tools/Benchmark-Product-Matrix.ps1
./Tools/Benchmark-Soak.ps1
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
./Tools/Test-SourceProvenance.ps1
./Tools/Test-Contracts.ps1 -Offline
./Tools/Test-Generation.ps1 -Offline
./Tools/Test-Simulation.ps1 -Offline
./Tools/Test-Game.ps1 -Offline
```

Package-Release `validation.json` must keep `releaseAcceptance=NotClaimed` until REL checklist evidence exists. Dirty working-tree builds are not acceptance candidates.

## Release gate record and handoff

Integrator updates the existing affected milestone handoff with task ID, FIX/original gate IDs, commit, artifact hashes, actual result, named reviewer where required, limitations and previous binary/content pair. Keep code completion, pure tests, Unity/player behavior, visual review and reference performance in separate fields. Preserve historical coordination closeouts; any later current-status/lease changes are made by the assigned integrator, not inferred by a docs task. Main contains every completed change before temporary branch/worktree cleanup when the owner requests commit; preserve ignored evidence outside any removed workspace. The 2026-11-14 T065 docs handoff does not authorize commit/push by itself.
