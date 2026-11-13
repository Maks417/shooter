#!/usr/bin/env python3
"""M7 H04 60 FPS stretch report. Never relaxes 30 FPS baselines. Missing captures => PendingHardware/FAIL stretch."""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

THRESHOLDS = {
    "frameP95Ms": 16.67,
    "frameP99Ms": 25.0,
    "gpuP95Ms": 14.0,
    "mainP95Ms": 8.0,
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--captures", type=Path, nargs="*", default=[])
    parser.add_argument("--workload", default="B03-combat-v1")
    args = parser.parse_args()
    out: Path = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)

    captures = []
    for path in args.captures:
        p = Path(path)
        if p.is_file():
            captures.append({"path": str(p), "present": True})
        else:
            captures.append({"path": str(p), "present": False})

    stretch_pass = False
    disposition = "PendingHardware"
    detail = "No reference-machine native720p B03 stretch captures supplied; stretch cannot PASS."
    if len(captures) >= 3 and all(c["present"] for c in captures):
        disposition = "FAIL"
        detail = "Captures present but automated threshold evaluation is not claimed without parsed PresentMon series in this closer."
        stretch_pass = False

    report = {
        "schemaVersion": 1,
        "milestone": "M7",
        "kind": "60fps-stretch",
        "workloadId": args.workload,
        "identicalToMandatory30FpsWorkload": True,
        "baselinesRelaxed": False,
        "thresholds": THRESHOLDS,
        "requiredCaptures": 3,
        "captures": captures,
        "stretchResult": "PASS" if stretch_pass else disposition,
        "mandatory30FpsReleaseImpact": "none",
        "detail": detail,
        "generatedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    (out / "stretch-60fps-report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"stretchResult": report["stretchResult"], "baselinesRelaxed": False}, indent=2))
    # Stretch failure/pending does not fail the process for mandatory release tooling.
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
