"""Verify the integrator-reviewed Game-v1 source/fixture baseline."""
import hashlib
import json
from pathlib import Path
import re


def inventory(root, directory, suffixes):
    base = root / directory
    if not base.exists():
        return {}
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(base.rglob("*")) if p.is_file() and p.suffix in suffixes}


def verify(root):
    lock = json.loads((root / "game-contract-lock.json").read_text(encoding="utf-8"))
    if lock.get("schemaVersion") != 1 or lock.get("contractVersion") != "Game-v1" or lock.get("status") != "FrozenByIntegrator":
        raise ValueError("Unsupported or unfrozen Game-v1 baseline")
    source = inventory(root, "UnityProject/Assets/Shooter/Game/Contracts", {".cs", ".asmdef", ".meta"})
    fixtures = inventory(root, "docs/game-fixtures", {".json"})
    for key, actual in [("sources", source), ("fixtures", fixtures)]:
        expected = lock.get(key)
        if not actual or not isinstance(expected, dict) or actual != expected:
            raise ValueError("Game-v1 " + key + " differ from frozen baseline; review changes and migration before refreezing")
    version = (root / "UnityProject/Assets/Shooter/Game/Contracts/GameValidation.cs").read_text(encoding="utf-8")
    if not re.search(r'const string Current = "Game-v1";', version):
        raise ValueError("Game-v1 version constant differs from baseline")
    toolchain = hashlib.sha256((root / "toolchain-lock.json").read_bytes()).hexdigest()
    if lock.get("toolchainHash") != toolchain:
        raise ValueError("Game-v1 toolchain differs from reviewed baseline")
    c0 = json.loads((root / "contract-lock.json").read_text(encoding="utf-8"))
    if c0.get("contractVersion") != "C0-v1":
        raise ValueError("Game-v1 freeze requires intact C0-v1")
    return {"result": "PASS", "contractVersion": lock["contractVersion"], "sourceFiles": len(source),
            "fixtureFiles": len(fixtures), "scope": "Frozen Game-v1 identity only; not milestone, runtime or hardware acceptance"}


if __name__ == "__main__":
    print(json.dumps(verify(Path(__file__).resolve().parents[1]), indent=2))
