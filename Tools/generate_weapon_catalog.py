"""Generate the frozen in-process weapon catalogs from Content/Weapons/*.weapon.json (ADR-077).

One definition file per weapon holds its combat stats, visual/handling profile, audio voice and kill-feed glyph.
This script validates every file and writes seven C# files:

  UnityProject/Assets/Shooter/Simulation/Combat/WeaponCatalog.g.cs              (combat rows, Simulation)
  UnityProject/Assets/Shooter/Simulation/Combat/SightCatalog.g.cs               (ADR-107 sight options, Simulation)
  UnityProject/Assets/Shooter/Presentation/Model/View/WeaponVisualProfiles.g.cs (presentation rows)
  UnityProject/Assets/Shooter/Presentation/Model/View/WeaponSightChoices.g.cs   (ADR-107 sight options, presentation)
  UnityProject/Assets/Shooter/Generation/Core/Environment/WeaponMeshCatalog.g.cs (meshes for the weapon package, ADR-078;
                                                                                  ADR-107 rail-mounted sight meshes)
  UnityProject/Assets/Shooter/Presentation/Model/Hud/KillFeedGlyphs.g.cs        (T195 kill-feed icon names, fallback
                                                                                  silhouettes and the review gallery)
  UnityProject/Assets/Shooter/Presentation/Model/Audio/Runtime/WeaponShotVoices.g.cs (T195 gun-report voices and the
                                                                                  interim derivation of named reports)

The catalogs stay compiled C# (ADR-072 forbids runtime JSON combat tables). Numbers are emitted with the exact
literal text from the JSON file, so the generated expressions are bit-identical to hand-written ones.

Usage:
  python Tools/generate_weapon_catalog.py           # validate and (re)write the generated files and their .meta
  python Tools/generate_weapon_catalog.py --check   # validate and fail if the generated files are stale
"""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFINITIONS = Path("Content/Weapons")
ASSETS = Path("UnityProject/Assets/Shooter")
CATALOG_OUT = ASSETS / "Simulation/Combat/WeaponCatalog.g.cs"
PROFILES_OUT = ASSETS / "Presentation/Model/View/WeaponVisualProfiles.g.cs"
MESHES_OUT = ASSETS / "Generation/Core/Environment/WeaponMeshCatalog.g.cs"
SIGHTS_OUT = ASSETS / "Simulation/Combat/SightCatalog.g.cs"
SIGHT_CHOICES_OUT = ASSETS / "Presentation/Model/View/WeaponSightChoices.g.cs"
GLYPHS_OUT = ASSETS / "Presentation/Model/Hud/KillFeedGlyphs.g.cs"
SHOT_VOICES_OUT = ASSETS / "Presentation/Model/Audio/Runtime/WeaponShotVoices.g.cs"
# T195: fallback kill-feed silhouette when the mesh-traced icon is unavailable; defaults by family.
SILHOUETTES = ["Rifle", "Carbine", "Pistol", "Knife", "Grenade"]
DEFAULT_SILHOUETTE = {"PrimaryGun": "Rifle", "Sidearm": "Pistol", "Melee": "Knife", "Throwable": "Grenade"}
GUN_FAMILIES = {"PrimaryGun", "Sidearm"}
# Shipped recorded reports a named gun's interim voice may be derived from (CombatAudioBankBaker.DerivedFrom).
DERIVE_SOURCES = {"RifleShot", "PistolShot"}
FEATURE_RECIPES = ASSETS / "Generation/FeatureRecipes"
SCHEMA_ID = "shooter.weapon.v1"
# Mesh keys built by LoadoutPropGeometry instead of a geometry.weapon recipe (no magazine or cycle part).
PROP_MESH_KEYS = {"knife", "grenade"}
# ADR-107 rail-mounted sights (loadout order) and the optic mode each one presents with.
SIGHT_OPTICS = {"irons": "Irons", "collimator": "Collimator", "scope2": "ScopedPip", "scope4": "ScopedPip", "scope8": "ScopedPip"}
SIGHT_ORDER = ["irons", "collimator", "scope2", "scope4", "scope8"]
# geometry.weapon 1.4.0 bare-rail bodies take a separate sight; these archetypes have a rail.
MOUNT_VERSIONS = {"1.4.0", "1.5.0"}  # ADR-107 bare-rail bodies; ADR-118 adds 1.5.0 and its kinds
MOUNT_KINDS = {"ak", "ar", "dmr", "bolt", "scar", "pump"}

# Enum names are read from the C# sources so the generator can never drift from the code it feeds.
ENUM_SOURCES = {
    "FireMode": ASSETS / "Simulation/Combat/WeaponDefinition.cs",
    "WeaponFamily": ASSETS / "Simulation/Combat/WeaponCatalog.cs",
    "WeaponOpticMode": ASSETS / "Presentation/Model/View/WeaponVisualProfiles.cs",
    "SynthVoiceKind": ASSETS / "Presentation/Model/Audio/Runtime/SynthAudioMixer.cs",
    "KillFeedWeapon": ASSETS / "Presentation/Model/Hud/KillFeedText.cs",
    "WeaponHandlingStyle": ASSETS / "Presentation/Model/View/WeaponVisualProfiles.cs",
}

ID_PATTERN = re.compile(r"^weapons/[a-z0-9]+(?:-[a-z0-9]+)*$")
KEY_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
LABEL_PATTERN = re.compile(r"^[A-Za-z0-9 .\-]{1,24}$")
NUMBER_PATTERN = re.compile(r"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$")

