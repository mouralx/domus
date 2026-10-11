import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
const root=new URL('../',import.meta.url).pathname;
const reserve=createServer();await new Promise(r=>reserve.listen(0,'127.0.0.1',r));const port=reserve.address().port;await new Promise(r=>reserve.close(r));
const server=spawn('node',['node_modules/vite/bin/vite.js','--host','127.0.0.1','--port',String(port)],{cwd:root+'src/portal',env:{...process.env,VITE_DATA_MODE:'api'},stdio:'pipe'});let browser;
try {
 const origin=`http://127.0.0.1:${port}`;for(let i=0;i<100;i++){try{if((await fetch(origin)).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
 browser=await chromium.launch({channel:'chrome',headless:true});const page=await browser.newPage({viewport:{width:600,height:900}});
 const file={version:1,nonce:'synthetic-nonce',ciphertext:'encrypted-synthetic-connection',tag:'synthetic-tag'};const code='x'.repeat(43);let exported=false,imported=false;
 await page.route('**/api/**',route=>route.fulfill({json:{user:null,providers:{},canManage:false}}));
 await page.route('**/ai-api/local-origin',route=>route.fulfill({json:{origin}}));
 await page.route('**/ai-api/api/connection/export',route=>{assert.equal(route.request().postDataJSON().destination,'https://domus.example.test');exported=true;return route.fulfill({json:{file,code,email:'synthetic@example.test'}});});
 await page.route('**/ai-api/api/connection/import',route=>{assert.deepEqual(route.request().postDataJSON(),{file,code});imported=true;return route.fulfill({json:{connected:true}});});
 await page.goto(origin);
 await page.evaluate(async()=>{
  const {default:React}=await import('/node_modules/.vite/deps/react.js');const {default:ReactDOM}=await import('/node_modules/.vite/deps/react-dom_client.js');const {ConnectionTransfer}=await import('/src/features/ai/ConnectionTransfer.tsx');await import('/src/features/ai/ai.css');
  const node=document.createElement('div');node.className='ai-settings';document.body.replaceChildren(node);const root=ReactDOM.createRoot(node);
  window.mountTransfer=(connected)=>root.render(React.createElement(ConnectionTransfer,{key:connected?'source':'target',workspace:'workspace',connected,configured:connected,onChanged:async()=>{window.changed=true;}}));window.mountTransfer(true);
 });
 await page.getByText('Move connection to another Domus',{exact:true}).click();
 await page.getByLabel('Destination Domus URL').fill('https://domus.example.test');
 const download=page.waitForEvent('download');await page.getByRole('button',{name:'Export and disconnect here'}).click();assert.equal((await download).suggestedFilename(),'domus-chatgpt-connection.json');
 await page.getByRole('button',{name:'Copy code'}).waitFor();assert.equal(exported,true);assert.equal(await page.getByLabel('Transfer code').getAttribute('type'),'password');assert.equal(await page.getByLabel('Transfer code').inputValue(),code);
 await page.evaluate(()=>window.mountTransfer(false));
 await page.getByLabel('Protected connection file').setInputFiles({name:'connection.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(file))});
 await page.getByLabel('Transfer code').fill(code);await page.getByRole('button',{name:'Import ChatGPT connection'}).click();await page.getByText('ChatGPT connection imported.',{exact:true}).waitFor();assert.equal(imported,true);assert.equal(await page.getByLabel('Transfer code').inputValue(),'');
 await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
 await page.getByLabel('Protected connection file').setInputFiles({name:'invalid.json',mimeType:'application/json',buffer:Buffer.from('{}')});await page.getByRole('alert').waitFor();assert.equal(await page.getByRole('button',{name:'Import ChatGPT connection'}).isDisabled(),true);
 console.log('PASS: reviewed export/disconnect UI, encrypted download, protected transfer code, import payload, cleared code, invalid file handling and mobile layout.');
}finally{if(browser)await browser.close();server.kill('SIGTERM');await new Promise(r=>server.once('exit',r));}
