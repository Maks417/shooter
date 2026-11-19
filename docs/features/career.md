# Feature: career ranks and service record

[ADR-108](../decisions.md#adr-108--accepted-career-ranks-unlocks-buffs-and-local-service-record) adds an offline career. Every finished match earns career points (CP): the player's personal score, plus 500 for finishing, plus 1000 for a win (500 for a draw). Aborted or restarted matches earn nothing. CP set one of 30 military ranks (20 until [ADR-119](../decisions.md#adr-119--accepted-30-rank-career-ladder-and-four-new-perks)), and each rank above Private unlocks one reward: a gun, a sight or a small buff that applies to every firearm the player holds. Bots keep the full arsenal and never get buffs.

## Scale

Reaching rank *n* takes `1000·(n²−1)` CP, so each step costs 2,000 CP more than the last. At about 4,000 CP per match the player reaches Corporal after about 6 matches, First Sergeant after about 25, Captain after about 110, General after about 196 and General of the Armies after about 225 (roughly 55 hours). The ladder follows the US Army: Private to Sergeant Major of the Army (1–13), Warrant Officer 1 to Chief Warrant Officer 5 (14–18), Second Lieutenant to Colonel (19–24), Brigadier General to General of the Armies (25–30). The full table lives in [ADR-119](../decisions.md#adr-119--accepted-30-rank-career-ladder-and-four-new-perks); rewards in short:

- **Guns:** M16A4 and G17 from the start; M4A1 (Specialist), AK-102 (Corporal), Golden Eagle (Sergeant First Class), M870 shotgun (First Sergeant), AK-47 (Sergeant Major), Mk 16 (Warrant Officer 1), Mk 14 (Chief Warrant Officer 4), M24 SWS (First Lieutenant), M-TAC 338 (Lieutenant Colonel).
- **Sights:** iron sights from the start; collimator (Private 2nd Class), 2x prism (Sergeant), 4x (Sergeant Major of the Army), 8x (Second Lieutenant). Each optic arrives before the gun whose factory sight it is.
- **Buffs:** reload −8%/−15%, ADS spread −8%/−15%, recoil −8%/−15%, equip −20%, +1/+2 spare magazines, and since ADR-119 hip spread −8%/−15% (Hip Shooter), shotgun pellet spread −15% (Choke), bolt/pump cycle −8%/−15% (Slick Action) and move speed +3% with a gun out (Light Load). Damage never changes.
- **General of the Armies:** gold insignia only.

Saves store CP only, so an existing career is re-read on the 30-rank ladder; rewards that moved to a higher rank lock again until reached (accepted by the owner, no migration).

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

The career has shipped in installers since 0.3.22. The 30-rank ladder (ADR-119) is committed but not yet in an installer. The owner's in-game review is Pending ([next playable build](../next-playable-build.md)).
