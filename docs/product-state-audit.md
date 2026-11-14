# Product state audit — 2026-11-14

Reviewed source: `f3d995497d88376d171c86b4088ae37fa8f289f0` on clean `main`. Scope: the Windows offline two-map Team Deathmatch release in [vision](vision.md), not public multiplayer or optional T025. This is a source/evidence audit with fresh pure checks; no fresh Unity build, interactive player review or hardware capture was performed. The [repair plan](release-remediation-plan.md) and [validation matrix](release-remediation-validation.md) turn these findings into bounded future assignments. This documentation assignment authorizes no runtime implementation or worker dispatch.

## Current disposition

M0–M7 retain their historical administrative closures and owner waivers. M8–M11 code and local build/package evidence exist, so describing all four as unstarted is incorrect. Their 2026-11-13 `CLOSED_SOFTWARE` / `CLOSED_SOFTWARE_PACKAGE` entries also do not establish technical product completion: M8–M10 integration is incomplete and M11 release acceptance is open. Historical handoff ledgers and `coordination.json` entries remain records of what was claimed at that time; this audit supersedes their current product-readiness interpretation without rewriting raw results.

| Gate | Implemented/reusable | Current acceptance disposition |
|---|---|---|
| M8 | Game-v1 freeze (ADR-034), OfflineGameSession, collider/path adapter, input/camera/text view, real client/server builds | Incomplete: visible map/actors, audible output, pause and lifecycle integration; RUN02–RUN05 not accepted from READY/smoke markers |
| M9 | TDM reducer, full-size initial roster, 11-bot capacity, session tick binding | Incomplete: repeated respawn, life rebinding, team targeting, real corner following and safe-spawn behavior; TDM07/08 still pending |
| M10 | Compound/depot numeric planners, catalog, minimal shell, file settings store | Incomplete: verified content packages, functional menu transitions/tables, settings editor and live device application |
| M11 | T064 Package-Release software + older historical dirty package; T065 checklist/runbook | Open / NotClaimed: REL01–REL06 need one clean source-matched candidate, P05 FAIL retained, full matches, visual/device reviews, reference performance/soak and rollback |

## Findings and source evidence

Paths below start at `UnityProject/Assets/Shooter/` unless prefixed `Tools/`. Line numbers describe the reviewed source, not a permanent API. Source conclusions are distinguished from measured tests; no defect below was interactively reproduced in a fresh Unity player during this audit.

