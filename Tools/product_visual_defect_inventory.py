#!/usr/bin/env python3
"""T062 product visual defect inventory writer. Never claims P05 PASS or authorizes clearance search."""
from __future__ import annotations

import argparse
import copy
import json
import sys
from pathlib import Path

SCHEMA_ID = "shooter.product-visual-defect-inventory.v1"
P05_ACCEPTANCE = "FAIL"
ADR_ID = "ADR-024"

DEFECTS = [
    {
        "id": "P05-D01",
        "category": "clearance",
        "title": "Left forearm / chest-strap overlap",
        "description": (
            "Conservative clearance probe fails near down-aim reload phase 0.45 with approximately "
            "2.18 mm dense possible left forearm/strap overlap on loft1.0.2/motion.spike.v3."
        ),
        "historicalEvidence": "docs/soldier-baseline.md; immutable M1 Artifacts captures. ADR-095 v6 shooting posture: unchanged conservative probe minimum +3.0 mm over the full gate envelope (outward support-hand reload bow). Visual review pending.",
        "status": "NumericallyResolvedPendingReview",
        "requiredFor": "RUN05, REL03, FIX11",
    },
    {
        "id": "P05-D02",
        "category": "alignment",
        "title": "Eye / sight-line unresolved",
        "description": (
            "Neutral eye/sight-line separation is about 6.49 cm; worst reported reload residual "
            "about 12.70 cm. Grip/contact passes do not override this failure."
        ),
        "historicalEvidence": "docs/soldier-baseline.md; ADR-023 candidate review retained as FAIL. ADR-095 v6 shooting posture: eye/sight-line residual 0.000 mm over the full gate envelope (eye placed on the sight line by construction). Visual review pending.",
        "status": "NumericallyResolvedPendingReview",
        "requiredFor": "RUN05, REL03, FIX11",
    },
    {
        "id": "P05-D03",
        "category": "clearance",
        "title": "Iteration-2 trigger-arm / torso penetration (historical)",
        "description": (
            "Neutral right shoulder/elbow/hand samples showed interior forearm points inside the "
            "loft ellipse and sleeve/plate intersection despite exact grip targets."
        ),
        "historicalEvidence": "ADR-023 retained iteration-2 defect; M1 continuation artifacts. ADR-095 v6 shooting posture: trigger-arm minimum clearance +3.0 mm over the full gate envelope. Visual review pending.",
        "status": "NumericallyResolvedPendingReview",
        "requiredFor": "RUN05, REL03, FIX11",
    },
    {
        "id": "PROD-D01",
        "category": "readability",
        "title": "Team-identifiable living soldiers in product player",
        "description": "Product captures must show distinct team materials on living actors for both maps.",
        "historicalEvidence": "FIX05/T056 pending product-player captures; triage with T062 overlays.",
        "status": "PendingCapture",
        "requiredFor": "FIX05, FIX11, REL03",
    },
    {
        "id": "PROD-D02",
        "category": "lod",
        "title": "Required LOD / grip / foot / clearance tolerances in live poses",
        "description": (
            "Compare product poses against required LODs and grip/foot/clearance tolerances using "
            "overlay metrics; do not edit generator sources to chase clearance."
        ),
        "historicalEvidence": "FIX11 overlays on integrated client; P05 FAIL remains.",
        "status": "PendingCapture",
        "requiredFor": "FIX11, REL03",
    },
    {
        "id": "PROD-D03",
        "category": "ui",
        "title": "720p HUD / shell readability at 100–150% UI scale",
        "description": "Named review of menu/HUD readability at native 720p. Separate from soldier clearance search.",
        "historicalEvidence": "REL03 usability; shell owned by T059 with visual sign-off under T062/T065.",
        "status": "PendingReview",
        "requiredFor": "REL03, FIX11",
    },
]

DEFAULT_POINTERS = [
    "docs/soldier-baseline.md#third-bounded-clearance-candidate",
    "docs/decisions.md#adr-024--accepted-p05-soldier-visual-scope-stop-after-exhausted-clearance-iterations",
    "docs/milestones/m1-procedural-feasibility/handoff.md",
    "Artifacts/m1-procedural-feasibility/ (immutable failed P05 captures/caches — do not delete or relabel)",
]


