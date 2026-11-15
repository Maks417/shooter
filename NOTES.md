# T077 HUD polish (visual/t077-hud)

## Visual changes
- Combat plates: translucent charcoal + hairline edges (`TacticalUi.CombatPlate`)
- Health: large HP number, 5-segment dual-layer bar, warn (≤40 amber) / danger (≤25 coral), edge vignette (honors ReducedFlash)
- Ammo: mag-dominant hierarchy, tactile reload progress caret
- Compass: 15° ticks, stronger cardinals under score strip
- Minimap/squad: denser list with ally rail, ally facing ticks (friendly-only)
- Kill feed: fade + YOU killer/victim tint rails
- Reticle: thinner arms, center gap/dot; ADS still hides hip reticle
- Shell: denser Main rail; compact score/clock strip

## Files
- `Presentation/Hud/CombatHudVisual.cs`, `KillFeedText.cs` (+ metas)
- `Presentation/Session/ProductSessionView.cs` (HUD draw methods only)
- `Presentation/Session/TacticalUi.cs`, `ProductShellHost.cs`
- Tests under `Tools/Simulation.Tests/Hud/`

## Merge note
Leave `ProductSessionView` sun/FX / actor presentation regions untouched; expect conflicts only if another branch edits the same HUD draw methods.