TOP_KEYS = {"schemaId", "id", "order", "displayLabel", "family", "combat", "visual", "audio", "hud"}
COMBAT_REQUIRED = ["fireMode", "fireIntervalTicks", "reloadTicks", "equipTicks", "magazineSize", "reserveCapacity",
                   "baseDamage", "hipSpreadDegrees", "adsSpreadDegrees", "recoilPitchDegrees", "recoilYawHalfDegrees"]

VISUAL_REQUIRED = ["meshKey", "optic", "magazineCycle", "nearFieldScale", "nearFieldHipBias", "hipRotationDegrees",
                   "cycleTravelMeters"]
VISUAL_OPTIONAL = {"geometryRecipe", "geometryParts", "muzzleFx", "adsSeconds",
                   # ADR-102: handling choreography, magnified-optic FOV and muzzle flash size.
                   "handlingStyle", "adsFovDegrees", "muzzleFlashScale", "muzzleFlashSideJets",
                   # ADR-107: rail-mounted sight choices {"default": name, "allowed": [names]}.
                   "sights"}
# ADR-082 geometry.weapon 1.1.0 numeric rifle parts (millimetres or counts).
GEOMETRY_PART_NUMBERS = ["handguardFrontMm", "barrelFrontMm", "topRailTeeth", "gasBlockZMm", "gasTubeZMm",
                         "gasTubeLengthMm", "frontSightZMm"]
FAMILY_FIRE_MODES = {
    "PrimaryGun": {"Automatic", "SemiAutomatic", "Burst"},
    "Sidearm": {"Automatic", "SemiAutomatic"},
    "Melee": {"Melee"},
    "Throwable": {"Throwable"},
}

HEADER = "// <auto-generated>\n// Generated by Tools/generate_weapon_catalog.py from Content/Weapons/*.weapon.json (ADR-077).\n// Do not edit by hand: change the definition file and rerun the generator.\n// </auto-generated>\n"
META_TEMPLATE = ("fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
                 "  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n"
                 "  assetBundleName: \n  assetBundleVariant: \n")


class DefinitionError(ValueError):
    pass


# Optional combat fields fall back to the WeaponDefinition constructor defaults (COMBAT_OPTIONAL below).
class Literal(str):
    """A JSON number kept as its exact source text."""


COMBAT_OPTIONAL = {"maxRangeMeters": Literal("300"), "fuseTicks": Literal("0"),
                   "throwSpeedMetersPerSecond": Literal("0"), "blastRadiusMeters": Literal("0"),
                   "consumesAmmo": True, "allowsSprintFire": False,
                   # ADR-102 handling: burst cadence, range falloff, move speed and random-loadout weight.
                   "burstCount": Literal("0"), "burstIntervalTicks": Literal("0"),
                   "falloffStartMeters": Literal("0"), "falloffEndMeters": Literal("0"), "falloffMinScale": Literal("1"),
                   "moveSpeedScale": Literal("1"), "spawnWeight": Literal("1"),
                   # ADR-118: shotgun pellets per shot, their cone, and shell-by-shell reloading.
                   "pelletCount": Literal("1"), "pelletSpreadDegrees": Literal("0"), "reloadPerRound": False,
                   # ADR-125: every hit takes the target's remaining health (bolt-action snipers).
                   "lethalOnHit": False}


def enum_members(root: Path, name: str) -> set:
    text = (root / ENUM_SOURCES[name]).read_text(encoding="utf-8")
    match = re.search(r"\benum\s+" + name + r"\b\s*(?::\s*\w+)?\s*\{([^}]*)\}", text)
    if not match:
        raise DefinitionError("enum " + name + " not found in " + str(ENUM_SOURCES[name]))
    body = re.sub(r"//[^\n]*", "", match.group(1))
    return {part.split("=")[0].strip() for part in body.split(",") if part.strip()}


def parse(text: str, where: str):
    def no_duplicates(pairs):
        seen = {}
        for key, value in pairs:
            if key in seen:
                raise DefinitionError(where + ": duplicate key " + key)
            seen[key] = value
        return seen

    try:
        return json.loads(text, object_pairs_hook=no_duplicates, parse_float=Literal, parse_int=Literal)
    except json.JSONDecodeError as error:
        raise DefinitionError(where + ": invalid JSON: " + str(error)) from error


def keys(obj, where, required, optional=()):
    if not isinstance(obj, dict):
        raise DefinitionError(where + ": expected an object")
    missing = [k for k in required if k not in obj]
    unknown = sorted(set(obj) - set(required) - set(optional))
    if missing:
        raise DefinitionError(where + ": missing " + ", ".join(missing))
    if unknown:
        raise DefinitionError(where + ": unknown " + ", ".join(unknown))


def number(value, where, minimum=0.0, maximum=None, integer=False):
    if isinstance(value, bool) or not isinstance(value, Literal):
        raise DefinitionError(where + ": expected a number")
    if not NUMBER_PATTERN.match(value) or (integer and "." in value):
        raise DefinitionError(where + ": expected " + ("an integer" if integer else "a plain decimal") + ", got " + value)
    parsed = float(value)
    if parsed < minimum or (maximum is not None and parsed > maximum):
        raise DefinitionError(where + ": " + value + " is outside [" + str(minimum) + ", " + str(maximum) + "]")
    return value


def boolean(value, where):
    if not isinstance(value, bool):
        raise DefinitionError(where + ": expected true or false")
    return value


def choice(value, where, allowed):
    if not isinstance(value, str) or value not in allowed:
        raise DefinitionError(where + ": expected one of " + ", ".join(sorted(allowed)))
    return value


