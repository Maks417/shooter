# Quality profiles, budgets and measurement

The numbers on this page are engineering allocations and release gates. They are not measured results or FPS guarantees.

The reference machine is a Ryzen 5 4600H with integrated Radeon, 16 GB DDR4-3200 in two slots, and NVMe, running Windows x64 on D3D11 and AC power. Every result records the power plan, driver, thermal state and resolution. Editor numbers are diagnostic only.

## Profiles

| Control | Low (reference) | Balanced | High |
|---|---|---|---|
| Internal render | 1280×720, scale 1.0 for acceptance | 1600×900 | 1920×1080 |
| Directional shadow | Soft, 2 cascades, 50 m, 2048 atlas ([ADR-116](decisions.md#adr-116--accepted-realistic-map-colour-texture-and-shadows-materialnoise-120)) | 60 m, 2048 | 100 m, 2048 |
| Additional lights | ≤2 per object, no shadows | ≤4, no shadows | ≤4; a local shadow needs budget proof |
| MSAA / post | Off; simple grade | 2× if proven | 2×; advanced post opt-in |
| AO / SSR / volumetrics | Off | Cheap AO only after gate | Experimental, separately budgeted |
| Texture layers | 256 typical, 512 hero | 512 | 1024 hero |
| Anisotropy | 2× | 4× | 8× |
| Geometry / rig | Aggressive LOD, ≤4 weights/vertex | More near detail | More near detail |
| Cosmetic distance | 80 m (silhouettes to 200 m) | 120 m | 200 m |

How profiles behave:
- **Defined in source.** Profiles do not rely on Unity defaults.
- **Low is the default.** The user can pick a higher tier, and nothing is chosen automatically from the device name.
- **Visuals only.** Changing quality never regenerates or changes gameplay content.
- **No masking a native-720p failure.** Dynamic resolution or upscaling may come later as a recovery feature, but never to hide a failed native-720p gate. UI always renders at output resolution.

## Low runtime envelope

| Resource | Ceiling |
|---|---|
| Frame time | p95 ≤33.33 ms, p99 ≤50 ms |
| GPU / main thread / render thread | p95 ≤27 / ≤12 / ≤8 ms |
| Simulation | mean ≤4 ms/tick, p95 ≤6 ms at 60 Hz |
| AI within simulation | mean ≤1.5 ms, p95 ≤2 ms/tick |
| Visible triangles / live characters | ≤600k / ≤160k |
| Draw calls / SetPass (diagnostic) | ≤300 / ≤80 |
| Generated texture residency | ≤192 MiB |
| Mesh and rig buffers / render targets | ≤128 MiB / ≤96 MiB |
| Gameplay + nav + collision | ≤256 MiB CPU |
| Private committed bytes | ≤3 GiB steady, ≤4 GiB loading peak |
| Graphics allocations | ≤512 MiB (shared RAM, not double-counted) |
| Steady managed allocation | 0 B/tick and 0 B/frame after warmup |
| Streaming upload on the main thread | p95 ≤1 ms/frame, hard cap 2 ms |
| Cold / warm first playable load | ≤30 s / ≤10 s |

Rules for the envelope:
- Pools have explicit caps and overflow counters.
- Mesh CPU copies are discarded after upload unless tooling or collision needs them.
- No texture is baked per frame.
- Integrated-GPU memory is never described as dedicated VRAM.

The 60 FPS stretch uses the same 720p workload with p95 ≤16.67 ms, p99 ≤25 ms, GPU p95 ≤14 ms and main thread p95 ≤8 ms. It may not be reached by cutting bots or cover.

## Workloads

**Product TDM workloads** ([ADR-037](decisions.md#adr-037--accepted-product-tdm-measurement-workloads-t063--fix12), with sizes from [ADR-101](decisions.md#adr-101--accepted-selectable-team-sizes-5--8--10--15)):
- `TDM-<size>-v1` for Old Town, `-depot-v1` for Harbour and `-oil-v1` for Oil Depot, each at 5v5, 8v8, 10v10 and 15v15.
- One `TDM-soak-15v15-<map>-v1` soak per map.

They are defined in `Game/Session/ProductTdmWorkloads.cs` and [`game-fixtures/tdm-workloads.json`](game-fixtures/tdm-workloads.json). The map IDs `training-compound` and `depot-yard` are kept for identity stability.

**Diagnostic workloads B01–B03.** Their routes, placements and counters are in [`fixtures/benchmark-workloads.json`](fixtures/benchmark-workloads.json):
- B01 is a renderer/material sweep.
- B02 is a historical compound traversal.
- B03 is a controlled combat load with suppressed bot fire.

They are never presented as product measurements. B04 (network) is parked under ADR-076.

A changed workload gets a new version ID. Workloads are never silently substituted.

## Protocol

**Setup.** Use the release IL2CPP player on the reference machine. Confirm the selected GPU adapter and the native 720p internal size. Turn VSync off, run uncapped, and do not attach a deep profiler.

**Capture.** Warm up for 60 s, then capture 180 s, three times after thermal stabilization. An optional recorded preconditioning phase may come before the warmup.

**Record:**
- frame distribution;
- main, render and GPU timings;
- utilization, clocks and temperatures where available;
- allocations, process memory and resident content;
- draw and triangle counts;
- workload counters.

**Statistics.** Percentiles are nearest-rank. A metric that is unavailable is `null` with a reason, never 0. Report the worst run and the medians, not mean FPS. The collector (v3 and later) reports render time with the conservative [ADR-016](decisions.md#adr-016--accepted-conservative-render-budget-proof-and-continuous-thermal-conditioning) method.

**Pass gates:**
- all three runs meet p95 and p99;
- no frame over 100 ms in warmed traversal;
- at most one 50–100 ms hitch per minute;
- no steady memory growth over a 30-minute soak.

**Commands:**
- `Tools/Benchmark.ps1 -Workload <id>` and `Tools/Benchmark-Series.ps1` for single workloads and series.
- `Tools/Benchmark-Product-Matrix.ps1` runs 5v5 and 15v15 per map, three runs each. It needs the build run and a live sensor log.
- `Tools/Benchmark-Soak.ps1 -Workload <soak-id>` runs one soak per map.
- `python Tools/test_tdm_benchmark_analysis.py` covers the collector negatives.

Without reference-machine proof, every result is PendingHardware.

## When over budget

1. Measure the pass or thread first.
2. Reduce shader samples and overdraw.
3. Reduce shadows, visible density and render LOD.
4. Batch compatible materials and share geometry.
5. Move work into the compiler, spread AI planning and limit upload bursts.

Collision and visibility volumes and bot rules stay intact. Any gameplay-load change bumps the workload version. If native 720p stays infeasible, record the failed gate and revise visual scope with an ADR.
