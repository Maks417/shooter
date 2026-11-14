# Risk register and mitigation gates

Likelihood/impact H/M/L are planning judgments, not statistical forecasts. Owner roles map to [work packages](backlog.md). Escalate when trigger occurs; record evidence and new ADR rather than silently relaxing requirements.

| Risk | L/I | Trigger / early test | Mitigation and fallback | Owner / gate |
|---|---|---|---|---|
| RSK01 Unconvincing generated humans | H/H | M1 silhouette/pose review fails twice | Constrained helmeted uniform body, less exposed anatomy; retain procedural constraint, revisit visual scope | Character / M1 |
| RSK02 IK/skin deformation artifacts | H/H | Slide >5 cm, grip >2 cm, inversion in pose sweep | Joint-specific topology/weights, clamp ranges, reduce motion complexity | Character / M1–M2 |
| RSK03 iGPU fill/bandwidth bound | H/H | GPU p95 >27 ms B01/B03 | Cheaper shaders, shadow/overdraw reduction, precomputed detail; no silent render-scale target change | Render/perf / every gate |
| RSK04 Main-thread generation/upload stalls | H/H | Combat frame >100 ms or upload >2 ms | Compile offline, chunk uploads, prewarm shaders and bounded pools | Content/world / M3 |
| RSK05 Cache invalidation/determinism drift | M/H | Same key different bytes; hash divergence | Named streams, canonical encoding, version bump, quarantine outputs, golden review | Content / M1 |
| RSK06 Collision/visual/profile divergence | M/H | Visible cover absent in low or bullet passes wall | Separate canonical gameplay proxies; overlay review and cross-profile hashes | World/simulation / M2–M4 |
| RSK07 Network prediction complexity | H/H | Static motor correction p95 >0.1 m at test RTT | Early headless/command separation, replay motor, isolate transport; block M6 until resolved | Network / M5 |
| RSK08 Agent integration churn | H/M | Two owners change contract/settings or repeated broken merges | Single integrator, ownership leases, C0 fixtures, serialized shared changes | Integrator / all |
| RSK09 Memory/cache growth | M/H | >3 GiB steady, 30-min trend, >2 GiB shipped content | Shared arrays, bounded cache/pools, release CPU copies, fewer variants | Content/perf / M3–M7 |
| RSK10 Nav/terrain seam failures | M/H | Unreachable objective or chunk collision gap | Plan globally, shared border samples, prebuilt full-map nav initially | World / M3 |
| RSK11 Unbounded visual ambition | H/H | New systems added before gates pass | Explicit non-goals, measurable slice, separate ADR/backlog priority | Integrator / all |
| RSK12 Toolchain/package incompatibility | M/H | Editor/package mismatch, API variant fails headless | Pin toolchain, compile adapters, CPU bake fallback, upgrade in isolated branch | Integrator / M0 |
| RSK13 Asset constraint leakage | M/H | Unexplained binary/photo/clip, library imports visual data | Provenance audit and clean regeneration, remove source and rebuild affected artifacts | Integrator / CI |
| RSK14 Misleading benchmarks | M/H | Editor-only numbers, wrong GPU, reduced workload | Release player on actual iGPU, logs/hashes, three warm runs, workload versioning | Perf / M0+ |
| RSK15 Internet service/security scope | M/H | Public endpoints without auth/rate limits | N4 exposure gate, private/LAN first, supported encrypted transport/services | Network / M6+ |

## Unresolved implementation choices

| New release risk | Trigger / response | Owner / gate |
|---|---|---|
| RSK16 Closed tools mistaken for usable game | Product auto-start exists but renderer/audio/shell/lifecycle integration is incomplete; repair T053–T061 before acceptance | Integrator / M8 |
| RSK17 11-bot starvation/state drift | Single-target/eight-bot assumptions, reset sequences, stale visibility or fake reload; full-roster fairness and authority-state tests | AI / M9 |
| RSK18 Respawn traps and map imbalance | Unsafe team slots, all candidates exposed, recurrent spawn failure; native nav/LOS and normal-play route review | World/match / M9–M10 |
| RSK19 Menu/settings only exist as models | Pure layout serializes but devices/input/storage do not change; real UI/device/relaunch acceptance | Client / M10 |
| RSK20 Clean build omits a map/dependency | Developer cache hides packaging gap; empty-cache compile and extracted offline launch | Release / M11 |
| RSK21 Historical waivers silently inherited | Required player/P05/reference checks missing; preserve old reports and keep new exit Pending until actual evidence | Integrator / M8–M11 |

Editor/URP versions are already locked, not an unresolved T001 choice. M5 selected UTP but its remote package version remains pending locked-editor verification; offline M8–M11 does not depend on it. Soldier P05 remains FAIL under ADR-024; new live remediation/review is T039/T049. Actual reference driver/counters/thermal acceptance remains empirical. External sounds/fonts and public authentication stay outside this release. No temporary screenshot memory usage is a requirement. See [playable release review](playable-release-plan.md) for the new dependency order.

## Observed product defects — 2026-11-14

[Current audit B01–B10](product-state-audit.md) records remaining defects after committed T052–T064: provenance metadata, Generation cancellation, runtime catalog/visual generation, native navigation, view/settings and soak integration. [T066–T071](next-playable-build.md) assign remaining scope; [FIX01–FIX14](release-remediation-validation.md) retain evidence gates. Historical A01–A13 and their fixes remain recorded. M8–M10 integration/M11 acceptance are open; a fourth P05 clearance search still requires explicit owner scope under ADR-024.