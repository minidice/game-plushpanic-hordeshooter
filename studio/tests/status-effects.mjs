import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
const source=new URL('../../game/content/data/status_visuals.json',import.meta.url);
const before=await readFile(source,'utf8');
let checks=0;
const check=(ok,label)=>{assert.ok(ok,label);checks++;console.log('PASS '+label);};
const catalog=await(await fetch(base+'/api/statuses')).json();
check(catalog.statuses.length===12,'twelve shared status profiles');
check(catalog.statuses.filter(d=>d.binding==='preview').length===5,'five preview-only states clearly identified');
for(const d of JSON.parse(before).statuses){const got=catalog.statuses.find(e=>e.id===d.id);for(const key of Object.keys(d))assert.deepEqual(got[key],d[key]);check(got.svg.includes('<svg'),'shared icon and exact source fields '+d.id);}
const post=body=>fetch(base+'/api/statuses/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
for(const states of [[{id:'burn',stacks:6}],[{id:'burn',stacks:1.5}],null])check((await post({target:'player:goose',states,hit:false,release:true})).status===400,'reject malformed status payload');
check(!(await post({target:'player:goose',states:[{id:'missing',stacks:1}],hit:false,release:true})).ok,'reject unknown status before rendering');
check(!(await post({target:'monster:missing',states:[],hit:false,release:true})).ok,'reject unknown actor before rendering');
const out=new URL('../output/statuses/',import.meta.url);await mkdir(out,{recursive:true});
const samples=process.argv.includes('--catalog-only')?[]:[
  ['burn','player:goose',[{id:'burn',stacks:1}]],
  ['stacked','player:raccoon',[{id:'poison',stacks:3},{id:'burn',stacks:2},{id:'stun',stacks:1}]],
  ['monster',catalog.targets.find(d=>d.id.startsWith('monster:')).id,[{id:'freeze',stacks:1},{id:'bleed',stacks:1}]],
  ['armed','monster:armed_animal',[{id:'burn',stacks:1},{id:'stun',stacks:1}]],
  ['clear','player:goose',[]]
];
for(const [name,target,states] of samples){
  const response=await post({target,states,hit:true,release:true});const result=await response.json();
  check(response.ok&&result.frames?.length===43,name+' complete 2.8s clip');
  if(name==='armed')check(result.subject?.runtime_type==='RangedEnemy'&&result.subject.weapon_meshes>0,'armed status sample contains actual runtime weapon and grip');
  check(new Set(result.frames.map(f=>f.png)).size>3,name+' animated hit/status sample');
  check(result.frames.at(-1).png===result.frames[33].png,name+' final cleared state stable');
  for(const frame of [0,8,12,16,29,33,42])await writeFile(new URL(name+'-'+frame+'.png',out),Buffer.from(result.frames[frame].png,'base64'));
}
const library=await(await fetch(base+'/api/library')).json();check(library.monsters.length>0,'catalog intact after status rendering');
check(await readFile(source,'utf8')===before,'preview does not modify source');
console.log('STUDIO_STATUS_PASS '+checks);
