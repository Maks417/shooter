# Quality profiles, budgets and measurement

All numbers below are engineering allocations and release gates to validate, not measured results or FPS guarantees. Reference is Ryzen 5 4600H Radeon iGPU, 16 GB DDR4-3200, two slots used, NVMe. Log channel mode rather than assuming it. Windows x64, D3D11, AC power, documented power plan, driver, thermal state and resolution must accompany results. Unity Editor numbers are diagnostic only.

## Profile table

| Control | Low reference | Balanced probe | High probe |
|---|---|---|---|
| Internal render | 1280×720; scale 1.0 for acceptance | 1600×900 | 1920×1080 |
| URP path | Forward | Forward initially | Forward initially; compare Forward+ in spike |
| Light/shadow | 1 directional shadow, 2 cascades, 40 m, 1024 atlas | 60 m, 2048 | 100 m, 2048 |
| Additional lights | ≤2 per object, no shadows | ≤4, no shadows | ≤4; local shadow requires budget proof |
| MSAA / post | off, simple color grade; FXAA optional measured variant | 2× MSAA if proven | 2×; advanced post opt-in |
| AO / SSR / volumetrics | off / off / off | cheap AO only after gate | experimental; separately budgeted |
| Texture layer resolution | 256 typical, 512 hero cap | 512 typical | 1024 hero cap |
| Anisotropy / normal detail | 2× / near only | 4× | 8× |
| Geometry / rig | aggressive LOD, ≤4 weights/vertex | more near detail | more near detail |
| Cosmetic distance | 80 m; core silhouettes to 200 m | 120 m | 200 m |

These settings are source-defined profiles, not reliance on current Unity defaults. Implement renderer version checks. SRP Batcher and conventional instancing are initial candidates; GPU Resident Drawer is not baseline because compatibility and actual benefit depend on pinned URP/render path. See [references](references.md). No automatic high preset based only on device name. Defaults Low; user chooses higher and can run benchmark. Changing quality never regenerates gameplay content.

Dynamic resolution/upscaling can be a recovery feature after fixed-resolution gate; it cannot mask a failed native-720p acceptance test. UI stays at output resolution. Switch profiles via preloaded verified variants; shader/texture loading is asynchronous and profile change may show a loading notice outside active combat.

## Runtime envelope at Low

| Resource | Initial allocation / ceiling | Notes |
|---|---|---|
| Frame time | p95 ≤33.33 ms, p99 ≤50 ms | Sustained replay; separate hitch rule below |
| GPU | p95 ≤27 ms | Headroom for display/driver variance |
| Main thread | p95 ≤12 ms | Not added to GPU; overlap is expected |
| Render thread | p95 ≤8 ms | Measure batching wins |
| Simulation | mean ≤4 ms/tick, p95 ≤6 ms | 60 Hz even with 30 Hz display |
| AI within simulation | mean ≤1.5 ms, p95 ≤2 ms/tick | Eight bots; planning staggered |
| Visual triangles | ≤600k visible including characters | Shadow pass cost tracked separately |
| Draw calls / SetPass | ≤300 / ≤80 | Diagnostic ceilings, not substitute for timings |
| Total live character triangles | ≤160k | Player arms plus eight mixed-LOD enemies |
| Generated texture residency | ≤192 MiB | Includes mip chains/arrays; format report |
| Mesh+rig CPU and GPU buffers | ≤128 MiB | Count duplicate readable copies |
| Render targets | ≤96 MiB | Capture actual formats/MSAA |
| Gameplay+nav+collision | ≤256 MiB CPU | Chunk residency bounded |
| Client private committed bytes | ≤3 GiB steady, ≤4 GiB loading peak | Measure OS counter; RSS also reported |
| Graphics allocations | ≤512 MiB target | Shared RAM: not added twice to process accounting |
| Steady gameplay managed allocation | 0 B/tick and 0 B/frame after warmup | Loading/UI changes measured separately |
| Main-thread streaming upload | ≤1 ms/frame p95 | Hard scheduling cap 2 ms, split payloads |
| Cold / warm first-playable load | ≤30 s / ≤10 s | Exclude first-ever shader build; test shipping cache |

Memory sub-budgets are allocations within total ceilings, not a proof they sum to OS usage. Measure shared graphics allocations and CPU copies; do not describe iGPU memory as separate dedicated VRAM. Discard Mesh CPU copies after upload unless required by tooling/collision; collision data has its own representation. Pools have explicit caps and overflow counters. No per-frame texture noise baking or large temporary RenderTextures.