def vector(value, where, limit):
    if not isinstance(value, list) or len(value) != 3:
        raise DefinitionError(where + ": expected [x, y, z]")
    return [number(x, where, minimum=-limit, maximum=limit) for x in value]


def adsfov(value, where):
    text = number(value, where, maximum=80)
    if float(text) != 0 and float(text) < 5:
        raise DefinitionError(where + ": 0 (optic default) or 5..80 degrees")
    return text


def validate(root: Path, path: Path, doc, enums) -> dict:
    where = str(path.relative_to(root)).replace("\\", "/")
    keys(doc, where, sorted(TOP_KEYS))
    if doc["schemaId"] != SCHEMA_ID:
        raise DefinitionError(where + ": schemaId must be " + SCHEMA_ID)
    weapon_id = doc["id"]
    if not isinstance(weapon_id, str) or not ID_PATTERN.match(weapon_id):
        raise DefinitionError(where + ": id must match weapons/<kebab-name>")
    if path.name != weapon_id.split("/", 1)[1] + ".weapon.json":
        raise DefinitionError(where + ": file name must be " + weapon_id.split("/", 1)[1] + ".weapon.json")
    order = int(number(doc["order"], where + ".order", maximum=255, integer=True))
    label = doc["displayLabel"]
    if not isinstance(label, str) or not LABEL_PATTERN.match(label):
        raise DefinitionError(where + ".displayLabel: 1-24 ASCII letters, digits, spaces, '.' or '-'")
    family = choice(doc["family"], where + ".family", enums["WeaponFamily"])

    combat = doc["combat"]
    keys(combat, where + ".combat", COMBAT_REQUIRED, COMBAT_OPTIONAL)
    c = where + ".combat."
    fire_mode = choice(combat["fireMode"], c + "fireMode", enums["FireMode"])
    if fire_mode not in FAMILY_FIRE_MODES.get(family, set()):
        raise DefinitionError(c + "fireMode: " + fire_mode + " is not valid for family " + family)
    fields = {"fireMode": fire_mode}
    for name in ("fireIntervalTicks", "reloadTicks", "equipTicks", "fuseTicks"):
        fields[name] = number(combat.get(name, COMBAT_OPTIONAL.get(name)), c + name, maximum=4294967295, integer=True)
    fields["magazineSize"] = number(combat["magazineSize"], c + "magazineSize", minimum=1, maximum=65535, integer=True)
    for name in ("reserveCapacity", "baseDamage"):
        fields[name] = number(combat[name], c + name, maximum=65535, integer=True)
    for name in ("hipSpreadDegrees", "adsSpreadDegrees", "recoilPitchDegrees", "recoilYawHalfDegrees"):
        fields[name] = number(combat[name], c + name, maximum=45)
    for name in ("maxRangeMeters", "throwSpeedMetersPerSecond", "blastRadiusMeters"):
        fields[name] = number(combat.get(name, COMBAT_OPTIONAL[name]), c + name, maximum=10000)
    for name in ("consumesAmmo", "allowsSprintFire"):
        fields[name] = boolean(combat.get(name, COMBAT_OPTIONAL[name]), c + name)
    fields["burstCount"] = number(combat.get("burstCount", COMBAT_OPTIONAL["burstCount"]), c + "burstCount", maximum=8, integer=True)
    fields["burstIntervalTicks"] = number(combat.get("burstIntervalTicks", COMBAT_OPTIONAL["burstIntervalTicks"]),
                                          c + "burstIntervalTicks", maximum=60, integer=True)
    for name in ("falloffStartMeters", "falloffEndMeters"):
        fields[name] = number(combat.get(name, COMBAT_OPTIONAL[name]), c + name, maximum=1000)
    fields["falloffMinScale"] = number(combat.get("falloffMinScale", COMBAT_OPTIONAL["falloffMinScale"]), c + "falloffMinScale",
                                       minimum=0.1, maximum=1)
    fields["moveSpeedScale"] = number(combat.get("moveSpeedScale", COMBAT_OPTIONAL["moveSpeedScale"]), c + "moveSpeedScale",
                                      minimum=0.5, maximum=1.2)
    fields["spawnWeight"] = number(combat.get("spawnWeight", COMBAT_OPTIONAL["spawnWeight"]), c + "spawnWeight",
                                   maximum=1000, integer=True)
    fields["pelletCount"] = number(combat.get("pelletCount", COMBAT_OPTIONAL["pelletCount"]), c + "pelletCount",
                                   minimum=1, maximum=16, integer=True)
    fields["pelletSpreadDegrees"] = number(combat.get("pelletSpreadDegrees", COMBAT_OPTIONAL["pelletSpreadDegrees"]),
                                           c + "pelletSpreadDegrees", maximum=15)
    fields["reloadPerRound"] = boolean(combat.get("reloadPerRound", COMBAT_OPTIONAL["reloadPerRound"]), c + "reloadPerRound")
    if int(fields["pelletCount"]) > 1 and (float(fields["pelletSpreadDegrees"]) <= 0 or fire_mode != "SemiAutomatic"):
        raise DefinitionError(c + "pelletCount > 1 needs pelletSpreadDegrees > 0 and fireMode SemiAutomatic")
    if int(fields["pelletCount"]) == 1 and float(fields["pelletSpreadDegrees"]) != 0:
        raise DefinitionError(c + "pelletSpreadDegrees is only valid with pelletCount > 1")
    if fields["reloadPerRound"] and family not in ("PrimaryGun", "Sidearm"):
        raise DefinitionError(c + "reloadPerRound is only valid for guns")
    fields["lethalOnHit"] = boolean(combat.get("lethalOnHit", COMBAT_OPTIONAL["lethalOnHit"]), c + "lethalOnHit")
    if fields["lethalOnHit"] and (family not in ("PrimaryGun", "Sidearm") or int(fields["pelletCount"]) != 1):
        raise DefinitionError(c + "lethalOnHit is only valid for single-projectile guns")
    if fire_mode == "Burst" and (int(fields["burstCount"]) < 2 or int(fields["burstIntervalTicks"]) < 1):
        raise DefinitionError(c + "Burst needs burstCount >= 2 and burstIntervalTicks >= 1")
    if fire_mode != "Burst" and int(fields["burstCount"]) != 0:
        raise DefinitionError(c + "burstCount is only valid for fireMode Burst")
    if float(fields["falloffStartMeters"]) > 0 and float(fields["falloffEndMeters"]) <= float(fields["falloffStartMeters"]):
        raise DefinitionError(c + "falloffEndMeters must be greater than falloffStartMeters")
    if family in ("PrimaryGun", "Sidearm") and int(fields["fireIntervalTicks"]) < 1:
        raise DefinitionError(c + "fireIntervalTicks: guns need at least 1 tick between shots")
    if family == "Throwable" and (int(fields["fuseTicks"]) < 1 or float(fields["blastRadiusMeters"]) <= 0):
        raise DefinitionError(c + "throwables need fuseTicks >= 1 and blastRadiusMeters > 0")

    visual = doc["visual"]
    keys(visual, where + ".visual", VISUAL_REQUIRED, VISUAL_OPTIONAL)
    v = where + ".visual."
    mesh_key = visual["meshKey"]
    if not isinstance(mesh_key, str) or not KEY_PATTERN.match(mesh_key):
        raise DefinitionError(v + "meshKey: expected a kebab-case key")
    optic = choice(visual["optic"], v + "optic", enums["WeaponOpticMode"])
    magazine_cycle = boolean(visual["magazineCycle"], v + "magazineCycle")
    if family in ("Melee", "Throwable") and (optic != "None" or magazine_cycle):
        raise DefinitionError(v + family + " weapons need optic None and magazineCycle false")
    muzzle_fx = visual.get("muzzleFx", "pistol" if family == "Sidearm" else "none" if family in ("Melee", "Throwable") else "rifle")
    if muzzle_fx not in {"none", "rifle", "pistol", "carbine"}:
        raise DefinitionError(v + "muzzleFx: unknown profile")
    recipe_source = None
    recipe_version = recipe_kind = None
    if "geometryRecipe" in visual and "geometryParts" in visual:
        raise DefinitionError(v + "use geometryRecipe or geometryParts, not both")
    if "geometryParts" in visual:
        # ADR-082: inline geometry.weapon 1.1.0 rifle parts; the C# recipe parser enforces every range.
        if mesh_key in PROP_MESH_KEYS:
            raise DefinitionError(v + "meshKey: " + mesh_key + " is reserved for the built-in prop")
        if family not in ("PrimaryGun", "Sidearm"):
            raise DefinitionError(v + "geometryParts: only guns have rifle parts")
        parts = visual["geometryParts"]
        keys(parts, v + "geometryParts", GEOMETRY_PART_NUMBERS + ["stock", "optic"])
        resolved = {name: int(number(parts[name], v + "geometryParts." + name, maximum=1000, integer=True))
                    for name in GEOMETRY_PART_NUMBERS}
        resolved["stock"] = choice(parts["stock"], v + "geometryParts.stock", {"fixed", "none"})
        resolved["optic"] = choice(parts["optic"], v + "geometryParts.optic", {"compact", "none"})
        if resolved["optic"] == "none" and optic == "ScopedPip":
            raise DefinitionError(v + "optic: ScopedPip needs geometryParts.optic compact")
        recipe = {"schemaId": "shooter.recipe.weapon.v1", "recipeId": weapon_id, "generatorId": "geometry.weapon",
                  "generatorVersion": "1.1.0", "seed": "460072030",
                  "parameters": {"kind": "rifle", "parts": {k: resolved[k] for k in sorted(resolved)}}, "dependencies": []}
        recipe_source = json.dumps(recipe, separators=(",", ":"), ensure_ascii=True)
    elif "geometryRecipe" not in visual:
        if mesh_key not in PROP_MESH_KEYS:
            raise DefinitionError(v + "geometryRecipe or geometryParts: required unless meshKey is a built-in prop ("
                                  + ", ".join(sorted(PROP_MESH_KEYS)) + ")")
        if magazine_cycle:
            raise DefinitionError(v + "magazineCycle: prop meshes have no magazine or cycle part")
    else:
        if mesh_key in PROP_MESH_KEYS:
            raise DefinitionError(v + "meshKey: " + mesh_key + " is reserved for the built-in prop")
        recipe_name = visual["geometryRecipe"]
        if not isinstance(recipe_name, str) or not KEY_PATTERN.match(recipe_name):
            raise DefinitionError(v + "geometryRecipe: expected a recipe file stem")
        recipe_path = root / FEATURE_RECIPES / (recipe_name + ".json")
        if not recipe_path.is_file():
            raise DefinitionError(v + "geometryRecipe: " + str(FEATURE_RECIPES / (recipe_name + ".json")) + " not found")
        recipe = json.loads(recipe_path.read_text(encoding="utf-8"))
        if recipe.get("generatorId") != "geometry.weapon":
            raise DefinitionError(v + "geometryRecipe: " + recipe_name + " is not a geometry.weapon recipe")
        recipe_source = json.dumps(recipe, separators=(",", ":"), ensure_ascii=True)
        recipe_version = recipe.get("generatorVersion")
        recipe_kind = recipe.get("parameters", {}).get("kind")

    sights = None
    mounted = "geometryRecipe" in visual and recipe_version in MOUNT_VERSIONS and recipe_kind in MOUNT_KINDS
    if "sights" in visual:
        if not mounted or family != "PrimaryGun":
            raise DefinitionError(v + "sights: only primary guns built from a geometry.weapon 1.4.0/1.5.0"
                                  + " rail recipe take mounted sights")
        spec = visual["sights"]
        keys(spec, v + "sights", ["default", "allowed"])
        allowed = spec["allowed"]
        if not isinstance(allowed, list) or not allowed:
            raise DefinitionError(v + "sights.allowed: expected a non-empty list")
        for i, name in enumerate(allowed):
            choice(name, v + "sights.allowed[" + str(i) + "]", set(SIGHT_OPTICS))
        if len(set(allowed)) != len(allowed):
            raise DefinitionError(v + "sights.allowed: duplicate sight")
        if allowed != sorted(allowed, key=SIGHT_ORDER.index):
            raise DefinitionError(v + "sights.allowed: list sights in loadout order (" + ", ".join(SIGHT_ORDER) + ")")
        default = choice(spec["default"], v + "sights.default", set(allowed))
        if optic != SIGHT_OPTICS[default]:
            raise DefinitionError(v + "optic: the default sight " + default + " presents as " + SIGHT_OPTICS[default])
        sights = {"kind": recipe_kind, "default": default, "allowed": list(allowed)}
    elif mounted:
        raise DefinitionError(v + "sights: required for a geometry.weapon 1.4.0/1.5.0 rail recipe")

    audio = doc["audio"]
    keys(audio, where + ".audio", ["shot"], ["derive"])
    derive = None
    if "derive" in audio:
        d = audio["derive"]
        keys(d, where + ".audio.derive", ["from", "pitch", "gain"])
        if family not in GUN_FAMILIES:
            raise DefinitionError(where + ".audio.derive: only gun reports are derived")
        derive = {"from": choice(d["from"], where + ".audio.derive.from", DERIVE_SOURCES),
                  "pitch": number(d["pitch"], where + ".audio.derive.pitch", minimum=0.25, maximum=4),
                  "gain": number(d["gain"], where + ".audio.derive.gain", minimum=0.1, maximum=2)}
    hud = doc["hud"]
    keys(hud, where + ".hud", ["killFeedGlyph"], ["silhouette"])
    glyph = choice(hud["killFeedGlyph"], where + ".hud.killFeedGlyph", enums["KillFeedWeapon"] - {"None"})
    silhouette = choice(hud.get("silhouette", DEFAULT_SILHOUETTE[family]), where + ".hud.silhouette", set(SILHOUETTES))

    return {
        "source": where, "id": weapon_id, "order": order, "label": label, "family": family, "combat": fields,
        "meshKey": mesh_key, "optic": optic, "magazineCycle": magazine_cycle,
        "nearFieldScale": number(visual["nearFieldScale"], v + "nearFieldScale", minimum=0.1, maximum=4),
        "nearFieldHipBias": vector(visual["nearFieldHipBias"], v + "nearFieldHipBias", 1),
        "hipRotationDegrees": vector(visual["hipRotationDegrees"], v + "hipRotationDegrees", 180),
        "muzzleFx": muzzle_fx,
        "cycleTravelMeters": number(visual["cycleTravelMeters"], v + "cycleTravelMeters", maximum=0.5),
        # ADR-099: cosmetic ADS raise time; the C# default is 0.15 s.
        "adsSeconds": number(visual.get("adsSeconds", Literal("0.15")), v + "adsSeconds", minimum=0.05, maximum=1),
        "handlingStyle": choice(visual.get("handlingStyle", "Auto"), v + "handlingStyle", enums["WeaponHandlingStyle"]),
        "adsFovDegrees": adsfov(visual.get("adsFovDegrees", Literal("0")), v + "adsFovDegrees"),
        "muzzleFlashScale": number(visual.get("muzzleFlashScale", Literal("-1")), v + "muzzleFlashScale", minimum=-1, maximum=2.5)
        if "muzzleFlashScale" not in visual else number(visual["muzzleFlashScale"], v + "muzzleFlashScale", minimum=0, maximum=2.5),
        "muzzleFlashSideJets": boolean(visual.get("muzzleFlashSideJets", False), v + "muzzleFlashSideJets"),
        "shotVoice": choice(audio["shot"], where + ".audio.shot", enums["SynthVoiceKind"]),
        "shotDerive": derive,
        "glyph": glyph,
        "silhouette": silhouette,
        "recipeSource": recipe_source,
        "sights": sights,
    }


