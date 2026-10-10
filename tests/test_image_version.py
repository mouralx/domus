"""Validate automatic semantic versioning, release changes and workflow retries."""
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / '.github/scripts/image_version.py'
spec = importlib.util.spec_from_file_location('image_version', SCRIPT)
versions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(versions)
BASE = {'major': 0, 'minor': 1, 'patch': 0, 'first_run': 1}


class ImageVersionTests(unittest.TestCase):
    def test_first_publication_and_successive_workflow_runs(self):
        self.assertEqual('v0.1.0', versions.image_version(BASE, '1'))
        self.assertEqual('v0.1.1', versions.image_version(BASE, '2'))
        self.assertEqual('v0.1.41', versions.image_version(BASE, '42'))

    def test_minor_and_major_release_series_reset_patch(self):
        for major, minor in ((0, 2), (1, 0)):
            config = dict(BASE, major=major, minor=minor, first_run=42)
            self.assertEqual(f'v{major}.{minor}.0', versions.image_version(config, '42'))
            self.assertEqual(f'v{major}.{minor}.1', versions.image_version(config, '43'))

    def test_existing_patch_baseline_is_supported(self):
        self.assertEqual('v0.1.8', versions.image_version(dict(BASE, patch=7, first_run=42), '43'))

    def test_bad_configuration_and_output_injection_are_rejected(self):
        for key in BASE:
            for value in (-1, True, '1', 1.5):
                with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                    versions.image_version(dict(BASE, **{key: value}), '42')
        for value in ('0', '-1', '01', '1.1', '1\nother=value', 'bad', '9' * 128):
            with self.subTest(run=value), self.assertRaises(ValueError):
                versions.image_version(BASE, value)
        with self.assertRaises(ValueError):
            versions.image_version(dict(BASE, first_run=43), '42')
        with self.assertRaises(ValueError):
            versions.image_version({'major': 1}, '42')

    def test_retries_reuse_same_version_without_extra_segments(self):
        with tempfile.TemporaryDirectory() as directory:
            for attempt in ('1', '2'):
                output = Path(directory) / ('attempt-' + attempt)
                env = dict(os.environ, GITHUB_OUTPUT=str(output), GITHUB_RUN_NUMBER='42', GITHUB_RUN_ATTEMPT=attempt, IMAGE_CHANNEL='stable', GITHUB_REF='refs/heads/main')
                subprocess.run(['python3', str(SCRIPT)], env=env, check=True, stdout=subprocess.DEVNULL)
                self.assertEqual('version=v0.1.41\nchannel=stable\n', output.read_text())
                self.assertRegex(output.read_text().splitlines()[0].split('=')[1], r'^v\d+\.\d+\.\d+$')

    def test_stable_and_preview_tags(self):
        self.assertEqual('v0.1.41', versions.publication_version(BASE, '42', 'stable', 'refs/heads/main'))
        self.assertEqual('prev-v0.1.41', versions.publication_version(BASE, '42', 'preview', 'refs/heads/develop'))
        # A main-branch build can be previewed without publishing it as stable.
        self.assertEqual('prev-v0.1.41', versions.publication_version(BASE, '42', 'preview', 'refs/heads/main'))
        self.assertEqual('prev-v0.1.41', versions.publication_version(BASE, '42', 'preview', 'refs/heads/feature/example'))

    def test_stable_channel_rejects_other_refs_and_unknown_channels(self):
        for ref in ('refs/heads/develop', 'refs/heads/feature/example', 'refs/tags/v0.1.41', ''):
            with self.subTest(ref=ref), self.assertRaises(ValueError):
                versions.publication_version(BASE, '42', 'stable', ref)
        with self.assertRaises(ValueError):
            versions.publication_version(BASE, '42', 'invalid', 'refs/heads/main')

    def test_preview_output_has_version_and_channel(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'output'
            env = dict(os.environ, GITHUB_OUTPUT=str(output), GITHUB_RUN_NUMBER='42', IMAGE_CHANNEL='preview', GITHUB_REF='refs/heads/develop')
            subprocess.run(['python3', str(SCRIPT)], env=env, check=True, stdout=subprocess.DEVNULL)
            self.assertEqual('version=prev-v0.1.41\nchannel=preview\n', output.read_text())


if __name__ == '__main__':
    unittest.main()
