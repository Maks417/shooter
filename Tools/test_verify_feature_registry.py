"""Regression tests for Tools/verify_feature_registry.py (ADR-121). Run: python Tools/test_verify_feature_registry.py"""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import verify_feature_registry as reg  # noqa: E402

REPO = reg.ROOT


class CanonicalTests(unittest.TestCase):
    def test_comments_and_whitespace_do_not_matter(self):
        a = "public static int X(int a, int b) { return a + b; } // sum\n"
        b = "/// <summary>Sum.</summary>\npublic static   int X( int a,\n    int b )\n{\n    return a + b; /* add */\n}\n"
        self.assertEqual(reg.cs_canonical(a), reg.cs_canonical(b))

    def test_code_literals_and_numbers_matter(self):
        base = reg.cs_canonical('float k = 0.25f; string s = "a // b";')
        self.assertNotEqual(base, reg.cs_canonical('float k = 0.26f; string s = "a // b";'))
        self.assertNotEqual(base, reg.cs_canonical('float k = 0.25f; string s = "a //  b";'))
        self.assertNotEqual(reg.cs_canonical("int ab;"), reg.cs_canonical("int a b;"))
        self.assertIn('"a // b"', base)

    def test_char_escapes_and_verbatim_strings(self):
        self.assertEqual("char q = '\\'';", reg.cs_canonical("char q = '\\''; // quote"))
        self.assertEqual('var p = @"C:\\x ""y"" // z";', reg.cs_canonical('var p = @"C:\\x ""y"" // z"; // c'))


class RegistryTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.root = Path(self.dir.name)
        for rel in list(reg.DIRECTORIES) + ["docs/weapon-recipe-baseline.md", "docs/soldier-baseline.md", "feature-content-lock.json"]:
            src, dst = REPO / rel, self.root / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            if src.is_dir():
                shutil.copytree(src, dst)
            else:
                shutil.copyfile(src, dst)
        lock = json.loads((self.root / "feature-content-lock.json").read_text(encoding="utf-8"))
        if lock.get("sourceHashPolicy") != reg.POLICY:
            reg.migrate_policy("ADR-121", root=self.root)

    def tearDown(self):
        self.dir.cleanup()

    def some_cs(self):
        return sorted((self.root / reg.DIRECTORIES[1]).glob("*.cs"))[0]

    def test_committed_lock_verifies(self):
        self.assertEqual("PASS", reg.verify(REPO)["result"])

    def test_comment_edit_passes_code_edit_fails(self):
        path = self.some_cs()
        text = path.read_text(encoding="utf-8")
        path.write_bytes(("// reviewed note\n" + text.replace("\n", "\n\n", 3)).encode("utf-8"))
        self.assertEqual("PASS", reg.verify(self.root)["result"])
        path.write_bytes((text.replace("return", "return  0 * 0 +", 1) if "return" in text else text + "\nclass Extra {}\n").encode("utf-8"))
        with self.assertRaises(ValueError):
            reg.verify(self.root)

    def test_recipe_bytes_stay_raw(self):
        recipe = sorted((self.root / "UnityProject/Assets/Shooter/Generation/FeatureRecipes").glob("*.json"))[0]
        recipe.write_bytes(recipe.read_bytes() + b"\n")
        with self.assertRaises(ValueError):
            reg.verify(self.root)

    def test_refreeze_requires_an_adr(self):
        with self.assertRaises(ValueError):
            reg.refreeze("because", root=self.root)


if __name__ == "__main__":
    unittest.main()
