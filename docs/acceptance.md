# Milestones and acceptance

This page holds the milestone gates, the scenario IDs each gate needs and the final release sequence. Current status lives in [next playable build](next-playable-build.md). Evidence goes in the milestone handoffs:
- [M8](milestones/m8-runtime-composition/handoff.md)
- [M9](milestones/m9-team-deathmatch/handoff.md)
- [M10](milestones/m10-maps-shell/handoff.md)
- [M11](milestones/m11-playable-release/handoff.md)

## Gates

M0–M7 are closed under owner waivers (ADR-017, ADR-025–028, ADR-030–032). Their FAIL/Pending evidence stays in git history.

M5 and M6 were network gates. They are **Parked** under [ADR-076](decisions.md#adr-076--accepted-offline-only-scope-and-bounded-local-evidence), not Pending.

M8–M11 are open. No new acceptance waiver is authorized.

| Gate | Deliverable | Exit scenarios |
|---|---|---|
| M8 Runtime composition | Real player path: procedural content, authority, buffered input, bots, rig/HUD/audio and safe teardown on Game-v1 | RUN00–RUN05 |
| M9 Team Deathmatch | Authoritative team match: rosters, 150 kills or 15 active minutes, safe respawn, immutable results | TDM01–TDM08 |
| M10 Maps and shell | Three distinct packaged maps, map/size selection, pause, live and final tables, persistent working settings | MAP01–MAP03, UI01–UI02, SET01–SET02 |
| M11 Playable release | Source-matched Windows installer with in-place upgrades, accepted content and honest reference performance | REL01–REL06, FIX01–FIX14, [INS01–INS06](features/windows-installer.md) |

The visual quality gates VD0–VD9 in [visual direction](visual-direction.md) extend visual acceptance inside M8–M11. They do not waive P05 or REL03.

## Evidence rules

Every run must record:
- the source commit and its dirty state;
- the build and toolchain hashes;
- the C0, Game-v1 and content versions;
- the map, seed and nav identities;
- the profile, roster and workload;
- the duration and the artifact paths.

Hardware runs also record the reference-machine proof and counters required by [performance](performance.md).

Use the result labels strictly:

| Label | Use it when |
|---|---|
| PASS | The check was executed and passed, within its stated scope only |
| FAIL | The check actually failed |
| NotRun | Nobody executed it |
| Pending | A reviewer or device was unavailable |
| PendingHardware | The reference machine or counters were unavailable |

- A suite needs nonzero case counts and zero skipped cases.
- Keep raw logs, captures and failed attempts in ignored `Artifacts/`. Commit only summaries and pointers in the handoff.
- Never edit payloads, goldens, fixtures or thresholds to make a run pass.
- Builder, smoke, package, pure-test and docs-hash PASS results are not gameplay evidence. They never replace a player run, named review or reference measurement.

## M8 — runtime composition

| ID | Scenario | Required result |
|---|---|---|
| RUN00 | Source baseline | Metadata/GUID provenance passes with no blanket allowlist exception. Repeat the assembly checks when source changes. |
| RUN01 | Pure conformance | Game-v1 builds without Unity. Fixtures pass for: invalid request/version/count, pause clock, leased data, stale load/stop and life mapping. Frozen locks stay valid. |
| RUN02 | Real map adapter | Release player installs packaged collision/navigation. It rejects a missing or corrupt package, routes bots around walls with real path corners and fails safely on an invalid spawn. |
| RUN03 | Actual local gameplay | Named reviewer plays with keyboard/mouse and all weapon slots against bots. Camera, rig, health, ammo, hit feedback and audio follow real authority. |
| RUN04 | Lifecycle | Pause or focus loss freezes authority, and resume clears input/debt. Cancel or late completion cannot cross sessions. Ten start/stop/replay cycles release resources. |
| RUN05 | Evidence and risk | First real runtime captures/logs/manifests and the P05 defect list are retained. Hardware timing and subjective soldier acceptance stay separate. |

## M9 — Team Deathmatch

| ID | Scenario | Required result |
|---|---|---|
| TDM01 | Scoring | Enemy kills score once. Allies, self and environment add nothing. A death increments once. Invalid killers and duplicate or stale events cannot corrupt stats. |
| TDM02 | Deadline and trades | Boundary at 53999/54000 active ticks. A zero-score timeout is a draw, and a 149:149 trade gives a 150:150 draw. Same-tick score plus time reports ScoreLimit. Exactly one finish is published. |
| TDM03 | Respawn | After 180 active ticks the actor gets fresh health, ammo and EntityId with the same participant stats. Unsafe spawns defer with a 30-tick retry and fail after 600 ticks. All queues are cancelled on finish. |
| TDM04 | Rosters and bots | Every supported size (5v5, 8v8, 10v10, 15v15) is exact. Bots acquire only sensed enemies. Walls clear stale fire. Reloads are authoritative and route corners are followed. |
| TDM05 | Query fairness | Global limits per tick: ≤16 vision rays and ≤2 new paths. Each bot gets a sensing opportunity within 12 active ticks. No nav ticket or cover claim leaks on death or replay. |
| TDM06 | Pause and finish | Pause stops the clock, bots and respawn. Resume clears fire edges. Final results stay identical after unload. An abort never appears as a victory. |
| TDM07 | Real complete matches | Release player reaches results, replay and return to menu. At least one unshortened 15-minute timeout and one score-limit finish use real combat. Accelerated fixtures do not count. |
| TDM08 | Quality and capacity | Scripted commands produce identical gameplay across Low, Balanced and High. Full-roster release-player counters, allocations and duration are recorded. |

## M10 — maps, shell and settings

| ID | Scenario | Required result |
|---|---|---|
| MAP01 | Catalog | Old Town, Harbour and Oil Depot resolve to distinct content and support TDM at every catalog size. Malformed or unsupported IDs reject before spawn. |
| MAP02 | Map safety | Each team has ≥12 spawn slots and two staging exits. There are three inter-team routes, including a covered flank. No initial spawn can be shot. Native nav and clearance pass, and identities reproduce cold and warm. See [map layouts](features/tdm-map-layouts.md). |
| MAP03 | Lifecycle and profile | 20 alternating map loads with cancel, error and retry, and 100 traversal cycles, with bounded handles. All profiles agree on gameplay. |
| UI01 | Launch to results | Executable → map/size selection → load → play → finish/results → replay or menu, on every map. Pointer and keyboard both work. No click leaks into fire. |
| UI02 | Tables and accessibility | At 720p with 100% and 150% UI scale, the columns fit and the local row is visible. Outcome and reason are correct. Markers do not rely on colour alone. |
| SET01 | Persistence | All fields and bindings survive relaunch. Corrupt, newer-schema and read-only files recover. An interrupted atomic write keeps the last good file. |
| SET02 | Actual devices | Audio gains and mute are audible. Quality changes visuals only. FOV, mouse and rebinds apply. The display mode reverts after 15 s on timeout, focus loss or restart. |

## M11 — release

| ID | Scenario | Required result |
|---|---|---|
| REL01 | Clean artifact | Content reproduces from an empty cache. The installed game launches with no Unity, dev path, network or server. Missing or corrupt content fails recoverably. |
| REL02 | Full play matrix | Every map × size in the release player, including score and timeout finishes. Results, replay, menu and settings work. At least one manual complete match per map. Draw is verified by the boundary fixture. |
| REL03 | Visual and usability sign-off | A named reviewer records build, seed and captures, and resolves P05 per the [defect inventory](visual-defect-inventory.md). Menu and HUD are checked at 720p 150%. The original P05 FAIL stays immutable. |
| REL04 | Reference performance | `Benchmark-Product-Matrix.ps1` runs 5v5 and 15v15 on every map, three runs each (18 captures), at native 720p Low under the [performance](performance.md) protocol. Add a ≥30-minute normal-AI 15v15 soak per map. B01–B03 do not substitute. |
| REL05 | Regression and provenance | Pure, Unity and player suites pass. Provenance and schema identities are valid. |
| REL06 | Delivery and rollback | Installer path and hash, identities, every gate status and known issues are recorded. A previous binary+content rollback is rehearsed. Work is committed on `main`. |

### FIX01–FIX14 repair evidence

These are fault-focused additions to the scenarios above, one per remediation task. They supplement the original gates; they never replace them.

| ID / task | Extra evidence on top of the original gate | Gates |
|---|---|---|
| FIX01 / T052 | Locked/offline wrappers run nonzero suites. A negative test rejects zero selected tests. | REL05 |
| FIX02 / T053 | ≥100 life replacements across both teams, including the human. Registries stay bounded and EntityIds are never reused. Repeat in the player. | RUN03–04, TDM03–06 |
| FIX03 / T054 | 10 s pause plus focus loss. Input applies once at 30/60/144 FPS. UI clicks never fire, reload or jump. | RUN04, TDM06, UI01 |
| FIX04 / T055 | Content reproduces cold, warm and forced. Hashes are verified before install. Partial loads release handles. The release player never compiles recipes. | RUN02, MAP01–03, REL01 |
| FIX05 / T056 | Source-matched player captures per map show terrain, team-identifiable soldiers, weapons and motion following authority. | RUN03–05, REL03 |
| FIX06 / T057 | Mixed-team bots target only sensed living enemies. Reload is authoritative. State resets on respawn. | TDM04–05 |
| FIX07 / T058 | Spawn and route rules from MAP02, with named route/spawn review on every map. | TDM03, MAP01–03 |
| FIX08 / T059 | 20 alternating launches/replays with bounded resources. UI is checked at 100% and 150% scale. | UI01–02 |
| FIX09 / T060 | Every settings field roundtrips and changes the actual device. Real display revert. | SET01–02 |
| FIX10 / T061 | Named review on a real audio device: shots, impacts, footsteps and UI. The voice cap and gains are verified. | SET02, REL03 |
| FIX11 / T062 | Player captures track the named P05 defects with a named review. | REL03 |
| FIX12 / T063 | REL04 captures and soaks through the ADR-037 collector. | TDM08, REL04 |
| FIX13 / T064 | Package preflight rejects dirty or mismatched builds. An inventory of the clean extract is kept. Rollback is rehearsed. | REL01, REL05–06 |
| FIX14 / T065 | The final acceptance sequence below, run on one stable candidate. | REL01–06 |

## Final acceptance sequence (T065)

Run this from the repository root on the intended candidate only, with a clean committed tree. Each step is detailed in the [runbook](bootstrap-runbook.md). Package `validation.json` keeps `releaseAcceptance=NotClaimed` until every REL item has evidence.

**1. Build the candidate.**

```powershell
./Tools/Compile-GameContent.ps1 -Native
./Tools/Build.ps1 -Target Client
./Tools/Package-Release.ps1 -ClientBuildRunDirectory <clean-client-build-run> -RequireNativeNav [-PreviousPackageDirectory <prior>]
./Tools/Build-Installer.ps1 -PackageDirectory <package run> -ReleaseVersion <n.n.n> -PreviousInstallerReport <installer-report.json>
```

**2. Install and upgrade.** Install, and upgrade over the previous version. Run INS01–INS06 and REL01.

**3. Reviews and play.** Run the named reviews (FIX03, FIX05–FIX11) and the REL02 matrix on the installed game. Use the [playtest guide](playtest-guide.md).

**4. Reference performance (REL04).** Run this on the idle reference laptop:

```powershell
./Tools/Benchmark-Product-Matrix.ps1 -BuildRunDirectory <same-build-run> -SensorLog <csv> -HardwareEvidence <proof-json>
./Tools/Benchmark-Soak.ps1 -BuildRunDirectory <same-build-run> -Workload <soak-id> -HardwareEvidence <proof-json>
```

Without the reference machine or its counters, the result is PendingHardware.

**5. Regression (REL05).**

```powershell
python docs/fixtures/verify_fixtures.py
python docs/verify_docs.py
./Tools/Test-SourceProvenance.ps1
./Tools/Test-Contracts.ps1
./Tools/Test-Generation.ps1
./Tools/Test-Simulation.ps1
./Tools/Test-Game.ps1
```

**6. Close out (REL06).** Record in the M11 handoff:
- the installer hash;
- the evidence inventory;
- every FAIL, Pending and PendingHardware result;
- the rollback pair;
- the reviewers.