| ID / priority | Observed source and consequence | Repair |
|---|---|---|
| A01 / critical | `World/Foundation/CompoundMapRuntime.cs:169` creates GameObjects, BoxColliders and StableSurface only. `Presentation/Session/ProductSessionView.cs:33` creates camera/light/text but no map, soldier or weapon renderers. Product composition bypasses the generated galleries. Collider installation and READY do not prove a visible game. | T055–T056 |
| A02 / critical | `Simulation/TeamDeathmatch/TeamDeathmatchMatch.cs:159` spawns a new life without retiring the old one. `Simulation/Session/HarnessActorLifecycle.cs:27` caps its retained states at 12; DespawnLife only removes its own dictionaries, not harness authority. Initial 6v6 therefore fills capacity before the first respawn. `Game/Session/OfflineGameSession.cs` retains the original humanEntity and bot registrations. | T053 |
| A03 / critical | Paused `OfflineGameSession.Step` returns Busy; `Bootstrap/Runtime/FoundationEntry.cs:165` throws for unsuccessful steps unless Finished, while Update keeps advancing its clock. Escape pause can therefore reach fatal session handling. | T054 |
| A04 / high | `Simulation/AI/TeamBotPlanner.cs:34` supplies all living participants as candidates; `CombatAiDirector.PlanEnemies` only excludes self. Allies can consume target/fire intent despite friendly damage rejection. `PollPaths` discards completed corners; BuildCommand moves toward PathGoal. Reload state changes before command construction also need authority-backed coverage. | T057 |
| A05 / high | `TeamDeathmatchMatch.TryPickSpawn` chooses the lowest-ID statically valid slot without actor occupancy/enemy-pressure ranking. `CompoundCatalogFactory` supplies six initial slots per team rather than the specified minimum twelve and can repeat fallback positions. Retry updates the due tick used for timeout; preserve an independent first-failure deadline. | T058 |
| A06 / high | ~~`FoundationEntry.StartProduct` loads/starts before the shell exists…~~ **T059 software (2026-11-14):** product launch prepares session/catalog only; `ProductShellHost` owns Main→Setup→Loading→Playing→Paused→Results, cancellable load, Tab scoreboard and retained results. Remaining: player 20-launch soak, 720p UI review, full-match acceptance after T057–T058. | T059 |
| A07 / high | ~~Historical schema-1 semicolon store / quality-only apply.~~ **T060+T059/T061 software (2026-11-14):** ADR-036 schema 2, `UserSettingsEditor`, `FileUserSettingsStore` JSON safety, `ProductClientSettingsApplier` audio/display/FOV/input bind. Remaining: SET02 named device/15s Keep/Revert on real display; full rebind UI polish. Input buffer repair is T054. | T054, T060, T059, T061 |
| A08 / high | Historical: `NotifyCombat` had no caller; mixer output discarded; no AudioSource path. **T061 software repair (2026-11-14):** ordered event lease → router → `UnitySynthAudioOutput`/`OnAudioFilterRead`; pure Fx PASS. Named device/reviewer audible acceptance and T060 settings binding remain open — waveforms alone are not FIX10 device PASS. | T061 |
| A09 / high | `Tools/Compile-GameContent.ps1` runs Compound-filtered tests and reports seeds; it does not compile/publish a dependency package and does not use Map to select actual outputs. Catalog visual hashes are hashes of labels. CompoundMapRuntime generates map plans in the release player instead of loading verified compiled content. | T055 |
| A10 / high | **T063 software 2026-11-14:** ADR-037 adds depot measurement IDs + product collector/analysis/wrappers; negatives cover wrong map/roster/profile/build. Twelve captures + soaks remain PendingHardware. Historical audit note: B01-only path and compound-only fixtures were the baseline gap. | T063 |
| A11 / high | ~~Copy/hash-only Package-Release that stamped HEAD and shipped backup/debug folders.~~ **T064/FIX13 software (2026-11-14):** `Tools/Package-Release.ps1` + `Tools/release-package-policy.json` reject dirty/mismatched/absent build identities (no HEAD stamp onto arbitrary binaries), require verified map-package closure, exclude backup/debug/pdb/user files, emit archive/SHA256/inventory/licenses/provenance/rollback pair, and rehearse clean extract + corrupt recovery + prior-pair rollback. Synthetic wrapper suite PASS (`Tools/test_package_release.ps1`, 7 cases); dirty `03fa6dd` client build FAIL as required. **Pending:** player launch / REL01–REL06 on one clean source-matched candidate (T065). | T064 |
| A12 / high | Fresh required `Tools/Test-Contracts.ps1 -Offline` fails restore with NU1004: Foundation now references Generation.Core but `Tools/Contracts.Tests/packages.lock.json` has an older project graph. No Contracts tests execute in that attempt. | T052 |
| A13 / release blocker | P05 remains FAIL under ADR-024. **T062 tooling 2026-11-14:** capture/overlay helpers + defect inventory (ToolingReady); no fourth clearance search; FAIL not relabeled. Named player/UI/soldier review, full matches and reference performance/soak are still missing. Historical waivers do not accept new gates. | T062, T065 |

## Fresh verification at audited source

Evidence paths are repository-relative and ignored; preserve them before any workspace removal. Initial sandboxed dotnet attempts exited before tests with incomplete diagnostics. The successful/failing runs below were rerun with the process access needed by the existing wrappers. Failed attempts remain separate; no result was changed into PASS.

| Check | Actual outcome | Evidence / limitation |
|---|---|---|
| Specification fixtures | PASS: 35 assertions / 17 JSON files | `python docs/fixtures/verify_fixtures.py`; specification only |
| Documentation integrity | PASS: 117 Markdown pages, 867 links, 152 integrity files | `python docs/verify_docs.py`; audit-time counts, not runtime evidence |
| Source provenance | PASS: 538 asset entries / 269 GUIDs | `Tools/Test-SourceProvenance.ps1`; old 51-metadata/one-Markdown planning failure is no longer current |
| Generation wrapper | PASS: 298 passed / zero skipped | `Artifacts/m1-procedural-feasibility/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T093052026Z-82d371e9/` |
| Simulation wrapper | PASS: 61 passed / zero skipped | `Artifacts/m2-combat-harness/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T093122259Z-bf34510e/` |
| Game-v1 suite | PASS: 15 passed / zero skipped | Direct `dotnet test ... -c Release --no-restore`; `Artifacts/project-state-audit/f3d9954/audit-game.trx`; not a successful Test-Game wrapper run |
| Contracts wrapper | FAIL before tests: NU1004 | `Artifacts/m0-foundation/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T093045793Z-da623fd8/restore.log` |
| C0/content/feature/Game-v1 identity guards | PASS in audit commands/wrappers | Frozen identity only; not runtime conformance |
| Fresh Unity/player/device/hardware checks | NotRun / PendingHardware | Historical build/smoke logs exist, but no new acceptance from this audit |

