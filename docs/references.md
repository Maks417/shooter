# Sources and version-sensitive guidance

Reviewed 2026-11-11. These are primary Unity sources supporting architectural cautions, not evidence that this project is implemented or that a chosen package works on the minimum machine. Resolve exact installed versions at M0 and record version-specific URLs in toolchain lock notes. Current online documentation can describe later Unity 6 releases than the selected LTS.

| Primary source | What it supports / boundary |
|---|---|
| [Unity netcode packages](https://docs.unity.com/en-us/multiplayer/netcode/netcode) | Distinguishes GameObject-oriented networking from DOTS-oriented server authority/prediction; framework choice changes architecture |
| [Unity client anticipation, version 2.2](https://mp-docs.dl.it.unity3d.com/netcode/2.2.0/advanced-topics/client-anticipation/) | That NGO version describes anticipation without a full rollback/replay loop; verify selected version, do not assume turnkey FPS prediction |
| [Unity competitive multiplayer quickstart](https://docs.unity.com/en-us/multiplayer/quickstarts/competitive-quickstart) | Official server-authoritative NGO example and service involvement; does not establish this design's performance or service cost |
| [GPU Resident Drawer in URP, Unity 6000.5](https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer) | Documents render-path/compute/component constraints for that release; explains why this optimization is a version-gated experiment |
| [Unity 6 feature overview](https://docs.unity.com/en-us/engine/6000.5/manual/whats-new/unity6) | Documents Unity 6 dedicated-server and multiplayer feature context; use pinned package docs for implementation APIs |

Hardware configuration is supplied by user/reference conversation; no dual-channel inference or benchmark claim is made. Prior discussion's HDRP recommendation was superseded by integrated-GPU requirement. Transient screenshot memory consumption is excluded. Visual design, budgets, API proposals and milestones are engineering decisions in this set, not quotations or claims from external sources.


## Review closure sources

[PCG minimal C implementation](https://www.pcg-random.org/using-pcg-c-basic.html) provides the selected XSH-RR scheme and published example sequence. [Dedicated Server build guidance](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/dedicated-server/build) documents build subtarget and UNITY_SERVER; verify on the selected toolchain. Fixed gameplay defaults, schemas and benchmark routes are project design decisions, not claims from those sources.