def load(root: Path) -> list:
    enums = {name: enum_members(root, name) for name in ENUM_SOURCES}
    files = sorted((root / DEFINITIONS).glob("*.weapon.json"))
    if not files:
        raise DefinitionError("no weapon definitions under " + str(DEFINITIONS))
    weapons = []
    for path in files:
        raw = path.read_bytes()
        if b"\r" in raw:
            raise DefinitionError(str(path.relative_to(root)) + ": expected LF line endings")
        weapons.append(validate(root, path, parse(raw.decode("utf-8"), str(path.relative_to(root))), enums))
    for attr in ("id", "order"):
        seen = {}
        for weapon in weapons:
            if weapon[attr] in seen:
                raise DefinitionError(weapon["source"] + ": " + attr + " also used by " + seen[weapon[attr]])
            seen[weapon[attr]] = weapon["source"]
    meshes = {}
    for weapon in weapons:
        previous = meshes.setdefault(weapon["meshKey"], weapon)
        if previous["recipeSource"] != weapon["recipeSource"]:
            raise DefinitionError(weapon["source"] + ": meshKey " + weapon["meshKey"] + " is built from a different recipe in "
                                  + previous["source"])
    families = {w["family"] for w in weapons}
    for required in sorted(FAMILY_FIRE_MODES):
        if required not in families:
            raise DefinitionError("catalog needs at least one " + required + " weapon")
    return sorted(weapons, key=lambda w: w["order"])


