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


def _cli(root, refreeze_fn, verify_fn):
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refreeze", metavar="ADR", help="record a reviewed change approved by this ADR (T195)")
    args = parser.parse_args()
    print(json.dumps(refreeze_fn(root, args.refreeze) if args.refreeze else verify_fn(root), indent=2))


def _record(lock, decision):
    if not decision or not decision.startswith("ADR-"):
        raise ValueError("A refreeze names the ADR that approved it")
    decisions = lock.setdefault("refreezeDecisions", [])
    if decision not in decisions:
        decisions.append(decision)
    import datetime
    lock["reviewDate"] = datetime.date.today().isoformat()


def refreeze(root, decision):
    """Reviewed refreeze: the C0 sources/fixtures/toolchain as they are now, recorded under the approving ADR."""
    path = root / "contract-lock.json"
    lock = json.loads(path.read_text(encoding="utf-8"))
    _record(lock, decision)
    lock["sources"] = inventory(root, "UnityProject/Assets/Shooter/Contracts", {".cs", ".asmdef", ".meta"})
    lock["fixtures"] = inventory(root, "docs/fixtures", {".json", ".bin"})
    lock["toolchainHash"] = hashlib.sha256((root / "toolchain-lock.json").read_bytes()).hexdigest()
    path.write_text(json.dumps(lock, indent=2) + "\n", encoding="utf-8", newline="\n")
    return verify(root)


if __name__ == "__main__":
    _cli(Path(__file__).resolve().parents[1], refreeze, verify)
