# UI design system

This page holds the tokens and rules for menus and HUD. Art direction and quality gates are in [visual direction](visual-direction.md). Screen behavior is in the [game shell](features/game-shell.md). The UI code is mainly:
- `Presentation/Session/TacticalUi.cs`
- `ProductShellHost`
- `ProductMenuScene`
- `ProductSessionView`

## Principles

The interface is restrained and military: large clear headings, tactile equipment, natural light.

- **No imported UI art.** No image sprites, imported icons, logos or background art are used. Everything is drawn from layout rectangles, lines, analytic SDF shapes and generated meshes.
- **Readability over detail.** The UI favors believable proportions, physical material scale, restrained wear and clear silhouettes over noisy detail.
- **Team identity is never colour alone.** It is always paired with shape or text.
- **The HUD only reads.** HUD and preferences consume gameplay; they never change it.

## Tokens

| Token | Value | Use |
|---|---|---|
| surface | `#161C20` | Menus, plates |
| text | `#F1F3EF` | Primary text |
| muted | `#ADB7B7` | Secondary text |
| focus | `#E6C56A` | Keyboard/controller focus and selection |
| danger | `#E47B70` | Damage and enemy, always paired with shape or text |
| ally | `#7BC5D1` | Friendly marker, with icon or label |

**Shapes.** Shapes are SDF (`Shooter/UiSdf`, `UiShapeParams`, [ADR-084](decisions.md#adr-084--accepted-analytic-sdf-ui-primitives-and-ui-motion-model)):

| Shape | Radius |
|---|---|
| Plate | 4 |
| Button | 3 |
| Card | 6 |
| Chip | 2 |
| HUD plate | 3 |

| Effect | Value |
|---|---|
| Soft shadow | 3 px down, 10 px soft, 38% |
| Focus glow | 8 px, 28% |

Hover, focus and selection ease through `UiMotion`. Under reduced motion they snap. Under reduced flash there is no glint and the glow is softer.

**Icons.** Every glyph comes from the generated SDF atlas (`IconLibrary`, [ADR-085](decisions.md#adr-085--accepted-generated-sdf-icon-atlas-with-mesh-traced-weapon-silhouettes)). Weapon icons are silhouettes traced from the packaged weapon meshes. Faction emblems differ by shape as well as colour: a shield for allies, a hexagon for enemies. Rank insignia come from the same atlas (30 grades, [ADR-119](decisions.md#adr-119--accepted-30-rank-career-ladder-and-four-new-perks)).

**Typography.** Barlow Condensed (OFL, [ADR-066](decisions.md#adr-066--accepted-non-commercial-ui-typeface-bank-barlow-condensed)), loaded from `Presentation/Fonts/Resources`. The engine IMGUI font is the fallback. At 720p:

| Element | Weight | Size |
|---|---|---|
| Body and captions | Regular | 18 px |
| Buttons | SemiBold | — |
| Key ammo | — | 28 px |
| Display titles | Bold | 32–56 px (Bold from 22 px) |

**Layout.**
- Spacing is 4, 8, 12, 16 or 24.
- The minimum interactive target is 32×32 px.
- UI scale runs from 100% to 150%, with a 5% safe inset.
- Text contrast is at least 4.5:1, checked on the actual colours.

Text tints compensate for the Linear-colour IMGUI path.

## Screens and HUD

**Menu.** The menu is scene-led. A procedural staging scene renders on layer 12 to a 1280×720 target at up to 30 Hz, and only while the front end owns it. It has compact left navigation and operation cards rendered from the actual packaged map meshes. There are no colliders, navigation or live participants. Missing verified bytes show a repair message.

**Loading.** Loading uses an insertion briefing: map name, rules line, phase strip and an indeterminate activity bar. It never shows a guessed percentage.

**Controls and settings.**
- Tab or the arrow keys move focus.
- Enter or Space selects.
- Left and right adjust sliders.
- Escape is reserved for back and recovery.
- Settings groups are Audio, Video, Controls and Accessibility.

**Combat HUD.** All plates are opaque and compact. The HUD shows:
- the centred team score and clock;
- health, armor and ammo in the lower corners, with cartridge glyphs and a reload ring;
- damage-direction arcs;
- score badges and the kill ribbon ([ADR-059](decisions.md#adr-059--accepted-personal-scoreboard-score-and-combat-score-badges), ADR-073);
- the elimination feed, with weapon silhouettes and a headshot mark.

ADS hides the hip reticle.

**Minimap.** The minimap is a 150 px heading-up disc ringed by the compass ([ADR-104](decisions.md#adr-104--accepted-circular-heading-up-minimap-with-ring-compass)). It shows collision footprints, allies, and temporary enemy gunfire reveals within 50 m ([ADR-058](decisions.md#adr-058--accepted-minimap-self-marker-and-temporary-gunfire-reveal)). Silent enemies stay hidden.

## Accessibility

- Mouse sensitivity is independent.
- FOV runs from 70 to 100 vertical degrees and is labelled.
- Reduced camera motion and reduced flash are supported. Reduced flash suppresses muzzle, tracer and explosion flash and halves the damage-arc alpha.
- Every control is remappable.
- Team indicators never depend on colour.

Rifle ADS uses an analytic screen-space PiP optic ([ADR-065](decisions.md#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery)), not recursive render-to-texture optics. Settings persist in versioned user configuration.

**Acceptance** needs 720p captures of every screen at 100% and 150% scale, with:
- no clipping;
- verified contrast;
- keyboard navigation working;
- no prohibited assets;
- a named reviewer, build and seed.
