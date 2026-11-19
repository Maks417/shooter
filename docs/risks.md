# Risk register

Likelihood and impact are planning judgments. When a trigger fires, record the evidence and an ADR. Never quietly relax a requirement.

| Risk | L/I | Trigger | Mitigation | Owner |
|---|---|---|---|---|
| RSK01 Unconvincing generated soldiers | H/H | Named visual review fails P05 ([defect inventory](visual-defect-inventory.md)) | Constrained helmeted uniform body, less exposed anatomy; revisit scope with the owner. Never import art. | Character |
| RSK02 IK and skin deformation artifacts | H/H | Slide >5 cm, grip >2 cm or inversion in pose sweeps | Joint-specific topology and weights, clamped ranges, simpler motion | Character |
| RSK03 iGPU fill/bandwidth bound | H/H | GPU p95 >27 ms in product workloads | Cheaper shaders, fewer shadows and less overdraw, precomputed detail. Never a silent render-scale change. | Render |
| RSK04 Main-thread upload stalls | H/H | Frame >100 ms in combat or upload >2 ms | Offline compile, chunked uploads, shader prewarm, bounded pools | Content |
| RSK05 Cache or determinism drift | M/H | Same key gives different bytes | Named streams, canonical encoding, version bump, golden review | Content |
| RSK06 Collision/visual/profile divergence | M/H | Cover missing on Low, or a bullet passes through a wall | Canonical gameplay proxies, cross-profile hashes | World |
| RSK09 Memory growth | M/H | >3 GiB steady or a rising 30-minute trend | Shared arrays, bounded pools, released CPU copies | Content |
| RSK11 Unbounded visual ambition | H/H | New systems added before gates pass | Explicit non-goals and ADR priority | Integrator |
| RSK13 Asset-constraint leakage | M/H | An unexplained binary, photo or clip | Provenance audit, remove the source and regenerate | Integrator |
| RSK14 Misleading benchmarks | M/H | Editor numbers, wrong GPU or reduced workload | Release player on the reference iGPU, three warm runs, versioned workloads | Perf |
| RSK16 Tooling PASS mistaken for a usable game | H/H | Builder/smoke PASS reported as gameplay | Require the recorded T071 playtest and REL evidence | Integrator |
| RSK17 Bot starvation at 15v15 | M/H | Stale visibility or query starvation with 29 bots | TDM04–TDM05 full-roster fairness tests | AI |
| RSK18 Spawn traps and map imbalance | M/H | Exposed exits or recurrent spawn failure | `SiteRules` exposure rules plus a named route/spawn playtest | World |
| RSK19 Settings that do not apply | M/M | A setting saves, but the device or input does not change | SET02 real-device review | Client |
| RSK20 Clean build misses a dependency | M/H | A developer cache hides a packaging gap | Empty-cache compile and installed offline launch | Release |

The network risks (prediction, internet exposure) are parked with networking under ADR-076. Current status of findings is in [next playable build](next-playable-build.md).
