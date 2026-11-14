# M10 — Two maps, menus and settings — handoff and gate record

**Current disposition — 2026-11-14, reviewed at 59316d3:** M10 integration open: two-map package publication and shell/schema-2/settings/audio software landed in 59316d3; release catalog/native content delivery, editable applied bindings/accessibility and named UI/device validation remain (T067/T069). See the [current audit](../../product-state-audit.md), [next playable-build tasks](../../next-playable-build.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence below remain; no new coding lease is created.

## Latest-commit review — 59316d3

Shell, settings schema 2/editor/device applier and audio callback are committed in 59316d3. T067 still owns data-only catalog/actual visual closure/native nav and packaged recovery; T069 owns editable applied bindings and missing accessibility consumers. LocalPlayerInput still reads fixed keys, so binding count/store tests do not meet control acceptance. Named UI/display/audio reviews and 20 load/replay cycles remain pending.

## T059 / FIX08 — shell software (2026-11-14)

| Field | Record |
|---|---|
| Task | T059 / FIX08 (software focus) |
| Paths | `Presentation/Menus/`, `Presentation/Scoreboard/`, `Presentation/Session/ProductShellHost.cs`, `ShellSessionCoordinator.cs`, `IProductShellBackend.cs`; Bootstrap `FoundationEntry.StartProduct` (no auto-start) + `FoundationClientComposition` shell backend |
| Behavior | Launch → Main (no active match); Setup builds current catalog map/size once; cancellable Loading; Playing with Esc pause / Tab scoreboard; Paused Resume/Settings/Restart/Leave; immutable Results with Play again / Main menu; `ClearPresentation`/`BindVerifiedMap` on start/leave; cursor/focus owned by shell |
| Pure tests | `Tools/Test-Simulation.ps1 -Offline` PASS — 92 passed / 0 skipped (includes ShellNavigation, ShellSessionCoordinator, SetupSelection, ScoreboardTable/Layout) |
| Evidence | `Artifacts/m2-combat-harness/6329262446428f92617600d8526b13f9c90a358a/20261114T105702962Z-d82e4cab/` |
| FIX08 status | **Partial (software)** — automated shell/scoreboard policy green. Pending: 20 alternating product launches/replays, native720p 100%/150% UI pointer+keyboard review, full-match acceptance gated on T057–T058 |
| Limitations | Settings software path bound to ADR-036 schema 2; SET02 named device/15s Keep/Revert on real display Pending. Audio software path bound; named audible review Pending. No PlayerPrefs/CLI auto-start of the selected match |

### Settings / audio bind (T059+T061 → ADR-036)

- Settings UI → `UserSettingsEditor` Draft/Apply/Cancel/RestoreDefaults; display Keep/Revert + `DisplayChangeConfirmClock` (15s / focus loss).
- Store path: `Application.persistentDataPath/shooter-user-settings.json` via `FileUserSettingsStore` (schema-1 migrates; newer schema never overwritten).
- Audio: `ProductClientSettingsApplier` / `UserSettingsAudioBinding` → `SynthAudioMixer.SetChannelGains`; `ClearPresentation` / leave / abort detaches and stops voices.
- **Integrator:** Do not treat pure shell PASS as UI01–UI02 player acceptance. Alternate-load soak and 720p scale review still required for FIX08 closeout.

## Historical closeout record — 2026-11-13

Historical 2026-11-13 software closeout (retained; superseded for current product acceptance): **CLOSED (software)**, 2026-11-13. Two packaged maps (training-compound via CompoundPlanner; depot-yard via distinct DepotPlanner grammar), GameShellMenu, and FileUserSettingsStore landed. Full interactive polish / named UI review remain Pending.

| Field | Historical recorded disposition |
|---|---|
| Tasks | T044–T047 |
| Catalog | StaticMapCatalog release seed with planner-aligned spawns |
| Depot | `DepotPlanner` linear bay/dock grammar (seed 460072031) |
| Shell | `GameShellMenu` map/size selection + PlayerPrefs relaunch |
| Settings | `FileUserSettingsStore` atomic schema=1 file store |
| UI01–UI02 / SET02 device | Partial / Pending named device review |

[Release roadmap](../../playable-release-plan.md) · [Previous](../m9-team-deathmatch/index.md) · [Next](../m11-playable-release/index.md).
