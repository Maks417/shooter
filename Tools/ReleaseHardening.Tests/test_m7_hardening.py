#!/usr/bin/env python3
import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]


def test_stretch_report_pending_without_captures():
    with tempfile.TemporaryDirectory() as tmp:
        out = Path(tmp)
        script = REPO / "Tools" / "ReleaseHardening" / "stretch_60fps_report.py"
        proc = subprocess.run([sys.executable, str(script), "--out", str(out)], capture_output=True, text=True)
        assert proc.returncode == 0, proc.stderr
        report = json.loads((out / "stretch-60fps-report.json").read_text(encoding="utf-8"))
        assert report["baselinesRelaxed"] is False
        assert report["stretchResult"] == "PendingHardware"
        assert report["mandatory30FpsReleaseImpact"] == "none"


def test_deferred_ledger_forbids_unmeasured_claims():
    ledger = json.loads((REPO / "Tools" / "ReleaseHardening" / "deferred_scope_ledger.json").read_text(encoding="utf-8"))
    assert "unmeasured 60 FPS acceptance" in ledger["releaseClaimsForbidden"]
    assert any(i["id"] == "T025-projectiles-penetration" and i["status"] == "deferred" for i in ledger["items"])


def test_regression_matrix_runs():
    # Ensure docs integrity matches working tree before matrix (closer refreshes on doc edits).
    subprocess.run([sys.executable, str(REPO / "docs" / "verify_docs.py"), "--refresh-manifest"], cwd=str(REPO), check=True)
    with tempfile.TemporaryDirectory() as tmp:
        out = Path(tmp)
        script = REPO / "Tools" / "ReleaseHardening" / "regression_matrix.py"
        proc = subprocess.run([sys.executable, str(script), "--repo", str(REPO), "--out", str(out)], capture_output=True, text=True)
        assert proc.returncode == 0, proc.stdout + proc.stderr
        report = json.loads((out / "regression-matrix.json").read_text(encoding="utf-8"))
        assert report["result"] in ("PASS_WITH_PENDING", "PASS")
        assert any(g["id"] == "H01-clean-unity-build" and g["result"] == "Pending" for g in report["gates"])
        assert not any(g.get("result") == "FAIL" for g in report["gates"])


if __name__ == "__main__":
    test_stretch_report_pending_without_captures()
    test_deferred_ledger_forbids_unmeasured_claims()
    test_regression_matrix_runs()
    print("ok")
