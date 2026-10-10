import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir, mkdtemp, rm, mkdir, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { validateManifest, assertManifest, resolvePackagePath } from '../../infra/scripts/automations/lib/packages/manifest.mjs';
import { createRunPlan } from '../../infra/scripts/automations/lib/packages/run-plan.mjs';
import { describePackageSource } from '../../infra/scripts/automations/lib/packages/sources.mjs';
import { writePackageManifest } from '../../infra/scripts/automations/lib/packages/prepare.mjs';
import { stripPrivateWorkData } from '../../infra/scripts/automations/lib/packages/integration.mjs';
const templates = {};
for (const file of (await readdir(new URL('../fixtures/manifests/', import.meta.url))).filter(name => name.endsWith('.json'))) {
  const template = JSON.parse(await readFile(new URL('../fixtures/manifests/' + file, import.meta.url), 'utf8'));
  templates[template.id] = template;
}
const pending = templates['pending-records'];
const simplified = templates['simplified-info-request'];
const options = { packageDirectory: path.resolve('installed/package'), runDirectory: path.resolve('runs/new-id'), platform: 'win32', arch: 'x64' };

test('release packages exclude user workbooks, downloads and backups', () => {
  const source = "  for (const file of ['rows.xlsx', '.env.example', 'README.md']) {\n    copy(file);\n  }\n  copy(path.join(ROOT, 'downloads'), path.join(OUT, 'downloads'));\n  copy(path.join(ROOT, 'backups'), path.join(OUT, 'backups'));\n  copyRuntime();";
  const result = stripPrivateWorkData(source);
  assert.doesNotMatch(result, /rows\.xlsx|downloads|backups/);
  assert.match(result, /\.env\.example/);
  assert.match(result, /copyRuntime\(\)/);
  assert.equal(stripPrivateWorkData(result), result);
});

test('all four synthetic manifest fixtures satisfy the package contract', () => {
  assert.equal(Object.keys(templates).length, 4);
  for (const template of Object.values(templates)) assert.deepEqual(validateManifest(template), []);
  assert.equal(simplified.effects.createsPaidRequests, true);
  assert.equal(templates['add-comments'].effects.publishesComments, true);
});

test('rejects unsupported versions, missing fields and undeclared manifest properties', () => {
  for (const mutate of [
    manifest => { manifest.schemaVersion = 2; },
    manifest => { delete manifest.execution; },
    manifest => { manifest.version = 'latest'; },
    manifest => { manifest.unknown = true; },
    manifest => { manifest.execution.isolation = 'resume'; }
  ]) {
    const manifest = structuredClone(pending); mutate(manifest);
    assert.throws(() => assertManifest(manifest), /Invalid automation manifest/);
  }
});

test('rejects absolute, traversal, drive and Windows separator paths', () => {
  for (const relative of ['../escape.exe', '/tmp/app.exe', 'C:/app.exe', 'folder/../../app.exe', 'folder/../app.exe', '\\\\server\\app.exe', 'app\u0000.exe']) {
    const manifest = structuredClone(pending); manifest.execution.executable = relative;
    assert.throws(() => assertManifest(manifest));
    assert.throws(() => resolvePackagePath(options.packageDirectory, relative));
  }
});

test('rejects duplicate inputs, reserved bindings and secret defaults', () => {
  const duplicate = structuredClone(pending); duplicate.inputs.push(duplicate.inputs[0]);
  assert.throws(() => assertManifest(duplicate), /duplicate/);
  const secret = structuredClone(simplified); secret.inputs.find(input => input.type === 'secret').default = 'never-store-me';
  assert.throws(() => assertManifest(secret), /secrets cannot have defaults/);
  const reserved = structuredClone(pending); reserved.inputs[0].binding = { envFileKey: 'PENDING_RECORDS_HOME' };
  assert.throws(() => assertManifest(reserved), /reserved/);
  const home = structuredClone(pending); home.execution.packagePaths.PENDING_RECORDS_HOME = 'other';
  assert.throws(() => assertManifest(home), /override/);
});

test('creates a direct process plan with a separate run home and state directory', () => {
  const plan = createRunPlan(pending, { workbook: 'rows.xlsx', limit: 2 }, options);
  assert.equal(plan.shell, false);
  assert.equal(plan.executable, path.join(options.packageDirectory, 'PendingRecords.exe'));
  assert.deepEqual(plan.arguments, ['--state-dir', path.join(options.runDirectory, '.state'), '--workbook', path.join(options.runDirectory, 'rows.xlsx'), '--limit', '2']);
  assert.equal(plan.environment.PENDING_RECORDS_HOME, options.runDirectory);
  assert.equal(plan.environment.PLAYWRIGHT_BROWSERS_PATH, path.join(options.packageDirectory, 'browsers'));
});

test('keeps passwords out of arguments and puts them only in local environment-file values', () => {
  const plan = createRunPlan(simplified, { workbook: 'rows.xlsx', url: 'https://example.invalid', password: 'test-password' }, options);
  assert.ok(!plan.arguments.includes('test-password'));
  assert.equal(plan.environmentFileValues.PREDONLINE_PASSWORD, 'test-password');
  assert.equal(plan.environment.PREDONLINE_PASSWORD, undefined);
  assert.equal(plan.authentication.mode, 'configured-or-manual');
});

