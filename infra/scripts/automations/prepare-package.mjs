import { readFile } from 'node:fs/promises';
import { writePackageManifest } from './lib/packages/prepare.mjs';
const [templateFile, packageDirectory, version] = process.argv.slice(2);
if (!templateFile || !packageDirectory || !version || process.argv.length !== 5) {
  throw new Error('Usage: node scripts/prepare-package.mjs <template.json> <package-directory> <artifact-version>');
}
const template = JSON.parse(await readFile(templateFile, 'utf8'));
console.log(await writePackageManifest(template, { packageDirectory, version }));
