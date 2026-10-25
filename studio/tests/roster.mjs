import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
let checks=0;
const check=(ok,label)=>{assert.ok(ok,label);checks++;console.log('PASS '+label);};
const response=await fetch(base+'/api/roster');
check(response.ok,'shared common roster endpoint available');
const catalog=await response.json();
check(catalog.errors.length===0,'catalog validated by game');
check(catalog.raw.species.length===7,'six animal species plus human zombie');
check(catalog.raw.presets.length===10,'ten appearance-only zombie presets');
const source=new URL('../../game/content/data/common_roster.json',import.meta.url);
const before=await readFile(source,'utf8');
const protectedFiles=[new URL('../../game/content/data/common_roster.json',import.meta.url),...catalog.raw.species.map(s=>new URL(`../../game/content/enemies/${s.look}.tres`,import.meta.url)),
  ...['appearance.cfg','settings.cfg'].map(file=>path.join(process.env.APPDATA,'Godot/app_userdata/Horde Shooter - Stage 1',file))];
async function snapshot(){return Promise.all(protectedFiles.map(async file=>{try{return createHash('sha256').update(await readFile(file)).digest('hex');}catch(e){if(e.code==='ENOENT')return null;throw e;}}));}
const protectedBefore=await snapshot();
assert.deepEqual(catalog.raw,JSON.parse(before));checks++;
const defaults={faction:'armed',species:'eagle',palette:catalog.raw.palettes[0].id,weapon:'mp5',variant:0,gear:0,mode:'individual',rotation:0,zoom:1};
const post=body=>fetch(base+'/api/roster/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
for(const change of [{variant:10},{variant:1.5},{gear:2},{gear:3},{gear:4},{rotation:null},{mode:'invalid'},{faction:'zombie',species:'eagle'},{faction:'hunter',weapon:'mp5'},{faction:'hunter',weapon:'club',gear:1},{species:'missing'},{mode:'pack',pack:'missing'}]){
  const r=await post({...defaults,...change});check(!r.ok,'invalid combination rejected '+JSON.stringify(change));
}
const out=new URL('../output/roster/',import.meta.url);await mkdir(out,{recursive:true});
let sample;
for(const change of [{},{mode:'pack'},...['eagle','parrot','lion','tiger','reindeer','rhino'].map(species=>({species,faction:'hunter',weapon:'club'})),{faction:'zombie',species:'human',palette:'ochre',weapon:'unarmed',mode:'pack'}]){
  const r=await post({...defaults,...change});const result=await r.json();
  check(r.ok&&result.png?.length>1000,'real render '+JSON.stringify(change));
  check(result.members.every(m=>m.runtime_type==='Enemy'||m.runtime_type==='RangedEnemy'),'actual Enemy initialization');
  check(result.members.every(m=>!(m.identity.gear&2)&&m.visible_modules.every(n=>!/(Glasses|Eyepatch)/i.test(n))),'no eyewear in actual rendered members');
  check(result.members.every(m=>m.stats.hp>0&&m.stats.move_speed>4.4),'resolved baseline combat values');
  if(change.mode==='pack'){
    check(result.members.length===10,'ten coherent pack members');
    check(new Set(result.members.map(m=>m.identity.species)).size===1&&new Set(result.members.map(m=>m.identity.palette)).size===1,'pack species and palette coherent');
    check(new Set(result.members.map(m=>m.identity.variant)).size===10,'ten distinct appearance indices');
    if(change.faction!=='zombie')check(result.members.filter(m=>m.identity.weapon==='mp5').length===4&&result.members.filter(m=>m.identity.weapon==='ak47').length===6,'exact MP5 4 / AK47 6 composition');
  }
  sample??=result;
  await writeFile(new URL(`${change.faction||'armed'}-${change.species||'eagle'}-${change.mode||'individual'}.png`,out),Buffer.from(result.png,'base64'));
}
for(const variant of [1,9]){
  const r=await post({...defaults,variant});const result=await r.json();check(r.ok,'cosmetic variant render');
  assert.deepEqual(result.members[0].stats,sample.members[0].stats);checks++;
}
for(const id of ['hunter_parrot','armed_rhino','zombie_street']){
  const profile=catalog.raw.packs.find(p=>p.id===id);
  const r=await post({...defaults,mode:'pack',pack:id});const result=await r.json();
  check(r.ok&&result.members.length===10,'actual encounter profile '+id);
  for(const [i,m] of result.members.entries())assert.deepEqual([m.identity.faction,m.identity.species,m.identity.palette,m.identity.weapon],[profile.faction,profile.species,profile.palette,profile.weapons[i%profile.weapons.length]]);
  check(true,'exact shared pack identity order '+id);
  await writeFile(new URL(id+'.png',out),Buffer.from(result.png,'base64'));
}
check((await fetch(base+'/api/library')).ok,'legacy library remains available');
check((await fetch(base+'/api/statuses')).ok,'status preview catalog remains available');
for(const route of ['/','/monsters','/objects','/effects','/sounds','/weapons','/statuses','/roster']){
  const html=await(await fetch(base+route)).text();check(html.includes('href="/roster"'),'roster navigation '+route);
}
check(await readFile(source,'utf8')===before,'source unchanged by preview');
assert.deepEqual(await snapshot(),protectedBefore,'catalog, model definitions and user saves unchanged');checks++;
console.log('STUDIO_ROSTER_PASS '+checks);
