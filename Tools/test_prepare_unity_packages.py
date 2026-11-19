import gzip
import hashlib
import io
import json
import os
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

import prepare_unity_packages as preparation
from prepare_unity_packages import REMOVED, immutable_archive, inventory


class ImmutablePackageTests(unittest.TestCase):
    def populate(self, root):
        files = {"package.json": b'{"name":"test.numeric","version":"1.0.0"}',
                 "Runtime/Numbers.cs": b"// Numeric source only\n", "Runtime/Empty.meta": b"fileFormatVersion: 2\n"}
        files.update({path: b"excluded test-only placeholder" for path in REMOVED})
        for name, data in files.items():
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        (root / "Runtime/Empty").mkdir()
        return files

    def test_independent_gzip_tar_reader_sees_exact_curated_sources(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            files = self.populate(root)
            with tarfile.open(fileobj=io.BytesIO(gzip.decompress(immutable_archive(root)))) as archive:
                actual = {entry.name.removeprefix("package/"): archive.extractfile(entry).read()
                          for entry in archive if entry.isfile()}
                self.assertEqual(actual, {name: data for name, data in files.items() if name not in REMOVED})
                for entry in archive.getmembers():
                    self.assertEqual((entry.uid, entry.gid, entry.mtime), (0, 0, 0))

    def test_host_paths_and_timestamps_do_not_change_archive_identity(self):
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            a, b = Path(first), Path(second)
            self.populate(a)
            self.populate(b)
            for path in b.rglob("*"):
                os.utime(path, (123456789, 987654321))
            self.assertEqual(immutable_archive(a), immutable_archive(b))


class PreparationPreflightTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.editor = self.root / "Editor/Unity.exe"
        self.source = self.editor.parent / "Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.core"
        ImmutablePackageTests().populate(self.source)
        self.archive_bytes = immutable_archive(self.source)
        self.archive_relative = ".local/engine-packages/com.unity.render-pipelines.core-1.0.0-shooter.tgz"
        self.archive = self.root / self.archive_relative
        self.proof = {
            "schemaVersion": 2, "package": "test.numeric", "version": "1.0.0",
            "upstreamInventorySha256": inventory(self.source),
            "curatedInventorySha256": inventory(self.source, REMOVED), "removedPaths": REMOVED,
            "archiveFormat": "pax-tar-gzip-stored-v1", "archivePath": self.archive_relative,
            "archiveSha256": hashlib.sha256(self.archive_bytes).hexdigest(),
            "reason": "Exclude unused Resources debug UI prefabs and their authored sprite dependencies from client/server players; see ADR-013",
        }
        self.lock_path = self.root / "toolchain-lock.json"
        self.manifest_path = self.root / "UnityProject/Packages/manifest.json"
        self.manifest_path.parent.mkdir(parents=True)
        self.manifest_path.write_text('{"dependencies":{"test.package":"1.0.0"}}', encoding="utf-8")
        self.report = self.root / "Artifacts/unity-setup/core-package-curation.json"
        self.write_lock(self.proof)

    def write_lock(self, proof):
        value = {"urpVersion": "1.0.0"}
        if proof is not None:
            value["corePackageCuration"] = proof
        self.lock_path.write_text(json.dumps(value), encoding="utf-8")

    def snapshot(self):
        return {path.relative_to(self.root).as_posix(): path.read_bytes() if path.is_file() else None
                for path in self.root.rglob("*")}

    def invoke(self, *arguments):
        with patch.object(preparation, "ROOT", self.root), \
                patch("sys.argv", ["prepare_unity_packages.py", "--editor", str(self.editor), *arguments]), \
                patch("sys.stdout", new_callable=io.StringIO), patch("sys.stderr", new_callable=io.StringIO):
            preparation.main()

    def assert_rejected_without_changes(self, *arguments, exception=ValueError):
        before = self.snapshot()
        with self.assertRaises(exception):
            self.invoke(*arguments)
        self.assertEqual(self.snapshot(), before)

    def test_missing_or_mismatched_pin_never_materializes_archive(self):
        for proof in [None, {**self.proof, "archiveSha256": "0" * 64},
                      {**self.proof, "curatedInventorySha256": "0" * 64}]:
            with self.subTest(proof=proof):
                self.write_lock(proof)
                self.assert_rejected_without_changes()

    def test_initial_lock_cannot_replace_existing_different_proof(self):
        self.write_lock({**self.proof, "archiveSha256": "0" * 64})
        self.assert_rejected_without_changes("--record-initial-lock")

    def test_migration_requires_exact_schema_one_inputs(self):
        old_proof = {key: value for key, value in self.proof.items()
                     if key not in ("archiveFormat", "archivePath", "archiveSha256")}
        old_proof["schemaVersion"] = 1
        for proof in [None, self.proof, {**old_proof, "curatedInventorySha256": "0" * 64}]:
            with self.subTest(proof=proof):
                self.write_lock(proof)
                self.assert_rejected_without_changes("--migrate-immutable-archive")

    def test_initial_and_migration_flags_are_incompatible(self):
        self.assert_rejected_without_changes("--record-initial-lock", "--migrate-immutable-archive",
                                             exception=SystemExit)

    def test_changed_existing_archive_and_report_are_preserved(self):
        self.archive.parent.mkdir(parents=True)
        self.archive.write_bytes(b"existing unapproved archive")
        self.report.parent.mkdir(parents=True)
        self.report.write_bytes(b"previous report")
        self.assert_rejected_without_changes()

    def test_invalid_manifest_cannot_partially_apply_initial_lock(self):
        for manifest in ["invalid JSON", '{}', '{"dependencies":null}']:
            with self.subTest(manifest=manifest):
                self.write_lock(None)
                self.manifest_path.write_text(manifest, encoding="utf-8")
                self.assert_rejected_without_changes("--record-initial-lock", exception=(ValueError, KeyError, TypeError))

    def test_approved_verification_materializes_exact_archive_without_changing_inputs(self):
        lock_before = self.lock_path.read_bytes()
        manifest_before = self.manifest_path.read_bytes()
        self.invoke()
        self.assertEqual(self.archive.read_bytes(), self.archive_bytes)
        self.assertEqual(json.loads(self.report.read_text()), self.proof)
        self.assertEqual(self.lock_path.read_bytes(), lock_before)
        self.assertEqual(self.manifest_path.read_bytes(), manifest_before)
        before = self.snapshot()
        self.invoke()
        self.assertEqual(self.snapshot(), before)

    def test_unchanged_inputs_skip_rehash_and_any_change_forces_full_check(self):
        self.invoke()
        self.assertTrue((self.root / ".local/unity-curation.stamp").exists())
        with patch.object(preparation, "immutable_archive", side_effect=AssertionError("rehashed despite stamp")):
            self.invoke()
        self.archive.write_bytes(b"tampered archive bytes")
        with self.assertRaises(ValueError):
            self.invoke()
        self.archive.write_bytes(self.archive_bytes)
        (self.source / "Runtime/Numbers.cs").write_bytes(b"// edited source\n")
        with self.assertRaises(ValueError):
            self.invoke()

    def test_approved_initial_lock_and_migration_keep_same_archive_format(self):
        old_proof = {key: value for key, value in self.proof.items()
                     if key not in ("archiveFormat", "archivePath", "archiveSha256")}
        old_proof["schemaVersion"] = 1
        for flag, proof in [("--record-initial-lock", None), ("--migrate-immutable-archive", old_proof)]:
            with self.subTest(flag=flag):
                self.write_lock(proof)
                self.invoke(flag)
                self.assertEqual(self.archive.read_bytes(), self.archive_bytes)
                self.assertEqual(json.loads(self.lock_path.read_text())["corePackageCuration"], self.proof)
                self.assertEqual(json.loads(self.manifest_path.read_text())["dependencies"]["com.unity.render-pipelines.core"],
                                 "file:../../" + self.archive_relative)


if __name__ == "__main__":
    unittest.main()
