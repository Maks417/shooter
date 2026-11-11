"""Verify delivered docs. Run: python verify_docs.py [--refresh-manifest]."""
from pathlib import Path
import hashlib
import json
import re
import sys
from urllib.parse import unquote

root = Path(__file__).resolve().parent
refresh = '--refresh-manifest' in sys.argv
errors = []
pages = sorted(root.rglob('*.md'))
links = 0
graph = {}

def anchors(text):
    counts, result = {}, set()
    for line in text.splitlines():
        if not re.match(r'^#{1,6}\s', line):
            continue
        heading = re.sub(r'^#{1,6}\s+', '', line).strip()
        slug = re.sub(r'[^\w\- ]', '', heading.lower()).replace(' ', '-')
        n = counts.get(slug, 0)
        counts[slug] = n + 1
        result.add(slug if not n else f'{slug}-{n}')
    return result

for page in pages:
    graph[page.resolve()] = set()
    text = page.read_text(encoding='utf-8')
    if not text.startswith('# ') or len(text.split()) < 50:
        errors.append(f'Empty/invalid page: {page.name}')
    if text.count('```') % 2:
        errors.append(f'Unbalanced fences: {page.name}')
    for target in re.findall(r'(?<!!)\[[^\]]*\]\(([^)]+)\)', text):
        if re.match(r'^[a-zA-Z]+://', target):
            continue
        links += 1
        pathpart, _, fragment = target.partition('#')
        resolved = (page.parent / unquote(pathpart)).resolve() if pathpart else page
        if not resolved.is_relative_to(root):
            errors.append(f'Outside docs: {page.name}: {target}')
        elif not resolved.is_file():
            errors.append(f'Missing: {page.name}: {target}')
        elif fragment and fragment not in anchors(resolved.read_text(encoding='utf-8')):
            errors.append(f'Missing anchor: {page.name}: {target}')
        if resolved.is_file() and resolved.suffix == '.md':
            graph[page.resolve()].add(resolved)

reachable = set()
pending = [(root / 'index.md').resolve()]
while pending:
    current = pending.pop()
    if current in reachable:
        continue
    reachable.add(current)
    pending.extend(graph.get(current, set()) - reachable)
for page in pages:
    if page.resolve() not in reachable:
        errors.append(f'Unreachable from root index: {page.relative_to(root).as_posix()}')

files = sorted(p for p in root.rglob('*') if p.is_file() and p.suffix in {'.md', '.py', '.json', '.bin'} and p.name not in {'documentation-manifest.json', 'verification-report.json'} and '__pycache__' not in p.parts)
hashes = {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
manifest_path = root / 'documentation-manifest.json'
if refresh:
    manifest_path.write_text(json.dumps({'schemaVersion': 1, 'designDate': '2026-11-11', 'files': hashes}, indent=2) + '\n', encoding='utf-8')
elif not manifest_path.exists():
    errors.append('Missing integrity manifest; intentional initial creation requires --refresh-manifest')
else:
    expected = json.loads(manifest_path.read_text(encoding='utf-8'))['files']
    if expected != hashes:
        errors.append('File inventory or SHA-256 differs from manifest')

report = {'schemaVersion': 1, 'root': str(root), 'markdownFiles': len(pages),
          'wordCount': sum(len(p.read_text(encoding='utf-8').split()) for p in pages),
          'relativeLinksChecked': links, 'integrityFiles': len(hashes), 'errors': errors,
          'result': 'PASS' if not errors else 'FAIL',
          'scope': 'Documentation existence, index coverage, local links/anchors, fences and hashes including fixtures/scripts. No game implementation or runtime tests.'}
(root / 'verification-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, indent=2))
sys.exit(bool(errors))
