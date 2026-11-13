"""B01-materials workload validation; numeric scene inventory is not a GPU measurement."""
import hashlib
import json
import re

FAMILIES = ["metal", "polymer", "concrete", "plaster", "asphalt", "soil", "fabric"]
ROUTE = {"cameraHeightMm": 1650, "cameraZMm": -6000, "cycleTicks": 2520, "firstXMm": -6000,
         "interpolation": "hold-cut", "pitchMilliDegrees": 0, "stepXMm": 2000, "viewTicks": 360,
         "views": 7, "yawMilliDegrees": 0}


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("ascii")


def route_hash():
    return hashlib.sha256(canonical(ROUTE)).hexdigest()


def validate(capture_path, meta, build, ticks):
    if meta.get("routeHash") != route_hash():
        raise ValueError("B01 materials route identity mismatch")
    if meta.get("materialContentManifestFile") != "materials-manifest.json":
        raise ValueError("B01 materials content manifest path mismatch")
    payload = (capture_path / "materials-manifest.json").read_bytes()
    if len(payload) > 65536 or hashlib.sha256(payload).hexdigest() != meta.get("contentManifestHash"):
        raise ValueError("B01 materials content manifest hash mismatch")
    content = json.loads(payload)
    if payload != canonical(content):
        raise ValueError("B01 materials content manifest is not canonical")
    expected = {"floor": "foundation-matte-10m-v1", "materialCubes": 7, "renderInstances": 8,
                "schema": "shooter.b01-materials-content.v1", "textureFormat": "RGBA32",
                "toolchainHash": build["toolchainHash"], "triangles": 96, "workloadId": "B01-materials-v1"}
    if set(content) != set(expected) | {"materials"} or any(content[k] != v for k, v in expected.items()):
        raise ValueError("B01 materials scene/content identity mismatch")
    materials = content["materials"]
    if not isinstance(materials, list) or len(materials) != 7:
        raise ValueError("B01 materials family count mismatch")
    for i, item in enumerate(materials):
        if set(item) != {"contentId", "family", "manifestSha256", "tileMm"} or item["family"] != FAMILIES[i] or item["tileMm"] != [1000, 500, 2000, 2000, 2000, 2000, 250][i]:
            raise ValueError("B01 materials family/physical scale mismatch")
        if any(not isinstance(item[k], str) or re.fullmatch(r"[0-9a-f]{64}", item[k]) is None for k in ("contentId", "manifestSha256")):
            raise ValueError("B01 materials identity hash invalid")
    if len({item["contentId"] for item in materials}) != 7:
        raise ValueError("B01 materials identities must be distinct")
    if any(meta.get(k) != v for k, v in {"materialCubes": 7, "renderInstances": 8, "sceneTriangles": 96, "texturePayloadBytes": 7340004}.items()):
        raise ValueError("B01 materials inventory mismatch")
    checks = meta.get("materialCameraChecks")
    if type(checks) is not int or checks < meta["frames"]:
        raise ValueError("B01 materials per-frame camera verification incomplete")
    if any(int(t["materialView"]) != ((int(t["tick"]) + 1) % 2520) // 360 for t in ticks):
        raise ValueError("B01 materials tick camera schedule mismatch")
    return meta["contentManifestHash"]
