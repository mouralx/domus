"""Regression checks for image selection and push-range handling."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / '.github/scripts/image_matrix.py'
spec = importlib.util.spec_from_file_location('image_matrix', SCRIPT)
images = importlib.util.module_from_spec(spec)
spec.loader.exec_module(images)


class ImageSelectionTests(unittest.TestCase):
    def test_component_and_dependency_changes(self):
        cases = {
            'src/portal/src/app/App.tsx': {'portal'},
            'src/services/membership/Program.cs': {'membership'},
            'src/services/boards/Authentication/Auth.cs': {'boards', 'membership', 'boards-worker'},
            'src/services/automations/Program.cs': {'automations'},
            'src/services/automations/Core/Database.cs': {'automations', 'automation-worker'},
            'src/workers/boards/Program.cs': {'boards-worker'},
            'src/workers/automations/Program.cs': {'automation-worker'},
            'infra/containers/portal.conf': {'portal'},
            'docs/schemas/automation.schema.json': {'automations', 'automation-worker'},
            'src/global.json': images.BACKENDS,
            'src/Directory.Build.props': images.BACKENDS,
            '.dockerignore': images.ALL,
            '.github/workflows/publish-images.yml': images.ALL,
            '.github/scripts/image_matrix.py': images.ALL,
            '.github/scripts/image_version.py': images.ALL,
            '.github/image-version.json': images.ALL,
        }
        for path, expected in cases.items():
            with self.subTest(path=path):
                self.assertEqual(expected, images.affected_images([path]))
        for name, dockerfile in images.COMPONENTS:
            with self.subTest(dockerfile=dockerfile):
                self.assertEqual({name}, images.affected_images(['infra/containers/' + dockerfile]))

    def test_documentation_tests_and_deployment_only_do_not_publish(self):
        self.assertEqual(set(), images.affected_images([
            'README.md', 'docs/database-migration.md', 'tests/integration.mjs',
            'src/services/boards/tests/Kanbada.Api.Tests/ApiTests.cs',
            'src/portal/README.md', 'infra/compose.yaml', 'infra/.env.example',
        ]))

    def test_combined_changes_publish_once_in_stable_order(self):
        result = images.matrix(images.affected_images([
            'src/portal/src/app/App.tsx', 'src/portal/src/main.tsx',
            'src/services/automations/Core/Database.cs',
        ]))
        self.assertEqual(['portal', 'automations', 'automation-worker'],
                         [item['component'] for item in result['include']])

    def test_push_range_includes_all_commits_and_deleted_paths(self):
        previous = Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            os.chdir(directory)
            try:
                def git(*args):
                    return subprocess.check_output(['git', *args], stderr=subprocess.DEVNULL).decode().strip()
                def commit():
                    git('add', '-A')
                    git('-c', 'user.name=Synthetic', '-c', 'user.email=test@example.test', 'commit', '-qm', 'fixture')
                    return git('rev-parse', 'HEAD')
                git('init', '-q')
                Path('README.md').write_text('fixture')
                before = commit()
                Path('src/portal').mkdir(parents=True)
                Path('src/portal/file.ts').write_text('fixture')
                commit()
                Path('src/workers/boards').mkdir(parents=True)
                Path('src/portal/file.ts').rename('src/workers/boards/file.ts')
                after = commit()
                Path('event.json').write_text(json.dumps({'before': before}))
                with patch.dict(os.environ, GITHUB_EVENT_PATH='event.json', GITHUB_EVENT_NAME='push', GITHUB_SHA=after, GITHUB_OUTPUT='output'):
                    images.main()
                output = dict(line.split('=', 1) for line in Path('output').read_text().splitlines())
                # Net diff from before sees worker code. Comparing from the previous commit
                # also includes the deleted portal path despite Git recognizing a rename.
                self.assertEqual('true', output['has_changes'])
                self.assertEqual(['boards-worker'], [row['component'] for row in json.loads(output['matrix'])['include']])
                first_push_after = git('rev-parse', 'HEAD~1')
                Path('event.json').write_text(json.dumps({'before': first_push_after}))
                Path('output').unlink()
                with patch.dict(os.environ, GITHUB_EVENT_PATH='event.json', GITHUB_EVENT_NAME='push', GITHUB_SHA=after, GITHUB_OUTPUT='output'):
                    images.main()
                output = dict(line.split('=', 1) for line in Path('output').read_text().splitlines())
                self.assertEqual(['portal', 'boards-worker'], [row['component'] for row in json.loads(output['matrix'])['include']])
            finally:
                os.chdir(previous)

    def test_manual_and_first_push_publish_all_and_docs_push_skips(self):
        with tempfile.TemporaryDirectory() as directory:
            event = Path(directory) / 'event.json'
            output = Path(directory) / 'output'
            for kind, before in [('workflow_dispatch', ''), ('push', '0' * 40)]:
                event.write_text(json.dumps({'before': before}))
                with patch.dict(os.environ, GITHUB_EVENT_PATH=str(event), GITHUB_EVENT_NAME=kind, GITHUB_OUTPUT=str(output)):
                    images.main()
                self.assertIn('has_changes=true', output.read_text())
                self.assertEqual(len(images.COMPONENTS), len(json.loads(output.read_text().splitlines()[-2][7:])['include']))
                output.unlink()
            event.write_text(json.dumps({'before': 'previous'}))
            with patch.dict(os.environ, GITHUB_EVENT_PATH=str(event), GITHUB_EVENT_NAME='push', GITHUB_SHA='current', GITHUB_OUTPUT=str(output)), patch.object(images.subprocess, 'run'), patch.object(images.subprocess, 'check_output', return_value=b'README.md\0'):
                images.main()
            self.assertIn('has_changes=false', output.read_text())
            self.assertIn('matrix={"include":[]}', output.read_text())


if __name__ == '__main__':
    unittest.main()
