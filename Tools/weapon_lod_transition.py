"""Analyze weapon LOD transition sample reports against ScreenHeightLod policy.

Mirrors UnityProject/Assets/Shooter/Generation/Core/Geometry/ScreenHeightLod.cs:
thresholds 0.50/0.20/0.08, 10% hysteresis, first-person forces LOD0, camera cuts may
cross every level in one Select. Inspect override is presentation-only (Automatic=false)
and is accepted without re-running automatic selection.
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

SCHEMA_VERSION = 1
THRESHOLDS = (0.50, 0.20, 0.08)
HYSTERESIS = 0.10
POLICY = {
    "thresholds": list(THRESHOLDS),
    "hysteresis": HYSTERESIS,
    "firstPersonForcesLod0": True,
    "inspectBypassesAutomaticSelection": True,
    "cameraCutsMayCrossAllLevels": True,
}


def select(height: float, previous: int = -1, first_person: bool = False) -> int:
    if not math.isfinite(height) or height < 0 or previous < -1 or previous > 3:
        raise ValueError("height/previous out of range")
    if first_person:
        return 0
    if previous < 0:
        initial = 0
        while initial < 3 and height < THRESHOLDS[initial]:
            initial += 1
        return initial
    lod = previous
    while lod < 3 and height < THRESHOLDS[lod] * (1 - HYSTERESIS):
        lod += 1
    while lod > 0 and height > THRESHOLDS[lod - 1] * (1 + HYSTERESIS):
        lod -= 1
    return lod


def _override_name(value):
    if value is None or value == "" or value == "none" or value is False:
        return None
    if isinstance(value, str):
        key = value.strip()
        if key in ("firstPerson", "inspect"):
            return key
    raise ValueError(f"Unrecognized override: {value!r}")


def _sample_fields(sample: dict):
    if not isinstance(sample, dict):
        raise ValueError("Each sample must be an object")
    try:
        height = float(sample["heightFraction"])
        selected = int(sample["selectedLod"])
        previous = int(sample["previousLod"])
    except (KeyError, TypeError, ValueError) as exc:
        raise ValueError("Sample requires heightFraction, selectedLod, previousLod") from exc
    if not math.isfinite(height) or height < 0:
        raise ValueError("Invalid heightFraction")
    if selected < 0 or selected > 3 or previous < -1 or previous > 3:
        raise ValueError("LOD values out of range")
    override = _override_name(sample.get("override"))
    return height, selected, previous, override


def _in_deadband(height: float, threshold: float) -> bool:
    return threshold * (1 - HYSTERESIS) <= height <= threshold * (1 + HYSTERESIS)


def analyze(report) -> dict:
    if not isinstance(report, dict):
        raise ValueError("Report must be a JSON object")
    version = report.get("schemaVersion", SCHEMA_VERSION)
    if version != SCHEMA_VERSION:
        raise ValueError(f"Unsupported schemaVersion {version}")
    samples = report.get("samples")
    if not isinstance(samples, list) or not samples:
        raise ValueError("Report requires a non-empty samples array")

    switch_counts = {f"{a}->{b}": 0 for a in range(4) for b in range(4) if a != b}
    total_switches = 0
    policy_violations = []
    flicker_events = []
    override_counts = {"firstPerson": 0, "inspect": 0, "none": 0}
    # Continuous stream window: only samples whose previousLod chains from the prior selectedLod.
    stream = []  # (index, height, selected, override)

    for index, raw in enumerate(samples):
        height, selected, previous, override = _sample_fields(raw)
        override_counts[override or "none"] += 1

        if override == "firstPerson":
            expected = 0
            if selected != expected:
                policy_violations.append({
                    "index": index,
                    "kind": "firstPersonMustBeLod0",
                    "selectedLod": selected,
                    "expectedLod": expected,
                    "heightFraction": height,
                    "previousLod": previous,
                })
        elif override == "inspect":
            expected = selected  # presentation Inspect(lod) bypasses automatic selection
        else:
            expected = select(height, previous, first_person=False)
            if selected != expected:
                policy_violations.append({
                    "index": index,
                    "kind": "automaticSelectionMismatch",
                    "selectedLod": selected,
                    "expectedLod": expected,
                    "heightFraction": height,
                    "previousLod": previous,
                })

        chained = (not stream) or (previous == stream[-1][2] and previous >= 0)
        if not chained:
            stream = []
        if previous != selected:
            key = f"{previous}->{selected}"
            switch_counts[key] = switch_counts.get(key, 0) + 1
            total_switches += 1
        stream.append((index, height, selected, override))
        if len(stream) >= 3:
            i0, h0, a, o0 = stream[-3]
            _, h1, b, o1 = stream[-2]
            _, h2, c, o2 = stream[-1]
            if a == c and a != b and not any((o0, o1, o2)) and abs(a - b) == 1:
                # Rapid A-B-A on a continuous stream while heights stay inside the shared deadband.
                threshold = THRESHOLDS[min(a, b)]
                if _in_deadband(h1, threshold) and _in_deadband(h2, threshold):
                    flicker_events.append({
                        "index": i0,
                        "pattern": f"{a}-{b}-{c}",
                        "threshold": threshold,
                        "heights": [h0, h1, h2],
                    })

    numeric_pass = len(policy_violations) == 0 and len(flicker_events) == 0
    return {
        "schemaVersion": SCHEMA_VERSION,
        "policy": POLICY,
        "sampleCount": len(samples),
        "totalSwitches": total_switches,
        "switchCounts": {k: v for k, v in sorted(switch_counts.items()) if v},
        "overrideCounts": override_counts,
        "policyViolations": policy_violations,
        "flickerEvents": flicker_events,
        "numericPolicyResult": "PASS" if numeric_pass else "FAIL",
        "visualCaptureResult": "Pending",
        "visualCaptureNote": (
            "No Unity silhouette/ADS transition images in this harness. "
            "Formal visual sign-off remains pending for the integrator."
        ),
        "result": "PASS" if numeric_pass else "FAIL",
    }


def build_sweep_report(
    diameters=(("rifle", 1.04), ("pistol", 0.30)),
    fov_degrees=60.0,
) -> dict:
    """Produce a deterministic sample report exercising hysteresis bands and overrides."""
    samples = []
    for name, diameter in diameters:
        for band_index, threshold in enumerate(THRESHOLDS):
            near, far = band_index, band_index + 1
            # Continuous sticky stream on the near side, then an explicit far crossing.
            lod = near
            for factor in (1.05, 1.02, 0.98, 0.95, 0.98, 1.02):
                height = threshold * factor
                selected = select(height, lod)
                samples.append({
                    "weapon": name,
                    "diameterMeters": diameter,
                    "heightFraction": height,
                    "selectedLod": selected,
                    "previousLod": lod,
                    "override": None,
                    "stream": f"{name}-band{band_index}-near",
                })
                lod = selected
            leave_near = threshold * (1 - HYSTERESIS - 0.01)
            selected = select(leave_near, lod)
            samples.append({
                "weapon": name,
                "diameterMeters": diameter,
                "heightFraction": leave_near,
                "selectedLod": selected,
                "previousLod": lod,
                "override": None,
                "stream": f"{name}-band{band_index}-near",
            })

            # Continuous sticky stream on the far side, then an explicit near crossing.
            lod = far
            for factor in (0.95, 0.98, 1.02, 1.05, 1.02, 0.98):
                height = threshold * factor
                selected = select(height, lod)
                samples.append({
                    "weapon": name,
                    "diameterMeters": diameter,
                    "heightFraction": height,
                    "selectedLod": selected,
                    "previousLod": lod,
                    "override": None,
                    "stream": f"{name}-band{band_index}-far",
                })
                lod = selected
            leave_far = threshold * (1 + HYSTERESIS + 0.01)
            selected = select(leave_far, lod)
            samples.append({
                "weapon": name,
                "diameterMeters": diameter,
                "heightFraction": leave_far,
                "selectedLod": selected,
                "previousLod": lod,
                "override": None,
                "stream": f"{name}-band{band_index}-far",
            })

            # Projection depth approach: far then near, continuous previous chain.
            tan_half = math.tan(fov_degrees * math.pi / 360.0)
            lod = near
            for height in (threshold * 1.15, threshold * 1.05, threshold * 0.95, threshold * 0.85):
                depth = diameter / (2 * height * tan_half)
                projected = diameter / (2 * depth * tan_half)
                selected = select(projected, lod)
                samples.append({
                    "weapon": name,
                    "diameterMeters": diameter,
                    "forwardDepthMeters": depth,
                    "verticalFieldOfViewDegrees": fov_degrees,
                    "heightFraction": projected,
                    "selectedLod": selected,
                    "previousLod": lod,
                    "override": None,
                    "stream": f"{name}-band{band_index}-project",
                })
                lod = selected

    # Camera cuts and first-person override (documented in ScreenHeightLod).
    samples.append({
        "heightFraction": 0.001,
        "selectedLod": select(0.001, 0),
        "previousLod": 0,
        "override": None,
        "note": "camera-cut far",
    })
    samples.append({
        "heightFraction": 2.0,
        "selectedLod": select(2.0, 3),
        "previousLod": 3,
        "override": None,
        "note": "camera-cut near",
    })
    samples.append({
        "heightFraction": 0.001,
        "selectedLod": 0,
        "previousLod": 3,
        "override": "firstPerson",
        "note": "view-arms override",
    })
    # Inspect is presentation-only; accepted as an explicit override sample.
    samples.append({
        "heightFraction": 0.001,
        "selectedLod": 2,
        "previousLod": 0,
        "override": "inspect",
        "note": "GeneratedWeaponLod.Inspect bypasses automatic selection",
    })
    return {
        "schemaVersion": SCHEMA_VERSION,
        "policy": POLICY,
        "source": "numeric-sweep-v1",
        "samples": samples,
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, help="JSON sample report to analyze")
    parser.add_argument("--emit-sweep", type=Path, help="Write a deterministic numeric sweep report")
    parser.add_argument("--output", type=Path, help="Write analysis summary JSON")
    args = parser.parse_args(argv)

    if args.emit_sweep:
        report = build_sweep_report()
        args.emit_sweep.parent.mkdir(parents=True, exist_ok=True)
        args.emit_sweep.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        if not args.input:
            args.input = args.emit_sweep

    if not args.input:
        parser.error("Provide --input and/or --emit-sweep")

    report = json.loads(args.input.read_text(encoding="utf-8"))
    summary = analyze(report)
    text = json.dumps(summary, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    else:
        sys.stdout.write(text)
    return 0 if summary["result"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
