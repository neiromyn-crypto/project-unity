"""Static/package checks only. Does not compile Unity or run C#.
Install: python -m pip install tree-sitter tree-sitter-c-sharp
Run: python Validation/validate_package.py
"""
import hashlib
import json
import re
import struct
import zlib
from pathlib import Path
import tree_sitter
import tree_sitter_c_sharp

ROOT = Path(__file__).resolve().parents[1]
errors = []
parser = tree_sitter.Parser(tree_sitter.Language(tree_sitter_c_sharp.language()))
sources = sorted(ROOT.rglob('*.cs'))
for p in sources:
    if parser.parse(p.read_bytes()).root_node.has_error:
        errors.append('C# syntax: ' + p.name)
    if 'namespace DawnGuard.BlackwoodV2' not in p.read_text():
        errors.append('Unexpected namespace: ' + p.name)
if len(sources) != 12:
    errors.append('Expected twelve C# files')

pngs = []
for p in sorted(ROOT.rglob('*.png')):
    data = p.read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        errors.append('PNG signature: ' + p.name)
        continue
    offset = 8
    while offset < len(data):
        length = struct.unpack('>I', data[offset:offset+4])[0]
        chunk = data[offset+4:offset+8+length]
        crc = struct.unpack('>I', data[offset+8+length:offset+12+length])[0]
        if zlib.crc32(chunk) & 0xffffffff != crc:
            errors.append('PNG CRC: ' + p.name)
        offset += 12 + length
    w, h = struct.unpack('>II', data[16:24])
    pngs.append({'file': str(p.relative_to(ROOT)), 'width': w, 'height': h})
if len(pngs) != 16:
    errors.append('Expected sixteen PNG files')

runtime = ROOT / 'Assets/DawnGuard/BlackwoodV2/Runtime'
balance = (runtime/'BlackwoodBalance.cs').read_text()
reward = {k: int(v) for k, v in re.findall(r'r.Enemy\("([^"]+)"\).reward=(\d+)', balance)}
waves = []
for line in balance.splitlines():
    m = re.search(r'W\("([^"]+)",(\d+),(.+)', line)
    if not m:
        continue
    title, duration, rest = m.group(1), int(m.group(2)), m.group(3)
    loot = 0
    groups = []
    for enemy, count, start, interval in re.findall(r'G\("([^"]+)",(\d+),([\d.]+)f?,([\d.]+)f?\)', rest):
        count, start, interval = int(count), float(start), float(interval)
        if start + (count - 1)*interval >= duration:
            errors.append('Spawn after dawn: ' + title)
        loot += count * reward[enemy]
        groups.append({'enemy': enemy, 'count': count, 'start': start, 'interval': interval})
    waves.append({'night': len(waves)+1, 'title': title, 'duration': duration, 'groups': groups, 'max_kill_income': loot})
if len(waves) != 8:
    errors.append('Expected eight waves')
budget = 120 + sum(120 + w['max_kill_income'] for w in waves[:-1])
if budget != 1381:
    errors.append('Budget mismatch')
checks = (ROOT/'Assets/DawnGuard/BlackwoodV2/Editor/BlackwoodChecks.cs').read_text()
if checks.count(',ref passed);') != 11:
    errors.append('Expected eleven provided Unity checks')
controller = (runtime/'BlackwoodRoot.cs').read_text()
if '"blackwood-v2.json"' not in controller or '"dawnguard-v1.json"' in controller:
    errors.append('Save isolation')
for p in (ROOT/'Assets').rglob('*'):
    if p.is_file() and not str(p.relative_to(ROOT)).startswith('Assets/DawnGuard/BlackwoodV2/'):
        errors.append('Non-additive project path: ' + str(p))

report = {
    'csharp_sources_syntax_checked': len(sources),
    'png_files_checked': len(pngs),
    'pngs': pngs,
    'waves': waves,
    'pre_final_night_gross_budget_without_investments': budget,
    'unity_checks_supplied': 11,
    'unity_checks_executed_here': False,
    'unity_compilation_executed': False,
    'unity_playmode_executed': False,
    'device_performance_measured': False,
    'blender_models_generated': False,
    'errors': errors,
}
(ROOT/'Validation/report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
manifest = {}
for p in sorted(ROOT.rglob('*')):
    if p.is_file() and p.name != 'SHA256.json':
        manifest[str(p.relative_to(ROOT))] = hashlib.sha256(p.read_bytes()).hexdigest()
(ROOT/'SHA256.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('pngs','waves')},ensure_ascii=False,indent=2))
raise SystemExit(1 if errors else 0)
