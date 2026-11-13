# M8 — Runtime composition and Game-v1 foundation — handoff and gate record

Status: **IN PROGRESS** (software foundation started 2026-11-13). Not closed. Historical M0–M7 administrative closures and all FAIL/Pending reports remain intact. No inherited hardware waiver.

| Field | Current disposition |
|---|---|
| Owner / reviewer / base / final main commit | Cursor M8 integrator (bc-b005c261); base `5fb3e30`; see coordination `m8Start` |
| Tasks | T035–T039 |
| Game-v1 / C0 / content / toolchain / workload identities | **Game-v1 frozen** (ADR-034, `game-contract-lock.json`); C0-v1 unchanged; TDM-5v5-v1 / TDM-6v6-v1 / RUN-compound-vertical-v1 defined |
| Build / packaged content / executable paths and SHA256 | Diagnostic IL2CPP client + server builds + smoke PASS (FoundationEntry floor/box). Product OfflineGameSession not yet default Bootstrap composition |
| Pure / Unity / player / visual checks | RUN00 PASS; RUN01 PASS (13 Game-v1 tests); diagnostic Build/Smoke PASS; RUN02–RUN05 product path Pending/NotRun |
| Reference hardware / counters / thermal review | NotRun / PendingHardware |
| Scope limitations / rollback pair | Product still defaults to diagnostic empty-actor harness; P05 FAIL retained (ADR-024); no M8 close |
| Known entry blocker | Cleared for RUN00 (metadata + Assets markdown relocated) |

## Deliver before downstream assignment

| Artifact | Status |
|---|---|
| Game-v1 source/fixture/lock | Done (ADR-034) |
| Authority lifecycle adapter | Done (`Simulation/Session/HarnessActorLifecycle`, `Game/Session/OfflineGameSession`) |
| Team deathmatch pure reducer | Done (`Simulation/TeamDeathmatch/TeamDeathmatchMatch`) — M9 consumer |
| Packaged compound + real collision/nav | Pending (catalog seed + FakeMapRuntime for tests only) |
| Client input/camera/HUD/audio product path | Pending |
| Vertical product player review | Pending |
| Frozen workloads | Done in `docs/game-fixtures/tdm-workloads.json` |

## Task and acceptance ledger

| Task | Status |
|---|---|
| T035 — Freeze product boundary first | **PASS** (RUN00 + Game-v1 freeze) |
| T036 — Extract and compose real authority | **PASS** (pure OfflineGameSession + lifecycle; Bootstrap product switch Pending) |
| T037 — Compile and install the compound | Partial (StaticMapCatalog seed; real Unity install Pending) |
| T038 — Connect the client path | NotRun |
| T039 — Run and review the vertical player | Partial (diagnostic smoke PASS; product vertical NotRun) |

| ID | Result | Evidence |
|---|---|---|
| RUN00 | PASS | Provenance after meta repair; `docs/notes/procedural-viewmodel-editmode.md` |
| RUN01 | PASS | `Tools/Test-Game.ps1 -Offline` → 13/13; Artifacts under `Artifacts/m8-runtime-composition/` |
| RUN02 | Pending | Real map adapter not installed in player |
| RUN03 | Pending | Product gameplay composition not default |
| RUN04 | Partial | Diagnostic client/server smoke PASS; product pause/lifecycle Unity Pending |
| RUN05 | Pending | Product captures / P05 live triage Pending |

### Diagnostic player evidence (not product acceptance)

- Configure-Unity PASS
- Build Client PASS: `Artifacts/m0-foundation/5fb3e30.../build-client-20261113T222325743Z-5c6c490f`
- Build Server PASS: `Artifacts/m0-foundation/5fb3e30.../build-server-20261113T222806904Z-ae36c994`
- Smoke Client PASS: `Artifacts/m0-foundation/5fb3e30.../smoke-client-20261113T222953886Z-0a20692f`
- Smoke Server PASS: `Artifacts/m0-foundation/5fb3e30.../smoke-server-20261113T223006585Z-87e4c008`

[Release roadmap](../../playable-release-plan.md) · [Shared documentation](../../index.md) · [Previous pack](../m7-hardening-stretch/index.md) · [Next pack](../m9-team-deathmatch/index.md).
