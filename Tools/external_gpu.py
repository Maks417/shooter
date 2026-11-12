"""Validate process-targeted PresentMon per-present GPU measurements for the exact capture."""
import csv
import hashlib
import json
import math
from collections import Counter
from pathlib import Path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def gpu_samples(manifest_path, capture_path, frames):
    manifest_path = Path(manifest_path)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    meta = json.loads((capture_path / "capture.json").read_text(encoding="utf-8-sig"))
    if not frames or meta.get("collectorVersion") not in (2, 3) or not meta.get("timestampFrequencyMatches") or meta.get("stopwatchFrequency", 0) <= 0 or meta.get("nativeCpuFrequency") != meta.get("stopwatchFrequency"):
        raise ValueError("External GPU correlation requires verified native QPC clock identity")
    previous_end = None
    for frame in frames:
        lower, upper, end = (int(frame[key]) for key in ("lowerTimestamp", "upperTimestamp", "endTimestamp"))
        if not 0 < lower < upper < end or (previous_end is not None and upper != previous_end):
            raise ValueError("Incomplete/reordered frame QPC interval")
        if abs((end - upper) * 1000 / meta["stopwatchFrequency"] - float(frame["frameMs"])) > .00001:
            raise ValueError("Frame duration differs from its QPC interval")
        previous_end = end
    lock = json.loads((Path(__file__).parent / "presentmon-lock.json").read_text())
    if manifest.get("schemaVersion") != 1 or manifest.get("toolVersion") != lock["version"] or manifest.get("toolSha256") != lock["sha256"]:
        raise ValueError("Unrecognized external GPU tool identity")
    if manifest.get("captureSha256") != sha(capture_path / "capture.json") or manifest.get("traceExitCode") != 0:
        raise ValueError("External GPU capture identity or process result mismatch")
    launch = json.loads((capture_path / "player-launch.json").read_text(encoding="utf-8-sig"))
    if launch.get("processId") != manifest.get("processId") or launch.get("exitCode") != 0 or launch.get("executableSha256") != manifest.get("playerSha256"):
        raise ValueError("External GPU trace does not identify this player launch")
    csv_path, log_path = manifest_path.parent / "presentmon.csv", manifest_path.parent / "presentmon.log"
    if sha(csv_path) != manifest.get("csvSha256") or sha(log_path) != manifest.get("logSha256"):
        raise ValueError("External GPU source hash mismatch")
    text = log_path.read_text(encoding="utf-8-sig").lower()
    if any(marker in text for marker in ("events lost", "buffers lost", "events were lost", "buffers were lost", "overflowed present", "failed", "error:")):
        raise ValueError("External GPU trace reported errors or event loss")
    low, high = int(frames[0]["upperTimestamp"]), int(frames[-1]["endTimestamp"])
    selected = []
    with csv_path.open(newline="", encoding="utf-8-sig") as stream:
        for row in csv.DictReader(stream):
            if row["Application"].lower() != "shooter.exe" or int(row["ProcessID"]) != manifest["processId"]:
                raise ValueError("Unexpected process in targeted GPU trace")
            timestamp = int(row["TimeInQPC"])
            if low < timestamp <= high:
                selected.append(row)
    details = {"method": "PresentMon 2.6.0 ETW MsGPUTime per process presentation; aggregate QPC capture interval",
               "manifestSha256": sha(manifest_path), "csvSha256": sha(csv_path), "processId": manifest["processId"],
               "lowerQpcExclusive": low, "upperQpcInclusive": high, "expectedFrames": len(frames), "presentedFrames": len(selected),
               "scope": "Independent per-present series, not row-wise imputation into incomplete native GPU samples"}
    if len(selected) != len(frames):
        details["unavailable"] = "Present count does not match the exact measured frame interval"
        return None, details
    timestamps = [int(row["TimeInQPC"]) for row in selected]
    if any(b <= a for a, b in zip(timestamps, timestamps[1:])) or len(set(row["SwapChainAddress"] for row in selected)) != 1:
        raise ValueError("Duplicate/reordered presents or multiple swapchains in GPU trace")
    if any(row["PresentRuntime"] != "DXGI" for row in selected):
        raise ValueError("External trace API differs from B01")
    intervals = Counter(int(row["SyncInterval"]) for row in selected)
    if any(interval < 0 or interval > 4 for interval in intervals):
        raise ValueError("Invalid driver/compositor flip interval")
    # PresentMon 2.6.0 overwrites application SyncInterval with kernel FlipInterval
    # in HandleDxgkFlip/IndependentFlip. Nonzero CSV values cannot establish app VSync.
    # Require independent per-frame application guards before accepting that case.
    if any(interval != 0 for interval in intervals) and not (
            meta.get("configuredVSyncCount") == 0 and meta.get("targetFrameRate") == -1
            and meta.get("presentationConfigurationChecks") == len(frames)):
        raise ValueError("Nonzero driver flip interval requires independently verified per-frame application VSync/uncapped configuration")
    details["driverFlipIntervals"] = dict(intervals)
    details["applicationVSyncGuardedFrames"] = meta.get("presentationConfigurationChecks")
    values = []
    for row in selected:
        try: value = float(row["MsGPUTime"])
        except ValueError:
            details["unavailable"] = "Per-present GPU duration unavailable; no samples dropped or filled"
            return None, details
        if value < 0 or not math.isfinite(value):
            raise ValueError("Invalid external GPU duration")
        values.append(value)
    if not any(value > 0 for value in values):
        details["unavailable"] = "No positive GPU measurement; all-zero trace is not accepted"
        return None, details
    details["validSamples"] = len(values)
    return values, details
