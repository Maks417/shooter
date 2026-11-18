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
| Main menu | Rank insignia, rank name and CP bar under the menu; click opens the Career page. |
| Armory | The same badge in the header; locked guns and sights with the rank that unlocks them. |
| Career page | Current rank, CP to the next rank, the next reward, active buffs, the 20-rank ladder and the service record. |
| Tab board and debrief tables | Insignia before every name. Bots show a seeded rank within three of the player's. |
| Debrief | CP earned with its breakdown, the bar filling, and a promotion line naming the unlocked reward. |

## Service record

`shooter-career.json` sits beside the settings and Armory files (schema `shooter.career.v1`, fixtures in `docs/career-fixtures/`). It stores CP, matches, wins, losses, draws, kills, deaths, assists, headshot kills, total and best score, best kill streak and active play time. Writes are atomic with a last-good copy; a corrupt file is quarantined and restored from the last good copy; a newer or unreadable file is never overwritten.

## Status

Pure lanes and Unity EditMode PASS in source after 0.3.21. Unity PlayMode, client build, installer and the owner's in-game review are Pending; see the [M11 handoff](../milestones/m11-playable-release/handoff.md).
