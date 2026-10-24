// Windows launcher integration: real PowerShell processes/HTTP/mutex, fake costly renderer.
import assert from 'node:assert/strict';
import http from 'node:http';
import {spawn, execFileSync} from 'node:child_process';
import {mkdtemp, writeFile, readFile, rm} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const temp=await mkdtemp(path.join(os.tmpdir(),'studio-launch-'));
const processes=[];
const quote=s=>"'"+s.replaceAll("'","''")+"'";
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const text=async name=>readFile(path.join(temp,name),'utf8').catch(()=> '');
let checks=0;
const check=(value,message)=>{assert.ok(value,message);checks++;console.log('PASS '+message);};
async function until(predicate,message) {
 const deadline=Date.now()+12000;
 while(Date.now()<deadline){if(await predicate())return;await delay(100);}
 throw new Error(message);
}
const fixture=path.join(temp,'server.mjs');
await writeFile(fixture,`import http from 'node:http';
import {appendFileSync} from 'node:fs';
appendFileSync(process.env.LAUNCH_LOG,'start\\n');
if(process.env.LAUNCH_FAIL==='1')process.exit(7);
const port=Number(process.argv[process.argv.indexOf('--port')+1]);
setTimeout(()=>http.createServer((req,res)=>{res.setHeader('Content-Type','application/json');res.end(JSON.stringify({app:'plush-panic-studio',workspace:process.env.LAUNCH_ROOT,ready:true}));}).listen(port,'127.0.0.1',()=>console.log('Studio ready: http://127.0.0.1:'+port)),1800);
`);
await writeFile(path.join(temp,'Godot_v4.7.2-stable_mono_win64_console.exe'),'fixture');
const wrapper=path.join(temp,'wrapper.ps1');
await writeFile(wrapper,`param([int]$Port)
$ErrorActionPreference='Stop'
function node { & ${quote(process.execPath)} ${quote(fixture)} @args }
function Start-Process { param([string]$FilePath) Add-Content -LiteralPath $env:OPEN_LOG -Value $FilePath }
& ${quote(path.join(root,'studio/Start-Studio.ps1'))} -Port $Port -GodotDir ${quote(temp)}
`);
function launch(port,{fail=false,open='open.log'}={}) {
 const proc=spawn('powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',wrapper,'-Port',String(port)],{
  windowsHide:true,env:{...process.env,LAUNCH_LOG:path.join(temp,'starts.log'),OPEN_LOG:path.join(temp,open),LAUNCH_ROOT:root,LAUNCH_FAIL:fail?'1':'0'}});
 proc.output='';proc.stdout.on('data',d=>proc.output+=d);proc.stderr.on('data',d=>proc.output+=d);
 processes.push(proc);return proc;
}
function stop(proc){if(proc.exitCode===null){try{execFileSync('taskkill',['/PID',String(proc.pid),'/T','/F'],{stdio:'ignore'});}catch{}}}
async function freePort(){const server=http.createServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port;await new Promise(r=>server.close(r));return port;}
try {
 const port=await freePort();
 const first=launch(port);
 await until(async()=> (await text('starts.log')).includes('start'),'first launcher never started server');
 check(!(await text('open.log')),'browser stays closed before ready');
 const second=launch(port);
 await until(async()=> (await text('open.log')).trim().split(/\r?\n/).length===2,'both launchers should open only after readiness');
 await until(()=>second.exitCode!==null,'duplicate launcher should exit');
 check(second.exitCode===0,'concurrent launch reuses server successfully');
 check((await text('starts.log')).trim()==='start','only one server started during simultaneous launches');
 check((await text('open.log')).trim().split(/\r?\n/).every(s=>s===`http://127.0.0.1:${port}`),'browser opens selected port');
 const third=launch(port,{open:'reuse.log'});
 await until(()=>third.exitCode!==null,'ready server reuse should exit');
 check(third.exitCode===0&&(await text('reuse.log')).includes(String(port)),'ready server opens browser and exits');
 check((await text('starts.log')).trim()==='start','ready reuse does not rebuild');
 stop(first);
 const failed=launch(port,{fail:true,open:'failed.log'});
 await until(()=>failed.exitCode!==null,'startup failure should exit');
 check(failed.exitCode!==0&&!(await text('failed.log')),'startup failure does not open browser');
 let foreignState={app:'different-app',ready:true};
 const foreign=http.createServer((req,res)=>{res.setHeader('Content-Type','application/json');res.end(JSON.stringify(foreignState));});
 await new Promise(r=>foreign.listen(port,'127.0.0.1',r));
 try {
  const collision=launch(port,{open:'foreign.log'});
  await until(()=>collision.exitCode!==null,'foreign server should be rejected');
  check(collision.exitCode!==0&&!(await text('foreign.log')),'unrelated server is not opened');
  check((await text('starts.log')).trim().split(/\r?\n/).length===2,'occupied port does not start another server');
  foreignState={app:'plush-panic-studio',workspace:temp,ready:true};
  const otherWorkspace=launch(port,{open:'other-workspace.log'});
  await until(()=>otherWorkspace.exitCode!==null,'other workspace should be rejected');
  check(otherWorkspace.exitCode!==0&&!(await text('other-workspace.log')),'another Studio workspace is not reused');
 } finally {await new Promise(r=>foreign.close(r));}
 console.log('STUDIO_LAUNCHER_PASS '+checks);
} finally {
 processes.forEach(stop);
 await rm(temp,{recursive:true,force:true});
}
