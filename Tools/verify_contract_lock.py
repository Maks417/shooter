"""Verify the integrator-reviewed C0 source/fixture baseline; never refresh it implicitly."""
import hashlib
import json
from pathlib import Path
import re


def inventory(root, directory, suffixes):
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted((root / directory).rglob("*")) if p.is_file() and p.suffix in suffixes}


def verify(root):
    lock = json.loads((root / "contract-lock.json").read_text(encoding="utf-8"))
    if lock.get("schemaVersion") != 1 or lock.get("contractVersion") != "C0-v1" or lock.get("status") != "FrozenByIntegrator":
        raise ValueError("Unsupported or unfrozen C0 baseline")
    source = inventory(root, "UnityProject/Assets/Shooter/Contracts", {".cs", ".asmdef", ".meta"})
    fixtures = inventory(root, "docs/fixtures", {".json", ".bin"})
    for key, actual in [("sources", source), ("fixtures", fixtures)]:
        expected = lock.get(key)
        if not actual or not isinstance(expected, dict) or actual != expected:
            raise ValueError("C0 " + key + " differ from frozen baseline; review changes and migration before refreezing")
    version = (root / "UnityProject/Assets/Shooter/Contracts/Validation.cs").read_text(encoding="utf-8")
    if not re.search(r'const string Current = "C0-v1";', version):
        raise ValueError("C0 version constant differs from baseline")
    toolchain = hashlib.sha256((root / "toolchain-lock.json").read_bytes()).hexdigest()
    if lock.get("toolchainHash") != toolchain:
        raise ValueError("C0 toolchain differs from reviewed baseline")
    return {"result": "PASS", "contractVersion": lock["contractVersion"], "sourceFiles": len(source),
            "fixtureFiles": len(fixtures), "scope": "Frozen C0 identity only; not milestone, runtime or hardware acceptance"}


if __name__ == "__main__":
    print(json.dumps(verify(Path(__file__).resolve().parents[1]), indent=2))
