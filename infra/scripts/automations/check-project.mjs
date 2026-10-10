import { readdir } from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import './validate-packages.mjs';
async function visit(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) await visit(file);
    else if (/\.(?:mjs|js)$/.test(entry.name)) {
      const result = spawnSync(process.execPath, ['--check', file], { encoding: 'utf8' });
      if (result.status) throw new Error(result.stderr);
    }
  }
}
for (const directory of ['infra/scripts/automations', 'tests/automations']) await visit(directory);
console.log('Application and browser-source syntax checks passed.');
