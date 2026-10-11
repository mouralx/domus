import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';

const root = new URL('../', import.meta.url).pathname;
const reserve = createServer(); await new Promise(resolve => reserve.listen(0, '127.0.0.1', resolve));
const port = reserve.address().port; await new Promise(resolve => reserve.close(resolve));
const server = spawn('node', ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', String(port)], { cwd: root + 'src/portal', env: { ...process.env, VITE_DATA_MODE: 'api' }, stdio: 'pipe' });
let logs = ''; server.stdout.on('data', value => logs += value); server.stderr.on('data', value => logs += value);
let browser;
try {
  const origin = `http://127.0.0.1:${port}`;
  for (let i = 0; i < 100; i++) { try { if ((await fetch(origin)).ok) break; } catch {} await new Promise(resolve => setTimeout(resolve, 100)); }
  browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHANNEL } : {}) });
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  let advertisedOrigin = origin;
  await page.route('**/ai-api/local-origin', route => route.fulfill({ json: { origin: advertisedOrigin } }));
  let generated = 0;
  const provider = { provider: 'openai', model: 'test-model', enabled: true, hasKey: true };
  const workspace = { configuration: { enabled: true, provider: 'openai',  }, providers: [provider], canManage: true, usage: { requests: 0, inputTokens: 0, outputTokens: 0 } };
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let body = {};
    if (path === '/api/auth/session') body = { user: null, providers: { google: false, microsoft: false } };
    else if (path === '/api/platform/branding') return route.fulfill({status:401,json:{error:'Authentication required'}});
    else if (path === '/api/mock-branding') body = { version: 0, name: 'domus', logo: null, showName: true, primary: '#167d8d', accent: '#a8ded6', lightBackground: '#f5f7fa', darkBackground: '#101923', defaultTheme: 'light', sidebarBackground: '#172b3a', fontScale: 100, cornerRadius: 8 };
    else if (path === '/api/platform/branding/access') body = { canManage: true };
    else if (path === '/ai-api/api/workspace') body = workspace;
    else if (path === '/ai-api/api/connection') body = { configured: true, connected: true, email: 'test@example.test', model: 'test-model', provider: 'openai' };
    else if (path === '/ai-api/api/assistant') { const input = route.request().postDataJSON(); assert.equal(input.projectId, input.scope==='automations'?'my-activities':'project'); assert.ok(input.question); if(input.scope==='automations') return route.fulfill({json:{conversationId:'automation-session',answer:'Two automations are installed.',references:[],cardsIncluded:0,totalCards:0,model:'test-model'}}); body = { conversationId: 'session', answer: 'The project has one overdue card.', references: [{id: 'card-one', title: 'Card', project: 'project', href: '/?workspace=workspace&card=card-one'}], cardsIncluded: 1, totalCards: 2, model: 'test-model' }; }
    else if (path === '/ai-api/api/connection/models') body = [{ id: 'test-model', name: 'Test model' }]; else if (path === '/ai-api/api/suggestions') {
      const request = route.request().postDataJSON(); generated++;
      assert.deepEqual(Object.keys(request).sort(), ['action', 'cardId', 'locale']);
      body = { provider: 'openai', model: 'test-model', action: request.action, suggestion: { text: 'Proposed description', items: request.action === 'checklist' ? ['Existing item', 'New step'] : [] } };
    }
    await route.fulfill({ json: body });
  });
  await page.goto(origin.replace('127.0.0.1', 'localhost') + '/?workspace=test#view');
  await page.locator('.auth-page').waitFor();
  assert.equal(new URL(page.url()).hostname, '127.0.0.1');
  assert.equal(new URL(page.url()).search, '?workspace=test');
  assert.equal(new URL(page.url()).hash, '#view');
  await page.evaluate(async () => {
    const { default: React } = await import('/node_modules/.vite/deps/react.js');
    const { default: ReactDOM } = await import('/node_modules/.vite/deps/react-dom_client.js');
    const { CardAi } = await import('/src/features/ai/CardAi.tsx');
    const { AiSettings } = await import('/src/features/ai/AiSettings.tsx');
    const node = document.createElement('div'); document.body.replaceChildren(node);
    const root = ReactDOM.createRoot(node);
    const task = { id: 'card-one', title: 'Card', description: 'Original description', comments: [], checklist: [{ text: 'Existing item', done: true }], project: 'project', status: 'Open', priority: 'Medium', labels: [], assignees: [], due: '' };
    window.aiTest = { task, draft: task, applied: null, renderSettings: () => root.render(React.createElement('div', {className:'modal-overlay'}, React.createElement('section', {className:'modal ai-settings-modal'}, React.createElement('div', {className:'modal-heading'}, React.createElement('span', null, 'AI settings')), React.createElement(AiSettings, { workspace: 'workspace' })))), renderCard: (draft = task) => { window.aiTest.draft = draft; root.render(React.createElement(CardAi, { workspace: 'workspace', saved: task, draft, onApply: value => { window.aiTest.applied = value; window.aiTest.renderCard(value); } })); } };
    window.aiTest.renderCard();
  });
  await page.getByRole('button', { name: 'Help with AI', exact: true }).click();
  await page.getByRole('button', { name: 'Generate suggestion', exact: true }).click();
  await page.getByRole('button', { name: 'Apply to draft', exact: true }).waitFor();
  assert.equal(await page.evaluate(() => window.aiTest.applied), null, 'Generation must not apply changes');
  await page.getByRole('button', { name: 'Apply to draft', exact: true }).click();
  assert.equal(await page.evaluate(() => window.aiTest.applied.description), 'Proposed description');
  assert.equal(await page.getByRole('button', { name: 'Generate suggestion', exact: true }).isDisabled(), true, 'Unsaved edits must disable generation');
  await page.evaluate(() => window.aiTest.renderCard(window.aiTest.task));
  await page.getByRole('combobox', { name: 'AI action', exact: true }).selectOption('checklist');
  await page.getByRole('button', { name: 'Generate suggestion', exact: true }).click();
  await page.getByRole('button', { name: 'Apply to draft', exact: true }).click();
  assert.deepEqual(await page.evaluate(() => window.aiTest.applied.checklist), [{ text: 'Existing item', done: true }, { text: 'New step', done: false }]);
  await page.evaluate(() => window.aiTest.renderCard(window.aiTest.task));
  await page.getByRole('button', { name: 'Generate suggestion', exact: true }).click();
  await page.getByRole('button', { name: 'Apply to draft', exact: true }).waitFor();
  await page.evaluate(() => window.aiTest.renderCard({ ...window.aiTest.task, title: 'Changed title' }));
  assert.equal(await page.getByRole('button', { name: 'Apply to draft', exact: true }).isDisabled(), true, 'Stale suggestion must be blocked');
  await page.evaluate(() => window.aiTest.renderSettings());
  await page.getByRole('combobox', { name: 'ChatGPT model', exact: true }).waitFor();
  assert.equal(await page.getByLabel('API key', { exact: true }).count(), 0);
  assert.equal(await page.getByLabel('Monthly request limit', { exact: true }).count(), 0);
  assert.equal(await page.getByText('Connected account', { exact: false }).count(), 1);
  for (const theme of ['light', 'dark']) {
    await page.evaluate(theme => document.documentElement.setAttribute('data-theme', theme), theme);
    const inset = await page.locator('.ai-settings').evaluate(element => { const section = element.querySelector('.ai-provider'); return section.getBoundingClientRect().left - element.getBoundingClientRect().left; });
    assert.ok(inset >= 20, 'AI settings content must have modal gutters');
    await page.getByRole('button', {name:'Disconnect ChatGPT', exact:true}).hover();
    await page.waitForTimeout(300);
    if (theme === 'dark') assert.ok(await page.getByRole('button', {name:'Disconnect ChatGPT', exact:true}).evaluate(el => Math.max(...getComputedStyle(el).backgroundColor.match(/\d+/g).map(Number)) < 100), 'Secondary button hover must retain a dark surface');
    await page.screenshot({path:`/private/tmp/domus-ai-settings-${theme}.png`});
  }
  await page.setViewportSize({ width: 390, height: 844 });
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  assert.equal(generated, 3);
  await page.evaluate(async () => {
    const { default: React } = await import('/node_modules/.vite/deps/react.js');
    const { default: ReactDOM } = await import('/node_modules/.vite/deps/react-dom_client.js');
    const { ProjectAssistant } = await import('/src/features/ai/ProjectAssistant.tsx');
    const node = document.createElement('div'); document.body.replaceChildren(node);
    window.openedCard = null;
    const root = ReactDOM.createRoot(node); window.renderAutomationAssistant = () => root.render(React.createElement(ProjectAssistant,{key:'automations',mode:'automations',workspace:'workspace',projects:[],initialProject:'my-activities',open:true,onClose:()=>{},onSettings:()=>{},onCard:async()=>{}}));
    root.render(React.createElement(ProjectAssistant, {workspace: 'workspace', projects: [{id:'project', name:'Project'}], initialProject:'project', open:true, onClose:()=>{}, onSettings:()=>{}, onCard:async id=>{window.openedCard=id;} }));
  });
  let releaseAnswer;
  const answerGate = new Promise(resolve => { releaseAnswer = resolve; });
  await page.route('**/ai-api/api/assistant', async route => {
    await answerGate;
    await route.fulfill({json:{conversationId:'session',answer:'The project has one overdue card.',references:[{id:'card-one',title:'Card',project:'project',href:'/?card=card-one'}],cardsIncluded:1,totalCards:2,model:'test-model'}});
  }, {times:1});
  const composer = page.locator('#assistant-question');
  await composer.fill('Which cards are at risk?');
  await composer.press('Shift+Enter');
  assert.equal(await composer.inputValue(), 'Which cards are at risk?\n');
  await composer.press('Enter');
  await page.getByRole('status').waitFor();
  assert.equal(await composer.inputValue(), '');
  assert.equal(await page.locator('.assistant-question').textContent(), 'Which cards are at risk?');
  releaseAnswer();
  await page.getByText('The project has one overdue card.').waitFor();
  await page.getByRole('link', {name:'Card', exact:true}).click();
  assert.equal(await page.evaluate(()=>window.openedCard), 'card-one');
  assert.ok(await page.getByText('Consulted', {exact:false}).count());
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth <= innerWidth), true);
  await page.screenshot({path:'/private/tmp/domus-ai-assistant-mobile.png', fullPage:true});
  await page.setViewportSize({width:1280,height:900});
  await page.screenshot({path:'/private/tmp/domus-ai-assistant-desktop.png', fullPage:true});
  await page.getByRole('button', {name:'New conversation', exact:true}).click();
  assert.equal(await page.getByText('The project has one overdue card.').count(),0);

  await page.evaluate(async () => {
    const { default: React } = await import('/node_modules/.vite/deps/react.js');
    const { default: ReactDOM } = await import('/node_modules/.vite/deps/react-dom_client.js');
    const { AssistantText } = await import('/src/features/ai/AssistantText.tsx');
    const node = document.createElement('div'); document.body.replaceChildren(node);
    ReactDOM.createRoot(node).render(React.createElement(AssistantText, {text: '**Em curso**' + String.raw`\n\nPendentes:\n- Rever decisões\n- Confirmar participantes` + '\n\n<img src=x onerror=alert(1)> `C:\\new`'}));
  });
  await page.locator('.assistant-prose strong').waitFor();
  assert.equal(await page.locator('.assistant-prose strong').textContent(), 'Em curso');
  assert.equal(await page.locator('.assistant-prose li').count(), 2);
  assert.equal(await page.locator('.assistant-prose img').count(), 0, 'AI text must never inject HTML');
  assert.ok(!(await page.locator('.assistant-prose').textContent()).includes('**'));
  assert.equal(await page.locator('.assistant-prose code').textContent(), String.raw`C:\new`, 'Code escapes must be preserved');
  await page.evaluate(()=>{const node=document.createElement('div');document.body.replaceChildren(node);window.automationNode=node;});
  await page.evaluate(async()=>{const {default:React}=await import('/node_modules/.vite/deps/react.js');const {default:ReactDOM}=await import('/node_modules/.vite/deps/react-dom_client.js');const {ProjectAssistant}=await import('/src/features/ai/ProjectAssistant.tsx');ReactDOM.createRoot(window.automationNode).render(React.createElement(ProjectAssistant,{mode:'automations',workspace:'workspace',projects:[],initialProject:'my-activities',open:true,onClose:()=>{},onSettings:()=>{},onCard:async()=>{}}));});
  await page.getByRole('dialog',{name:'Automation assistant',exact:true}).waitFor();
  assert.equal(await page.getByRole('combobox',{name:'Assistant project'}).count(),0);
  await page.getByRole('button',{name:'Which automations are installed?',exact:true}).click();
  await page.getByText('Two automations are installed.',{exact:true}).waitFor();
  await page.route('**/automations-api/api/**', route=>route.fulfill({json:new URL(route.request().url()).pathname.endsWith('/installations')?[]:{packages:[],runs:[],metrics:{running:0,completed:0},automations:[]}}));
  await page.evaluate(async () => {
    const { default: React } = await import('/node_modules/.vite/deps/react.js');
    const { default: ReactDOM } = await import('/node_modules/.vite/deps/react-dom_client.js');
    const { App } = await import('/src/app/App.tsx');
    const { LogoutContext } = await import('/src/features/auth/LogoutContext.ts');
    const { remoteRepository } = await import('/src/infrastructure/remoteRepository.ts');
    const state={workspace:{id:'workspace',name:'Test workspace',canManage:true},version:1,notifications:[],tasks:[],projects:[{id:'my-activities',name:'My activities',description:'',color:'#167d8d',system:'activities'},{id:'project',name:'Project',description:'',color:'#167d8d'}],members:[{name:'Test owner',email:'test@example.test',initials:'TO',color:'#167d8d'}],activity:[],statuses:[{id:'open',name:'Open',color:'#167d8d',complete:false}],buckets:[],labels:[],swimlanes:[]};
    remoteRepository.load=async()=>structuredClone(state);remoteRepository.switchWorkspace=async()=>structuredClone(state);remoteRepository.listWorkspaces=async()=>[state.workspace];
    history.replaceState(null,'','/');
    const node=document.createElement('div');document.body.replaceChildren(node);ReactDOM.createRoot(node).render(React.createElement(LogoutContext.Provider,{value:{busy:false,error:'',logout:async()=>{}}},React.createElement(App)));
  });
  const launcher=page.getByRole('button',{name:'Open assistant',exact:true});
  await launcher.waitFor();
  assert.deepEqual(await page.locator('.sidebar-bottom button').allTextContents(),['Help','Sign out']);
  for (const theme of ['light','dark']) {
    await page.evaluate(theme=>document.documentElement.setAttribute('data-theme',theme),theme);
    const box=await launcher.boundingBox();assert.ok(box.x+box.width<=1280&&box.y+box.height<=900);assert.ok(box.width>=44&&box.height>=44);
  }
  await launcher.click();
  await page.getByRole('dialog',{name:'Assistant',exact:true}).waitFor();
  await page.getByRole('group',{name:'Assistant area'}).getByRole('button',{name:'Automations',exact:true}).click();
  await page.getByRole('button',{name:'Which automations are installed?',exact:true}).waitFor();
  await page.getByRole('dialog').getByRole('button',{name:'Close',exact:true}).click();
  await launcher.waitFor();await page.waitForFunction(()=>document.activeElement?.classList.contains('assistant-launcher'));assert.equal(await launcher.evaluate(el=>el===document.activeElement),true);
  const navigation=page.getByRole('navigation',{name:'Workspace navigation'});
  await navigation.getByRole('button',{name:'Management',exact:true}).click();
  await navigation.getByRole('button',{name:'Members',exact:true}).click();await launcher.waitFor();
  await page.getByRole('button',{name:'Help',exact:true}).click();await launcher.waitFor();
  const helpSearch=page.getByLabel('Search help');
  for (const query of ['MCP','Shift+Enter','several automations','resize','icon']) {
    await helpSearch.fill(query);assert.ok(await page.locator('.help-articles details').count()>0,'Missing help: '+query);
  }
  await helpSearch.fill('');assert.equal(await page.locator('.help-articles details').count(),65);
  await navigation.locator('.nav-group-toggle').filter({hasText:'Automations'}).click();
  await navigation.getByRole('button',{name:'Flows',exact:true}).click();await launcher.click();
  await page.getByRole('button',{name:'Which automations are installed?',exact:true}).waitFor();
  await page.getByRole('dialog').getByRole('button',{name:'Close',exact:true}).click();
  await page.setViewportSize({width:390,height:844});const mobile=await launcher.boundingBox();assert.ok(mobile.x+mobile.width<=390&&mobile.y+mobile.height<=844);
  await launcher.click();assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  await page.screenshot({path:'/private/tmp/domus-global-assistant-mobile.png',fullPage:true});
  await page.getByRole('dialog').getByRole('button',{name:'Close',exact:true}).click();
  advertisedOrigin = 'https://untrusted.example';
  await page.goto(origin.replace('127.0.0.1', 'localhost'));
  await page.locator('.auth-page').waitFor();
  assert.equal(new URL(page.url()).hostname, 'localhost', 'Only same-port loopback aliases may redirect');
  console.log('PASS: reviewed description/checklist suggestions, duplicate filtering, unsaved/stale guards, personal subscription settings without API keys/monthly limits, persistent platform assistant, area switching, focus restoration, updated help and mobile layout.');
} finally {
  if (browser) await browser.close();
  server.kill('SIGTERM');
  await new Promise(resolve => server.once('exit', resolve));
}
