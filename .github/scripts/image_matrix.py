#!/usr/bin/env python3
"""Select images affected by the entire pushed commit range."""
import json
import os
from pathlib import Path
import subprocess

COMPONENTS = (
    ('portal', 'portal.Dockerfile'),
    ('membership', 'membership.Dockerfile'),
    ('boards', 'boards-api.Dockerfile'),
    ('automations', 'automations-api.Dockerfile'),
    ('boards-worker', 'boards-worker.Dockerfile'),
    ('automation-worker', 'automations-worker.Dockerfile'),
)
BACKENDS = {name for name, _ in COMPONENTS if name != 'portal'}
ALL = {name for name, _ in COMPONENTS}


def affected_images(paths):
    selected = set()
    for path in paths:
        if path in {'.dockerignore', '.github/workflows/publish-images.yml', '.github/scripts/image_matrix.py', '.github/scripts/image_version.py', '.github/image-version.json'}:
            selected.update(ALL)
        if path in {'src/global.json', 'src/Directory.Build.props'}:
            selected.update(BACKENDS)
        for name, dockerfile in COMPONENTS:
            if path == 'infra/containers/' + dockerfile:
                selected.add(name)
        if path == 'infra/containers/portal.conf':
            selected.add('portal')
        if path == 'docs/schemas/automation.schema.json':
            selected.update({'automations', 'automation-worker'})
        # Test projects and documentation are excluded from application builds.
        if '/tests/' in path or path.endswith('.md'):
            continue
        if path.startswith('src/portal/'):
            selected.add('portal')
        if path.startswith('src/services/boards/'):
            selected.update({'boards', 'membership', 'boards-worker'})
        if path.startswith('src/services/membership/'):
            selected.add('membership')
        if path.startswith('src/services/automations/'):
            selected.add('automations')
        if path.startswith('src/services/automations/Core/'):
            selected.add('automation-worker')
        if path.startswith('src/workers/boards/'):
            selected.add('boards-worker')
        if path.startswith('src/workers/automations/'):
            selected.add('automation-worker')
    return selected


def matrix(selected):
    return {'include': [{'component': name, 'context': '.', 'dockerfile': 'infra/containers/' + file}
                        for name, file in COMPONENTS if name in selected]}


def main():
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    before = event.get('before', '')
    if os.environ['GITHUB_EVENT_NAME'] == 'workflow_dispatch' or not before or set(before) == {'0'}:
        selected = ALL  # Manual rebuild or first push has no previous revision.
    else:
        if subprocess.run(['git', 'cat-file', '-e', before + '^{commit}'], stderr=subprocess.DEVNULL).returncode:
            subprocess.run(['git', 'fetch', '--no-tags', 'origin', before], check=True)
        # Disabling rename detection includes both old and new paths when code moves.
        changed = subprocess.check_output(['git', 'diff', '--name-only', '--no-renames', '-z', before, os.environ['GITHUB_SHA']])
        selected = affected_images(changed.decode().split('\0'))
    value = json.dumps(matrix(selected), separators=(',', ':'))
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'matrix={value}\nhas_changes={str(bool(selected)).lower()}\n')
    print('Images to publish: ' + (', '.join(item['component'] for item in matrix(selected)['include']) or 'none'))


if __name__ == '__main__':
    main()
