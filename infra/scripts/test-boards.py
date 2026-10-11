#!/usr/bin/env python3
"""Run boards regression tests against an isolated temporary PostgreSQL server."""
import os
from pathlib import Path
import subprocess
import time
import uuid
root = Path(__file__).resolve().parents[2]
context = os.environ.get('DOCKER_CONTEXT')
docker = ['docker'] + (['--context', context] if context else [])
name = 'domus-unit-' + uuid.uuid4().hex[:8]
password = uuid.uuid4().hex
subprocess.run([*docker, 'run', '-d', '--name', name, '-p', '127.0.0.1::5432', '-e', 'POSTGRES_PASSWORD=' + password, 'postgres:18-bookworm'], check=True, stdout=subprocess.DEVNULL)
try:
    for attempt in range(150):
        if subprocess.run([*docker, 'exec', name, 'pg_isready', '-U', 'postgres'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
            break
        time.sleep(.2)
    else:
        raise RuntimeError('Temporary PostgreSQL did not become ready.')
    port = subprocess.check_output([*docker, 'port', name, '5432']).decode().strip().rsplit(':', 1)[1]
    env = dict(os.environ, ConnectionStrings__Postgres=f'Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Password={password}')
    env.pop('DOMUS_MEMBERSHIP_URL', None)  # Exercise the inherited standalone boards regression suite.
    result = subprocess.run(['dotnet', 'test', 'services/boards/tests/Domus.Boards.Api.Tests/Domus.Boards.Api.Tests.csproj'], cwd=root / "src", env=env)
finally:
    subprocess.run([*docker, 'rm', '-fv', name], stdout=subprocess.DEVNULL, check=True)
raise SystemExit(result.returncode)
