# M10 — Two maps, menus and settings — handoff and gate record

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M10 integration validation remains open. Persisted catalog/visual packages, shell/schema-2/settings/audio and T069 editable applied bindings/accessibility are built into `7cd7e81`. Native-navigation publication and named map/UI/control/display/audio/20-cycle player review remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

## T069 — preferences / usability software

| Field | Record |
|---|---|
| Task | T069 / FIX09–10 preferences completion (software) |
| Paths | `Game/Settings/InputBindingEditor.cs`, `UserSettingsEditor` display persist; `Presentation/Input/BoundControlMap.cs`, `LocalPlayerInput`; `ProductShellHost` binding UI; `ProductSessionView.ApplyUserSettings`; `ProductClientSettingsApplier` resolutions |
| Behavior | Editable bindings with conflict swap/cancel; gameplay/scoreboard use applied map; Escape reserved; ReducedFlash/Subtitles/ReducedCameraMotion applied; resolution list from device modes; pending display not disk-persisted until Keep |
| Schema | ADR-036 schema 2 unchanged |
| Pure tests | `Tools/Test-Game.ps1 -Offline` UserSettings binding/apply/persist/confirm cases; EditMode `InputBufferTests.AppliedBindingsDriveActionLookupAndSampledGameplay` |
| FIX08–10 / SET01–02 | **Partial (software)** — Pending named 720p 100%/150% UI, 15s Keep/Revert on real display, audible output/gain/mute review, 20 load/replay cycles |

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

- Settings UI → `UserSettingsEditor` Draft/Apply/Cancel/RestoreDefaults + `InputBindingEditor` rebind/conflict; display Keep/Revert + `DisplayChangeConfirmClock` (15s / focus loss / restart rollback via confirmed-surface persist).
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
