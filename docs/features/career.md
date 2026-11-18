# Feature: career ranks and service record

[ADR-108](../decisions.md#adr-108--accepted-career-ranks-unlocks-buffs-and-local-service-record) adds an offline career. Every finished match earns career points (CP): the player's personal score, plus 500 for finishing, plus 1000 for a win (500 for a draw). Aborted or restarted matches earn nothing. CP set one of 20 military ranks, and each rank above Private unlocks one reward: a gun, a sight or a small buff that applies to every firearm the player holds. Bots keep the full arsenal and never get buffs.

## Scale

Reaching rank *n* takes `1000·(n²−1)` CP, so each step costs 2,000 CP more than the last. At about 4,000 CP per match the player reaches Corporal after about 6 matches, First Sergeant after about 25, Captain after about 56 and General after about 100 (roughly 25 hours). The full table lives in the ADR; rewards in short:

- **Guns:** M16A4 and G17 from the start; AK-102 (Specialist), Golden Eagle (Sergeant), AK-47 (Sergeant First Class), Mk 14 (Sergeant Major), M-TAC 338 (Captain).
- **Sights:** iron sights from the start; collimator (Private 2nd Class), 2x prism (Corporal), 4x (First Sergeant), 8x (First Lieutenant). Each optic arrives before the gun whose factory sight it is.
- **Buffs:** reload −8%/−15%, ADS spread −8%/−15%, recoil −8%/−15%, equip −20%, +1/+2 spare magazines. Damage, fire rate and movement never change.
- **General:** gold insignia only.

## Where ranks appear

| Place | What it shows |
|---|---|
| Main menu | Layered rank emblem, rank name and segmented CP bar under the menu; click opens the Career page. |
| Armory | The same badge in the header; locked guns and sights with a lock tag and the rank emblem that unlocks them. |
| Career page | Two tabs ([ADR-114](../decisions.md#adr-114--accepted-corner-bob-motor-fix-nav-bake-that-respects-walls-and-a-tabbed-career-page), Q / E switch). **Progress:** hero band (large emblem, rank, CP bar, CP to the next rank, next reward card) and the horizontal progress line with one emblem node and reward card per rank. **Statistics:** active-perk chips and service-record tiles. |
| Tab board and debrief tables | Embossed insignia before every name. Bots show a seeded rank within three of the player's. |
| Debrief | CP earned with its breakdown, the bar filling with the gain lit, and on promotion a gold edge and the unlocked reward's icon. |

## Look (ADR-113)

[ADR-113](../decisions.md#adr-113--accepted-career-progress-line-layered-rank-emblems-and-reward-art) draws the career with procedural art only: rank emblems layer an enamel plate (enlisted shield, officer hexagon, general burst in a laurel wreath), a metal rim and the embossed insignia in the band's metal (olive brass, steel, bronze, gold); small sizes draw just the embossed insignia. CP bars are recessed grooves with a metal fill. Every reward has an icon: gun silhouettes traced from the weapon meshes, side-view sights, perk glyphs, dog tags for the starter kit and a crown for General. Reward cards are colour-coded by kind (weapon amber, optic cyan, perk green, insignia gold) and show locked, next or unlocked.

## Service record

`shooter-career.json` sits beside the settings and Armory files (schema `shooter.career.v1`, fixtures in `docs/career-fixtures/`). It stores CP, matches, wins, losses, draws, kills, deaths, assists, headshot kills, total and best score, best kill streak and active play time. Writes are atomic with a last-good copy; a corrupt file is quarantined and restored from the last good copy; a newer or unreadable file is never overwritten.

## Status

Pure lanes and Unity EditMode PASS in source after 0.3.21; the ADR-113 redesign is in source after that. ADR-114 splits the page into tabs on a pure layout tested for overlap at 720p–1080p and 100–150% UI scale. Unity PlayMode, client build, installer and the owner's in-game and visual review are Pending; see the [M11 handoff](../milestones/m11-playable-release/handoff.md).
