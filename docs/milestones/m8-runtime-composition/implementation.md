# M8 — Runtime composition and Game-v1 foundation — implementation order

**Current disposition — 2026-11-14 review from 84befb9 / a54ed83:** M8 integration validation remains open. T066 provenance/cancellation and T067 packaged catalog/visual loading are repaired; T068 authority-driven weapon/action views are landed in the retained `7cd7e81` candidate. Native-navigation publication/query semantics and actual RUN/player/repeated-life evidence remain open. See the [audit](../../product-state-audit.md), [remaining queue](../../next-playable-build.md), [playtest guide](../../playtest-guide.md) and [FIX acceptance](../../release-remediation-validation.md). Original requirements and historical evidence remain; this review creates no coding lease or waiver.

Original implementation/port requirements retained. Current implementation is partial; product acceptance is open. Read [entry](index.md), [Game-v1](../../game-session-contracts.md) and the authoritative feature specs before editing. Task IDs extend T001–T034 without renumbering historical tasks. Start only assigned work; integrate through main in dependency order.

## T035 — Freeze product boundary first

Historical T035 entry: the 51 missing metadata entries and Assets Markdown failure were repaired before f3d9954, where provenance passed and Game-v1 was frozen under ADR-034. At 59316d3 provenance instead fails on 19 new metadata entries. Preserve established GUIDs and actual source guards. Do not recreate the contracts; T052 lock repair and T053–T056 software landed in 59316d3; T066–T068 now address the current baseline/content/view gaps. Assembly/server isolation still needs source-matched player verification.

T035 already created/froze separate Game-v1 sources/fixtures/lock and compound TDM workload descriptions. Review coverage against current required semantics; FIX checks expose missing lifecycle/settings/publication/depot-workload behavior. Changes follow ADR/version/migration before consumers, without altering historical B01–B04 or assuming freeze means complete acceptance.

## T036 — Extract and compose real authority

Integrator extracts reusable actor lifecycle from CombatHarnessSimulation into Simulation/Session without changing C0 behavior, preserves the diagnostic adapter, and installs one real session/composition root. Feed local buffered input into ticks, wire movement/weapon/pose/damage and coherent view publications, pause/resume and epoch-safe cancellation. Separate product launch from existing diagnostic command-line routes.

## T037 — Compile and install the compound

World/content worker builds a genuine full compound dependency package and real Unity collision/nav runtime against frozen ports. Validate required content before Ready; follow actual native path corners. Keep diagnostic compilers explicit; complete the existing limited Compile-GameContent and Test-Maps wrappers through T055, with bounded failures and real content provenance.

## T038 — Connect the client path

Client worker supplies actual camera/mouse controls, generated weapon and soldier views, HUD and audio-device output; use generated content and font exception only. Bot state is fed from authority and commands enter the next tick. The existing eight-bot fixture is the M8 composition workload; TDM capacity belongs to M9.

## T039 — Run and review the vertical player

Load the actual compound in an IL2CPP client, move/aim/fire/reload, receive damage and observe bots following paths/acting, hear synthesized effects and restart/unload cleanly. Triage named P05 defects with live captures and bounded source fixes under version/provenance review; acceptance remains open until T049. Retain server isolation and unchanged diagnostic smoke.

## Merge and completion

Each task returns source changes, relevant test results, source/toolchain/content/seed/workload identity and a limitation record. Integrator handles shared files once, reruns affected integration checks, updates this pack's handoff and commits completed changes into main. A future-only path or wrapper in this plan must become real and tested before it counts as a deliverable.

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).
