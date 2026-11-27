# Visual direction and quality gates

The owner's target (2026-11-14) is a finished visual design as good as **Battlefield V, Bodycam and Warface**. That means perceived quality, coherence, physical credibility and presentation. Parity is **unproven**. Every VD exit below is Pending until there is real evidence and an owner decision. Current delivery status is in [next playable build](next-playable-build.md). UI tokens are in the [brandbook](brandbook.md). P05 soldier defects are in the [defect inventory](visual-defect-inventory.md).

## References

| Reference | Emphasize | Reject |
|---|---|---|
| Battlefield V | Scene-led menus, depth and composition, a readable world-first HUD, coherent place and character presentation | A flat contour-only shell, a HUD that dominates the frame, a menu that feels like a debug panel |
| Bodycam | Physical material response, natural exposure, environmental depth, grounded weapon/camera presence | Obligatory heavy shake, unreadable darkness, distortion that breaks aim truth |
| Warface | Clear soldier/equipment silhouettes, readable combat feedback, polished weapon handling | Mannequin anatomy, floating kit, invisible reload or magazine discontinuities |

Battlefield 6 and Call of Duty are secondary mood context only. References inform original fictional factions, kit and places. Screenshots and footage are review material only. They are never shipping textures, models, animation or menu backgrounds, and they are never committed under `Assets/`.

Comparison register:
- **Battlefield V:** the owner's screenshots, supplied 2026-11-14.
- **Bodycam:** [Steam media](https://store.steampowered.com/app/2406770/Bodycam/); exact frame IDs and timecodes are Pending owner supply.
- **Warface:** [Steam media](https://steamcommunity.com/app/291480/announcements/); exact frame IDs and timecodes are Pending owner supply.

Store evidence captures under `Artifacts/visual-design/<source>/<VD-id>/<run>/`.

## Binding constraints

- **Procedural by default.** Geometry, materials, rigs and motion are procedural, with the font exception and the licensed art track for soldier bodies and clips ([art boundary](vision.md#art-boundary), [ADR-124](decisions.md#adr-124--accepted-licensed-authored-art-track-characters-first)). There is no silent external art fallback.
- **Locked toolchain and Low gate.** Use the locked toolchain. The gate is native 720p Low at 30 FPS on the reference machine, and 60 FPS is a separate stretch ([performance](performance.md)).
- **Quality tiers never change gameplay.** They never change collision, navigation, visibility rules, hit volumes or weapon mechanics.
- **Accessibility wins over cosmetics.** Reduced-motion and reduced-flash preferences are authoritative for camera and FX cosmetics. Camera effects keep aim truthful.
- **P05 FAIL is retained** ([ADR-024](decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations)). Any new soldier-generator clearance work needs an explicit owner assignment.
- **Failure is reported, not hidden.** A gate that cannot be met within these constraints goes back to the owner as a concrete tradeoff. It never silently lowers the target, the roster or the resolution.

## Visual language

**World.** Old Town, Harbour and Oil Depot each read as their real place ([map layouts](features/tdm-map-layouts.md)). Surfaces read at physical scale: wear sits at edges and contact points, not as white-noise splat. Daylight keeps silhouettes readable in both sun and shade.

**Characters and kit.** Team identity is clear from silhouette and labels as well as the cyan/coral accents. Equipment sits on the body. Hands stay connected to weapons through fire, reload and switch.

**First person.**
- Tactile weapons with a visible magazine exchange.
- Rail-mounted sights ([ADR-107](decisions.md#adr-107--accepted-armory-loadouts-and-reusable-rail-mounted-sights)).
- Near-wall composition that does not hide targets.

**Interface.** Scene-led menus, opaque HUD plates and a circular heading-up minimap ([ADR-104](decisions.md#adr-104--accepted-circular-heading-up-minimap-with-ring-compass)). See the [brandbook](brandbook.md).

**Motion and FX.**
- Weighty locomotion.
- Bounded analytic shot, impact and smoke effects.
- Synchronized audio.
- Cosmetics never apply authoritative recoil a second time.

## VD0–VD9 quality gates

These gates sit inside M8–M11 ([acceptance](acceptance.md)). Performance is measured at every stage from VD1 onward. VD8 is the final consolidation, not the first optimization pass.

| ID | Deliverable | Exit evidence |
|---|---|---|
| VD0 | Approved art direction and comparison register (this page) | The owner approves the direction and names the exact Bodycam/Warface frames |
| VD1 | Representative art slice: building/cover cluster, material families, an equipped soldier, hands plus rifle, a short action sequence, the intended HUD | The owner judges it reference-comparable at native 720p Low, and full-roster reference evidence supports the Low budget |
| VD2 | Soldier body, uniform and equipment system with LODs | Approved neutral and live views for both teams. Pose, grip, clearance and hit-volume tolerances pass. P05 defects are recorded against the candidate. |
| VD3 | First-person hands, weapons, magazines and sights | Every weapon passes cold bind, hip/ADS, near-wall and LOD/socket checks, with approved close and player views |
| VD4 | Procedural locomotion and full weapon-action presentation | Uninterrupted clips cover fire → reload → ready and switches, every stance, slopes, slide and death/respawn, with no snapping or detached magazine |
| VD5 | Every catalog map at the VD1 environment standard | Near/mid/far and interior/exterior walkthroughs are approved. MAP02 route/spawn/collision checks and cross-quality gameplay equivalence pass. |
| VD6 | Materials, lighting, atmosphere and combat effects | Material turntables and bright/shadow/interior combat captures pass review. Effect caps and teardown are measured in the player. |
| VD7 | Final menus, loading, pause, settings, scoreboard, results and HUD | Named review of every screen at 720p, at 100% and 150% UI scale. UI/SET checks pass. |
| VD8 | Visual scalability on one stable build | REL04 passes on the reference machine with correct counters. Higher tiers never substitute for Low evidence. |
| VD9 | Comparative sign-off plus the final upgrading installer | The owner signs every review category. REL/FIX/INS are complete on the same source/content pair. |

## Review rubric

Review actual release-player output at native 720p Low first. Record higher-quality captures separately. Match scene purpose, subject distance and framing to the references, and record any unknown reference FOV or processing. No AI enhancement or upscaling may stand in for rendered quality.

| Category | Question for the owner |
|---|---|
| Menu/UI craft | Do the composition, hierarchy, typography and interaction feel as finished as the references? |
| Soldiers and equipment | Are proportions, silhouette, kit construction and deformation convincing at the reviewed distances? |
| Hands/weapons/actions | Do hands, sights, recoil, magazine exchange and switches form one credible continuous action? |
| Environment/material/light | Does each route feel like a real place, with believable surfaces, depth, scale and light? |
| Motion/feedback | Do movement, contacts, weight, camera response, effects and cues convince in uninterrupted play? |
| Whole-scene consistency | Do menu, world, characters, first-person art and HUD belong to one finished product at Low? |

Score each category on this scale:

| Score | Label |
|---|---|
| 0 | Missing |
| 1 | Blockout |
| 2 | Functional |
| 3 | Polished but below target |
| 4 | Reference-comparable |

Acceptance requires all of the following:
- a 4 in every category that applies to the gate;
- the owner's written rationale;
- passing technical exits.

Do not average away a weak category. Technical or AI self-review never replaces the owner's decision.
