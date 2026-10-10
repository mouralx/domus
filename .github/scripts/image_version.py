#!/usr/bin/env python3
"""Generate vMAJOR.MINOR.PATCH from a release series and workflow counter."""
import json
import os
from pathlib import Path
import re

CONFIG = Path(__file__).resolve().parents[1] / 'image-version.json'


def image_version(config, run_number):
    if set(config) != {'major', 'minor', 'patch', 'first_run'}:
        raise ValueError('Configure major, minor, patch and first_run.')
    for key, number in config.items():
        if type(number) is not int or number < (1 if key == 'first_run' else 0):
            raise ValueError(f'{key} must be a valid nonnegative integer (first_run starts at 1).')
    if not re.fullmatch(r'[1-9][0-9]*', str(run_number)):
        raise ValueError('Workflow run number must be a positive integer.')
    offset = int(run_number) - config['first_run']
    if offset < 0:
        raise ValueError('Workflow run number is earlier than the release series first_run.')
    patch = config['patch'] + offset
    value = f"v{config['major']}.{config['minor']}.{patch}"
    if len(value) > 128:
        raise ValueError('Image tag exceeds the Docker tag length limit.')
    return value


def publication_version(config, run_number, channel, ref):
    if channel not in {'stable', 'preview'}:
        raise ValueError('Publication channel must be stable or preview.')
    if channel == 'stable' and ref != 'refs/heads/main':
        raise ValueError('Stable images can only be published from main.')
    version = image_version(config, run_number)
    version = 'prev-' + version if channel == 'preview' else version
    if len(version) > 128:
        raise ValueError('Image tag exceeds the Docker tag length limit.')
    return version


def main():
    channel = os.environ['IMAGE_CHANNEL']
    version = publication_version(json.loads(CONFIG.read_text()), os.environ['GITHUB_RUN_NUMBER'], channel, os.environ['GITHUB_REF'])
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'version={version}\nchannel={channel}\n')
    print(f'Image version: {version} ({channel})')


if __name__ == '__main__':
    main()
