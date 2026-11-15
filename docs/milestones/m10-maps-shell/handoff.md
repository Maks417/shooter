# M10 — Two maps, menus and settings — handoff and gate record

## T074 — tactical shell and HUD, stage 1

The owner's screenshot clarification supersedes the flat Main with a live procedural menu scene and actual map cards, plus a compact compass/minimap/squad/ammo/elimination HUD. The clean 0.3.1 installer uses source b531da9; EditMode 29 / PlayMode 16 and Client/Server builds pass. [Exact identities and limits](../m11-playable-release/handoff.md#t074--scene-led-menu-and-tactical-hud-preview) record the interrupted final Computer Use review. Named visual/device/full-game acceptance remains Pending. Existing soldier/environment fidelity is not accepted by this interface pass.

Owner assigned implementation on 2026-11-14, beginning with menus/HUD and using Battlefield/Call of Duty/Warface/Bodycam as references. [Brandbook](../../brandbook.md) records the original procedural direction and later visual stages. This stage changes only the client presentation and related docs: `Presentation/Session/TacticalUi.cs`, `Presentation/Session/ProductShellHost.cs` and `ProductSessionView.cs`. No gameplay, map bytes, generator identities, settings schema, contract lock, toolchain or installer identity changes.

Main/local setup/loading/pause/confirms/settings/live and final team tables use the shared charcoal/amber design, applied scale, 5% safe area, keyboard focus and grouped scrollable settings. The HUD adds backed health/ammo, formatted team score/time, authoritative reload progress, empty-ammo hint, respawn feedback and ADS-aware analytic reticle. Binding capture skips its initiating frame. Existing display Keep/Revert and persistent preferences remain in their services.

Initial dirty-workspace verification at base `9f2219f`: EditMode **28/28 PASS** (`Artifacts/m0-foundation/9f2219f9d67a91c5103decdf5ea6bd0958eb0573/tests-editmode-20261114T210613272Z-7f73f778/`); provenance **676 entries / 338 GUIDs PASS**; fixtures **35 assertions / 17 JSON files PASS**; documentation verification PASS. The sandboxed editor first crashed in Mono's internal socket initialization (`tests-editmode-20261114T210422584Z-e2a35f08/`); retained rerun used the locked editor outside that sandbox. These are software checks, not named visual/device acceptance. Clean-build/player evidence follows in the [M11 handoff](../m11-playable-release/handoff.md). P05 FAIL, manual full-match/reference/soak and formal M10/M11 acceptance remain open.

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M10 integration validation remains open. Persisted catalog/visual packages, shell/schema-2/settings/audio and T069 editable applied bindings/accessibility are built into `7cd7e81`. Native-navigation publication and named map/UI/control/display/audio/20-cycle player review remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.


## T072 slide delivery

Source 5eaced5 is built and packaged; clean Game 56, Unity EditMode 26 and PlayMode 13 pass. The [M11 delivery record](../m11-playable-release/handoff.md#t072--slide-enabled-playtest-delivery) and [guide](../../playtest-guide.md) identify the current slide preview. Actual named UI/device and full gameplay review remain Pending; final Windows installer/upgrades are planned T073.

## T072 slide input/settings extension

ADR-039 adds a rebindable Slide action (F + applied Sprint, default Left Shift + F), legacy schema-2 normalization that preserves customized F, and copied authority stance/eye/slide presentation. M8/M11 handoffs record exact tests/build limitations. The old 7cd7e81 archive has no slide; named local device/interactive slide validation remains Pending.

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

## T075 presentation follow-up — 2026-11-15

Three owner-authorized agents are integrated and temporary worktrees removed. Source `7407bc4` adds procedural map surface publication/binding, menu/card rendering and analytic near-field equipment handling. [Canonical delivery, verification, migration and Pending evidence](../m11-playable-release/handoff.md#t075--procedural-presentation-preview-034). No milestone or P05 acceptance is changed.
