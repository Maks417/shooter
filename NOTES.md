# T077 HUD polish (visual/t077-hud)

## Visual changes
- Combat plates: translucent charcoal + hairline edges (`TacticalUi.CombatPlate` / `Edge` / `AccentBar`)
- Health: large HP number, 5-segment dual-layer bar, warn (≤40 amber) / danger (≤25 coral), edge vignette (honors ReducedFlash)
- Ammo: larger mag digits, clearer reserve, edged reload progress bar
- Compass: 15° ticks, stronger cardinals under score strip
- Minimap/squad: stronger building contrast, thin border, quieter squad chrome with +/- alive/dead (friendly-only)
- Kill feed: fade + left YOU killer/victim tint rails
- Reticle: refined stroke + center gap; ADS still hides hip reticle
- Shell: compact BFV score/clock strip
- Product sun: Bodycam daylight (~1,0.92,0.82 @ 1.45, Euler 48/-35); soft ambient for GeneratedSky trilight
- Menu stage: warmer key + denser apron bounce (StageLayer isolation preserved)

## Files
- `Presentation/Hud/CombatHudVisual.cs`, `KillFeedText.cs` (+ metas)
- `Presentation/Session/ProductSessionView.cs` (HUD draw + sun/ambient)
- `Presentation/Session/TacticalUi.cs`, `ProductShellHost.cs`, `ProductMenuScene.cs`
- Tests under `Tools/Simulation.Tests/Hud/`

## Merge note
Sun/ambient and menu lights are intentional for this slice; expect conflicts if atmosphere workers edit the same Initialize lighting block.
