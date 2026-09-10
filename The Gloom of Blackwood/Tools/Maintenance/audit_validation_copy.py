"""Preserve unique source files before removing the disposable validation project.
This utility only reads and copies; removal is a separate, reviewed operation.
"""
import hashlib, json, pathlib, shutil

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'IntegrationEvidence/UnityValidationProject'
ARCHIVE = ROOT / 'Archive/Maintenance-2026-09-10/ValidationProjectDelta'
REPORT = ROOT / 'Archive/Maintenance-2026-09-10/validation-copy-manifest.json'
GENERATED = {'Library', 'Temp', 'Logs', 'obj', '.vs'}

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def main():
    if SOURCE.resolve() != ROOT / 'IntegrationEvidence/UnityValidationProject':
        raise RuntimeError('Unexpected validation directory')
    if not SOURCE.is_dir():
        raise RuntimeError('Validation project is absent')
    records, generated_bytes, duplicate_bytes, preserved_bytes = [], 0, 0, 0
    for path in SOURCE.rglob('*'):
        if path.is_symlink():
            raise RuntimeError('Symlink in validation project: ' + str(path))
        if not path.is_file():
            continue
        rel = path.relative_to(SOURCE)
        size = path.stat().st_size
        if rel.parts[0] in GENERATED or path.suffix in {'.csproj', '.sln', '.slnx'}:
            generated_bytes += size
            continue
        sha = digest(path)
        current = ROOT / rel
        if current.is_file() and current.stat().st_size == size and digest(current) == sha:
            duplicate_bytes += size
            action = 'identical_to_working_project'
        else:
            dest = ARCHIVE / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            if dest.exists() and digest(dest) != sha:
                raise RuntimeError('Archive collision: ' + str(dest))
            shutil.copy2(path, dest)
            if digest(dest) != sha:
                raise RuntimeError('Archive verification failed')
            preserved_bytes += size
            action = 'preserved_in_archive'
        records.append(dict(path=rel.as_posix(), bytes=size, sha256=sha, action=action))
    result = dict(source=str(SOURCE), generatedBytes=generated_bytes,
                  identicalSourceBytes=duplicate_bytes, preservedBytes=preserved_bytes,
                  preservedFiles=sum(r['action']=='preserved_in_archive' for r in records), files=records)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf8')
    print(json.dumps({k:v for k,v in result.items() if k!='files'}, indent=2, ensure_ascii=False))

if __name__ == '__main__':
    main()