test('validates required fields, integer ranges, upload paths and platform compatibility', () => {
  assert.throws(() => createRunPlan(pending, {}, options), /Missing required input/);
  assert.throws(() => createRunPlan(pending, { workbook: 'rows.xlsx', limit: 0 }, options), /Invalid integer/);
  assert.throws(() => createRunPlan(pending, { workbook: '../outside.xlsx' }, options), /safe package-relative/);
  assert.throws(() => createRunPlan(pending, { workbook: 'rows.csv' }, options), /Unsupported file extension/);
  assert.throws(() => createRunPlan(pending, { workbook: 'rows.xlsx', unexpected: 'value' }, options), /Unknown input/);
  assert.throws(() => createRunPlan(pending, { workbook: 'rows.xlsx' }, { ...options, platform: 'linux' }), /not compatible/);
  assert.throws(() => createRunPlan(pending, { workbook: 'rows.xlsx' }, { ...options, runDirectory: path.join(options.packageDirectory, 'run') }), /Separate package/);
});

test('both acquisition routes normalize independently of automation identity', () => {
  assert.deepEqual(describePackageSource({ type: 'zip', filename: 'package.zip' }), { type: 'zip', filename: 'package.zip' });
  const github = describePackageSource({ type: 'github', owner: 'mouralx', repository: 'automations' });
  assert.equal(github.releasesUrl, 'https://api.github.com/repos/mouralx/automations/releases');
  assert.throws(() => describePackageSource({ type: 'github', owner: '../other', repository: 'repo' }));
  assert.throws(() => describePackageSource({ type: 'github', owner: 123, repository: 'repo' }));
  assert.throws(() => describePackageSource({ type: 'zip', filename: 'payload.exe' }));
});

test('package preparation emits the artifact version rather than the template version', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'portal-package-test-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const destination = await writePackageManifest(pending, { packageDirectory: directory, version: '1.0.7' });
  const manifest = JSON.parse(await readFile(destination, 'utf8'));
  assert.equal(path.basename(destination), 'automation.json');
  assert.equal(manifest.version, '1.0.7');
  assert.equal(pending.version, '1.0.0');
  assert.deepEqual(validateManifest(manifest), []);
});

test('automation build integration emits the actual VERSION and preserves existing script behavior', async () => {
  const { addManifestEmission } = await import('../../infra/scripts/automations/lib/packages/integration.mjs');
  const source = "before();\n  fs.writeFileSync(path.join(OUT, 'VERSION'), `${VERSION}\\n`);\nafter();\n";
  const patched = addManifestEmission(source);
  assert.ok(patched.includes('version: VERSION'));
  assert.ok(patched.startsWith('before();'));
  assert.ok(patched.endsWith('after();\n'));
  assert.equal(addManifestEmission(patched), patched);
  assert.throws(() => addManifestEmission('unknown();'), /Unrecognized/);
});

test('interaction event integration emits a real newline-delimited status event', async()=>{
  const {addInteractionEvents}=await import('../../infra/scripts/automations/lib/packages/integration.mjs');
  const source="    await nextPage.goto(url, { waitUntil: 'domcontentloaded' });\n    return nextPage;";
  const patched=addInteractionEvents(source);
  const AsyncFunction=Object.getPrototypeOf(async function(){}).constructor;
  const emitted=[];
  await new AsyncFunction('nextPage','url','nextHeadless','process',patched)({goto:async()=>{}},'https://example.invalid',false,{env:{AUTOMATIONS_PORTAL_EVENTS:'1'},stdout:{write:value=>emitted.push(value)}});
  assert.equal(emitted[0],'@@automation-event@@'+JSON.stringify({type:'interaction',state:'awaiting-user'})+'\n');
});

test('rejects null bytes in settings before process execution',()=>{
  assert.throws(()=>createRunPlan(simplified,{workbook:'rows.xlsx',url:'https://example.invalid',password:'secret\u0000value'},options),/Invalid text input/);
});


test('integration uses automation-owned metadata without overwriting it', async t => {
  const { planAutomationIntegration } = await import('../../infra/scripts/automations/lib/packages/integration.mjs');
  const repository = await mkdtemp(path.join(os.tmpdir(), 'automation-metadata-test-'));
  t.after(() => rm(repository, { recursive: true, force: true }));
  for (const manifest of Object.values(templates)) {
    const directory = path.join(repository, 'src', manifest.id);
    await mkdir(path.join(directory, 'scripts'), { recursive: true });
    await writeFile(path.join(directory, 'automation.json'), JSON.stringify({ ...manifest, name: 'Author-maintained name' }));
    await writeFile(path.join(directory, 'scripts', 'build-windows.js'), "  for (const file of ['.env.example', 'README.md']) {\n  }\n  fs.writeFileSync(path.join(OUT, 'VERSION'), `${VERSION}\\n`);\n");
  }
  await mkdir(path.join(repository, 'src', 'shared'), { recursive: true });
  await writeFile(path.join(repository, 'src', 'shared', 'browser-session.js'), '// @@automation-event@@');
  await mkdir(path.join(repository, 'docs'));
  await writeFile(path.join(repository, 'docs', 'windows-packages.md'), 'Package documentation.');
  const changes = await planAutomationIntegration(repository);
  assert.equal(changes.length, 4);
  assert.ok(changes.every(change => change.path.endsWith('build-windows.js')));
  for (const manifest of Object.values(templates)) {
    const actual = JSON.parse(await readFile(path.join(repository, 'src', manifest.id, 'automation.json'), 'utf8'));
    assert.equal(actual.name, 'Author-maintained name');
  }
});
