"""Unit tests for weapon silhouette review manifest hashing and checklist schema."""
from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import weapon_silhouette as ws


class NamingTests(unittest.TestCase):
    def test_compile_gallery_basenames(self):
        self.assertEqual(ws.expected_png_name("rifle", 0, "ads"), "rifle-ads.png")
        self.assertEqual(ws.expected_png_name("pistol", 0, "three-quarter"), "pistol-three-quarter.png")
        self.assertEqual(ws.expected_png_name("rifle", 3, "side"), "rifle-lod3.png")
        self.assertIsNone(ws.expected_png_name("rifle", 3, "ads"))
        self.assertIsNone(ws.expected_png_name("pistol", 1, "front"))

    def test_required_matrix_includes_key_and_optional_mids(self):
        views = ws.build_required_views(include_mid_lods=True)
        keys = {(v["weapon"], v["lod"], v["angle"]) for v in views}
        for weapon in ("rifle", "pistol"):
            for angle in ws.ANGLES:
                self.assertIn((weapon, 0, angle), keys)
                self.assertIn((weapon, 3, angle), keys)
            self.assertIn((weapon, 1, "side"), keys)
            self.assertIn((weapon, 2, "side"), keys)
        emitted = [v for v in views if v["compileGalleryEmits"]]
        self.assertEqual(len(emitted), 2 * (4 + 3))  # lod0 x4 angles + lod1..3 side


class ChecklistTests(unittest.TestCase):
    def test_pending_checklist_validates(self):
        checklist = ws.pending_checklist()
        self.assertEqual(ws.validate_checklist(checklist), checklist)
        self.assertEqual(ws.assert_checklist_pending(checklist), checklist)

    def test_checklist_rejects_missing_unknown_and_autopass(self):
        with self.assertRaisesRegex(ValueError, "missing"):
            ws.validate_checklist({"scalePlausible": "pending"})
        bad = ws.pending_checklist()
        bad["extra"] = "pending"
        with self.assertRaisesRegex(ValueError, "unknown"):
            ws.validate_checklist(bad)
        passed = ws.pending_checklist()
        passed["silhouetteIntact"] = "pass"
        with self.assertRaisesRegex(ValueError, "pending"):
            ws.assert_checklist_pending(passed)
        with self.assertRaisesRegex(ValueError, "visual PASS"):
            ws.build_review_manifest(
                source_commit="deadbeef",
                source_dirty=False,
                package_status="ReadyForHumanReview",
                toolchain_note="test",
                visual_result="PASS",
            )


class HashAndPackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.capture = self.root / "captures"
        self.capture.mkdir()
        self.package = self.root / "package"

    def _write_png(self, name: str, payload: bytes) -> str:
        path = self.capture / name
        path.write_bytes(payload)
        return hashlib.sha256(payload).hexdigest()

    def _seed_required_captures(self):
        digests = {}
        for weapon in ("rifle", "pistol"):
            for angle in ws.ANGLES:
                digests[f"{weapon}-{angle}.png"] = self._write_png(
                    f"{weapon}-{angle}.png", f"{weapon}-{angle}-bytes".encode()
                )
            for lod in (1, 2, 3):
                digests[f"{weapon}-lod{lod}.png"] = self._write_png(
                    f"{weapon}-lod{lod}.png", f"{weapon}-lod{lod}-bytes".encode()
                )
        return digests

    def test_package_hashes_match_files_and_checklist_stays_pending(self):
        digests = self._seed_required_captures()
        manifest = ws.package_captures(
            self.capture,
            self.package,
            source_commit="abc1234deadbeef",
            source_dirty=True,
            include_mid_lods=True,
            toolchain_note="unit-test",
        )
        self.assertEqual(manifest["packageStatus"], "ReadyForHumanReview")
        self.assertEqual(manifest["disposition"], "PendingReview")
        self.assertEqual(manifest["visualResult"], "pending")
        self.assertFalse(manifest["visualRealismPass"])
        self.assertTrue(manifest["inventoryComplete"])
        self.assertEqual(manifest["missing"], [])
        self.assertEqual(manifest["sourceDirty"], True)
        for field in ws.CHECKLIST_FIELDS:
            self.assertEqual(manifest["checklist"][field], "pending")
        for name, digest in digests.items():
            self.assertEqual(manifest["images"][name]["sha256"], digest)
            copied = self.package / "images" / name
            self.assertTrue(copied.is_file())
            self.assertEqual(ws.sha256_file(copied), digest)
        ws.validate_manifest(manifest)
        on_disk = json.loads((self.package / "silhouette-review.json").read_text(encoding="utf-8"))
        self.assertEqual(on_disk["images"]["rifle-ads.png"]["sha256"], digests["rifle-ads.png"])
        self.assertEqual(on_disk["disposition"], "PendingReview")
        alias = json.loads((self.package / "review-manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(alias["disposition"], "PendingReview")

    def test_missing_required_capture_fails(self):
        self._seed_required_captures()
        (self.capture / "rifle-ads.png").unlink()
        with self.assertRaisesRegex(ValueError, "Missing required"):
            ws.package_captures(
                self.capture,
                self.package,
                source_commit="abc1234deadbeef",
                source_dirty=False,
                toolchain_note="unit-test",
            )

    def test_inventory_missing_and_complete_sets(self):
        self._seed_required_captures()
        (self.capture / "rifle-ads.png").unlink()
        incomplete = ws.inventory_captures(
            self.capture,
            self.package / "incomplete",
            source_commit="abc1234deadbeef",
            source_dirty=False,
        )
        self.assertFalse(incomplete["inventoryComplete"])
        self.assertIn("rifle-ads.png", incomplete["missing"])
        self.assertEqual(incomplete["disposition"], "PendingCapture")
        self.assertFalse(incomplete["visualRealismPass"])
        self.assertTrue((self.package / "incomplete" / "silhouette-review.json").is_file())

        self._write_png("rifle-ads.png", b"rifle-ads-bytes")
        complete = ws.inventory_captures(
            self.capture,
            self.package / "complete",
            source_commit="abc1234deadbeef",
            source_dirty=False,
        )
        self.assertTrue(complete["inventoryComplete"])
        self.assertEqual(complete["missing"], [])
        self.assertEqual(complete["disposition"], "PendingReview")
        self.assertFalse(complete["visualRealismPass"])
        self.assertTrue((self.package / "complete" / "images" / "rifle-front.png").is_file())

    def test_incomplete_image_map_rejected_for_ready_status(self):
        views = ws.build_required_views(True)
        images = {"rifle-front.png": {"sha256": "0" * 64, "bytes": 1}}
        with self.assertRaisesRegex(ValueError, "Missing required"):
            ws.build_review_manifest(
                source_commit="abc1234deadbeef",
                source_dirty=False,
                images=images,
                views=views,
                package_status="ReadyForHumanReview",
                toolchain_note="unit-test",
            )

    def test_dry_run_writes_pending_capture_without_images(self):
        manifest = ws.write_pending_capture_report(
            self.package,
            source_commit="abc1234deadbeef",
            source_dirty=False,
            toolchain_note="dry-run-test",
        )
        self.assertEqual(manifest["packageStatus"], "PendingCapture")
        self.assertEqual(manifest["disposition"], "PendingCapture")
        self.assertEqual(manifest["visualResult"], "pending")
        self.assertFalse(manifest["visualRealismPass"])
        self.assertEqual(manifest["images"], {})
        self.assertFalse((self.package / "images").exists())
        self.assertTrue((self.package / "silhouette-review.json").is_file())
        pending = json.loads((self.package / "PendingCapture.json").read_text(encoding="utf-8"))
        self.assertIn("rifle-ads.png", pending["requiredEmittedFileNames"])
        self.assertIn("rifle-lod3-ads", pending["unsupportedByCurrentCapture"])
        for view in manifest["views"]:
            self.assertFalse(view["present"])

    def test_cli_dry_run_and_validate(self):
        code = ws.main([
            "--output-dir", str(self.package),
            "--source-commit", "abc1234deadbeef",
            "--source-dirty", "false",
            "--dry-run",
        ])
        self.assertEqual(code, 0)
        manifest_path = self.package / "silhouette-review.json"
        self.assertTrue(manifest_path.is_file())
        self.assertEqual(ws.main(["--validate-manifest", str(manifest_path)]), 0)

    def test_cli_packages_capture_root(self):
        digests = self._seed_required_captures()
        code = ws.main([
            "--output-dir", str(self.package),
            "--source-commit", "abc1234deadbeef",
            "--source-dirty", "true",
            "--capture-root", str(self.capture),
        ])
        self.assertEqual(code, 0)
        payload = json.loads((self.package / "silhouette-review.json").read_text(encoding="utf-8"))
        self.assertEqual(payload["packageStatus"], "ReadyForHumanReview")
        self.assertEqual(payload["disposition"], "PendingReview")
        self.assertEqual(payload["visualResult"], "pending")
        self.assertFalse(payload["visualRealismPass"])
        self.assertEqual(payload["images"]["pistol-lod3.png"]["sha256"], digests["pistol-lod3.png"])

    def test_cli_missing_capture_exits_nonzero(self):
        self._seed_required_captures()
        (self.capture / "pistol-front.png").unlink()
        code = ws.main([
            "--output-dir", str(self.package),
            "--source-commit", "abc1234deadbeef",
            "--source-dirty", "false",
            "--capture-root", str(self.capture),
        ])
        self.assertEqual(code, 1)
        payload = json.loads((self.package / "silhouette-review.json").read_text(encoding="utf-8"))
        self.assertFalse(payload["inventoryComplete"])
        self.assertIn("pistol-front.png", payload["missing"])
        self.assertEqual(payload["disposition"], "PendingCapture")
        self.assertFalse(payload["visualRealismPass"])


if __name__ == "__main__":
    unittest.main()
