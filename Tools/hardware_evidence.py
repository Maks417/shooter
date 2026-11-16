"""Apply explicitly reviewed, hash-verified hardware observations without editing raw reports."""
import csv
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import math
from pathlib import Path
import re
from shooterlib import sha, utc as _utc


def utc(value):
    return _utc(value, require_timezone=True)




def review(value):
    if not isinstance(value, dict) or not all(isinstance(value.get(k), str) and value[k].strip() for k in ["reviewer", "reviewedUtc", "rationale"]):
        raise ValueError("Evidence requires an explicit reviewer, date and rationale")
    utc(value["reviewedUtc"])


def proof(root, value, extension):
    if not isinstance(value, dict) or not isinstance(value.get("path"), str) or not re.fullmatch("[0-9a-f]{64}", value.get("sha256", "")):
        raise ValueError("Malformed hardware source proof")
    path = (root / value["path"]).resolve()
    if not path.is_relative_to((root / "Artifacts").resolve()) or path.suffix.lower() != extension or not path.is_file():
        raise ValueError("Hardware proof must resolve to the expected file inside repository Artifacts")
    if sha(path) != value["sha256"]:
        raise ValueError("Hardware evidence source hash mismatch")
    return path


def sensor_window(path, offset_minutes, meta):
    if type(offset_minutes) is not int or not -720 <= offset_minutes <= 840:
        raise ValueError("Sensor UTC offset must be explicit minutes")
    rows = list(csv.reader(io.StringIO(path.read_bytes().decode("cp1252"))))
    if not rows or not rows[0] or len(rows[0]) != len(set(rows[0])):
        raise ValueError("Missing or duplicate sensor headers")
    header = rows[0]
    names = ["CPU (Tctl/Tdie) [°C]", "GPU Temperature [°C]", "Thermal Throttling (HTC) [Yes/No]",
             "Thermal Throttling (PROCHOT CPU) [Yes/No]", "Thermal Throttling (PROCHOT EXT) [Yes/No]", "Throttle Reason - Thermal [Yes/No]"]
    if any(name not in header for name in names):
        raise ValueError("Required thermal sensors/flags missing")
    samples = []
    offset = timezone(timedelta(minutes=offset_minutes))
    for line, row in enumerate(rows[1:], 2):
        if len(row) != len(header):
            if line == len(rows):
                continue  # Preserve a live snapshot's incomplete final row; never alter its bytes.
            raise ValueError("Malformed interior sensor row")
        if row == header or row[0] == row[1] == "":
            continue
        timestamp = datetime.strptime(row[0] + " " + row[1], "%d.%m.%Y %H:%M:%S.%f").replace(tzinfo=offset)
        if samples and timestamp <= samples[-1][0]:
            raise ValueError("Sensor timestamps must be strictly ordered")
        samples.append((timestamp, row))
    start, end = utc(meta["captureStartUtc"]), utc(meta["captureEndUtc"])
    if end <= start or not samples or samples[0][0] > start or samples[-1][0] < end:
        raise ValueError("Sensor log does not bracket this capture")
    selected = [row for timestamp, row in samples if start <= timestamp <= end]
    if len(selected) < 2:
        raise ValueError("Insufficient concurrent thermal samples")
    ranges = {}
    for name in names[:2]:
        values = [float(row[header.index(name)]) for row in selected]
        if any(not math.isfinite(v) or v <= 0 or v >= 150 for v in values):
            raise ValueError("Invalid temperature observation")
        ranges[name] = {"min": min(values), "max": max(values)}
    flags = {name: sorted(set(row[header.index(name)] for row in selected)) for name in names[2:]}
    if any(values != ["No"] for values in flags.values()):
        raise ValueError("Reviewed no-throttling thermal evidence has missing/positive flags")
    return {"sampleCount": len(selected), "ranges": ranges, "thermalFlags": flags,
            "scope": "Sampled thermal observations; reviewer rationale, not an automatic numerical stabilization threshold"}


def apply_evidence(root, path, hardware, meta, capture_path):
    root, path = Path(root).resolve(), Path(path).resolve()
    evidence = json.loads(path.read_text(encoding="utf-8-sig"))
    if evidence.get("schemaVersion") != 1 or evidence.get("kind") != "shooter.hardware-review.v1":
        raise ValueError("Unknown hardware evidence version/kind")
    review(evidence.get("review"))
    subject = evidence.get("subject", {})
    for key in ["cpu", "ramBytes", "ramSpeed"]:
        expected, actual = subject.get(key), hardware.get(key)
        if key == "cpu" and isinstance(expected, str) and isinstance(actual, str):
            expected, actual = " ".join(expected.split()), " ".join(actual.split())
        if expected is None or actual is None or expected != actual:
            raise ValueError("Hardware evidence subject mismatch: " + key)
    result = dict(hardware)
    applied = {"schemaVersion": 1, "evidenceSha256": sha(path), "evidencePath": str(path),
               "rawCaptureSha256": sha(capture_path), "review": evidence["review"], "applied": {}}
    memory = evidence.get("memory")
    if memory is not None:
        review(memory.get("review"))
        if memory.get("channelMode") not in ["Dual-channel", "Single-channel"]:
            raise ValueError("Unknown reviewed channel mode")
        source = proof(root, memory.get("source"), ".png")
        if result.get("channelMode") is not None and result["channelMode"] != memory["channelMode"]:
            raise ValueError("Reviewed memory conflicts with measured channel mode")
        result["channelMode"] = memory["channelMode"]
        applied["applied"]["channelMode"] = {"value": result["channelMode"], "source": str(source), "sourceSha256": sha(source), "review": memory["review"]}
    thermal = evidence.get("thermal")
    if thermal is not None:
        review(thermal.get("review"))
        if thermal.get("acceptedStabilization") is not True or thermal.get("captureSha256") != sha(capture_path):
            raise ValueError("Thermal review must explicitly accept this exact capture")
        source = proof(root, thermal.get("source"), ".csv")
        observed = sensor_window(source, thermal.get("utcOffsetMinutes"), meta)
        result["thermalStabilized"] = True
        applied["applied"]["thermalStabilized"] = {"value": True, "source": str(source), "sourceSha256": sha(source), "review": thermal["review"], "observed": observed}
    if not applied["applied"]:
        raise ValueError("Hardware evidence applies no supported observation")
    return result, applied
