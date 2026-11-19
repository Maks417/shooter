"""Move whole members of one C# class into sibling partial-class files, byte-for-byte (T195 class splits).

Usage: python Tools/split_partial.py <file.cs> <ClassName> <plan.json>
plan.json: {"<Suffix>": {"summary": "...", "members": ["MemberName", ...]}, ...}
Each named member (by the identifier before its "(" / property / field name on its declaration line, first match at
class-member indentation) moves with its leading doc comments and attributes, up to the next member. Members not
named stay in the original file. The class gains the `partial` modifier. A new file gets a Unity .meta with a fresh
GUID. Nothing is reformatted: every moved line is copied verbatim.
"""
import json
import re
import sys
import uuid
from pathlib import Path

META = ("fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
        "  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n"
        "  assetBundleVariant: \n")
MEMBER_INDENT = "        "


def member_name(line: str):
    if not line.startswith(MEMBER_INDENT) or line.startswith(MEMBER_INDENT + " "):
        return None
    text = line.strip()
    if text.startswith(("//", "[", "{", "}", "#")) or not text:
        return None
    m = re.match(r"^(?:[\w<>\[\],.?]+\s+)*?([A-Za-z_]\w*)\s*(?:<[^()]*>)?\s*(\(|=>|\{|;|=)", text)
    if m and m.group(1) not in {"if", "for", "while", "switch", "return", "new", "else"}:
        return m.group(1)
    return None


def main(path, cls, plan_path):
    path = Path(path)
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    crlf = "\r\n" in text
    lines = text.replace("\r\n", "\n").split("\n")
    plan = json.loads(Path(plan_path).read_text(encoding="utf-8"))

    decl = next(i for i, l in enumerate(lines) if re.search(r"\bclass\s+" + cls + r"\b", l))
    open_brace = next(i for i in range(decl, len(lines)) if lines[i].strip() == "{")
    depth, close = 0, None
    for i in range(open_brace, len(lines)):
        depth += lines[i].count("{") - lines[i].count("}")
        if depth == 0:
            close = i
            break
    header = [l for l in lines[:next(i for i, l in enumerate(lines) if l.startswith("namespace "))]]
    namespace = next(l for l in lines if l.startswith("namespace "))

    # Member starts at depth 1 inside the class body, with leading comment/attribute lines.
    starts, depth = [], 0
    for i in range(open_brace + 1, close):
        line = lines[i]
        if depth == 0:
            name = member_name(line)
            if name:
                first = i
                while first - 1 > open_brace and lines[first - 1].strip().startswith(("///", "//", "[")):
                    first -= 1
                starts.append((first, name))
        depth += line.count("{") - line.count("}")
        # Ignore braces inside string/char literals well enough for these sources (no brace-bearing literals assumed).
    spans = []
    for k, (first, name) in enumerate(starts):
        end = starts[k + 1][0] if k + 1 < len(starts) else close
        spans.append((first, end, name))

    wanted = {}
    for suffix, spec in plan.items():
        for name in spec["members"]:
            wanted.setdefault(name, suffix)
    moved = {suffix: [] for suffix in plan}
    taken = set()
    found = set()
    for first, end, name in spans:
        suffix = wanted.get(name)
        if suffix is None:
            continue
        found.add(name)
        block = lines[first:end]
        while block and not block[-1].strip():
            block.pop()
        moved[suffix].append(block)
        taken.update(range(first, end))
    missing = sorted(set(wanted) - found)
    if missing:
        raise SystemExit("members not found: " + ", ".join(missing))

    kept = [l for i, l in enumerate(lines) if i not in taken]
    kept_decl = next(i for i, l in enumerate(kept) if re.search(r"\bclass\s+" + cls + r"\b", l))
    if " partial " not in kept[kept_decl]:
        kept[kept_decl] = re.sub(r"\bclass\s+" + cls + r"\b", "partial class " + cls, kept[kept_decl], count=1)
    decl_line = kept[kept_decl].strip()
    # Collapse runs of blank lines left behind.
    out = []
    for l in kept:
        if not l.strip() and out and not out[-1].strip():
            continue
        out.append(l)

    def write(p: Path, body_lines):
        s = "\n".join(body_lines)
        if crlf:
            s = s.replace("\n", "\r\n")
        p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + s.encode("utf-8"))

    write(path, out)
    stem = path.stem
    for suffix, blocks in moved.items():
        target = path.with_name(stem + "." + suffix + ".cs")
        body = header + [namespace, "{", "    /// <summary>" + plan[suffix]["summary"] + "</summary>",
                                  "    " + decl_line, "    {"]
        for b, block in enumerate(blocks):
            if b:
                body.append("")
            body.extend(block)
        body += ["    }", "}", ""]
        write(target, body)
        meta = Path(str(target) + ".meta")
        if not meta.exists():
            meta.write_bytes(META.format(guid=uuid.uuid4().hex).encode())
        print(f"{target.name}: {len(blocks)} members, {sum(len(b) for b in blocks)} lines")
    print(f"{path.name}: {len(out)} lines remain")


if __name__ == "__main__":
    main(*sys.argv[1:4])
