# M0 — Foundation — handoff and gate record

Status: **IN PROGRESS / PENDING UNITY INSTALLATION**, 2026-11-11. Independent contract work is implemented and tested. No milestone sign-off, C0 freeze, Unity build or hardware acceptance is claimed. See [acceptance](validation-acceptance.md) and [entry](index.md).

| Field | Value to fill at execution |
|---|---|
| Owner / integrator / reviewer | Codex, one integrator; no parallel workers; reviewer unassigned; ownership in root `coordination.json` |
| Source commit and upstream tag | Docs baseline `55cfe5644a383e2c2bbcbdedacb1e600b7cf3b3e`; tested implementation committed as `e7838a9fde3dd0202b173980a533a0e0b0ed163f`; no accepted milestone tag |
| Editor/URP/toolchain hash | No editor found; no editor/package/toolchain lock invented. SDK 10.0.301 pinned in `global.json`; Unity compiler/import unverified |
| Contract/schema/generator/protocol versions | C0-candidate.1; prediction schema 1; specification/fixtures v1.1 unchanged; no shipping generator or transport format frozen |
| Seed/content/world/workload manifests | Existing seed 460072030 specification vectors only; no generated shipping content/world/B01 evidence |
| Client/server build artifacts | Pending editor, URP and Windows IL2CPP/Dedicated Server module selection plus license/build verification |
| Test results and benchmark evidence | 47/47 pure C# tests pass, zero skipped; 6/6 full Draft 2020-12 schema cases pass; 35 standard-library fixture assertions pass; source-only provenance and documentation checks pass. Benchmark pending |
| Hardware/power/driver details | Not measured; no reference-machine claim |
| Accepted ADRs and known issues | Existing ADRs unchanged; no normative units/enums/seeds/schema changes. Pending integration and environment issues below |
| Gate status / reviewer / date | M0 PENDING, 2026-11-11; not ready for parallel feature integration; reviewer sign-off absent |

## Step status

- [ ] F01: Git baseline/policy and SDK pin done; Unity installation/selection, packages, license and real client/server toolchain verification pending.
- [ ] F02: independent candidate implemented and pure tests pass; freeze awaits F01, Unity compilation and adapter/composition acceptance. Do not assign dependent workers yet.
- [ ] F03: not started; no scene, scheduler, shipping composition, Editor build entry points or real world/navigation adapters.
- [ ] F04: independent fixture/provenance tooling done; quality assets, B01 capture, full generated/package/font provenance and CI integration pending.

## Actual source and checks

`UnityProject/Assets/Shooter/Contracts` contains the no-engine asmdef plus validated IDs/math/enums, wrap-aware serial rules, physics/nav results and bounded buffers, commands/network port buffers, snapshot/event/prediction DTOs, typed generation/content payloads and lease types. `Tools/Contracts/Shooter.Contracts.csproj` compiles these exact files as .NET Standard 2.1 with C# 9 and warnings as errors. Source `.meta` GUIDs are committed; no generated visual assets or caches exist.

`Tools/Contracts.Tests` pins xUnit 2.9.3, runner 4.0.1 and Microsoft.NET.Test.Sdk 18.11.0 with a committed dependency lock. Tests consume independent command/snapshot/prediction and hash/vector fixtures, reject nonfinite/invalid values, test serial/ordinal limits, overlap overflow including the 257 sentinel, path capacity/stale/cancel consumption, eight held snapshot/event buffers, content cancellation isolation, payload copying/backpressure and typed content limits. Test-only synthetic world/nav/content/pool/queue adapters are outside Unity. They establish port conformance scenarios, not actual physics, simulation, content loading or network acceptance.

Fixture JSON codecs and C# reference PCG/canonical encoding are test-only. Production T005 generators, P02 geometry formats and N01 transport formats remain future tasks. No fixture values or binary payloads changed from the Git baseline. Text line endings follow root `.gitattributes`; documentation hashes were intentionally refreshed for source/status changes and LF checkout consistency.

Latest C# evidence directory:

`Artifacts/m0-foundation/55cfe5644a383e2c2bbcbdedacb1e600b7cf3b3e/20261111T193712678Z-6f28e6d4/`

Contains `run.json`, `contracts.trx`, portable `test-results.xml` and SDK/restore/test logs. The run explicitly records baseline HEAD plus dirty source, source-tree SHA256 `c2d769df11e64477af64af898b4ee009baf491c8639cb7c56eaa263da2c788d0` and fixture-tree SHA256 `e474640f6649945f5fd8f907b7338e6b9ca8f16806ecdc5b00286b84ea53c97f`. The tested implementation source was then committed as `e7838a9`; subsequent changes are documentation/handoff only. `Tools/Test-Contracts.ps1` succeeded with online locked restore and advisory checks enabled for all dependency levels.

Other local evidence:

- `Artifacts/toolchain-discovery/discovery.json` and `dotnet-info.txt`: discovery outside the sandbox; no registered editor or Hub configurations found in checked locations. Visual Studio 18 Community C++ tools and Windows SDK include versions 10.0.19041.0/10.0.26100.0 exist, but actual Unity builds are still required.
- `Artifacts/toolchain-discovery/schema-results.json`: jsonschema 4.26.0, all six cases and expected invalid paths pass. Wheel versions/hashes in `Tools/requirements-fixtures.lock`; repository-local CPython 3.12 environment only.
- `Artifacts/source-provenance/result.json`: present Assets tree has 28 source/metadata entries and 14 unique GUIDs; named three-byte codec fixture hash matches. No engine font is used or allowlisted yet.
- `docs/verification-report.json`: documentation scope only; `docs/fixtures/verify_fixtures.py` separately checks 17 JSON files and 35 assertions. Neither is a Unity/game test.

## Failures and pending prerequisites

The first sandboxed VSTest run aborted after its local testhost connection timed out (`Artifacts/initial-contracts/contracts.trx`). Running the suite outside the sandbox resolved it. The first sandboxed `dotnet --info` also failed service enumeration; the outside-sandbox discovery captured a successful full report. Initial sandboxed NuGet advisory access failed; the final wrapper run completed online audit-enabled restore. No test or requirement was disabled to claim the final C# result.

No Unity editor or Hub was found via PATH, standard installs, uninstall/Unity registry entries, Hub configuration locations, or an additional accessible search of Program Files, Job, app-data and alternate software directories. This does not certify every arbitrary filesystem location. Next integrator action is to locate or install an authorized supported Unity 6 LTS editor plus matching Windows IL2CPP/Dedicated Server modules, verify entitlement and real builds, then pin its exact revision and package versions. Do not open the partial source directory with an arbitrary editor and accept upgrades.

No `ProjectVersion.txt`, package locks, toolchain-lock, profile assets, Bootstrap scene, Unity test/build/content/benchmark wrappers or CI jobs exist yet. Their results are pending, never PASS. Generated-content provenance, font exception and actual Ryzen 4600H iGPU measurements also remain pending.

## Downstream handoff

Publish C0 fixtures, fake ports, build entry points and toolchain hash. WP-01 starts generation; WP-05 may start motor tests against fakes. Render/geometry/character workers consume frozen DTOs.

## Sign-off rule

Use PASS only when every required check has evidence. Use BLOCKED/PENDING with exact owner/dependency when required evidence is absent. Stretch/deferred work has separate status and cannot be represented as completed by the mandatory gate. Record measured differences from upstream baseline, data migrations, rollback tag and artifacts to retain. A reviewer signs the gate; an implementation agent cannot certify hardware it has not tested.
