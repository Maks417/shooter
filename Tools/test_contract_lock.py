"""C0 drift tests against disposable copies of the reviewed baseline."""
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from verify_contract_lock import verify


class ContractLockTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        repository = Path(__file__).resolve().parents[1]
        for directory in ["UnityProject/Assets/Shooter/Contracts", "docs/fixtures"]:
            shutil.copytree(repository / directory, self.root / directory)
        for name in ["contract-lock.json", "toolchain-lock.json"]:
            shutil.copyfile(repository / name, self.root / name)

    def test_reviewed_baseline(self):
        self.assertEqual(verify(self.root)["result"], "PASS")

    def test_source_drift(self):
        path = self.root / "UnityProject/Assets/Shooter/Contracts/Values.cs"
        path.write_bytes(path.read_bytes() + b"// drift\n")
        with self.assertRaisesRegex(ValueError, "sources differ"):
            verify(self.root)

    def test_new_source_requires_review(self):
        (self.root / "UnityProject/Assets/Shooter/Contracts/Unreviewed.cs").write_text("// test-only")
        with self.assertRaisesRegex(ValueError, "sources differ"):
            verify(self.root)

    def test_fixture_drift(self):
        path = next((self.root / "docs/fixtures").glob("*.json"))
        path.unlink()
        with self.assertRaisesRegex(ValueError, "fixtures differ"):
            verify(self.root)

    def test_unfrozen_status(self):
        path = self.root / "contract-lock.json"
        lock = json.loads(path.read_text())
        lock["status"] = "Candidate"
        path.write_text(json.dumps(lock))
        with self.assertRaisesRegex(ValueError, "unfrozen"):
            verify(self.root)


if __name__ == "__main__":
    unittest.main()
