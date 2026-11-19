# Sources and version-sensitive guidance

These primary sources back particular architectural cautions. They are not evidence that this project works on the reference machine. Always check against the locked Unity version (`toolchain-lock.json`), because current online documentation can describe later Unity 6 releases.

| Source | What it supports |
|---|---|
| [GPU Resident Drawer in URP](https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer) | Its render-path and compute constraints are why it is a version-gated experiment, not the baseline |
| [Frame Timing Manager](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/manager) | GPU timings can arrive late or be missing, which is why PresentMon is used for external timing |
| [Editor command line](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor) | Batch-mode test and build invocation |
| [PCG minimal C implementation](https://www.pcg-random.org/using-pcg-c-basic.html) | The selected XSH-RR scheme and its published example sequence |

The hardware configuration was supplied by the owner. Engine and design choices are recorded in the [decisions](decisions.md). Networking sources are parked with networking under ADR-076.
