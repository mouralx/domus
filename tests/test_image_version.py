"""Validate automatic version tags, including partial workflow reruns."""
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


class ImageVersionTests(unittest.TestCase):
    def test_version_has_zero_padded_date_run_and_attempt(self):
        self.assertEqual('v2026.01.02.42.1', versions.image_version('2026-01-02', '42', '1'))

    def test_runs_and_reruns_have_distinct_versions(self):
        tags = {versions.image_version('2026-10-10', run, attempt)
                for run in ('1', '2', '42') for attempt in ('1', '2')}
        self.assertEqual(6, len(tags))

    def test_invalid_dates_counters_and_output_injection_are_rejected(self):
        for day in ('2026-02-30', '2026-1-2', '20260102', '2026-10-10\nother=value'):
            with self.subTest(day=day), self.assertRaises(ValueError):
                versions.image_version(day, '1', '1')
        for number in ('0', '-1', '01', '1.1', '1\nother=value', 'bad', '9' * 128):
            with self.subTest(number=number):
                with self.assertRaises(ValueError):
                    versions.image_version('2026-10-10', number, '1')
                with self.assertRaises(ValueError):
                    versions.image_version('2026-10-10', '1', number)

    def test_failed_job_rerun_uses_current_attempt_and_original_shared_date(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'output'
            env = dict(os.environ, GITHUB_OUTPUT=str(output), IMAGE_BUILD_DATE='2026-10-10', GITHUB_RUN_NUMBER='42', GITHUB_RUN_ATTEMPT='2')
            subprocess.run(['python3', str(SCRIPT)], env=env, check=True, stdout=subprocess.DEVNULL)
            self.assertEqual('version=v2026.10.10.42.2\n', output.read_text())

    def test_shared_date_output_is_valid_iso_date(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'output'
            env = dict(os.environ, GITHUB_OUTPUT=str(output))
            subprocess.run(['python3', str(SCRIPT), '--date-only'], env=env, check=True, stdout=subprocess.DEVNULL)
            self.assertRegex(output.read_text(), r'^date=\d{4}-\d{2}-\d{2}\n$')


if __name__ == '__main__':
    unittest.main()
