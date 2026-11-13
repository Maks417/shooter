"""Build M1 P03 weapon silhouette/ADS review packages from Compile-Gallery captures.

Compile-Gallery.ps1 -Capture (ShooterFeatureBuild.Capture) emits 22 PNGs. Weapon
silhouette/ADS views use these basenames:

  LOD 0:  {rifle|pistol}-{front|side|three-quarter|ads}.png
  LOD N:  {rifle|pistol}-lod{N}.png   (side angle only; N in 1..3)

This module lists the formal review matrix (rifle/pistol x angles x key LODs
0/3, optional mid LODs), copies available PNGs into a review package, hashes
them, and writes a checklist that defaults to pending. It never auto-passes
visual judgment.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

SCHEMA_VERSION = 1
KIND = "shooter.weapon-silhouette-review.v1"
WEAPONS = ("rifle", "pistol")
ANGLES = ("front", "side", "three-quarter", "ads")
KEY_LODS = (0, 3)
OPTIONAL_LODS = (1, 2)
CHECKLIST_FIELDS = (
    "scalePlausible",
    "silhouetteIntact",
    "adsAlignment",
    "lowerLodReadable",
    "prohibitedArtAbsent",
)
CHECKLIST_VALUES = frozenset({"pending", "pass", "fail", "n/a"})
PENDING = "pending"
CAPTURE_CONVENTION = {
    "source": "ShooterFeatureBuild.Capture via Tools/Compile-Gallery.ps1 -Capture",
    "lod0Pattern": "{weapon}-{angle}.png",
    "lodNSidePattern": "{weapon}-lod{N}.png",
    "note": (
        "Current Capture shoots every angle at LOD 0 and side-only shots for "
        "LOD 1..3. Non-side angles at LOD>0 are required for formal review but "
        "are not emitted by Compile-Gallery today."
    ),
}


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def expected_png_name(weapon: str, lod: int, angle: str):
    """Return Compile-Gallery basename when Capture emits the view; else None."""
    if weapon not in WEAPONS:
        raise ValueError(f"Unknown weapon: {weapon}")
    if angle not in ANGLES:
        raise ValueError(f"Unknown angle: {angle}")
    if lod < 0 or lod > 3:
        raise ValueError(f"LOD out of range: {lod}")
    if lod == 0:
        return f"{weapon}-{angle}.png"
    if angle == "side":
        return f"{weapon}-lod{lod}.png"
    return None


def pending_checklist() -> dict:
    return {field: PENDING for field in CHECKLIST_FIELDS}


def validate_checklist(checklist) -> dict:
    if not isinstance(checklist, dict):
        raise ValueError("Checklist must be an object")
    missing = [field for field in CHECKLIST_FIELDS if field not in checklist]
    if missing:
        raise ValueError("Checklist missing fields: " + ", ".join(missing))
    extra = sorted(set(checklist) - set(CHECKLIST_FIELDS))
    if extra:
        raise ValueError("Checklist has unknown fields: " + ", ".join(extra))
    for field, value in checklist.items():
        if not isinstance(value, str) or value not in CHECKLIST_VALUES:
            raise ValueError(f"Checklist field {field} has invalid value: {value!r}")
    return {field: checklist[field] for field in CHECKLIST_FIELDS}


def assert_checklist_pending(checklist) -> dict:
    validated = validate_checklist(checklist)
    non_pending = [f for f, v in validated.items() if v != PENDING]
    if non_pending:
        raise ValueError(
            "Automated harness must leave checklist pending; non-pending: "
            + ", ".join(non_pending)
        )
    return validated


def build_required_views(include_mid_lods: bool = True) -> list:
    lods = list(KEY_LODS)
    if include_mid_lods:
        lods = sorted(set(lods).union(OPTIONAL_LODS))
    views = []
    for weapon in WEAPONS:
        for lod in lods:
            for angle in ANGLES:
                name = expected_png_name(weapon, lod, angle)
                views.append({
                    "weapon": weapon,
                    "lod": lod,
                    "angle": angle,
                    "required": lod in KEY_LODS,
                    "optionalMidLod": lod in OPTIONAL_LODS,
                    "expectedFileName": name,
                    "compileGalleryEmits": name is not None,
                    "lodClass": "key" if lod in KEY_LODS else "optional-mid",
                })
    return views


def build_review_manifest(
    *,
    source_commit: str,
    source_dirty: bool,
    package_root=None,
    capture_root=None,
    images=None,
    views=None,
    include_mid_lods: bool = True,
    package_status: str,
    toolchain_note: str,
    toolchain=None,
    gallery_run=None,
    failure=None,
    visual_result: str = PENDING,
) -> dict:
    if visual_result != PENDING:
        raise ValueError("Harness must not claim visual PASS/FAIL; visualResult stays pending")
    if package_status not in ("PendingCapture", "ReadyForHumanReview", "Failed"):
        raise ValueError(f"Unknown packageStatus: {package_status}")
    if not isinstance(source_commit, str) or len(source_commit) < 7:
        raise ValueError("sourceCommit is required")

    resolved_views = views if views is not None else build_required_views(include_mid_lods)
    checklist = assert_checklist_pending(pending_checklist())
    image_map = images or {}
    enriched = []
    missing_required = []
    for view in resolved_views:
        entry = dict(view)
        name = entry.get("expectedFileName")
        present = False
        digest = None
        bytes_count = None
        if name and name in image_map:
            meta = image_map[name]
            present = True
            digest = meta.get("sha256")
            bytes_count = meta.get("bytes")
            if not isinstance(digest, str) or len(digest) != 64:
                raise ValueError(f"Image {name} missing sha256")
        entry["present"] = present
        entry["sha256"] = digest
        entry["bytes"] = bytes_count
        if entry.get("required") and entry.get("compileGalleryEmits") and not present:
            if package_status == "ReadyForHumanReview":
                missing_required.append(name)
        enriched.append(entry)

    if package_status == "ReadyForHumanReview" and missing_required:
        raise ValueError("Missing required Compile-Gallery captures: " + ", ".join(missing_required))

    tool = {
        "note": toolchain_note,
        "compileGalleryCaptureSwitch": "-Capture",
        "unityRequiredForLiveCapture": True,
    }
    if toolchain:
        tool.update(toolchain)

    # Formal packaging disposition: PendingReview once captures are packaged; never visual PASS.
    disposition = {
        "PendingCapture": "PendingCapture",
        "ReadyForHumanReview": "PendingReview",
        "Failed": "PendingCapture",
    }[package_status]
    missing = [
        v["expectedFileName"]
        for v in enriched
        if v.get("required") and v.get("compileGalleryEmits") and not v.get("present")
    ]
    required_viewpoints = [
        {
            "id": Path(v["expectedFileName"]).stem if v.get("expectedFileName") else f"{v['weapon']}-lod{v['lod']}-{v['angle']}",
            "weapon": v["weapon"],
            "view": v["angle"],
            "lod": v["lod"],
            "fileName": v.get("expectedFileName"),
            "required": True,
            "compileGalleryEmits": v.get("compileGalleryEmits"),
        }
        for v in enriched
        if v.get("required")
    ]

    manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "kind": KIND,
        "scope": (
            "M1 P03 formal weapon silhouette/ADS capture review package. "
            "Checklist fields remain pending until human visual review; "
            "this harness never claims visual PASS."
        ),
        "sourceCommit": source_commit,
        "sourceDirty": bool(source_dirty),
        "packageStatus": package_status,
        "disposition": disposition,
        "visualResult": PENDING,
        "visualRealismPass": False,
        "toolchain": tool,
        "captureConvention": CAPTURE_CONVENTION,
        "checklist": checklist,
        "requiredViewpoints": required_viewpoints,
        "missing": missing,
        "inventoryComplete": len(missing) == 0 and package_status == "ReadyForHumanReview",
        "views": enriched,
        "images": image_map,
        "packageRoot": str(package_root) if package_root else None,
        "captureRoot": str(capture_root) if capture_root else None,
        "galleryRun": str(gallery_run) if gallery_run else None,
        "includeMidLods": bool(include_mid_lods),
        "failure": failure,
        "note": (
            "Packaging/inventory is not visual acceptance. Disposition stays PendingReview "
            "until an explicit human reviewer accepts silhouette/ADS realism."
        ),
    }
    validate_manifest(manifest)
    return manifest


def validate_manifest(manifest) -> dict:
    if not isinstance(manifest, dict):
        raise ValueError("Manifest must be an object")
    if manifest.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError("Unsupported schemaVersion")
    if manifest.get("kind") != KIND:
        raise ValueError("Unknown manifest kind")
    if manifest.get("visualResult") != PENDING:
        raise ValueError("visualResult must remain pending in automated packages")
    assert_checklist_pending(manifest.get("checklist"))
    views = manifest.get("views")
    if not isinstance(views, list) or not views:
        raise ValueError("Manifest requires a non-empty views array")
    images = manifest.get("images")
    if images is None:
        images = {}
    if not isinstance(images, dict):
        raise ValueError("images must be an object")
    for name, meta in images.items():
        if not isinstance(name, str) or not name.endswith(".png"):
            raise ValueError(f"Invalid image name: {name}")
        if not isinstance(meta, dict):
            raise ValueError(f"Invalid image metadata for {name}")
        digest = meta.get("sha256")
        if not isinstance(digest, str) or len(digest) != 64 or any(
            c not in "0123456789abcdef" for c in digest
        ):
            raise ValueError(f"Invalid sha256 for {name}")
        if "bytes" in meta and (not isinstance(meta["bytes"], int) or meta["bytes"] < 0):
            raise ValueError(f"Invalid bytes for {name}")
    for view in views:
        if not isinstance(view, dict):
            raise ValueError("Each view must be an object")
        for key in ("weapon", "lod", "angle"):
            if key not in view:
                raise ValueError(f"View missing {key}")
        name = view.get("expectedFileName")
        if name is not None:
            expected = expected_png_name(view["weapon"], int(view["lod"]), view["angle"])
            if name != expected:
                raise ValueError(f"View filename mismatch for {view}: expected {expected}")
            if view.get("present"):
                if name not in images:
                    raise ValueError(f"Present view {name} missing from images map")
                if view.get("sha256") != images[name]["sha256"]:
                    raise ValueError(f"View/image sha256 mismatch for {name}")
    return manifest


def collect_images(capture_root: Path, views: list) -> dict:
    if not capture_root.is_dir():
        raise ValueError(f"Capture root is not a directory: {capture_root}")
    images = {}
    for view in views:
        name = view.get("expectedFileName")
        if not name or not view.get("compileGalleryEmits"):
            continue
        path = capture_root / name
        if not path.is_file():
            continue
        data = path.read_bytes()
        images[name] = {
            "sha256": sha256_bytes(data),
            "bytes": len(data),
            "sourcePath": str(path.resolve()),
        }
    return images


def package_captures(
    capture_root: Path,
    package_root: Path,
    *,
    source_commit: str,
    source_dirty: bool,
    include_mid_lods: bool = True,
    toolchain_note: str,
    toolchain=None,
    gallery_run=None,
) -> dict:
    views = build_required_views(include_mid_lods)
    images = collect_images(capture_root, views)
    package_root.mkdir(parents=True, exist_ok=True)
    images_dir = package_root / "images"
    images_dir.mkdir(parents=True, exist_ok=True)
    packaged = {}
    for name, meta in images.items():
        dest = images_dir / name
        shutil.copy2(meta["sourcePath"], dest)
        digest = sha256_file(dest)
        if digest != meta["sha256"]:
            raise ValueError(f"Copied image hash mismatch: {name}")
        packaged[name] = {
            "sha256": digest,
            "bytes": dest.stat().st_size,
            "packagePath": str(dest.relative_to(package_root).as_posix()),
            "sourcePath": meta["sourcePath"],
        }

    required_emitted = [
        v["expectedFileName"]
        for v in views
        if v["required"] and v["compileGalleryEmits"]
    ]
    missing = [name for name in required_emitted if name not in packaged]
    if missing:
        raise ValueError("Missing required Compile-Gallery captures: " + ", ".join(missing))

    manifest = build_review_manifest(
        source_commit=source_commit,
        source_dirty=source_dirty,
        package_root=package_root,
        capture_root=capture_root,
        images=packaged,
        views=views,
        include_mid_lods=include_mid_lods,
        package_status="ReadyForHumanReview",
        toolchain_note=toolchain_note,
        toolchain=toolchain,
        gallery_run=gallery_run,
    )
    write_review_outputs(package_root, manifest)
    return manifest


def write_pending_capture_report(
    package_root: Path,
    *,
    source_commit: str,
    source_dirty: bool,
    include_mid_lods: bool = True,
    toolchain_note: str,
    toolchain=None,
    failure=None,
    capture_root=None,
) -> dict:
    package_root.mkdir(parents=True, exist_ok=True)
    status = "Failed" if failure else "PendingCapture"
    manifest = build_review_manifest(
        source_commit=source_commit,
        source_dirty=source_dirty,
        package_root=package_root,
        capture_root=capture_root,
        images={},
        views=build_required_views(include_mid_lods),
        include_mid_lods=include_mid_lods,
        package_status=status,
        toolchain_note=toolchain_note,
        toolchain=toolchain,
        failure=failure,
    )
    write_review_outputs(package_root, manifest)
    write_manifest(package_root / "PendingCapture.json", {
        "schemaVersion": SCHEMA_VERSION,
        "kind": "shooter.weapon-silhouette-pending-capture.v1",
        "packageStatus": manifest["packageStatus"],
        "visualResult": PENDING,
        "sourceCommit": source_commit,
        "sourceDirty": bool(source_dirty),
        "requiredEmittedFileNames": sorted({
            v["expectedFileName"]
            for v in manifest["views"]
            if v["required"] and v["compileGalleryEmits"]
        }),
        "unsupportedByCurrentCapture": sorted({
            f"{v['weapon']}-lod{v['lod']}-{v['angle']}"
            for v in manifest["views"]
            if v["required"] and not v["compileGalleryEmits"]
        }),
        "note": (
            "No PNGs were fabricated. Run Tools/Measure-WeaponSilhouette.ps1 -Capture "
            "on a machine with the locked Unity editor, or pass -CaptureRoot to an "
            "existing Compile-Gallery captures directory."
        ),
        "failure": failure,
    })
    return manifest


def write_manifest(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def write_review_outputs(package_root: Path, manifest: dict) -> None:
    """Canonical silhouette-review.json plus review-manifest.json alias for tooling."""
    package_root = Path(package_root)
    write_manifest(package_root / "silhouette-review.json", manifest)
    write_manifest(package_root / "review-manifest.json", manifest)


def inventory_captures(
    capture_root: Path,
    package_root: Path | None = None,
    *,
    source_commit: str = "unknown000",
    source_dirty: bool = False,
    include_mid_lods: bool = True,
    toolchain_note: str = "Inventory-only packaging; no visual PASS.",
    gallery_run=None,
) -> dict:
    """Inventory PNG captures, always emit silhouette-review.json, list missing views.

    Complete sets package images and use disposition PendingReview.
    Incomplete sets still write the review JSON with missing[] and PendingCapture;
    they do not fabricate images or claim visual acceptance.
    """
    capture_root = Path(capture_root)
    if package_root is None:
        package_root = capture_root
    else:
        package_root = Path(package_root)
    views = build_required_views(include_mid_lods)
    images = collect_images(capture_root, views)
    required_emitted = [
        v["expectedFileName"]
        for v in views
        if v["required"] and v["compileGalleryEmits"]
    ]
    missing = [name for name in required_emitted if name not in images]
    package_root.mkdir(parents=True, exist_ok=True)
    packaged = {}
    if not missing:
        images_dir = package_root / "images"
        images_dir.mkdir(parents=True, exist_ok=True)
        for name, meta in images.items():
            dest = images_dir / name
            shutil.copy2(meta["sourcePath"], dest)
            digest = sha256_file(dest)
            if digest != meta["sha256"]:
                raise ValueError(f"Copied image hash mismatch: {name}")
            packaged[name] = {
                "sha256": digest,
                "bytes": dest.stat().st_size,
                "packagePath": str(dest.relative_to(package_root).as_posix()),
                "sourcePath": meta["sourcePath"],
            }
        status = "ReadyForHumanReview"
    else:
        packaged = {
            name: {
                "sha256": meta["sha256"],
                "bytes": meta["bytes"],
                "sourcePath": meta["sourcePath"],
            }
            for name, meta in images.items()
        }
        status = "PendingCapture"

    manifest = build_review_manifest(
        source_commit=source_commit,
        source_dirty=source_dirty,
        package_root=package_root,
        capture_root=capture_root,
        images=packaged,
        views=views,
        include_mid_lods=include_mid_lods,
        package_status=status,
        toolchain_note=toolchain_note,
        gallery_run=gallery_run,
        failure=("Missing required Compile-Gallery captures: " + ", ".join(missing)) if missing else None,
    )
    write_review_outputs(package_root, manifest)
    return manifest


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--source-commit")
    parser.add_argument("--source-dirty", choices=("true", "false"))
    parser.add_argument("--capture-root", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--include-mid-lods", action="store_true", default=True)
    parser.add_argument("--no-mid-lods", action="store_true")
    parser.add_argument("--toolchain-note", default=(
        "Review package built from Compile-Gallery capture naming. "
        "Live capture requires locked Unity via Compile-Gallery.ps1 -Capture."
    ))
    parser.add_argument("--gallery-run", type=Path)
    parser.add_argument("--failure", default=None)
    parser.add_argument(
        "--validate-manifest",
        type=Path,
        help="Validate an existing manifest and exit",
    )
    args = parser.parse_args(argv)

    if args.validate_manifest:
        payload = json.loads(args.validate_manifest.read_text(encoding="utf-8"))
        validate_manifest(payload)
        sys.stdout.write(
            json.dumps({"result": "PASS", "path": str(args.validate_manifest)}, indent=2) + "\n"
        )
        return 0

    if not args.output_dir or not args.source_commit or args.source_dirty is None:
        parser.error(
            "--output-dir, --source-commit and --source-dirty are required "
            "unless --validate-manifest is used"
        )

    dirty = args.source_dirty == "true"
    include_mid = not args.no_mid_lods
    if args.dry_run or not args.capture_root:
        write_pending_capture_report(
            args.output_dir,
            source_commit=args.source_commit,
            source_dirty=dirty,
            include_mid_lods=include_mid,
            toolchain_note=args.toolchain_note,
            failure=args.failure,
            capture_root=args.capture_root,
        )
        sys.stdout.write(json.dumps({
            "result": "PendingCapture" if not args.failure else "Failed",
            "disposition": "PendingCapture",
            "visualResult": PENDING,
            "visualRealismPass": False,
            "packageRoot": str(args.output_dir),
        }, indent=2) + "\n")
        return 0

    # Inventory always writes silhouette-review.json; incomplete sets list missing and exit 1.
    manifest = inventory_captures(
        args.capture_root,
        args.output_dir,
        source_commit=args.source_commit,
        source_dirty=dirty,
        include_mid_lods=include_mid,
        toolchain_note=args.toolchain_note,
        gallery_run=args.gallery_run,
    )
    sys.stdout.write(json.dumps({
        "result": manifest["packageStatus"],
        "disposition": manifest["disposition"],
        "inventoryComplete": manifest["inventoryComplete"],
        "missing": manifest["missing"],
        "visualResult": PENDING,
        "visualRealismPass": False,
        "packageRoot": str(args.output_dir),
    }, indent=2) + "\n")
    return 0 if manifest["inventoryComplete"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
