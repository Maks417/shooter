"""Regression tests for Tools/generate_weapon_catalog.py (ADR-077). Run: python Tools/test_generate_weapon_catalog.py"""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_weapon_catalog as gen  # noqa: E402

REPO = gen.ROOT


class SyntheticRepo:
    """Copies only what the generator reads: definitions, enum sources and feature recipes."""

    def __init__(self):
        self.dir = tempfile.TemporaryDirectory()
        self.root = Path(self.dir.name)
        shutil.copytree(REPO / gen.DEFINITIONS, self.root / gen.DEFINITIONS)
        shutil.copytree(REPO / gen.FEATURE_RECIPES, self.root / gen.FEATURE_RECIPES)
        for source in gen.ENUM_SOURCES.values():
            (self.root / source).parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(REPO / source, self.root / source)
        for out in (gen.CATALOG_OUT, gen.PROFILES_OUT, gen.MESHES_OUT):
            (self.root / out).parent.mkdir(parents=True, exist_ok=True)

    def path(self, name):
        return self.root / gen.DEFINITIONS / name

    def edit(self, name, change):
        doc = json.loads(self.path(name).read_text(encoding="utf-8"))
        change(doc)
        self.path(name).write_bytes((json.dumps(doc, indent=2) + "\n").encode("utf-8"))

    def write_raw(self, name, text):
        self.path(name).write_bytes(text.encode("utf-8"))

    def close(self):
        self.dir.cleanup()


