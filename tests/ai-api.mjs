import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';

const root = new URL('../', import.meta.url).pathname;
const workspace = randomUUID(), actor = randomUUID();
const directory = await mkdtemp(join(tmpdir(), 'domus-ai-api-'));
let cardReads = 0;
const identity = createServer((request, response) => {
  response.setHeader('Content-Type', 'application/json');
  const role = request.headers.cookie;
  if (!['owner', 'member', 'admin'].includes(role)) { response.writeHead(401); return response.end('{}'); }
  if (request.url === '/api/domus/platform/context') return response.end(JSON.stringify({ userId: actor, canManagePlatform: role === 'admin' }));
  if (request.url === `/api/domus/workspaces/${workspace}/context`) return response.end(JSON.stringify({ userId: actor, workspaceId: workspace, canManage: role !== 'member', canManagePlatform: role === 'admin' }));
  if (request.url === `/api/workspaces/${workspace}/cards/allowed`) { cardReads++; return response.end(JSON.stringify({ id: 'allowed', title: 'Saved card', description: 'Description', comments: [], checklist: [] })); }
  if (request.url === `/api/domus/workspaces/${workspace}/projects/one/context?includeMetrics=false`) return response.end(JSON.stringify({project: {id:'one', name:'One'}}));
  if (request.url === `/api/workspaces/${workspace}`) return response.end(JSON.stringify({ projects: [{id: 'one', name: 'One'}], tasks: [], statuses: [], members: [] }));
  response.writeHead(404); response.end('{}');
});
await new Promise(resolve => identity.listen(0, '127.0.0.1', resolve));
const backend = `http://127.0.0.1:${identity.address().port}`;
const reserve = createServer(); await new Promise(resolve => reserve.listen(0, '127.0.0.1', resolve));
const port = reserve.address().port; await new Promise(resolve => reserve.close(resolve));
const origin = `http://127.0.0.1:${port}`;
const child = spawn('dotnet', [join(root, 'src/services/ai/bin/Debug/net10.0/Domus.Ai.Api.dll')], {
  cwd: root, env: { ...process.env, ASPNETCORE_URLS: origin, AI_ALLOW_FILE_STORE: 'true', AI_DATA_DIR: directory, PORTAL_PUBLIC_URL: origin, DOMUS_MEMBERSHIP_URL: backend, DOMUS_WORK_API_URL: backend }, stdio: 'pipe',
});
let logs = ''; child.stdout.on('data', value => logs += value); child.stderr.on('data', value => logs += value);
async function api(path, { role = 'admin', ws = workspace, method = 'GET', body, csrf = true } = {}) {
  return fetch(origin + '/api' + path, { method, headers: { Cookie: role, 'X-Domus-Workspace': ws, ...(csrf ? { Origin: origin, 'X-Domus-Request': '1' } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
}
try {
  let ready = false;
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch(origin + '/health')).ok) { ready = true; break; } } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  assert.ok(ready, 'AI service did not become healthy: ' + logs);
  assert.equal((await api('/workspace', { role: 'anonymous' })).status, 401);
  assert.equal((await api('/workspace', { ws: randomUUID() })).status, 404);
  assert.equal((await api('/workspace', { role: 'member', method: 'PUT', body: { enabled: false, provider: 'openai' } })).status, 403);
  assert.equal((await api('/connection/start', { method: 'POST', body: {}, csrf: false })).status, 403);
  assert.equal((await api('/connection/start', { method: 'POST', body: {} })).status, 409, 'Missing eligible registration must be explicit');
  const metadata = await (await api('/connection', { role: 'member' })).json();
  assert.equal(metadata.connected, false); assert.equal(metadata.configured, false);
  assert.equal((await api('/connection/models')).status, 409);
  assert.equal((await api('/workspace', { method: 'PUT', body: { enabled: true, provider: 'openai' } })).status, 200);
  const settings = await (await api('/workspace')).json();
  assert.equal('monthlyRequestLimit' in settings.configuration, false);
  assert.equal((await api('/suggestions', { method: 'POST', body: { cardId: 'forbidden', action: 'summary', locale: 'pt-PT' } })).status, 404);
  assert.equal((await api('/suggestions', { method: 'POST', body: { cardId: 'allowed', action: 'execute', locale: 'pt-PT' } })).status, 400);
  assert.equal((await api('/suggestions', { method: 'POST', body: { cardId: 'allowed', action: 'summary', locale: 'pt-PT' } })).status, 409);
  assert.equal((await api('/assistant', { method: 'POST', body: { projectId: 'one', question: '', locale: 'en-US' } })).status, 400);
  assert.equal((await api('/assistant', { method: 'POST', body: { projectId: 'hidden', question: 'Summary', locale: 'en-US' } })).status, 404);
  assert.equal((await api('/assistant', { method: 'POST', body: { projectId: 'one', question: 'Summary', locale: 'en-US' } })).status, 409);
  assert.equal(cardReads, 1, 'Authorized saved card is read by backend');
  assert.equal((await api('/connection', { role: 'member', method: 'DELETE' })).status, 200);
  console.log('PASS: AI API authentication, workspace/card access, owner permissions, CSRF, personal OAuth metadata, registration requirement, no monthly setting and input validation.');
} finally {
  child.kill('SIGTERM');
  await new Promise(resolve => child.once('exit', resolve));
  await new Promise(resolve => identity.close(resolve));
  await rm(directory, { recursive: true, force: true });
}