def f(value: str) -> str:
    return value + "f"


def deg(value: str) -> str:
    return "Deg(" + f(value) + ")"


def b(value: bool) -> str:
    return "true" if value else "false"


def float3(values) -> str:
    return "new Float3(" + ", ".join(f(x) for x in values) + ")"


def pellet_args(c) -> str:
    """ADR-118 fields, emitted only when they differ from the constructor defaults (older rows stay byte-identical)."""
    if c["pelletCount"] == "1" and not c["reloadPerRound"]:
        return ""
    return (",\n                    pelletCount: " + c["pelletCount"] + ", pelletSpreadRadians: " + deg(c["pelletSpreadDegrees"])
            + ", reloadPerRound: " + b(c["reloadPerRound"]))


def lethal_args(c) -> str:
    """ADR-125 field, emitted only when set (other rows stay byte-identical)."""
    return ", lethalOnHit: true" if c["lethalOnHit"] else ""


def render_catalog(weapons) -> str:
    rows = []
    for w in weapons:
        c = w["combat"]
        rows.append(
            "            // " + w["source"] + "\n"
            "            new WeaponCatalogEntry(\n"
            "                new WeaponDefinition(\n"
            "                    new DefinitionId(\"" + w["id"] + "\"), FireMode." + c["fireMode"] + ",\n"
            "                    fireIntervalTicks: " + c["fireIntervalTicks"] + ", reloadTicks: " + c["reloadTicks"]
            + ", equipTicks: " + c["equipTicks"] + ",\n"
            "                    magazineSize: " + c["magazineSize"] + ", reserveCapacity: " + c["reserveCapacity"]
            + ", baseDamage: " + c["baseDamage"] + ",\n"
            "                    hipSpreadRadians: " + deg(c["hipSpreadDegrees"]) + ", adsSpreadRadians: "
            + deg(c["adsSpreadDegrees"]) + ",\n"
            "                    recoilPitchRadians: " + deg(c["recoilPitchDegrees"]) + ", recoilYawHalfRadians: "
            + deg(c["recoilYawHalfDegrees"]) + ",\n"
            "                    maxRangeMeters: " + f(c["maxRangeMeters"]) + ", fuseTicks: " + c["fuseTicks"]
            + ", throwSpeedMetersPerSecond: " + f(c["throwSpeedMetersPerSecond"]) + ",\n"
            "                    blastRadiusMeters: " + f(c["blastRadiusMeters"]) + ", consumesAmmo: " + b(c["consumesAmmo"])
            + ", allowsSprintFire: " + b(c["allowsSprintFire"]) + ",\n"
            "                    burstCount: " + c["burstCount"] + ", burstIntervalTicks: " + c["burstIntervalTicks"]
            + ", falloffStartMeters: " + f(c["falloffStartMeters"]) + ", falloffEndMeters: " + f(c["falloffEndMeters"])
            + ",\n"
            "                    falloffMinScale: " + f(c["falloffMinScale"]) + ", moveSpeedScale: " + f(c["moveSpeedScale"])
            + ", spawnWeight: " + c["spawnWeight"] + pellet_args(c) + lethal_args(c) + "),\n"
            "                WeaponFamily." + w["family"] + ", \"" + w["id"] + "\")")
    return (HEADER + "#nullable enable\nusing Shooter.Contracts;\n\nnamespace Shooter.Simulation.Combat\n{\n"
            "    public static partial class WeaponCatalog\n    {\n"
            "        private static WeaponCatalogEntry[] CreateGeneratedEntries() => new[]\n        {\n"
            + ",\n".join(rows) + "\n        };\n    }\n}\n")