class GeneratorTests(unittest.TestCase):
    def setUp(self):
        self.repo = SyntheticRepo()

    def tearDown(self):
        self.repo.close()

    def rejects(self, fragment):
        with self.assertRaises(gen.DefinitionError) as caught:
            gen.run(self.repo.root, check=False)
        self.assertIn(fragment, str(caught.exception))

    def test_committed_outputs_are_current(self):
        self.assertEqual([], gen.run(REPO, check=True))

    def test_write_then_check_is_clean_and_deterministic(self):
        written = gen.run(self.repo.root, check=False)
        self.assertEqual(10, len(written))  # five generated catalogs (ADR-107 adds the two sight files) and their .meta
        self.assertEqual([], gen.run(self.repo.root, check=True))
        first = (self.repo.root / gen.CATALOG_OUT).read_bytes()
        gen.run(self.repo.root, check=False)
        self.assertEqual(first, (self.repo.root / gen.CATALOG_OUT).read_bytes())
        self.assertNotIn(b"\r", first)

    def test_check_reports_stale_output(self):
        gen.run(self.repo.root, check=False)
        self.repo.edit("rifle.weapon.json", lambda d: d["combat"].update(baseDamage=31))
        self.assertIn(gen.CATALOG_OUT.as_posix(), gen.run(self.repo.root, check=True))

    def test_new_weapon_is_one_file(self):
        doc = json.loads(self.repo.path("rifle.weapon.json").read_text(encoding="utf-8"))
        doc["id"] = "weapons/dmr"
        doc["order"] = 20
        doc["displayLabel"] = "DMR"
        doc["combat"]["fireMode"] = "SemiAutomatic"
        self.repo.write_raw("dmr.weapon.json", json.dumps(doc, indent=2) + "\n")
        gen.run(self.repo.root, check=False)
        catalog = (self.repo.root / gen.CATALOG_OUT).read_text(encoding="utf-8")
        profiles = (self.repo.root / gen.PROFILES_OUT).read_text(encoding="utf-8")
        self.assertIn('new DefinitionId("weapons/dmr"), FireMode.SemiAutomatic', catalog)
        self.assertTrue(catalog.rindex("weapons/dmr") > catalog.rindex("weapons/grenade"))
        self.assertIn('displayLabel: "DMR"', profiles)

    def test_literal_text_is_preserved(self):
        gen.run(self.repo.root, check=False)
        catalog = (self.repo.root / gen.CATALOG_OUT).read_text(encoding="utf-8")
        self.assertIn("recoilPitchRadians: Deg(0.40f)", catalog)
        self.assertIn("hipSpreadRadians: Deg(1.0f)", catalog)

    def test_presentation_timing_copies_combat_ticks_and_optional_ads_seconds(self):
        # ADR-099: first-person choreography is timed by the same definition file.
        self.repo.edit("pistol.weapon.json", lambda d: d["visual"].update(adsSeconds=0.2))
        gen.run(self.repo.root, check=False)
        profiles = (self.repo.root / gen.PROFILES_OUT).read_text(encoding="utf-8")
        self.assertIn("equipTicks: 18, fireIntervalTicks: 10, reloadTicks: 108, fuseTicks: 0, adsSeconds: 0.2f", profiles)
        self.assertIn("equipTicks: 24, fireIntervalTicks: 6, reloadTicks: 144, fuseTicks: 0, adsSeconds: 0.15f", profiles)
        self.assertIn("fuseTicks: 120", profiles)
        self.repo.edit("pistol.weapon.json", lambda d: d["visual"].update(adsSeconds=0.01))
        self.rejects("adsSeconds")

    def test_unknown_and_missing_fields(self):
        self.repo.edit("rifle.weapon.json", lambda d: d["combat"].update(damage=5))
        self.rejects("unknown damage")
        self.repo.edit("rifle.weapon.json", lambda d: (d["combat"].pop("damage"), d["visual"].pop("optic")))
        self.rejects("missing optic")

    def test_fire_mode_must_suit_family(self):
        self.repo.edit("knife.weapon.json", lambda d: d["combat"].update(fireMode="Automatic"))
        self.rejects("not valid for family Melee")

    def test_enum_values_come_from_csharp(self):
        self.repo.edit("rifle.weapon.json", lambda d: d["audio"].update(shot="LaserShot"))
        self.rejects("audio.shot")
        self.repo.edit("rifle.weapon.json", lambda d: (d["audio"].update(shot="RifleShot"),
                                                       d["hud"].update(killFeedGlyph="None")))
        self.rejects("killFeedGlyph")

    def test_duplicate_order_and_keys(self):
        self.repo.edit("pistol.weapon.json", lambda d: d.update(order=0))
        self.rejects("order also used by")
        text = self.repo.path("knife.weapon.json").read_text(encoding="utf-8")
        self.repo.edit("pistol.weapon.json", lambda d: d.update(order=2))
        self.repo.write_raw("knife.weapon.json", text.replace('"order": 3,', '"order": 3,\n  "order": 7,'))
        self.rejects("duplicate key order")

    def test_file_name_must_match_id(self):
        self.repo.edit("pistol.weapon.json", lambda d: d.update(id="weapons/handgun"))
        self.rejects("file name must be handgun.weapon.json")

    def test_numbers_are_plain_and_bounded(self):
        self.repo.edit("rifle.weapon.json", lambda d: d["combat"].update(magazineSize=0))
        self.rejects("magazineSize")
        text = self.repo.path("pistol.weapon.json").read_text(encoding="utf-8")
        self.repo.edit("rifle.weapon.json", lambda d: d["combat"].update(magazineSize=30))
        self.repo.write_raw("pistol.weapon.json", text.replace('"hipSpreadDegrees": 1.5', '"hipSpreadDegrees": 1.5e0'))
        self.rejects("plain decimal")
        self.repo.write_raw("pistol.weapon.json", text.replace('"reloadTicks": 108', '"reloadTicks": 108.0'))
        self.rejects("integer")

    def test_geometry_recipe_must_exist_and_be_a_weapon_recipe(self):
        self.repo.edit("pistol.weapon.json", lambda d: d["visual"].update(geometryRecipe="missing"))
        self.rejects("not found")
        self.repo.edit("pistol.weapon.json", lambda d: d["visual"].update(geometryRecipe="metal"))
        self.rejects("not a geometry.weapon recipe")

    def test_one_mesh_key_one_recipe(self):
        self.repo.edit("pistol.weapon.json", lambda d: d["visual"].update(meshKey="rifle"))
        self.rejects("meshKey rifle is built from a different recipe")

    def test_new_weapon_can_reuse_existing_geometry(self):
        doc = json.loads(self.repo.path("rifle-carbine.weapon.json").read_text(encoding="utf-8"))
        doc.update(id="weapons/smg", order=20, displayLabel="SMG")
        self.repo.write_raw("smg.weapon.json", json.dumps(doc, indent=2) + "\n")
        gen.run(self.repo.root, check=False)
        meshes = (self.repo.root / gen.MESHES_OUT).read_text(encoding="utf-8")
        self.assertEqual(1, meshes.count('WeaponMeshSource.FromRecipe("rifle-carbine"'))

    def test_props_and_recipes_are_exclusive(self):
        self.repo.edit("knife.weapon.json", lambda d: d["visual"].update(meshKey="machete"))
        self.rejects("required unless meshKey is a built-in prop")
        self.repo.edit("knife.weapon.json", lambda d: d["visual"].update(meshKey="knife", magazineCycle=True))
        self.rejects("optic None and magazineCycle false")
        self.repo.edit("knife.weapon.json", lambda d: d["visual"].update(magazineCycle=False))
        self.repo.edit("pistol.weapon.json", lambda d: d["visual"].update(meshKey="grenade"))
        self.rejects("reserved for the built-in prop")

    def test_inline_geometry_parts_make_a_new_shape_from_one_file(self):
        doc = json.loads(self.repo.path("rifle-carbine.weapon.json").read_text(encoding="utf-8"))
        doc.update(id="weapons/pdw", order=20, displayLabel="PDW")
        del doc["visual"]["geometryRecipe"]
        doc["visual"]["meshKey"] = "pdw"
        doc["visual"]["optic"] = "Irons"
        doc["visual"]["geometryParts"] = {"handguardFrontMm": 320, "barrelFrontMm": 430, "topRailTeeth": 8,
                                          "gasBlockZMm": 320, "gasTubeZMm": 280, "gasTubeLengthMm": 60,
                                          "frontSightZMm": 330, "stock": "none", "optic": "none"}
        self.repo.write_raw("pdw.weapon.json", json.dumps(doc, indent=2) + "\n")
        gen.run(self.repo.root, check=False)
        meshes = (self.repo.root / gen.MESHES_OUT).read_text(encoding="utf-8")
        self.assertIn('WeaponMeshSource.FromRecipe("pdw"', meshes)
        self.assertIn('\\"generatorVersion\\":\\"1.1.0\\"', meshes)
        self.assertIn('\\"barrelFrontMm\\":430', meshes)

    def test_geometry_parts_validation(self):
        self.repo.edit("rifle.weapon.json", lambda d: d["visual"].update(geometryParts={}))
        self.rejects("not both")
        def scoped_without_optic(d):
            del d["visual"]["geometryRecipe"]
            d["visual"]["geometryParts"] = {"handguardFrontMm": 380, "barrelFrontMm": 520, "topRailTeeth": 10,
                                            "gasBlockZMm": 360, "gasTubeZMm": 300, "gasTubeLengthMm": 80,
                                            "frontSightZMm": 400, "stock": "fixed", "optic": "none"}
        self.repo.edit("rifle.weapon.json", scoped_without_optic)
        self.rejects("ScopedPip needs geometryParts.optic compact")

    def test_mesh_catalog_embeds_compact_recipes(self):
        gen.run(self.repo.root, check=False)
        meshes = (self.repo.root / gen.MESHES_OUT).read_text(encoding="utf-8")
        self.assertIn('WeaponMeshSource.Prop("knife")', meshes)
        self.assertIn('\\"silhouette\\":\\"carbine\\"', meshes)

    def test_melee_cannot_have_optic(self):
        self.repo.edit("knife.weapon.json", lambda d: d["visual"].update(optic="Irons"))
        self.rejects("optic None")

    def test_every_family_required(self):
        self.repo.path("grenade.weapon.json").unlink()
        self.rejects("at least one Throwable")

    def test_crlf_rejected(self):
        text = self.repo.path("rifle.weapon.json").read_text(encoding="utf-8")
        self.repo.write_raw("rifle.weapon.json", text.replace("\n", "\r\n"))
        self.rejects("LF line endings")

    def test_meta_guid_is_stable_and_unique(self):
        guids = {gen.meta_for(p) for p in (gen.CATALOG_OUT, gen.PROFILES_OUT, gen.MESHES_OUT)}
        self.assertEqual(3, len(guids))
        self.assertEqual(gen.meta_for(gen.CATALOG_OUT), gen.meta_for(gen.CATALOG_OUT))


if __name__ == "__main__":
    unittest.main(verbosity=1)