The Game-v1 session integration test advances only 30 ticks with FakeMapRuntime. Reducer tests use a recording lifecycle. These tests do not exercise the first real respawn, Bootstrap pause, rendered content, audio devices or menu-driven replay; their green results do not contradict the findings.

## Interpretation and maintenance

The [2026-11-13 planning validation](planning-validation.md) remains historical evidence. Current next work is [T052–T065](release-remediation-plan.md), replacing instructions to restart T035 or merely collect missing reviews. Preserve T035–T051 requirement IDs and RUN/TDM/MAP/UI/SET/REL acceptance IDs. Future implementation updates this audit's dispositions with source-matched evidence rather than deleting failures. The docs-only follow-up changes no contracts, package versions, generated content, goldens, benchmark fixtures or coordination leases.

## Documentation follow-up verification — 2026-11-14

The owner subsequently requested corrected repository docs and implementation/fix plans. This follow-up changes docs/ and AGENTS.md only; runtime source, generated content, frozen fixtures/locks and coordination.json remain unchanged. The index, milestone packs and entry instructions now distinguish current incomplete acceptance from retained historical software claims. New T052–T065/FIX01–FIX14 are planned assignments, not executed repairs.

Fixture verification (35 assertions), documentation links/inventory/hash checks, source provenance (538 entries/269 GUIDs), C0/content/feature/Game-v1 identity guards and whitespace checks pass. Final documentation counts are recorded in verification-report.json; documentation hashes remain delivery integrity only. The first verifier attempt found an unreachable historical view note after navigation edits; its index link was restored before final verification.

The required wrappers were rerun with docs dirty on the same f3d9954 runtime baseline: Generation PASS, 298/298, zero skipped, at `Artifacts/m1-procedural-feasibility/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T094648602Z-05769344/`; Contracts FAIL before tests with unchanged NU1004 at `Artifacts/m0-foundation/f3d995497d88376d171c86b4088ae37fa8f289f0/20261114T094640978Z-4e9545e2/`. T052 owns the implementation fix; no lock was relaxed here. No new Unity build/player/visual/audio/hardware acceptance was attempted or claimed by this documentation follow-up.

## Remediation software on dirty main — 2026-11-14 (post-audit)

After the docs-audit commit `6329262446428f92617600d8526b13f9c90a358a`, T052–T064 software remediation was implemented on an **uncommitted dirty `main` working tree**. That work is not a clean acceptance candidate. Honest dispositions for T065:

| Task | Software | Remaining |
|---|---|---|
| T052 FIX01 | Met | Offline NuGet advisory PendingNetwork |
| T053 FIX02 | Software met | Actual-player lifecycle Pending |
| T054 FIX03 | Software met | Player pause/focus device Pending |
| T055 FIX04 | Partial packages/load | Native Unity nav bake + release-player Pending |
| T056 FIX05 | Software wired | Player captures both maps Pending |
| T057 FIX06 | Pure AI met | Mixed-team real-map player Pending |
| T058 FIX07 | Software spawn safety | Native/player review Pending |
| T059 FIX08 | Shell software | 20-launch soak + 720p UI review Pending |
| T060 FIX09 | ADR-036 + UI/audio bind | SET02 real display Pending |
| T061 FIX10 | Device callback software | Named audible review Pending |
| T062 FIX11 | ToolingReady inventory | P05 FAIL retained; REL03 open |
| T063 FIX12 | ADR-037 tooling | 12 captures + soaks PendingHardware |
| T064 FIX13 | Package-Release software | Clean source-matched client + player launch Pending |
| T065 FIX14 | Checklist + operator runbook | REL01–REL06 NotClaimed until measured |

T065 acceptance steps and commands live in [M11 handoff](milestones/m11-playable-release/handoff.md). No REL gate is PASS; failed P05 artifacts remain; packages/editor were not upgraded by the T065 docs handoff. Commit of the large working tree was not requested.
