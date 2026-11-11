"""Analyze real M0 raw capture. Diagnostic captures never produce an acceptance result."""
import argparse
import csv
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path


def percentile(values, probability):
    if not values or not 0 < probability <= 1 or any(not math.isfinite(x) or x < 0 for x in values):
        raise ValueError("Invalid percentile sample set")
    values = sorted(values)
    return values[math.ceil(probability * len(values)) - 1]


def read_rows(path):
    with path.open(newline="", encoding="utf-8-sig") as stream:
        return list(csv.DictReader(stream))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def utc(text):
    return datetime.fromisoformat(text.replace("Z", "+00:00"))


def analyze(capture_path, build_path, hardware_path, run_index):
    meta = json.loads((capture_path / "capture.json").read_text(encoding="utf-8-sig"))
    build = json.loads(build_path.read_text(encoding="utf-8-sig"))
    hardware = json.loads(hardware_path.read_text(encoding="utf-8-sig"))
    rows, ticks = read_rows(capture_path / "frames.csv"), read_rows(capture_path / "ticks.csv")
    if build["result"] != "Succeeded" or build["target"] != "Client" or build["backend"] != "IL2CPP" or build["development"]:
        raise ValueError("Acceptance requires a release IL2CPP client")
    if (meta["workloadId"], meta["seed"], meta["profile"], meta["width"], meta["height"], meta["graphicsApi"]) != ("B01-minimal-v1", "460072030", "Low", 1280, 720, "Direct3D11"):
        raise ValueError("Workload/profile/render configuration mismatch")
    if len(rows) != meta["frames"] or len(ticks) != meta["ticks"] or not rows:
        raise ValueError("Capture counts mismatch")
    first, count = meta["warmupSeconds"] * 60, meta["captureSeconds"] * 60
    if [int(t["tick"]) for t in ticks] != list(range(first, first + count)):
        raise ValueError("Tick capture is incomplete, duplicated or reordered")
    frame_values = [float(row["frameMs"]) for row in rows]
    frame_ms = {key: percentile(frame_values, p) for key, p in [("p50", .5), ("p95", .95), ("p99", .99), ("max", 1)]}
    if meta["diagnostic"]:
        return {"schemaVersion": 1, "scope": "Short diagnostic capture only; not B01 acceptance", "diagnostic": True,
                "frames": len(rows), "ticks": len(ticks), "frameMs": frame_ms, "gpuAdapter": meta["gpuAdapter"], "gateStatus": "PendingHardware"}
    if (meta["warmupSeconds"], meta["captureSeconds"]) != (60, 180):
        raise ValueError("B01 acceptance durations were changed")
    unavailable = []

    def measured(column, probability=.95):
        values = [float(row[column]) for row in rows]
        if any(value < 0 or not math.isfinite(value) for value in values):
            unavailable.append(column + ": recorder/timing unavailable for part or all of capture")
            return None
        return percentile(values, probability)

    main, render, gpu = measured("mainMs"), measured("renderMs"), measured("gpuMs")
    allocated_frame = measured("managedFrameBytes", 1)
    allocated_tick = max(int(t["managedTickBytes"]) for t in ticks)
    if allocated_tick < 0:
        raise ValueError("Invalid allocation sample")
    simulation = [float(t["simulationMs"]) for t in ticks]
    simulation_p95 = percentile(simulation, .95)
    memory = [r for r in read_rows(capture_path / "memory.csv") if utc(meta["captureStartUtc"]) <= utc(r["utc"]) <= utc(meta["captureEndUtc"])]
    private_peak = max((int(r["privateBytes"]) for r in memory), default=None)
    rss_peak = max((int(r["rssBytes"]) for r in memory), default=None)
    if not memory:
        unavailable.append("process memory: no external 1Hz samples during capture")
    machine = {key: hardware.get(key) for key in ["cpu", "gpuAdapter", "ramBytes", "ramSpeed", "channelMode", "os", "driver", "powerPlan", "acPower"]}
    machine["gpuAdapter"] = meta["gpuAdapter"]
    for key, value in machine.items():
        if value is None:
            unavailable.append("machine." + key + ": unavailable; no assumption substituted")
    if "4600H" not in machine["cpu"] or "Radeon" not in machine["gpuAdapter"] or machine["ramBytes"] != 16 * 1024 ** 3 or machine["ramSpeed"] != 3200 or machine["acPower"] is not True:
        unavailable.append("reference hardware/power conditions not fully verified")
    fail = frame_ms["p95"] > 33.33 or frame_ms["p99"] > 50 or frame_ms["max"] > 100
    fail |= sum(50 < value <= 100 for value in frame_values) > 3
    fail |= allocated_tick > 0 or simulation_p95 > 6 or sum(simulation) / len(simulation) > 4
    for value, limit in [(main, 12), (render, 8), (gpu, 27), (private_peak, 3 * 1024 ** 3), (allocated_frame, 0)]:
        fail |= value is not None and value > limit
    if not all(hardware.get(key) for key in ["thermalStabilized", "nvmeVerified"]):
        unavailable.append("thermal stabilization / NVMe conditions unverified")
    route = {"cameraMm": [round(value * 1000) for value in meta["cameraMeters"]], "yawMilliDegrees": 0, "pitchMilliDegrees": 0}
    return {"schemaVersion": 1, "workloadId": meta["workloadId"], "seed": meta["seed"], "sourceCommit": build["sourceCommit"],
            "toolchainHash": build["toolchainHash"], "contentManifestHash": build["contentManifestHash"],
            "routeHash": hashlib.sha256(json.dumps(route, sort_keys=True, separators=(",", ":")).encode()).hexdigest(),
            "profile": "Low", "internalWidth": 1280, "internalHeight": 720, "warmupSeconds": 60, "captureSeconds": 180, "runIndex": run_index,
            "machine": machine, "metrics": {"frameMs": frame_ms, "mainMs": main, "renderMs": render, "gpuMs": gpu,
            "privateCommitBytesPeak": private_peak, "rssBytesPeak": rss_peak, "managedBytesPerFrame": allocated_frame, "managedBytesPerTick": allocated_tick, "uploadMsP95": None},
            "counters": {"actors": 0, "shotsAccepted": 0, "impactPeak": 0, "smokeProxies": 0, "chunkTransitions": 0},
            "unavailableMetrics": unavailable + ["uploadMsP95: no runtime streaming in B01-minimal; unavailable"],
            "gateStatus": "ComputedFail" if fail else "PendingHardware" if unavailable else "ComputedPass"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", type=Path, required=True)
    parser.add_argument("--build-manifest", type=Path, required=True)
    parser.add_argument("--hardware", type=Path, required=True)
    parser.add_argument("--run-index", type=int, choices=(1, 2, 3), default=1)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = analyze(args.capture, args.build_manifest, args.hardware, args.run_index)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