def build_inventory(
    source_commit: str,
    source_dirty: bool,
    inventory_root: str,
    capture_sample_paths: list[str] | None = None,
    historical_pointers: list[str] | None = None,
) -> dict:
    if P05_ACCEPTANCE != "FAIL":
        raise RuntimeError("P05 visual acceptance must remain FAIL under ADR-024.")
    payload = {
        "schemaId": SCHEMA_ID,
        "taskId": "T062",
        "fixId": "FIX11",
        "gateId": "P05",
        "adrId": ADR_ID,
        "visualAcceptance": P05_ACCEPTANCE,
        "fourthClearanceSearchAuthorized": False,
        "soldierRecipeIdentity": "soldier.loft1.0.2",
        "motionIdentity": "soldier.motion.v6.shooting",
        "sourceCommit": source_commit,
        "sourceDirty": bool(source_dirty),
        "inventoryRoot": inventory_root,
        "disposition": "InventoryOnly",
        "fix11Status": "ToolingReady",
        "rel03Status": "Open",
        "note": (
            "Initial capture/overlay tooling and honest defect inventory only. "
            "P05 remains FAIL under ADR-024. No fourth clearance/parameter search. "
            "Generator/recipe sources unchanged."
        ),
        "defects": copy.deepcopy(DEFECTS),
        "historicalArtifactPointers": historical_pointers or DEFAULT_POINTERS,
        "captureSamplePaths": capture_sample_paths or [],
        "overlay": {
            "runtimeType": "Shooter.Presentation.Rig.ProductVisualReviewOverlay",
            "enableFlag": "-shooterVisualReview",
            "outputFlag": "-shooterVisualReviewOutput",
            "presentationPath": "ProductSessionView / ProductActorPresentation",
            "commandExample": (
                "Player.exe -shooterVisualReview 1 "
                "-shooterVisualReviewOutput Artifacts/m8-runtime-composition/<commit>/visual-review-<run>/"
            ),
        },
    }
    validate_inventory(payload)
    return payload


def validate_inventory(payload: dict) -> None:
    if payload.get("visualAcceptance") != "FAIL":
        raise ValueError("visualAcceptance must be FAIL")
    if payload.get("fourthClearanceSearchAuthorized") is not False:
        raise ValueError("fourthClearanceSearchAuthorized must be false")
    if payload.get("schemaId") != SCHEMA_ID:
        raise ValueError("unexpected schemaId")
    for defect in payload.get("defects", []):
        did = defect.get("id", "")
        status = defect.get("status", "")
        if did.startswith("P05-") and status in ("Resolved", "PASS", "Pass", "pass"):
            raise ValueError(f"P05 defect {did} cannot be {status} without owner-authorized clearance work")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--source-dirty", required=True, choices=["true", "false"])
    parser.add_argument("--capture-root", default="")
    args = parser.parse_args(argv)

    out = Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)
    captures: list[str] = []
    if args.capture_root:
        root = Path(args.capture_root)
        if root.is_dir():
            captures = [str(p.as_posix()) for p in sorted(root.glob("sample-tick-*.json"))]

    payload = build_inventory(
        source_commit=args.source_commit,
        source_dirty=args.source_dirty == "true",
        inventory_root=str(out.as_posix()),
        capture_sample_paths=captures,
    )
    inventory_path = out / "defect-inventory.json"
    inventory_path.write_text(json.dumps(payload, indent=2, sort_keys=False) + "\n", encoding="utf-8")
    (out / "p05-status.json").write_text(
        json.dumps(
            {
                "gateId": "P05",
                "visualAcceptance": "FAIL",
                "adrId": "ADR-024",
                "fourthClearanceSearchAuthorized": False,
                "note": "Immutable FAIL; inventory/tooling only. Do not delete failed artifacts or relabel PASS.",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (out / "README.txt").write_text(
        "\n".join(
            [
                "T062 / FIX11 product visual defect inventory",
                "P05 visual acceptance: FAIL (ADR-024)",
                "Fourth clearance/parameter search: NOT authorized",
                "Generator/recipe sources: unchanged by this package",
                "",
                "Enable live overlays on the product client:",
                "  -shooterVisualReview 1",
                "  -shooterVisualReviewOutput <this-or-sibling Artifacts folder>",
                "",
                "This package is InventoryOnly / ToolingReady. REL03 remains Open until named review",
                "attaches actual captures with defects resolved/unresolved. Unresolved required",
                "defects keep REL03 open.",
                "",
            ]
        ),
        encoding="utf-8",
    )
    print(f"Wrote {inventory_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
