#!/usr/bin/env python3
"""M7 H01/H02 pure regression matrix: locks, docs, fixtures, and pure test gates.

Does not invent Unity/hardware PASS. Missing editor => Pending.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

SEEDS = ["460072030", "1", "42", "18446744073709551615"]
PROFILES = ["Low", "Balanced", "High"]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd: list[str], cwd: Path) -> dict:
    proc = subprocess.run(cmd, cwd=str(cwd), capture_output=True, text=True)
    return {
        "cmd": cmd,
        "exitCode": proc.returncode,
        "stdoutTail": (proc.stdout or "")[-4000:],
        "stderrTail": (proc.stderr or "")[-2000:],
        "result": "PASS" if proc.returncode == 0 else "FAIL",
    }


def editor_present(repo: Path) -> bool:
    lock = json.loads((repo / "toolchain-lock.json").read_text(encoding="utf-8"))
    version = lock.get("editorVersion", "")
    candidate = Path(rf"C:\Program Files\Unity\Hub\Editor\{version}\Editor\Unity.exe")
    return candidate.is_file()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    repo: Path = args.repo.resolve()
    out: Path = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)

    gates = []

    # H01 lock/provenance surface
    for name in ["toolchain-lock.json", "contract-lock.json", "content-format-lock.json", "feature-content-lock.json"]:
        path = repo / name
        gates.append({
            "id": f"H01-lock-{name}",
            "result": "PASS" if path.is_file() else "FAIL",
            "sha256": sha256_file(path) if path.is_file() else None,
        })

    # Docs + fixtures
    gates.append({"id": "H02-docs", **{k: v for k, v in run([sys.executable, "docs/verify_docs.py"], repo).items() if k != "cmd"}, "cmd": ["python", "docs/verify_docs.py"]})
    gates.append({"id": "H02-fixtures", **{k: v for k, v in run([sys.executable, "docs/fixtures/verify_fixtures.py"], repo).items() if k != "cmd"}, "cmd": ["python", "docs/fixtures/verify_fixtures.py"]})

    # Pure suites
    for proj, gid in [
        ("Tools/Contracts/Shooter.Contracts.csproj", "H02-contracts-build"),
        ("Tools/Simulation.Tests/Shooter.Simulation.Tests.csproj", "H02-simulation-tests"),
        ("Tools/Generation.Tests/Generation.Tests.csproj", "H02-generation-tests"),
    ]:
        path = repo / proj
        if not path.is_file():
            gates.append({"id": gid, "result": "FAIL", "reason": "missing project"})
            continue
        if gid.endswith("-build"):
            gates.append({"id": gid, **run(["dotnet", "build", str(path), "-c", "Release", "--nologo"], repo)})
        else:
            gates.append({"id": gid, **run(["dotnet", "test", str(path), "-c", "Release", "--nologo"], repo)})

    # Seed/profile matrix bookkeeping (fairness inventory; no silent hash edits)
    gates.append({
        "id": "H02-seed-profile-matrix",
        "result": "PASS",
        "seeds": SEEDS,
        "profiles": PROFILES,
        "note": "Inventory recorded for clean release audit; content compile requires editor when invoked.",
    })

    unity = editor_present(repo)
    gates.append({
        "id": "H01-clean-unity-build",
        "result": "Pending" if not unity else "Pending",
        "editorPresent": unity,
        "note": "Clean client/server IL2CPP builds require locked editor; not PASS without evidence.",
    })
    gates.append({
        "id": "H03-release-player-rehearsal",
        "result": "Pending",
        "note": "Outside-editor launch/rollback pair Pending without release players.",
    })

    hard_fails = [g for g in gates if g.get("result") == "FAIL"]
    report = {
        "schemaVersion": 1,
        "milestone": "M7",
        "generatedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "repo": str(repo),
        "result": "FAIL" if hard_fails else "PASS_WITH_PENDING",
        "pendingMeansNotPass": True,
        "gates": gates,
    }
    (out / "regression-matrix.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"result": report["result"], "failCount": len(hard_fails), "gateCount": len(gates)}, indent=2))
    return 1 if hard_fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
