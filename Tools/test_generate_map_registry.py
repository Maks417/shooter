"""Regression tests for Tools/generate_map_registry.py (ADR-083). Run: python Tools/test_generate_map_registry.py"""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_map_registry as gen  # noqa: E402

REPO = gen.ROOT


class Repo:
    def __init__(self):
        self.dir = tempfile.TemporaryDirectory()
        self.root = Path(self.dir.name)
        shutil.copytree(REPO / gen.DEFINITIONS, self.root / gen.DEFINITIONS)
        (self.root / gen.REGISTRY_SOURCE).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(REPO / gen.REGISTRY_SOURCE, self.root / gen.REGISTRY_SOURCE)

    def path(self, name):
        return self.root / gen.DEFINITIONS / name

    def edit(self, name, change):
        doc = json.loads(self.path(name).read_text(encoding="utf-8"))
        change(doc)
        self.path(name).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")

    def add(self, name, doc):
        self.path(name).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")


class MapRegistryGeneratorTests(unittest.TestCase):
    def setUp(self):
        self.repo = Repo()

    def tearDown(self):
        self.repo.dir.cleanup()

    def rejects(self, fragment):
        with self.assertRaises(gen.DefinitionError) as caught:
            gen.run(self.repo.root, check=False)
        self.assertIn(fragment, str(caught.exception))

    def new_map(self, **changes):
        doc = json.loads(self.repo.path("training-compound.map.json").read_text(encoding="utf-8"))
        doc.update(id="harbor", order=3, displayName="Harbor", seed="460072033", recipe="map.arena")
        doc.update(changes)
        return doc

    def test_committed_output_is_current(self):
        self.assertEqual([], gen.run(REPO, check=True))

    def test_new_map_is_one_file(self):
        self.repo.add("harbor.map.json", self.new_map())
        gen.run(self.repo.root, check=False)
        text = (self.repo.root / gen.OUT).read_text(encoding="utf-8")
        self.assertIn('new MapDefinition("harbor", 3, "Harbor"', text)
        self.assertTrue(text.index('"harbor"') > text.index('"oil-depot"'))
        self.assertEqual([], gen.run(self.repo.root, check=True))

    def test_frozen_recipes_stay_bound_to_their_maps(self):
        self.repo.add("harbor.map.json", self.new_map(recipe="map.compound"))
        self.rejects("frozen to training-compound")

    def test_planner_rules(self):
        self.repo.add("harbor.map.json", self.new_map(planner="code:harbor-arena"))
        self.rejects("unknown code planner harbor-arena")
        self.repo.edit("depot-yard.map.json", lambda d: d.update(planner="layout"))
        self.repo.path("harbor.map.json").unlink()
        self.rejects("planner layout needs a layout object")

    def test_identity_rules(self):
        self.repo.add("harbor.map.json", self.new_map(seed="460072030"))
        self.rejects("seed also used by")
        self.repo.add("harbor.map.json", self.new_map(order=1))
        self.rejects("order also used by")
        self.repo.add("harbor.map.json", self.new_map(id="port"))
        self.rejects("file name must be port.map.json")

    def test_presentation_hints_are_closed_sets(self):
        self.repo.edit("oil-depot.map.json", lambda d: d["presentation"].update(sky="neon"))
        self.rejects("presentation.sky")

    def test_meta_is_stable(self):
        self.assertEqual(gen.meta_for(gen.OUT), gen.meta_for(gen.OUT))


if __name__ == "__main__":
    unittest.main(verbosity=1)