def render_profiles(weapons) -> str:
    rows = []
    for w in weapons:
        rows.append(
            "            // " + w["source"] + "\n"
            "            new WeaponVisualProfile(\n"
            "                new DefinitionId(\"" + w["id"] + "\"), WeaponVisualFamily." + w["family"] + ", \""
            + w["meshKey"] + "\",\n"
            "                WeaponOpticMode." + w["optic"] + ", hasMagazineCycle: " + b(w["magazineCycle"])
            + ", shotVoice: SynthVoiceKind." + w["shotVoice"] + ", killFeedGlyph: KillFeedWeapon." + w["glyph"] + ",\n"
            "                nearFieldScale: " + f(w["nearFieldScale"]) + ", nearFieldHipBias: " + float3(w["nearFieldHipBias"])
            + ",\n"
            "                hipRotationDegrees: " + float3(w["hipRotationDegrees"]) + ", cycleTravelMeters: "
            + f(w["cycleTravelMeters"]) + ", displayLabel: \"" + w["label"] + "\", muzzleFx: \"" + w["muzzleFx"] + "\",\n"
            "                recoilPitchDegrees: " + f(w["combat"]["recoilPitchDegrees"]) + ", recoilYawHalfDegrees: "
            + f(w["combat"]["recoilYawHalfDegrees"]) + ",\n"
            "                equipTicks: " + w["combat"]["equipTicks"] + ", fireIntervalTicks: "
            + w["combat"]["fireIntervalTicks"] + ", reloadTicks: " + w["combat"]["reloadTicks"] + ", fuseTicks: "
            + w["combat"]["fuseTicks"] + ", adsSeconds: " + f(w["adsSeconds"]) + ",\n"
            "                handlingStyle: WeaponHandlingStyle." + w["handlingStyle"] + ", adsFovDegrees: " + f(w["adsFovDegrees"])
            + ", muzzleFlashScale: " + f(w["muzzleFlashScale"]) + ", muzzleFlashSideJets: " + b(w["muzzleFlashSideJets"]) + ")")
    return (HEADER + "#nullable enable\nusing Shooter.Contracts;\nusing Shooter.Presentation.Audio;\n"
            "using Shooter.Presentation.Hud;\n\nnamespace Shooter.Presentation.View\n{\n"
            "    public static partial class WeaponVisualProfiles\n    {\n"
            "        private static WeaponVisualProfile[] CreateGeneratedProfiles() => new[]\n        {\n"
            + ",\n".join(rows) + "\n        };\n    }\n}\n")


