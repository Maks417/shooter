# Machine-readable fixture catalog

Specification fixtures for C0 and compiler integration, not production-generated assets or measured game results. Run `python verify_fixtures.py` from this directory using Python3.10+; script resolves its own location and writes no files. Requires standard library only. Future C# implementations must independently reproduce golden values and consume negative cases; these JSON files do not implement gameplay.

| Fixture | Role |
|---|---|
| [Recipe schema](recipe.schema.json) / [valid box](box-recipe.json) | Minimal numeric construction source; no manual vertices |
| [Invalid recipe](invalid-box-recipe.json) | Negative width; expected InvalidInput at parameters.widthMm |
| [Golden vectors](golden-vectors.json) | PCG32, canonical JSON, source key, named stream and GUID examples |
| [Gameplay defaults](gameplay-defaults.json) | Versioned motor/weapon/objective configuration |
| [Soldier recipe](soldier-recipe.json) | Procedural anatomy seed parameters |
| [Command schema](command.schema.json) / [command](command.json) | JSON diagnostic representation of InputCommand |
| [Invalid command](invalid-command.json) | Unknown button bits/out-of-range input; expected InvalidInput |
| [Full prediction state](prediction-state.json) | Own-player restoration example including both weapon slots |
| [Snapshot](snapshot.json) | Small finalized snapshot diagnostic fixture, not full prediction restore state |
| [Manifest schema](manifest.schema.json) / [manifest](manifest.json) | Tiny deterministic three-byte payload example |
| [Payload](codec-payload.bin) | ASCII abc, standard SHA256 vector; not geometry |
| [Workloads](benchmark-workloads.json) | Deterministic workload IDs, routes and shot schedule |
| [Result schema](benchmark-result.schema.json) / [unmeasured result](benchmark-result.json) | PendingHardware example; no fabricated timings |
| [Expected cases](expected-cases.json) | Named valid/invalid cases and diagnostic paths |
| [Verifier](verify_fixtures.py) | Reads fixtures, checks keys/types/ranges and independently computed vectors |

Schemas use JSON Schema2020-12 for interoperability. The standard-library verifier checks project constraints for bundled examples, not arbitrary full JSON Schema syntax; implementation CI must use a pinned standards-compliant JSON Schema validator on valid/invalid examples too. A schema pass cannot enforce every semantic relationship: finite floats, unit vectors, unique IDs, dependency order and content hashes require semantic validation. Every fixture is included in root integrity manifest. Update goldens only for reviewed intentional format/version changes.
