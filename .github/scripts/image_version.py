#!/usr/bin/env python3
"""Generate a shared UTC build date and version each publication attempt."""
import argparse
from datetime import date, datetime, timezone
import os
import re


def image_version(build_date, run_number, run_attempt):
    day = date.fromisoformat(build_date)
    if day.isoformat() != build_date:
        raise ValueError('Build date must use YYYY-MM-DD.')
    for number in (run_number, run_attempt):
        if not re.fullmatch(r'[1-9][0-9]*', str(number)):
            raise ValueError('Run and attempt numbers must be positive integers.')
    value = f'v{day:%Y.%m.%d}.{run_number}.{run_attempt}'
    if len(value) > 128:
        raise ValueError('Image tag exceeds the Docker tag length limit.')
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--date-only', action='store_true')
    args = parser.parse_args()
    if args.date_only:
        key, value = 'date', datetime.now(timezone.utc).date().isoformat()
    else:
        key, value = 'version', image_version(os.environ['IMAGE_BUILD_DATE'], os.environ['GITHUB_RUN_NUMBER'], os.environ['GITHUB_RUN_ATTEMPT'])
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'{key}={value}\n')
    print(f'Image {key}: {value}')


if __name__ == '__main__':
    main()
