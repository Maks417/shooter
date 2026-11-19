# UI design system

This page is the token and layout spec for every player-facing surface: the app and installer icons, the main menu, loading, Armory, Career, Settings, pause, results and the HUD ([ADR-123](decisions.md#adr-123--accepted-unified-front-end-visual-system)). Art direction and quality gates are in [visual direction](visual-direction.md). Screen behaviour is in the [game shell](features/game-shell.md). The code is:
- `Presentation/Model/Style/UiTokens.cs`: colour roles (`UiTokens`), type scale (`UiType`), spacing and frame anatomy (`UiSpace`, `FrameKind`). It is pure, and this page mirrors it.
- `Presentation/Session/UiInk.cs`: the only place UI colours become Unity colours.
- `Presentation/Session/TacticalUi.cs`: frames, controls, header, footer, emblem and backdrop.
- `Presentation/Model/Menus/ShellLayouts.cs` and `CareerLayout.cs`: page layouts.
- `Presentation/Session/ProductMenuScene.cs` and `Presentation/Model/Menus/MenuVistaShot.cs`: the menu vista.
- `Generation/Core/Icons/BrandEmblem.cs`, `AppIcon.cs` and `InstallerArt.cs`: the emblem, app icon and installer art.

## Principles

The interface is restrained and military: condensed caps, hard-edged plates, thin amber rails, and the real battlefield behind the menus.

- **No imported UI art.** No sprites, imported icons, logos or background images are used. Everything is drawn from rectangles, lines, analytic SDF shapes, generated meshes and the packaged map geometry.
- **One system.** Every colour comes from a `UiTokens` role. `UiTokensTests` fails on a colour literal in the UI files. Every size comes from `UiType` or `UiSpace`.
- **Team identity is never colour alone.** It is always paired with shape or text: a shield for allies, a hexagon for enemies, and labels.
- **The HUD only reads.** HUD and preferences consume gameplay; they never change it.

## Colour roles

| Role | Value | Use |
|---|---|---|
| Shade | `#050809` | Scrims, underlays, card wells |
| Well | `#0A0E10` | Recessed wells, bar tracks |
| Base | `#12171A` | Shell floor, primary-button ink |
| Panel / PanelTop | `#1C2429` / `#232C31` | The plate gradient |
| Raised / RaisedHover | `#2A3439` / `#38444A` | Buttons and keycaps, hover |
| Edge | `#364248` | Rules and dividers |
| Text | `#F1F3EF` | Primary text |
| TextMuted | `#ADB7B7` | Secondary text |
| TextDim | `#768082` | Disabled and fine print |
| Accent | `#E6C56A` | Focus, selection, rails, brackets, the emblem |
| AccentBright / AccentDeep / AccentShadow | `#FFE69E` / `#C4A048` / `#966C2A` | Accent highlights, gradients and medal depth |
| Ally | `#7BC5D1` | Friendly, always with an icon or label |
| Enemy | `#E68074` | Enemy and damage, always with shape or text |
| HudPlate / HudPlateStrong | `#090C0E` at 50% / `#080B0D` at 65% | Translucent combat plates |

Career band metals, reward inks, medal enamels, blueprint lines, captions and diagnostic colours are further roles in the same file.

**Contrast.** `UiTokensTests` checks the following:
- Text, TextMuted, Accent, Ally and Enemy reach 4.5:1 on Base, Panel, PanelTop and Raised.
- Base reaches 4.5:1 on Accent and on AccentDeep, for primary-button ink.
- HUD text keeps 3:1 over a near-white scene behind a plate.
- Ally and enemy differ in luminance, not only in hue.

Text tints compensate for the Linear-colour IMGUI path (`TacticalUi.StyleInk`).

## Typography

Barlow Condensed (OFL, [ADR-066](decisions.md#adr-066--accepted-non-commercial-ui-typeface-bank-barlow-condensed)), loaded from `Presentation/Fonts/Resources`. Bold is used from 22 px up, SemiBold for buttons and Regular below 22 px. The engine font is the fallback.

| Step | Size (px at 100%) | Use |
|---|---|---|
| Display | 56 | Match banners, results outcome |
| Title | 32 | Page titles, headline, map name on loading |
| Heading | 22 | Panel titles, hero names |
| Lead | 18 | Buttons, setting rows, captions on the HUD |
| Body | 16 | Body copy |
| Small | 14 | Secondary lines |
| Caption | 12 | Eyebrows, section titles, footer |
| Micro | 10 | Tags and keycaps |

Single-line labels use `TacticalUi.LabelFit`, which steps the size down and then ellipsises. Text never spills out of its rect.

## Frames and controls

Every surface is one of four `FrameKind` weights. All have a 2 px radius, a vertical tone gradient, a 1 px top highlight, a hairline rim and a soft drop shadow:

| Kind | Fill | Extras | Used for |
|---|---|---|---|
| Plate | PanelTop → Panel, about 95% | optional amber rail or top rule | Content panels, fixed slots |
| Card | Well → Shade | inner media brackets | Map cards, media wells |
| Hero | Panel → Base, about 92% (the vista shows through) | amber corner brackets (top-left and bottom-right) | Briefings, the Armory hero, the Settings help pane |
| Modal | PanelTop → Base, opaque, deep shadow | amber corner brackets | Pause, confirm, recovery |

The flat fallback (no SDF shader) draws the same geometry without the soft shadow.

**Controls.**
- **Buttons.**
  - Default buttons are raised plates.
  - Primary buttons are an amber gradient with Base ink.
  - Quiet buttons are rail items.
  - Focus is one treatment everywhere: an amber left rail plus a soft amber glow, eased by `UiMotion`.
- **Sliders and switches.** Sliders are a recessed track with an amber fill and an ivory knob. Toggles are a pill switch with an ON/OFF label, the same in the flat fallback.
- **Keycaps.** Keycaps are a raised plate with a darker lip and condensed caps.

**Page chrome.**
- `Header` draws an amber tick and eyebrow, the title, optional context on the right, a full-width rule with an amber lead, and the emblem at the right.
- `KeyHints` draws keycap hints on the left and the status line on the right, above a hairline.
- `Section` draws an amber diamond, caption caps and a full-width rule.

**Shapes and effects.** Shapes are SDF (`Shooter/UiSdf`, `UiShapeParams`, [ADR-084](decisions.md#adr-084--accepted-analytic-sdf-ui-primitives-and-ui-motion-model)):
- The soft shadow is 3 px down, 10–12 px soft, 38–60%.
- The focus glow is 8 px at 28%.
- Under reduced motion, transitions snap. Under reduced flash there is no glint and glows are softer.

**Spacing.** Spacing is 4, 8, 12, 16, 24 or 32. The minimum interactive target is 32×32 px. UI scale runs from 100% to 150%, with a 5% safe inset.

## Brand emblem and icons

`BrandEmblem` is the single mark: a reticle ring crossed by four ticks, with an upward chevron inside, in Accent.
- **App icon.** `AppIcon` puts the emblem on a charcoal rounded tile (PanelTop to Shade, Edge rim), rendered natively at 16, 24, 32, 48, 64, 128 and 256 px. Small sizes thicken the strokes. `ShooterBuild.ConfigureAppIcon` bakes one texture per Standalone icon slot.
- **Installer.** `InstallerArt` writes the setup `.ico` (all sizes) and the wizard images: a charcoal panel with contour lines, an amber rail and the emblem.
- **In game.** `TacticalUi.Emblem` and `BrandMark` draw the same recipe with SDF strokes.

Every other glyph comes from the generated SDF atlas (`IconLibrary`, [ADR-085](decisions.md#adr-085--accepted-generated-sdf-icon-atlas-with-mesh-traced-weapon-silhouettes)). Weapon icons are silhouettes traced from the packaged weapon meshes. Rank insignia come from the same atlas (30 grades, [ADR-119](decisions.md#adr-119--accepted-30-rank-career-ladder-and-four-new-perks)).

## Screens

- **Main.** The live map vista fills the screen.
  - The squad stands on a rooftop overlook in the right third. Below them lies the focused map's real packaged geometry, under its sky gradient, fog and tier grade.
  - A scrimmed left rail holds the emblem lockup, the headline, navigation, the rank badge and the loadout.
  - Operation cards run along the bottom right. Hovering or focusing a card stages that map, with a 0.4 s cross-fade.
  - The layout is `MainMenuLayout`. The headline collapses to one line on short screens.
- **Setup, Armory, Career and Settings.** These pages use the same vista, dimmed 30–50%, and the same header and footer.
  - **Armory** is `ArmoryLayout`: slots, the weapon list, and a Hero panel with a large silhouette and stat bars over the sights.
  - **Settings** is `SettingsLayout`: a tab strip, the control column, a Hero help pane from 900 px wide, and four equal actions.
- **Loading.** Loading is a Hero briefing: the map card or blueprint, the map name, the rules, a phase strip and an indeterminate bar. It never shows a percentage. A plate below rotates field tips. Loading and Results keep the last vista frame, so the stage never renders during a match.
- **Pause and confirm.** These are Modal frames over a deep Shade scrim with a vignette.

**Combat HUD.** Plates are translucent HudPlate roles so the world stays readable, each with a 1 px edge. The HUD shows:
- the centred team score and clock;
- health, armor and ammo in the lower corners, with cartridge glyphs and a reload ring;
- damage-direction arcs;
- score badges and the kill ribbon ([ADR-059](decisions.md#adr-059--accepted-personal-scoreboard-score-and-combat-score-badges), ADR-073);
- the elimination feed, with weapon silhouettes and a headshot mark.

Sound captions sit on a HUD plate in the brand face. ADS hides the hip reticle.

**Minimap.** The minimap is a 150 px heading-up disc ringed by the compass ([ADR-104](decisions.md#adr-104--accepted-circular-heading-up-minimap-with-ring-compass)). It shows collision footprints, allies, and temporary enemy gunfire reveals within 50 m ([ADR-058](decisions.md#adr-058--accepted-minimap-self-marker-and-temporary-gunfire-reveal)). Silent enemies stay hidden.

## Accessibility

- Mouse sensitivity is independent.
- FOV runs from 70 to 100 vertical degrees and is labelled.
- Reduced camera motion stops menu drift, parallax and the vista cross-fade. Reduced flash suppresses muzzle, tracer and explosion flash and halves the damage-arc alpha.
- Every control is remappable. Escape is reserved for back, pause and recovery.
- Team indicators never depend on colour.

Rifle ADS uses an analytic screen-space PiP optic ([ADR-065](decisions.md#adr-065--accepted-screen-space-pip-rifle-scope-blurred-periphery)), not recursive render-to-texture optics. Settings persist in versioned user configuration.

**Acceptance** needs 720p and 1080p captures of every screen at 100% and 150% scale, with:
- no clipping;
- verified contrast;
- keyboard navigation working;
- no prohibited assets;
- a named reviewer, build and seed (VD7, REL03, UI02).
