#!/usr/bin/env python3
"""Logical migration from the previous two-server local domus deployment."""
import argparse
import datetime
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--context', default=os.environ.get('DOCKER_CONTEXT'))
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
docker = ['docker'] + (['--context', args.context] if args.context else [])

def run(*command, stdin=None, stdout=None):
    return subprocess.run([*docker, *command], cwd=root, input=stdin, stdout=stdout or subprocess.PIPE,
                          stderr=subprocess.PIPE, check=True).stdout

# Preflight before stopping any application writes.
source = 'domus-postgres-1'
legacy = run('exec', source, 'psql', '-U', 'domus', '-d', 'domus_work', '-tAc',
             "SELECT count(*) FROM pg_database WHERE datname='membership'").strip()
if legacy != b'0':
    raise SystemExit('Source is not the previous domus deployment; migration refused.')
run('exec', 'domus-automation-postgres-1', 'pg_isready', '-U', 'domus', '-d', 'domus_automations')
backup = root / '.local/database-backups' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
backup.mkdir(parents=True, mode=0o700)
os.chmod(backup.parent, 0o700)
writers = ['domus-portal-1', 'domus-boards-1', 'domus-boards-worker-1',
           'domus-automations-1', 'domus-automations-worker-1', 'domus-membership-1', 'domus-ai-1',
           'domus-work-api-1', 'domus-jira-worker-1', 'domus-automation-api-1', 'domus-automation-worker-1', 'domus-ai-api-1']
present = set(run('ps', '-a', '--format', '{{.Names}}').decode().splitlines())
active_writers = [name for name in writers if name in present]
if active_writers:
    run('stop', *active_writers)

sources = [('boards', source, 'domus_work', []),
           ('membership', source, 'domus_work', ['-t', 'public.users', '-t', 'public.sessions', '-t', 'public.identities', '-t', 'public.recovery_codes']),
           ('automations', 'domus-automation-postgres-1', 'domus_automations', [])]
counts = {}
for target, container, database, tables in sources:
    path = backup / (target + '.sql')
    descriptor = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    with os.fdopen(descriptor, 'wb') as output:
        run('exec', container, 'pg_dump', '-U', 'domus', '-d', database, '--no-owner', '--no-acl', *tables, stdout=output)
    if target in ('membership', 'boards'):
        tables_to_count = ['users', 'sessions', 'identities', 'recovery_codes'] if target == 'membership' else ['workspaces', 'projects', 'cards', 'members']
    else:
        tables_to_count = ['accounts', 'executions', 'packages', 'installations']
    for table in tables_to_count:
        counts[target, table] = run('exec', container, 'psql', '-U', 'domus', '-d', database, '-tAc', f'SELECT count(*) FROM "{table}"').strip()
print(f'Private logical backups saved to {backup.relative_to(root)}', flush=True)
# The old volumes are retained. Compose recreates postgres on the new platform-db volume.
run('compose', '-f', 'infra/compose.yaml', 'up', '-d', '--no-deps', '--wait', 'postgres')
roles = {'boards': 'domus_boards', 'membership': 'domus_membership', 'automations': 'domus_automations'}
for database, role in roles.items():
    existing = run('exec', source, 'psql', '-U', role, '-d', database, '-tAc',
                   "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'").strip()
    if existing != b'0':
        raise SystemExit(f'Target {database} is not empty. Writers remain stopped; retain backups and inspect the target.')
    run('exec', '-i', source, 'psql', '-U', role, '-d', database, '-v', 'ON_ERROR_STOP=1', stdin=(backup / (database + '.sql')).read_bytes())
    for (target, table), expected in counts.items():
        if target != database:
            continue
        restored = run('exec', source, 'psql', '-U', role, '-d', database, '-tAc', f'SELECT count(*) FROM "{table}"').strip()
        if restored != expected:
            raise SystemExit(f'Row count mismatch in {database}.{table}. Writers remain stopped.')
# Keep public user projections and foreign keys, remove authentication authority from boards.
run('exec', '-i', source, 'psql', '-U', 'domus_boards', '-d', 'boards', '-v', 'ON_ERROR_STOP=1', stdin=b'''
BEGIN;
TRUNCATE sessions, identities, recovery_codes;
UPDATE users SET password_hash=NULL, two_factor_secret=NULL, two_factor_pending_secret=NULL,
 two_factor_pending_expires_at=NULL, two_factor_last_step=NULL, two_factor_failed_attempts=0,
 two_factor_locked_until=NULL, photo_required=false;
COMMIT;
''')
run('compose', '-f', 'infra/compose.yaml', 'up', '--build', '-d', '--wait', '--wait-timeout', '240')
run('stop', 'domus-automation-postgres-1')
print('Migration complete. Original database volumes and private logical backups were retained.')