def sight_id(name: str) -> str:
    return "new DefinitionId(\"sights/" + name + "\")"


def render_sight_rows(weapons, row_type) -> str:
    rows = []
    for w in weapons:
        if w["sights"] is None:
            continue
        sight = w["sights"]
        rows.append("            // " + w["source"] + "\n"
                    "            new " + row_type + "(new DefinitionId(\"" + w["id"] + "\"), \"" + sight["kind"] + "\", "
                    + sight_id(sight["default"]) + ",\n"
                    "                new[] { " + ", ".join(sight_id(n) for n in sight["allowed"]) + " })")
    return ",\n".join(rows)


def render_sights(weapons) -> str:
    return (HEADER + "#nullable enable\nusing Shooter.Contracts;\n\nnamespace Shooter.Simulation.Combat\n{\n"
            "    public static partial class SightCatalog\n    {\n"
            "        private static WeaponSightOptions[] CreateGeneratedOptions() => new[]\n        {\n"
            + render_sight_rows(weapons, "WeaponSightOptions") + "\n        };\n    }\n}\n")


def render_sight_choices(weapons) -> str:
    return (HEADER + "#nullable enable\nusing Shooter.Contracts;\n\nnamespace Shooter.Presentation.View\n{\n"
            "    public static partial class WeaponSightChoices\n    {\n"
            "        private static WeaponSightChoice[] CreateGeneratedChoices() => new[]\n        {\n"
            + render_sight_rows(weapons, "WeaponSightChoice") + "\n        };\n    }\n}\n")


def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def render_meshes(weapons) -> str:
    rows, seen = [], set()
    for w in weapons:
        if w["meshKey"] in seen:
            continue
        seen.add(w["meshKey"])
        if w["recipeSource"] is None:
            rows.append("            // " + w["source"] + "\n"
                        "            WeaponMeshSource.Prop(\"" + w["meshKey"] + "\")")
        else:
            rows.append("            // " + w["source"] + "\n"
                        "            WeaponMeshSource.FromRecipe(\"" + w["meshKey"] + "\",\n"
                        "                " + csharp_string(w["recipeSource"]) + ")")
    # ADR-107: one mesh set per (sight, archetype) any gun allows; AK irons are part of the body.
    for w in weapons:
        if w["sights"] is None:
            continue
        kind = w["sights"]["kind"]
        for name in w["sights"]["allowed"]:
            key = "sight-" + name + "-" + kind
            if (kind == "ak" and name == "irons") or key in seen:
                continue
            seen.add(key)
            rows.append("            // ADR-107 " + name + " on the " + kind + " rail (" + w["source"] + ")\n"
                        "            WeaponMeshSource.MountedSight(\"" + name + "\", \"" + kind + "\")")
    return (HEADER + "#nullable enable\n\nnamespace Shooter.Generation.Environment\n{\n"
            "    public static partial class WeaponMeshCatalog\n    {\n"
            "        private static WeaponMeshSource[] CreateGeneratedSources() => new[]\n        {\n"
            + ",\n".join(rows) + "\n        };\n    }\n}\n")


FAMILY_RANK = {"PrimaryGun": 0, "Sidearm": 1, "Melee": 2, "Throwable": 3}
# Generic glyphs shared by the legacy test guns; the review gallery shows the named guns, knife and grenade.
GENERIC_GLYPHS = {"Rifle", "Carbine", "Pistol"}


def glyph_rows(weapons) -> list:
    """One row per kill-feed glyph: the first weapon (by order) that uses it names the icon and silhouette."""
    rows, seen = [], {}
    for w in weapons:
        g = w["glyph"]
        if g in seen:
            first = seen[g]
            if first["silhouette"] != w["silhouette"]:
                raise DefinitionError(w["source"] + ": glyph " + g + " already uses silhouette " + first["silhouette"])
            continue
        seen[g] = w
        rows.append(w)
    return rows


