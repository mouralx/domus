import { readdir, readFile } from 'node:fs/promises';
import { assertManifest } from './lib/packages/manifest.mjs';
const files = process.argv.slice(2);
const targets = files.length ? files : (await readdir(new URL('../../../tests/fixtures/manifests/', import.meta.url))).filter(name => name.endsWith('.automation.json')).map(name => new URL('../../../tests/fixtures/manifests/' + name, import.meta.url));
for (const target of targets) {
  const manifest = assertManifest(JSON.parse(await readFile(target, 'utf8')));
  console.log(`${manifest.id}@${manifest.version}: valid`);
}
