import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
const root = new URL('../', import.meta.url).pathname;
const workspace = randomUUID(); let revoked = false; let denyActions = false; const reads = []; const actions = [];
const handle = service => (request, response) => {
  response.setHeader('Content-Type', 'application/json');
  if (request.method !== 'GET' && (request.url === '/api/auth/profile' || request.url === '/api/workspace' || request.url === '/api/connection/model' || request.url === '/api/runs' || request.url.includes('/cancel') || request.url.endsWith('/cards') || request.url === '/api/catalog/install')) {
    if (revoked || denyActions || request.headers.cookie !== 'owner' || request.headers['x-domus-workspace'] !== workspace || request.headers['x-domus-request'] !== '1' || !request.headers.origin) { response.writeHead(403); return response.end('{}'); }
    let raw='';request.on('data',v=>raw+=v);request.on('end',()=>{actions.push({service,path:request.url,method:request.method,body:JSON.parse(raw)});response.end(JSON.stringify({ok:true}));});return;
  }
  if (['/api/membership/session/context','/api/status','/api/catalog','/api/connection','/api/workspace'].includes(request.url) || (request.method === 'GET' && (request.url.startsWith('/api/runs/') || request.url.startsWith('/api/installations/')))) {
    if (revoked || request.headers.cookie !== 'owner' || request.headers['x-domus-workspace'] !== workspace) { response.writeHead(403); return response.end('{}'); }
    return response.end(JSON.stringify({ok:true}));
  }
  if (revoked || request.headers.cookie !== 'owner' || !request.url.startsWith(`/api/domus/workspaces/${workspace}/projects/one/`)) { response.writeHead(404); return response.end('{}'); }
  reads.push(request.url);
  if (request.url.includes('/cards/search')) return response.end(JSON.stringify({cards:[{id:'card-999',title:'Steering Sonae'}],hasMore:true,nextCursor:'card-999'}));
  if (request.url.includes('/cards/card-999/context')) return response.end(JSON.stringify({id:'card-999',title:'Steering Sonae',checklistTotal:10,checklistDone:3}));
  if (request.url.includes('/cards/')) {response.writeHead(404);return response.end('{}');}
  response.end(JSON.stringify({project:{id:'one',name:'One'},metrics:{total:1000000,open:999999}}));
};
const services=Object.fromEntries(['boards','membership','automations','ai'].map(name=>[name,createServer(handle(name))]));
for (const server of Object.values(services)) await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const backend=services.boards;
const reserve=createServer();await new Promise(resolve=>reserve.listen(0,'127.0.0.1',resolve));const port=reserve.address().port;await new Promise(resolve=>reserve.close(resolve));
const origin=`http://127.0.0.1:${port}`;
const child=spawn('dotnet',[root+'src/services/mcp/bin/Debug/net10.0/Domus.Mcp.Server.dll'],{cwd:root,env:{...process.env,ASPNETCORE_URLS:origin,PORTAL_PUBLIC_URL:origin,DOMUS_WORK_API_URL:`http://127.0.0.1:${backend.address().port}`,DOMUS_MEMBERSHIP_URL:`http://127.0.0.1:${services.membership.address().port}`,DOMUS_AUTOMATIONS_URL:`http://127.0.0.1:${services.automations.address().port}`,DOMUS_AI_URL:`http://127.0.0.1:${services.ai.address().port}`},stdio:'pipe'});let logs='';child.stdout.on('data',v=>logs+=v);child.stderr.on('data',v=>logs+=v);
async function rpc(method,params={},options={}) {
  const response=await fetch(origin+'/mcp',{method:'POST',headers:{'Content-Type':'application/json',Accept:'application/json, text/event-stream','MCP-Protocol-Version':'2025-11-25',Cookie:options.cookie??'owner','X-Domus-Workspace':options.workspace??workspace,'X-Domus-Project':options.project??'one',...(options.origin?{Origin:options.origin}:{})},body:JSON.stringify({jsonrpc:'2.0',id:1,method,params})});
  const raw=await response.text();let body={};if(raw.startsWith('event:')||raw.startsWith('data:')) {const data=raw.split('\n').find(line=>line.startsWith('data:'));if(data)body=JSON.parse(data.slice(5));}else if(raw)body=JSON.parse(raw);return {status:response.status,body};
}
try {
  for(let i=0;i<100;i++){try{if((await fetch(origin+'/health')).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  const init=await rpc('initialize',{protocolVersion:'2025-11-25',capabilities:{},clientInfo:{name:'domus-tests',version:'1'}});assert.equal(init.status,200,logs);assert.ok(init.body.result.capabilities.tools);
  const listed=await rpc('tools/list');assert.equal(listed.body.result.tools.length,17);
  for (const name of ['get_my_profile','get_automation_status','get_automation_catalog','get_my_ai_connection','get_ai_settings']) {
    const result=await rpc('tools/call',{name,arguments:{}});assert.ok(!result.body.result.isError,name);
  }
  const writes=[['create_card',{title:'Reviewed task',description:'Details',status:null,priority:'Medium'}],['update_my_profile',{name:'New name'}],['install_automation',{assetId:'asset'}],['start_automation_run',{packageKey:'package',values:{target:'test'}}],['cancel_automation_run',{runId:randomUUID()}],['set_workspace_ai_enabled',{enabled:true}],['set_my_ai_model',{model:'available-model'}]];
  for (const [name,args] of writes) {
    assert.equal(listed.body.result.tools.find(t=>t.name===name).annotations.readOnlyHint,false);
    const result=await rpc('tools/call',{name,arguments:args});assert.ok(!result.body.result.isError,name);
  }
  assert.equal(actions.length,7);assert.deepEqual(actions.map(a=>a.service),['boards','membership','automations','automations','automations','ai','ai']);assert.equal(actions[0].body.project,'one');
  denyActions=true;const denied=await rpc('tools/call',{name:'set_workspace_ai_enabled',arguments:{enabled:false}});assert.equal(denied.body.result.isError,true);assert.equal(actions.length,7);denyActions=false;
  const search=await rpc('tools/call',{name:'search_cards',arguments:{query:'steering Sonae',status:null,priority:null,completed:null,overdue:null,assignee:null,cursor:null,limit:20}});assert.equal(search.status,200);assert.ok(!search.body.result.isError);const cards=JSON.parse(search.body.result.content[0].text);assert.equal(cards.cards[0].id,'card-999');assert.equal(cards.hasMore,true);
  assert.ok(reads.some(path=>path.includes('q=steering%20Sonae')&&path.includes('limit=20')));
  const detail=await rpc('tools/call',{name:'get_card',arguments:{cardId:'card-999'}});assert.equal(JSON.parse(detail.body.result.content[0].text).checklistDone,3);
  assert.equal((await rpc('tools/list',{}, {cookie:''})).status,401);
  assert.equal((await rpc('tools/list',{}, {workspace:randomUUID()})).status,403);
  assert.equal((await rpc('tools/list',{}, {project:'hidden'})).status,403);
  assert.equal((await rpc('tools/list',{}, {origin:'https://untrusted.example'})).status,403);
  const forbidden=await rpc('tools/call',{name:'get_card',arguments:{cardId:'other-project-card'}});assert.equal(forbidden.body.result.isError,true);
  await new Promise((resolve,reject)=>{const sdk=spawn('dotnet',['run','--project',root+'tests/Ai.Tests/Domus.Ai.Tests.csproj'],{cwd:root,env:{...process.env,MCP_TEST_URL:origin+'/mcp',MCP_TEST_WORKSPACE:workspace},stdio:'inherit'});sdk.on('exit',code=>code?reject(new Error('SDK client checks failed')):resolve());});
  revoked=true;assert.equal((await rpc('tools/call',{name:'get_project_metrics',arguments:{}})).status,403);
  console.log('PASS: real MCP discovery/calls, scoped reads, pagination, authenticated actions across four APIs, API permission denials, missing session, project isolation, origin checks and immediate revocation.');
}finally{child.kill('SIGTERM');await new Promise(resolve=>child.once('exit',resolve));for (const server of Object.values(services)) await new Promise(resolve=>server.close(resolve));}
