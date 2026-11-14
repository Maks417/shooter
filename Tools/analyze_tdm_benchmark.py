"""Analyze rendered product TDM captures (FIX12 / T063). Rejects wrong identities and shortened windows."""
import argparse
import csv
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
from hardware_evidence import apply_evidence
from external_gpu import gpu_samples
from render_budget import render_budget_samples
import tdm_workloads


REQUIRED_COUNTER_FIELDS = tdm_workloads.REQUIRED_COUNTERS


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


def require_int(meta, key):
    value = meta.get(key)
    if type(value) is not int:
        raise ValueError("Missing or non-integer counter field: " + key)
    if value < 0:
        raise ValueError("Negative counter rejected: " + key)
    return value


def analyze(capture_path, build_path, hardware_path, run_index, evidence_path=None, application_path=None,
            external_gpu_path=None, gpu_application_path=None, render_application_path=None):
    meta = json.loads((capture_path / "capture.json").read_text(encoding="utf-8-sig"))
    build = json.loads(build_path.read_text(encoding="utf-8-sig"))
    hardware = json.loads(hardware_path.read_text(encoding="utf-8-sig"))
    if evidence_path is not None:
        hardware, application = apply_evidence(Path(__file__).resolve().parents[1], evidence_path, hardware, meta, capture_path / "capture.json")
        application["rawHardwareSha256"] = sha(hardware_path)
        if application_path is not None:
            if application_path.exists():
                raise ValueError("Refusing to overwrite an evidence application report")
            application_path.write_text(json.dumps(application, indent=2) + "\n", encoding="utf-8")

    workload = tdm_workloads.require_measurement(meta.get("workloadId", ""))
    if not meta.get("productTdm"):
        raise ValueError("Product TDM collector marker missing")
    if meta.get("collectorVersion") != 4:
        raise ValueError("Product TDM collectorVersion must be 4")
    if build["result"] != "Succeeded" or build["target"] != "Client" or build["backend"] != "IL2CPP" or build["development"]:
        raise ValueError("Acceptance requires a release IL2CPP client")
    expected_seed = str(workload.get("seed") or ("460072031" if workload["mapId"] == "depot-yard" else "460072030"))
    if (meta.get("mapId"), meta.get("seed"), meta.get("teamSize"), meta.get("actors")) != (
            workload["mapId"], expected_seed, workload["teamSize"], workload["actors"]):
        raise ValueError("Workload/map/roster identity mismatch")
    if (meta.get("profile"), meta.get("width"), meta.get("height"), meta.get("graphicsApi")) != ("Low", 1280, 720, "Direct3D11"):
        raise ValueError("Workload/profile/render configuration mismatch")
    if not meta.get("contentManifestHash") or not meta.get("packageRootHash"):
        raise ValueError("Missing content/package identity proofs")

    rows, ticks = read_rows(capture_path / "frames.csv"), read_rows(capture_path / "ticks.csv")
    if len(rows) != meta["frames"] or len(ticks) != meta["ticks"] or not rows:
        raise ValueError("Capture counts mismatch")

    soak = bool(workload.get("soak"))
    precondition = meta.get("preconditionSeconds", 0)
    if type(precondition) is not int or not 0 <= precondition <= 1800:
        raise ValueError("Invalid preconditioning duration")
    if soak and precondition:
        raise ValueError("Soak workloads reject precondition windows")
    expected_warmup = 0 if soak else 60
    expected_capture = workload["captureTicks"] // 60
    if meta.get("diagnostic"):
        # Short diagnostics never accept.
        pass
    elif (meta["warmupSeconds"], meta["captureSeconds"]) != (expected_warmup, expected_capture):
        raise ValueError("Product TDM acceptance durations were changed or shortened")

    first = (precondition + meta["warmupSeconds"]) * 60
    count = meta["captureSeconds"] * 60
    if meta.get("captureStartTick") != first:
        raise ValueError("Preconditioning/capture tick identity mismatch")
    if [int(t["tick"]) for t in ticks] != list(range(first, first + count)):
        raise ValueError("Tick capture is incomplete, duplicated, reordered or shortened")

    for field in REQUIRED_COUNTER_FIELDS:
        camel = field
        if camel not in meta:
            raise ValueError("Incomplete counters: missing " + camel)
        require_int(meta, camel)
    for column in ("team1Kills", "team2Kills", "respawns", "aiRays", "pathRequests", "shots", "damageEvents", "leaseAcquires", "querySamples"):
        if column not in ticks[0]:
            raise ValueError("Tick telemetry missing counter column " + column)

    frame_values = [float(row["frameMs"]) for row in rows]
    frame_ms = {key: percentile(frame_values, p) for key, p in [("p50", .5), ("p95", .95), ("p99", .99), ("max", 1)]}
    allocations = [int(t["managedTickBytes"]) for t in ticks]
    calibrated = meta.get("tickAllocationAvailable") is True and meta.get("allocationMainControlBytes", 0) >= 4352 and meta.get("tickAllocationEndControlBytes", 0) >= 4096
    unavailable_tick = meta.get("tickAllocationAvailable") is False
    if any(value < 0 and not (unavailable_tick and value == -1) for value in allocations):
        raise ValueError("Invalid allocation sample")
    allocated_tick = max(allocations) if calibrated else None
    simulation = [float(t["simulationMs"]) for t in ticks]
    simulation_p95 = percentile(simulation, .95)

    external_values = gpu_application = None
    if external_gpu_path is not None:
        external_values, gpu_application = gpu_samples(external_gpu_path, capture_path, rows)
        if gpu_application_path is not None:
            if gpu_application_path.exists():
                raise ValueError("Refusing to overwrite GPU evidence application")
            gpu_application_path.write_text(json.dumps(gpu_application, indent=2) + "\n")

    enclosing = render_proof = None
    enclosing, render_proof = render_budget_samples(meta, rows)
    render_proof["rawCaptureSha256"] = sha(capture_path / "capture.json")
    render_proof["rawFramesSha256"] = sha(capture_path / "frames.csv")
    if render_application_path is not None:
        if render_application_path.exists():
            raise ValueError("Refusing to overwrite render proof")
        render_application_path.write_text(json.dumps(render_proof, indent=2) + "\n")

    if meta["diagnostic"]:
        return {"schemaVersion": 1, "scope": "Short diagnostic capture only; not TDM acceptance", "diagnostic": True,
                "workloadId": meta["workloadId"], "mapId": meta["mapId"], "contentManifestHash": meta["contentManifestHash"],
                "frames": len(rows), "ticks": len(ticks), "frameMs": frame_ms, "gpuAdapter": meta["gpuAdapter"],
                "gateStatus": "PendingHardware"}

    unavailable = []
    if not calibrated:
        unavailable.append("managedTickBytes: allocation positive control unavailable/unverified; legacy zeros are not measurements")

    def measured(column, probability=.95):
        values = [float(row[column]) for row in rows]
        if any(value < 0 or not math.isfinite(value) for value in values):
            unavailable.append(column + ": recorder/timing unavailable for part or all of capture")
            return None
        return percentile(values, probability)

    main = measured("mainMs")
    render = percentile(enclosing, .95) if enclosing is not None else None
    if enclosing is None:
        unavailable.append("renderMs: " + render_proof["unavailable"])
    if external_gpu_path is None:
        gpu = measured("gpuMs")
    else:
        gpu = percentile(external_values, .95) if external_values is not None else None
        if external_values is None:
            unavailable.append("gpuMs: " + gpu_application["unavailable"])
    allocated_frame = measured("managedFrameBytes", 1)
    if meta.get("frameAllocationAvailable") is not True or meta.get("allocationWorkerControlBytes", 0) < 4352 or meta.get("allocationEndControlBytes", 0) < 4096:
        allocated_frame = None
        unavailable.append("managedFrameBytes: all-thread allocation positive control unavailable/unverified")

    memory = [r for r in read_rows(capture_path / "memory.csv") if utc(meta["captureStartUtc"]) <= utc(r["utc"]) <= utc(meta["captureEndUtc"])]
    private_samples = [int(r["privateBytes"]) for r in memory]
    rss_samples = [int(r["rssBytes"]) for r in memory]
    if any(value < 0 for value in private_samples + rss_samples):
        raise ValueError("Invalid process memory sample")
    private_peak = max(private_samples, default=None)
    rss_peak = max(rss_samples, default=None)
    if not memory:
        unavailable.append("process memory: no external 1Hz samples during capture")

    machine = {key: hardware.get(key) for key in ["cpu", "gpuAdapter", "ramBytes", "ramSpeed", "channelMode", "os", "driver", "powerPlan", "acPower"]}
    machine["gpuAdapter"] = meta["gpuAdapter"]
    for key, value in machine.items():
        if value is None:
            unavailable.append("machine." + key + ": unavailable; no assumption substituted")
    if "4600H" not in (machine["cpu"] or "") or "Radeon" not in (machine["gpuAdapter"] or "") or machine["ramBytes"] != 16 * 1024 ** 3 or machine["ramSpeed"] != 3200 or machine["acPower"] is not True:
        unavailable.append("reference hardware/power conditions not fully verified")
    if not all(hardware.get(key) for key in ["thermalStabilized", "nvmeVerified"]):
        unavailable.append("thermal stabilization / NVMe conditions unverified")

    fail = frame_ms["p95"] > 33.33 or frame_ms["p99"] > 50 or frame_ms["max"] > 100
    fail |= sum(50 < value <= 100 for value in frame_values) > 3
    fail |= (allocated_tick is not None and allocated_tick > 0) or simulation_p95 > 6 or sum(simulation) / len(simulation) > 4
    for value, limit in [(main, 12), (render, 8), (gpu, 27), (private_peak, 3 * 1024 ** 3), (allocated_frame, 0)]:
        fail |= value is not None and value > limit

    counters = {name: meta[name] for name in REQUIRED_COUNTER_FIELDS}
    counters["actors"] = meta["actors"]
    route = {"mapId": meta["mapId"], "teamSize": meta["teamSize"], "seed": meta["seed"], "workloadId": meta["workloadId"]}
    return {"schemaVersion": 1, "workloadId": meta["workloadId"], "seed": meta["seed"], "mapId": meta["mapId"],
            "sourceCommit": build["sourceCommit"], "toolchainHash": build["toolchainHash"],
            "contentManifestHash": meta["contentManifestHash"], "packageRootHash": meta["packageRootHash"],
            "routeHash": hashlib.sha256(json.dumps(route, sort_keys=True, separators=(",", ":")).encode()).hexdigest(),
            "profile": "Low", "internalWidth": 1280, "internalHeight": 720,
            "warmupSeconds": meta["warmupSeconds"], "captureSeconds": meta["captureSeconds"], "runIndex": run_index,
            "machine": machine,
            "metrics": {"frameMs": frame_ms, "mainMs": main, "renderMs": render, "gpuMs": gpu,
                        "privateCommitBytesPeak": private_peak, "rssBytesPeak": rss_peak,
                        "managedBytesPerFrame": allocated_frame, "managedBytesPerTick": allocated_tick, "uploadMsP95": None},
            "counters": counters,
            "unavailableMetrics": unavailable + ["uploadMsP95: no runtime streaming in product TDM capture; unavailable"],
            "gateStatus": "ComputedFail" if fail else "PendingHardware" if unavailable else "ComputedPass"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", type=Path, required=True)
    parser.add_argument("--build-manifest", type=Path, required=True)
    parser.add_argument("--hardware", type=Path, required=True)
    parser.add_argument("--run-index", type=int, choices=(1, 2, 3), default=1)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--hardware-evidence", type=Path)
    parser.add_argument("--evidence-application", type=Path)
    parser.add_argument("--external-gpu-manifest", type=Path)
    parser.add_argument("--gpu-application", type=Path)
    parser.add_argument("--render-application", type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError("Refusing to overwrite a benchmark result; use a new review directory")
    report = analyze(args.capture, args.build_manifest, args.hardware, args.run_index, args.hardware_evidence,
                     args.evidence_application, args.external_gpu_manifest, args.gpu_application, args.render_application)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