60 FPS stretch means same 720p workload, p95 ≤16.67 ms, p99 ≤25 ms, GPU p95 ≤14 ms and main thread p95 ≤8 ms. Do not halve AI count or omit cover to claim it. Balanced/High have no reference-machine frame guarantee until measured on named hardware.

## Benchmark protocol from M0

Version three deterministic workloads: B01 empty renderer/material sweep; B02 256 m compound traversal with eight animated bot stand-ins, 12 building shells, 120 repeated props, one shadow sun, a rifle and pistol; B03 same environment with eight active bots, scripted shots at 10/s aggregate, 20 live capped impacts, smoke proxy and chunk transitions. Counts describe generated workload, not every object necessarily visible. B04 later repeats four-player+eight-bot network scenario with independent headless server. Each stores seed, build/content hashes, camera/input path, profile and expected counters. Fixed seed `460072030` is an initial decimal uint64 seed; additional seeds 1, 42 and 18446744073709551615 exercise boundaries.

Run release IL2CPP player on actual reference machine, verify selected GPU adapter, native 720p internal size, VSync off and uncapped for measurement, no deep profiler. Warm 60 s then capture 180 s; repeat three times after thermal stabilization. Log frame distributions, main/render/GPU timings, CPU/GPU utilization, clock/temperature where available, allocations, process memory, resident content, draw/triangle counts and gameplay workload counters. Present worst run and medians, not mean FPS alone. Time each frame with a monotonic clock; GPU timing availability is explicit. Missing GPU timing requires external supported capture, not invented values.

Gates: all three runs meet p95/p99 budgets, no frame >100 ms during warmed stable traversal, no more than one 50–100 ms hitch/minute, no steadily increasing memory over a 30-minute soak. Load/quality-switch hitches are separately measured and cannot be hidden in warmup if they occur during traversal. Run with ordinary documented background environment; temporary historical memory usage is irrelevant. Capture still images for visual regressions with a tolerance mask, not pixel-identical driver assumptions.

## Over-budget response order

Measure pass/thread first; then reduce shader samples/overdraw, shadows, visible density and render LOD. Batch compatible materials and share geometry. Move work to compiler, spread AI planning, limit upload bursts. Preserve collision/visibility volumes and bot thinking rules; any gameplay-load reduction changes benchmark version. If native 720p remains infeasible, record failed gate and revise visual scope with an ADR rather than declaring target achieved by upscaling.


## Exact workload definitions

New product workloads are **planned**, frozen by T035 and implemented before feature optimization: `TDM-5v5-v1` (one human + 9 bots / 10 actors) and `TDM-6v6-v1` (one human + 11 bots / 12 actors), each with distinct compound/depot map identities. Record exact recipe/content/seed, roster, input route, action schedule, duration, normal-AI or scripted mode and counters; do not replace historical B01–B04 or mutate their eight-bot fixtures. [M11 REL04](milestones/m11-playable-release/validation-acceptance.md) applies all original native720p Low30 main/render/GPU/frame/allocation/memory/hitch limits to each map/size, without multiplying CPU budget for more bots. Maximum shipped content ≤2 GiB applies to the combined release, not per map.

Three-run repeatable performance fixtures and normal-AI gameplay soak are separate evidence. Scripted fixed-shot workloads must declare any suppressed bot fire/health changes; ordinary team-match validation uses actual deaths/respawns and normal AI. The ≥30-minute normal-AI soak per map at 6v6 spans at least two 15-minute matches with replay and covers resource lifetime across the terminal/menu boundary. Missing native counters or reference-machine access remains PendingHardware. Today's B01 capture wrappers do not already support these team workloads: new route/counter/telemetry adapters are T035/T043/T048 deliverables, not parameters to pass to unsupported commands.

[Benchmark fixtures](benchmark-fixtures.md) and its JSON define B01-minimal-v1 at M0, B01-materials-v1 at M1 and B02/B03 placement/routes/counters. B03 fixed-shot performance mode explicitly suppresses bot firing and keeps actor health; normal-AI soak separately validates playable combat. Never present these different scenarios as identical. Benchmark results use nearest-rank percentiles, null unavailable metrics and explicit workload version.
