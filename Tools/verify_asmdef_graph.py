"""Verify that each pure Tools/*.csproj mirrors the Unity asmdef it compiles (ADR-076).

A Tools project that links exactly one asmdef folder must use that asmdef's assembly name, and its ProjectReferences
must name exactly the asmdef's references. That keeps the fast .NET lane enforcing the same boundaries as Unity.
Test projects (*.Tests) and tools that compile no asmdef folder are skipped. Prints a JSON report; exit 1 on drift.
"""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "UnityProject/Assets/Shooter"


def load_asmdefs(assets=ASSETS):
    guids, folders = {}, {}
    for path in assets.rglob("*.asmdef"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        meta = path.with_suffix(".asmdef.meta").read_text(encoding="utf-8")
        guids[re.search(r"guid: (\w+)", meta).group(1)] = data["name"]
        folders[path.parent.resolve()] = data
    result = {}
    for folder, data in folders.items():
        refs = set()
        for ref in data.get("references", []):
            name = guids.get(ref[5:]) if ref.startswith("GUID:") else ref
            if name and name.startswith("Shooter."):
                refs.add(name)
        result[folder] = (data["name"], refs)
    return result


def project_info(csproj):
    tree = ET.parse(csproj)
    name = next((e.text for e in tree.iter("AssemblyName") if e.text), csproj.stem)
    includes = [e.get("Include") for e in tree.iter("Compile") if e.get("Include")]
    refs = set()
    for e in tree.iter("ProjectReference"):
        target = (csproj.parent / e.get("Include")).resolve()
        target_tree = ET.parse(target)
        refs.add(next((x.text for x in target_tree.iter("AssemblyName") if x.text), target.stem))
    return name, includes, refs


def verify(root=ROOT):
    asmdefs = load_asmdefs(root / "UnityProject/Assets/Shooter")
    checked, errors = [], []
    for csproj in sorted((root / "Tools").glob("*/*.csproj")):
        if csproj.parent.name.endswith(".Tests"):
            continue
        name, includes, refs = project_info(csproj)
        folders = set()
        for include in includes:
            folder = (csproj.parent / re.sub(r"/(\*\*/)?\*\.cs$", "", include)).resolve()
            if folder in asmdefs:
                folders.add(folder)
        if len(folders) != 1:
            continue
        expected_name, expected_refs = asmdefs[folders.pop()]
        rel = csproj.relative_to(root).as_posix()
        checked.append(rel)
        if name != expected_name:
            errors.append(f"{rel}: assembly {name} does not match asmdef {expected_name}")
        if refs != expected_refs:
            errors.append(f"{rel}: references {sorted(refs)} differ from asmdef {sorted(expected_refs)}")
    return {"result": "FAIL" if errors else "PASS", "checked": checked, "errors": errors,
            "scope": "Pure Tools project graph equals the Unity asmdef graph; no compilation"}


if __name__ == "__main__":
    report = verify()
    print(json.dumps(report, indent=2))
    sys.exit(1 if report["errors"] else 0)
