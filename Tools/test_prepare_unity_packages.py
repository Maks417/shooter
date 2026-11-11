import gzip
import io
import os
from pathlib import Path
import tarfile
import tempfile
import unittest

from prepare_unity_packages import REMOVED, immutable_archive


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


if __name__ == "__main__":
    unittest.main()