def render_glyphs(weapons) -> str:
    rows = glyph_rows(weapons)
    icons = "".join("            KillFeedWeapon." + w["glyph"] + " => \"weapon." + w["meshKey"] + "\",\n" for w in rows)
    shapes = "".join("            KillFeedWeapon." + w["glyph"] + " => KillFeedSilhouette." + w["silhouette"] + ",\n" for w in rows)
    gallery = [w for w in sorted(rows, key=lambda w: (FAMILY_RANK[w["family"]], w["order"])) if w["glyph"] not in GENERIC_GLYPHS]
    return (HEADER + "#nullable enable\n\nnamespace Shooter.Presentation.Hud\n{\n"
            "    /// <summary>Analytic fallback shape class for a kill-feed glyph when its mesh-traced icon is unavailable.</summary>\n"
            "    public enum KillFeedSilhouette : byte\n    {\n"
            + ",\n".join("        " + name for name in SILHOUETTES) + "\n    }\n\n"
            "    /// <summary>Per-glyph kill-feed data generated from Content/Weapons (T195): no per-gun switch edits.</summary>\n"
            "    public static class KillFeedGlyphs\n    {\n"
            "        /// <summary>ADR-085 atlas name of the mesh-traced silhouette.</summary>\n"
            "        public static string IconName(KillFeedWeapon weapon) => weapon switch\n        {\n"
            + icons + "            _ => \"weapon.rifle\"\n        };\n\n"
            "        public static KillFeedSilhouette Silhouette(KillFeedWeapon weapon) => weapon switch\n        {\n"
            + shapes + "            _ => KillFeedSilhouette.Rifle\n        };\n\n"
            "        /// <summary>Named guns, then knife and grenade, for the HUD review gallery.</summary>\n"
            "        public static readonly KillFeedWeapon[] Gallery =\n        {\n"
            + ",\n".join("            KillFeedWeapon." + w["glyph"] for w in gallery) + "\n        };\n    }\n}\n")


def render_shot_voices(weapons) -> str:
    voices, derived, seen = [], [], set()
    for w in weapons:
        if w["family"] not in GUN_FAMILIES or w["shotVoice"] in seen:
            continue
        seen.add(w["shotVoice"])
        voices.append(w)
        if w["shotDerive"] is not None:
            derived.append(w)
    cases = "".join("                case SynthVoiceKind." + w["shotVoice"] + ":\n" for w in voices)
    derive_rows = "".join(
        "                SynthVoiceKind." + w["shotVoice"] + " => (SynthVoiceKind." + w["shotDerive"]["from"] + ", "
        + f(w["shotDerive"]["pitch"]) + ", " + f(w["shotDerive"]["gain"]) + "),\n" for w in derived)
    return (HEADER + "#nullable enable\n\nnamespace Shooter.Presentation.Audio\n{\n"
            "    /// <summary>Gun-report voices generated from Content/Weapons (T195): no per-gun mixer or baker edits.</summary>\n"
            "    public static class WeaponShotVoices\n    {\n"
            "        /// <summary>Every primary/sidearm report voice named by a weapon definition.</summary>\n"
            "        public static bool IsWeaponShot(SynthVoiceKind kind)\n        {\n"
            "            switch (kind)\n            {\n" + cases
            + "                    return true;\n                default:\n                    return false;\n            }\n        }\n\n"
            "        /// <summary>Interim derivation of a named report (audio.derive): source take, varispeed and gain.</summary>\n"
            "        public static bool TryGetDerivation(SynthVoiceKind kind, out SynthVoiceKind source, out float pitch, out float gain)\n"
            "        {\n            (source, pitch, gain) = kind switch\n            {\n" + derive_rows
            + "                _ => (default(SynthVoiceKind), 1f, 1f)\n            };\n"
            "            return source != default(SynthVoiceKind);\n        }\n    }\n}\n")


def meta_for(relative: Path) -> str:
    guid = hashlib.md5(("shooter.weapon-catalog:" + relative.as_posix()).encode("ascii")).hexdigest()
    return META_TEMPLATE.format(guid=guid)


def outputs(root: Path) -> dict:
    weapons = load(root)
    result = {}
    for relative, text in ((CATALOG_OUT, render_catalog(weapons)), (PROFILES_OUT, render_profiles(weapons)),
                           (MESHES_OUT, render_meshes(weapons)), (SIGHTS_OUT, render_sights(weapons)),
                           (SIGHT_CHOICES_OUT, render_sight_choices(weapons)), (GLYPHS_OUT, render_glyphs(weapons)),
                           (SHOT_VOICES_OUT, render_shot_voices(weapons))):
        result[relative] = text
        result[Path(str(relative) + ".meta")] = meta_for(relative)
    return result


def run(root: Path, check: bool) -> list:
    """Returns the stale paths (check) or the written paths (write)."""
    changed = []
    for relative, text in outputs(root).items():
        target = root / relative
        current = target.read_bytes().decode("utf-8") if target.is_file() else None
        if current == text:
            continue
        changed.append(relative.as_posix())
        if not check:
            target.write_bytes(text.encode("utf-8"))
    return changed


def main(argv) -> int:
    check = "--check" in argv[1:]
    unknown = [a for a in argv[1:] if a != "--check"]
    if unknown:
        print("usage: python Tools/generate_weapon_catalog.py [--check]", file=sys.stderr)
        return 2
    try:
        changed = run(ROOT, check)
    except DefinitionError as error:
        print("weapon definitions invalid: " + str(error), file=sys.stderr)
        return 1
    if check:
        if changed:
            print("generated weapon catalogs are stale; run python Tools/generate_weapon_catalog.py:", file=sys.stderr)
            for path in changed:
                print("  " + path, file=sys.stderr)
            return 1
        print("weapon catalogs up to date")
        return 0
    print("wrote " + str(len(changed)) + " file(s)" + ("".join("\n  " + p for p in changed)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
