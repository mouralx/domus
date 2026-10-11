import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { readFile, mkdir, mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { createServer } from 'node:net';
import { chromium, request } from '@playwright/test';
import yazl from 'yazl';
import { authenticatorCode, avatarPhoto } from '../src/portal/scripts/authenticator-test-helpers.mjs';

const root = new URL('../', import.meta.url).pathname;
const project = 'domus-qa-' + randomUUID().slice(0, 8);
const server = createServer(); await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port; await new Promise(resolve => server.close(resolve));
const origin = `http://localhost:${port}`;
const env = { ...process.env, DOMUS_PORT: String(port), DOMUS_PUBLIC_URL: origin, WORK_DB_PASSWORD: randomUUID(), AUTOMATION_DB_PASSWORD: randomUUID(), MEMBERSHIP_DB_PASSWORD: randomUUID(), AI_DB_PASSWORD: randomUUID(), POSTGRES_ADMIN_PASSWORD: randomUUID(), RABBITMQ_PASSWORD: randomUUID() };
const mcpFixture = await mkdtemp(tmpdir() + '/domus-mcp-integration-');
const mcpOverride = mcpFixture + '/compose.yaml';
await writeFile(mcpOverride, 'services:\n  mcp-server:\n    ports: ["127.0.0.1::4340"]\n');
const dockerArgs = [...(env.DOCKER_CONTEXT ? ['--context', env.DOCKER_CONTEXT] : []), 'compose', '-f', 'infra/compose.yaml', '-f', mcpOverride, '-p', project];
function compose(...args) {
  return new Promise((resolve, reject) => {
    const child = spawn('docker', [...dockerArgs, ...args], { cwd: root, env }); let output = '';
    child.stdout.on('data', value => output += value); child.stderr.on('data', value => output += value);
    child.on('error', reject); child.on('exit', code => code ? reject(new Error(output.slice(-12000))) : resolve(output));
  });
}
const contexts = []; let browser;
async function account(label) {
  const api = await request.newContext({ baseURL: origin, extraHTTPHeaders: { Origin: origin, 'X-Domus-Request': '1' } }); contexts.push(api);
  const email = label.toLowerCase() + '-' + randomUUID() + '@example.test'; const password = 'Synthetic-domus-password-2026';
  const register = await api.post('/api/auth/register', { data: { name: label, email, password } }); assert.equal(register.status(), 200);
  assert.equal((await api.get('/automations-api/api/status', { headers: { 'X-Domus-Workspace': 'studio' } })).status(), 403, '2FA enrollment must block automation access');
  assert.equal((await api.post('/api/auth/avatar', { data: { photo: avatarPhoto } })).status(), 204);
  const setup = await api.post('/api/auth/two-factor/setup', { data: { password } }); assert.equal(setup.status(), 200);
  const { secret } = await setup.json();
  const confirm = await api.post('/api/auth/two-factor/confirm', { data: { password, code: authenticatorCode(secret) } }); assert.equal(confirm.status(), 200);
  return { api, email, userId: (await register.json()).id };
}
async function auto(api, workspace, path, data, bytes) {
  return api.fetch('/automations-api/api' + path, { method: data !== undefined || bytes ? 'POST' : 'GET', headers: { 'X-Domus-Workspace': workspace, ...(bytes ? { 'Content-Type': 'application/octet-stream' } : {}) }, ...(data !== undefined ? { data } : bytes ? { data: bytes } : {}) });
}
async function until(callback) {
  for (let i = 0; i < 150; i++) { if (await callback()) return; await new Promise(resolve => setTimeout(resolve, 200)); }
  throw new Error('Timed out waiting for execution');
}
try {
  console.log(`Building all services with the single Compose file (${project})…`);
  await compose('up', '--build', '-d', '--wait', '--wait-timeout', '240');
  const running = (await compose('ps', '--services', '--status', 'running')).trim().split('\n'); assert.equal(running.length, 10);
  const anonymous = await request.newContext({ baseURL: origin }); contexts.push(anonymous);
  assert.equal((await auto(anonymous, 'studio', '/status')).status(), 401);
  assert.equal((await anonymous.get('/ai-api/api/workspace', { headers: { 'X-Domus-Workspace': 'studio' } })).status(), 401);
  for (const path of ['/api/platform/branding','/api/openapi.json','/api/scalar','/api/workspaces','/api/invitations/unknown','/api/membership/session/context','/api/platform/branding/access','/api/health','/api/membership/health','/ai-api/health','/automations-api/health']) {
    assert.equal((await anonymous.get(path)).status(),401,`${path} must not be publicly accessible`);
  }
  assert.equal((await anonymous.get('/api/auth/session')).status(),200,'Login bootstrap must remain available');
  const owner = await account('Owner');
  env.PLATFORM_ADMIN_EMAILS = owner.email;
  await compose('up', '-d', '--force-recreate', '--wait', 'boards', 'membership');
  const databases = await compose('exec', '-T', 'postgres', 'psql', '-U', 'domus_admin', '-d', 'postgres', '-tAc', "SELECT datname FROM pg_database WHERE datname IN ('boards','automations','membership','ai') ORDER BY datname");
  assert.deepEqual(databases.trim().split('\n'), ['ai', 'automations', 'boards', 'membership']);
  assert.ok(!running.some(service=>['work-api','automation-api','ai-api','jira-worker','automation-worker'].includes(service)));
  await compose('exec','-T','membership','sh','-c','test -z "$ConnectionStrings__Boards"');
  const membershipAccess = await compose('exec','-T','postgres','psql','-U','domus_admin','-d','postgres','-tAc',"SELECT has_database_privilege('domus_membership','boards','CONNECT')");assert.equal(membershipAccess.trim(),'f','Membership role must not access the boards database');
  const member = await account('Member'); const outsider = await account('Outsider');
  assert.equal((await member.api.post('/api/auth/profile', { data: { name: 'Updated Member' } })).status(), 204);
  assert.equal((await (await member.api.get('/api/membership/session/context')).json()).name, 'Updated Member');
  const memberWorkspace = await (await member.api.get('/api/workspaces/studio')).json();
  assert.equal(memberWorkspace.members.find(item=>item.userId===member.userId).name,'Updated Member','Profile projection must arrive through the owning API');
  const personalCount = await compose('exec','-T','postgres','psql','-U','domus_boards','-d','boards','-tAc',`SELECT count(*) FROM workspaces WHERE owner_id='${member.userId}' AND personal`);assert.equal(personalCount.trim(),'1','Projection retries must not duplicate personal workspaces');
  // Simulate a boards outage. Identity changes must commit with a durable pending event.
  await compose('stop','boards');
  assert.equal((await member.api.post('/api/auth/profile',{data:{name:'Member after retry'}})).status(),204);
  const pending=await compose('exec','-T','postgres','psql','-U','domus_membership','-d','membership','-tAc',`SELECT count(*) FROM membership_directory_outbox WHERE user_id='${member.userId}'`);assert.equal(pending.trim(),'1');
  await compose('up','-d','--wait','boards');
  await until(async()=>{const response=await member.api.get('/api/workspaces/studio');if(!response.ok())return false;return (await response.json()).members.some(item=>item.userId===member.userId&&item.name==='Member after retry');});
  const identityTables = await compose('exec', '-T', 'postgres', 'psql', '-U', 'domus_membership', '-d', 'membership', '-tAc', "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name");
  assert.deepEqual(identityTables.trim().split('\n'), ['identities', 'membership_directory_outbox', 'recovery_codes', 'sessions', 'users']);
  const stateResponse = await owner.api.post('/api/workspaces', { data: { name: 'Shared automations' } }); assert.equal(stateResponse.status(), 200);
  const state = await stateResponse.json(); const workspace = state.workspace.id;
  assert.equal((await auto(outsider.api, workspace, '/status')).status(), 404);
  state.members.push({ name: 'Member', email: member.email, initials: 'ME', color: '#aabbcc' });
  const invitation = await owner.api.put('/api/workspaces/' + workspace, { headers: { 'If-Match': String(state.version) }, data: state }); assert.equal(invitation.status(), 200);
  const invited = await invitation.json();
  const token = invited.members.find(item => item.email === member.email).invitationToken;
  assert.equal((await member.api.post('/api/invitations/' + token + '/accept')).status(), 200);
  assert.equal((await auto(member.api, workspace, '/status')).status(), 200);
  const aiHeaders = { 'X-Domus-Workspace': workspace, 'X-Domus-Request': '1' };
  assert.equal((await outsider.api.get('/ai-api/api/workspace', { headers: aiHeaders })).status(), 404);
  assert.equal((await member.api.get('/ai-api/api/connection', { headers: aiHeaders })).status(), 200);
  assert.equal((await member.api.put('/ai-api/api/workspace', { headers: aiHeaders, data: { enabled: false, provider: 'openai' } })).status(), 403);
  assert.equal((await owner.api.get('/ai-api/api/connection', { headers: aiHeaders })).status(), 200);
  assert.equal((await owner.api.put('/ai-api/api/workspace', { headers: aiHeaders, data: { enabled: false, provider: 'openai' } })).status(), 200);
  assert.equal((await owner.api.post('/ai-api/api/suggestions', { headers: aiHeaders, data: { cardId: 'missing', action: 'summary', locale: 'pt-PT' } })).status(), 409);

  const mcpPort = (await compose('port','mcp-server','4340')).trim().split(':').at(-1);
  async function mcpCall(api, name, argumentsValue = {}) {
    const cookies = (await api.storageState()).cookies.map(c=>c.name+'='+c.value).join('; ');
    const response = await fetch(`http://127.0.0.1:${mcpPort}/mcp`, {method:'POST',headers:{'Content-Type':'application/json',Accept:'application/json, text/event-stream','MCP-Protocol-Version':'2025-11-25',Cookie:cookies,Origin:origin,'X-Domus-Workspace':workspace,'X-Domus-Project':state.projects[0].id},body:JSON.stringify({jsonrpc:'2.0',id:1,method:'tools/call',params:{name,arguments:argumentsValue}})});
    const raw=await response.text();const data=raw.split('\n').find(line=>line.startsWith('data:'));
    return {status:response.status,body:JSON.parse(data?data.slice(5):raw)};
  }
  for (const name of ['get_project_metrics','get_my_profile','get_automation_status','get_ai_settings','get_my_ai_connection']) {
    const result = await mcpCall(owner.api,name);assert.equal(result.status,200);assert.ok(!result.body.result.isError,name);
  }
  const profileWrite=await mcpCall(owner.api,'update_my_profile',{name:'Owner'});assert.ok(!profileWrite.body.result.isError);
  const settingsWrite=await mcpCall(owner.api,'set_workspace_ai_enabled',{enabled:false});assert.ok(!settingsWrite.body.result.isError);
  const deniedWrite=await mcpCall(member.api,'set_workspace_ai_enabled',{enabled:true});assert.equal(deniedWrite.body.result.isError,true);
  assert.equal((await mcpCall(outsider.api,'get_my_profile')).status,403);

  const searchState = await (await owner.api.get('/api/workspaces/' + workspace)).json();
  const searchProject = searchState.projects[0].id;
  const template = searchState.tasks[0] ?? {description:'',status:searchState.statuses[0].name,priority:'Medium',labels:[],assignees:[],due:'',comments:[],checklist:[]};
  searchState.tasks = Array.from({length:105}, (_, index) => ({...template,id:'KB-SEARCH-'+String(index).padStart(3,'0'),project:searchProject,title:index===104?'Steering Sonae beyond the old limit':'Synthetic card '+index}));
  const seeded = await owner.api.put('/api/workspaces/' + workspace, {headers:{'If-Match':String(searchState.version)},data:searchState}); assert.equal(seeded.status(),200,await seeded.text());
  const readBase = `/api/domus/workspaces/${workspace}/projects/${searchProject}`;
  const found = await (await owner.api.get(readBase+'/cards/search?q=steering%20Sonae')).json();assert.equal(found.cards.length,1);assert.equal(found.cards[0].id,'KB-SEARCH-104');
  const pageOne = await (await owner.api.get(readBase+'/cards/search?limit=50')).json();assert.equal(pageOne.cards.length,50);assert.equal(pageOne.hasMore,true);
  const pageTwo = await (await owner.api.get(readBase+'/cards/search?limit=50&cursor='+pageOne.nextCursor)).json();assert.equal(pageTwo.cards.length,50);assert.ok(pageTwo.cards.every(card=>!pageOne.cards.some(first=>first.id===card.id)));
  assert.equal((await outsider.api.get(readBase+'/cards/search')).status(),404);
  assert.equal((await owner.api.get(readBase+'/cards/search?limit=1000')).status(),400);
  assert.equal((await owner.api.get(readBase+'/cards/search?q=%27%3B%20DROP%20TABLE%20cards%3B')).status(),200);
  const metrics = await (await owner.api.get(readBase+'/context')).json();assert.equal(metrics.metrics.total,105);
  const cardDetail = await (await owner.api.get(readBase+'/cards/KB-SEARCH-104/context')).json();assert.equal(cardDetail.id,'KB-SEARCH-104');
  assert.equal((await owner.api.get(`/api/domus/workspaces/${workspace}/projects/hidden/cards/KB-SEARCH-104/context`)).status(),404);

  const cardWrite=await mcpCall(owner.api,'create_card',{title:'Reviewed MCP card',description:'Created through the boards API',status:null,priority:'Medium'});
  assert.ok(!cardWrite.body.result.isError);const mcpCard=JSON.parse(cardWrite.body.result.content[0].text);assert.equal(mcpCard.project,searchProject);

  assert.equal((await auto(member.api, workspace, '/settings/github')).status(), 403);
  assert.equal((await auto(member.api, workspace, '/SETTINGS/GITHUB/')).status(), 403);
  assert.equal((await auto(member.api, workspace, '/packages/remove', { packageKey: 'fake' })).status(), 403);
  assert.equal((await auto(member.api, workspace, '/catalog/install', { assetId: 'fake' })).status(), 403);
  assert.equal((await auto(owner.api, workspace, '/register', { username: 'forged', password: 'fake' })).status(), 404);
  const pat = 'github_pat_' + 'synthetic'.repeat(10);
  assert.equal((await auto(owner.api, workspace, '/settings/github', { repository: 'fixtures/automations', token: pat })).status(), 200);
  const config = await (await auto(owner.api, workspace, '/settings/github')).json(); assert.equal(config.saved, true); assert.equal(config.scope, 'platform');
  const shared = await (await auto(owner.api, 'studio', '/settings/github')).json(); assert.equal(shared.repository, config.repository); assert.ok(!JSON.stringify(config).includes(pat));
  const stored = await compose('exec', '-T', 'postgres', 'psql', '-U', 'domus_automations', '-d', 'automations', '-tAc', 'SELECT encrypted_token FROM platform_github_settings'); assert.ok(!stored.includes(pat));
  const manifest = JSON.parse(await readFile(new URL('../tests/fixtures/manifests/pending-records.automation.json', import.meta.url)));
  const platform = await (await auto(owner.api, workspace, '/status')).json();
  manifest.id = 'domus-fixture'; manifest.name = 'Synthetic Domus automation'; manifest.platform = { os: platform.platform, arch: platform.arch };
  manifest.inputs = []; manifest.execution.executable = 'fixture.sh'; manifest.execution.packagePaths = {}; manifest.execution.workspaceResources = [];
  manifest.effects = { modifiesInput: false, publishesComments: false, createsPaidRequests: false };
  const zip = new yazl.ZipFile(); zip.addBuffer(Buffer.from(JSON.stringify(manifest)), 'automation.json'); zip.addBuffer(Buffer.from('#!/bin/sh\nset -eu\nprintf "DOMUS RESULT" > result.txt\necho "complete"\n'), 'fixture.sh', { mode: 0o100755 });
  const chunks = []; zip.outputStream.on('data', chunk => chunks.push(chunk)); const bytes = new Promise((resolve, reject) => { zip.outputStream.on('end', () => resolve(Buffer.concat(chunks))); zip.outputStream.on('error', reject); }); zip.end();
  const imported = await auto(owner.api, workspace, '/import', undefined, await bytes); assert.equal(imported.status(), 201);
  const installed = await imported.json();
  const run = await mcpCall(member.api, 'start_automation_run', { packageKey: installed.key, values: {} }); assert.ok(!run.body.result.isError); const job = JSON.parse(run.body.result.content[0].text);
  assert.equal(job.initiatedBy, member.userId);
  await until(async () => (await (await auto(owner.api, workspace, '/runs/' + job.id)).json()).status === 'completed');
  const detail = await (await auto(owner.api, workspace, '/runs/' + job.id)).json(); assert.ok(detail.files.length > 0);
  const download = await member.api.get('/automations-api/api/runs/' + job.id + '/results.zip?workspace=' + workspace); assert.equal(download.status(), 200); assert.equal((await download.body()).subarray(0, 2).toString(), 'PK');
  assert.equal((await outsider.api.get('/automations-api/api/runs/' + job.id + '/results.zip?workspace=' + workspace)).status(), 404);
  assert.equal((await auto(owner.api, 'studio', '/runs/' + job.id)).status(), 404, 'personal workspace must remain isolated');
  const snapshot = await (await owner.api.get('/api/workspaces/' + workspace)).json(); snapshot.members = snapshot.members.filter(item => item.email !== member.email);
  assert.equal((await owner.api.put('/api/workspaces/' + workspace, { headers: { 'If-Match': String(snapshot.version) }, data: snapshot })).status(), 200);
  assert.equal((await auto(member.api, workspace, '/status')).status(), 404, 'membership revocation must take effect immediately');
  browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ storageState: await owner.api.storageState(), viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage(); const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(origin + '/?workspace=' + workspace);
  const navigation = page.getByRole('navigation', { name: 'Workspace navigation' });
  await navigation.waitFor();
  const management = navigation.getByRole('button', { name: 'Management', exact: true });
  const automationGroup = navigation.locator('.nav-group-toggle').filter({ hasText: 'Automations' });
  const settings = navigation.getByRole('button', { name: 'Settings', exact: true });
  assert.deepEqual(await navigation.locator('.nav-group-toggle').evaluateAll(items => items.map(item => item.getAttribute('aria-expanded'))), ['false','false','false','false']);
  const glyphs = await page.locator('.sidebar nav button, .sidebar-bottom button').evaluateAll(items => items.map(item => item.querySelector(':scope > svg')?.innerHTML).filter(Boolean));
  assert.equal(new Set(glyphs).size, glyphs.length, 'every navigation item must have a distinct icon');
  await automationGroup.click();
  await navigation.getByRole('button', { name: 'Flows', exact: true }).click();
  await page.getByText('Synthetic Domus automation', { exact: true }).first().waitFor();
  assert.equal(await page.title(), 'domus');
  const branding = await (await owner.api.get('/api/platform/branding')).json();
  assert.equal(branding.name, 'domus'); assert.equal(branding.primary, '#167d8d'); assert.equal(branding.accent, '#a8ded6');
  assert.equal(await page.locator('.sidebar').evaluate(element => getComputedStyle(element).backgroundColor), 'rgb(23, 43, 58)');
  assert.equal(await page.locator('.sidebar .domus-logo-mark').count(), 1);
  assert.ok((await page.locator('link[data-platform-icon]').getAttribute('href')).endsWith('/domus-mark.svg'));
  await management.click();
  assert.equal(await automationGroup.getAttribute('aria-expanded'), 'false');
  const projects = navigation.getByRole('button', { name: 'Boards', exact: true, includeHidden: true });
  assert.equal(await projects.getAttribute('aria-expanded'), 'false');
  await projects.click(); assert.equal(await management.getAttribute('aria-expanded'), 'true');
  assert.equal(await navigation.getByRole('button', { name: 'My activities', exact: true }).count(), 1);
  await settings.click(); assert.equal(await management.getAttribute('aria-expanded'), 'false');
  assert.equal(await projects.getAttribute('aria-expanded'), 'false');
  assert.equal(await automationGroup.getAttribute('aria-expanded'), 'false');
  assert.equal(await page.locator('.sidebar-bottom').getByRole('button', { name: 'Settings', exact: true }).count(), 0);
  assert.equal(await page.locator('#domus-nav-settings').getByRole('button', { name: 'Workspace', exact: true }).count(), 1);
  assert.equal(await page.locator('#domus-nav-settings').getByRole('button', { name: 'Automations', exact: true }).count(), 1);
  await navigation.getByRole('button', { name: 'Overview', exact: true }).click();
  await page.getByRole('region', { name: 'Workspace dashboard' }).waitFor();
  const overview = page.getByRole('region', { name: 'Automation overview' });
  await overview.getByText('Synthetic Domus automation', { exact: true }).waitFor();
  assert.equal(await overview.locator('.domus-metrics strong').nth(2).textContent(), '1');
  await mkdir(new URL('../.local/screenshots/', import.meta.url), { recursive: true });
  await page.screenshot({ path: root + '.local/screenshots/domus-overview.png', fullPage: true });
  await page.getByRole('combobox', { name: 'Theme', exact: true }).selectOption('dark');
  await page.waitForFunction(() => document.documentElement.dataset.theme === 'dark');
  const overviewButtonBackground = await overview.getByRole('button', { name: 'View execution', exact: true }).evaluate(element => getComputedStyle(element).backgroundColor);
  assert.notEqual(overviewButtonBackground, 'rgb(255, 255, 255)', 'automation overview must respect the dark theme');
  await page.getByRole('combobox', { name: 'Theme', exact: true }).selectOption('light');
  await overview.getByRole('button', { name: 'View execution', exact: true }).click();
  await page.getByRole('link', { name: 'Descarregar resultados ZIP' }).waitFor();

  await automationGroup.click();
  assert.equal(await settings.getAttribute('aria-expanded'), 'false');
  await page.getByRole('button', { name: 'Executions', exact: true }).click(); await page.getByRole('button', { name: 'Ver execução' }).click(); await page.getByRole('link', { name: 'Descarregar resultados ZIP' }).waitFor();
  await mkdir(new URL('../.local/screenshots/', import.meta.url), { recursive: true }); await page.screenshot({ path: root + '.local/screenshots/domus-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 }); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
  await page.screenshot({ path: root + '.local/screenshots/domus-mobile.png', fullPage: true }); assert.deepEqual(errors, []);
  await owner.api.post('/api/auth/logout');
  assert.equal((await auto(owner.api, workspace, '/status')).status(), 401, 'logout must revoke automation access');
  console.log('Domus integration passed: separate APIs, one login, 2FA, workspace isolation, owner permissions, execution, ZIP, React portal and revocation.');
} catch (error) {
  console.error(await compose('logs', '--tail', '35', 'boards', 'automations', 'automations-worker', 'mcp-server', 'ai').catch(() => '')); throw error;
} finally {
  if (browser) await browser.close(); await Promise.all(contexts.map(context => context.dispose()));
  await compose('down', '--volumes', '--remove-orphans');
  await rm(mcpFixture,{recursive:true,force:true});
}
