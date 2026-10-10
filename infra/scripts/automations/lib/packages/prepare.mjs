import { mkdir, writeFile, rename, rm } from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { assertManifest } from './manifest.mjs';

export async function writePackageManifest(template, { packageDirectory, version }) {
  if (!packageDirectory || typeof version !== 'string') throw new Error('Package directory and actual artifact version are required');
  const manifest = assertManifest({ ...structuredClone(template), version });
  await mkdir(packageDirectory, { recursive: true });
  const destination = path.join(packageDirectory, 'automation.json');
  const temporary = destination + '.' + randomUUID() + '.tmp';
  try {
    await writeFile(temporary, JSON.stringify(manifest, null, 2) + '\n', { flag: 'wx' });
    await rename(temporary, destination);
  } finally {
    await rm(temporary, { force: true });
  }
  return destination;
}
