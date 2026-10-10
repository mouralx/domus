import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { assertManifest } from './manifest.mjs';

const ids = ['add-comments', 'crp-validation', 'pending-records', 'simplified-info-request'];
const marker = "  fs.writeFileSync(path.join(OUT, 'VERSION'), `${VERSION}\\n`);";
const emission = `  const metadata = JSON.parse(fs.readFileSync(path.join(ROOT, 'automation.json'), 'utf8'));
  fs.writeFileSync(path.join(OUT, 'automation.json'), JSON.stringify({ ...metadata, version: VERSION }, null, 2) + '\\n');
`;

export function addManifestEmission(source) {
  if (source.includes("fs.writeFileSync(path.join(OUT, 'automation.json')")) return source;
  if (!source.includes(marker)) throw new Error('Unrecognized Windows build script: VERSION output is missing');
  return source.replace(marker, emission + marker);
}

export function stripPrivateWorkData(source) {
  const files = /^  for \(const file of \[.*\]\) \{$/m;
  if (!files.test(source)) throw new Error('Unrecognized Windows package payload');
  return source.replace(files, "  for (const file of ['.env.example', 'README.md']) {")
    .split('\n').filter(line => !["  copy(path.join(ROOT, 'downloads'), path.join(OUT, 'downloads'));", "  copy(path.join(ROOT, 'backups'), path.join(OUT, 'backups'));"] .includes(line)).join('\n');
}

export function addInteractionEvents(source) {
  if (source.includes('@@automation-event@@')) return source;
  const marker = "    await nextPage.goto(url, { waitUntil: 'domcontentloaded' });\n    return nextPage;";
  if (!source.includes(marker)) throw new Error('Unrecognized browser-session transition');
  return source.replace(marker, "    await nextPage.goto(url, { waitUntil: 'domcontentloaded' });\n    if (process.env.AUTOMATIONS_PORTAL_EVENTS === '1') {\n      process.stdout.write('@@automation-event@@' + JSON.stringify({ type: 'interaction', state: nextHeadless ? 'running' : 'awaiting-user' }) + '\\n');\n    }\n    return nextPage;");
}

// Preflight all changes before writing to the automation repository.
export async function planAutomationIntegration(repository) {
  const changes = [];
  for (const id of ids) {
    const root = path.join(repository, 'src', id);
    const manifestPath = path.join(root, 'automation.json');
    // The automation repository owns its metadata; never replace it with portal fixtures.
    const manifest = assertManifest(JSON.parse(await readFile(manifestPath, 'utf8')));
    if (manifest.id !== id) throw new Error(`Manifest identity does not match its automation directory: ${manifestPath}`);
    const scriptPath = path.join(root, 'scripts', 'build-windows.js');
    const script = await readFile(scriptPath, 'utf8');
    const patched = addManifestEmission(stripPrivateWorkData(script));
    if (patched !== script) changes.push({ path: scriptPath, content: patched });
  }
  const browserPath = path.join(repository, 'src', 'shared', 'browser-session.js');
  const browserSource = await readFile(browserPath, 'utf8');
  const updatedBrowser = addInteractionEvents(browserSource);
  if (updatedBrowser !== browserSource) {
    changes.push({ path: browserPath, content: updatedBrowser });
    for (const id of ids) {
      const root = path.join(repository, 'src', id);
      const order = JSON.parse(await readFile(path.join(root, 'source-order.json'), 'utf8'));
      const parts = await Promise.all(order.map(part => path.resolve(root, part) === browserPath ? Buffer.from(updatedBrowser) : readFile(path.join(root, part))));
      changes.push({ path: path.join(root, 'app.cjs'), content: Buffer.concat(parts) });
    }
  }
  const windowsDocsPath = path.join(repository, 'docs', 'windows-packages.md');
  const windowsDocs = await readFile(windowsDocsPath, 'utf8');
  const updatedDocs = windowsDocs.replace('Os ficheiros Excel selecionados pelo script e as pastas `downloads/` e `backups/` são copiados quando existem.', 'Os pacotes incluem `automation.json` com a versão do artefacto. Ficheiros Excel de trabalho e as pastas `downloads/` e `backups/` não são distribuídos; o utilizador fornece o Excel ao executar.').replace('Rever esses dados antes de distribuir. ', '');
  if (updatedDocs !== windowsDocs) changes.push({ path: windowsDocsPath, content: updatedDocs });
  return changes;
}
