import assert from 'node:assert/strict';
import {readFile, writeFile, mkdir} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import {createHash} from 'node:crypto';
const root=fileURLToPath(new URL('../../',import.meta.url));
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
let checks=0;
const check=(ok,message)=>{assert.ok(ok,message);checks++;console.log('PASS '+message);};
const originals=['game/scripts/Audio/SoundRecipes.cs','game/scripts/Audio/Sfx.cs','game/content/data/object_interactions.json','game/scripts/World/Effects.cs'];
const protectedPaths=[...originals.map(f=>path.join(root,f)),...['settings.cfg','appearance.cfg'].map(f=>path.join(process.env.APPDATA,'Godot/app_userdata/Horde Shooter - Stage 1',f))];
const hashes=async()=>Promise.all(protectedPaths.map(async f=>{
 try {return createHash('sha256').update(await readFile(f)).digest('hex');}
 catch(error){if(error.code==='ENOENT')return null;throw error;}
}));
const before=await hashes();
const response=await fetch(base+'/api/media');
check(response.status===200,'media catalog available');
const catalog=await response.json();
check(catalog.effects.length>=8&&catalog.sounds.length>=30,'actual effect and sound catalogs populated');
for(const list of [catalog.effects,catalog.sounds]) {
 check(new Set(list.map(e=>e.id)).size===list.length,'unique catalog IDs');
 check(list.every(e=>e.name&&e.source&&e.properties.length>0),'entries expose source and properties');
}
for(const id of ['shot_rifle','explosion','reload_start']) check(catalog.sounds.some(e=>e.id===id),id+' registered');
const recipeSource=await readFile(path.join(root,'game/scripts/Audio/SoundRecipes.cs'),'utf8');
const recipeIds=[...recipeSource.matchAll(/new\("([a-z0-9_]+)",\s*"[^"\r\n]+"/g)].map(m=>m[1]);
assert.deepEqual(catalog.sounds.map(e=>e.id).sort(),recipeIds.sort());checks++;
await mkdir(path.join(root,'studio/output'),{recursive:true});
const post=async(url,data)=>{const r=await fetch(base+url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});return [r,await r.json()];};
for(const entry of catalog.sounds) {
 const [r,a]=await post('/api/sounds/preview',{id:entry.id});
 check(r.ok&&a.id===entry.id&&a.duration>0&&a.mime.startsWith('audio/'),entry.id+': actual audio');
 check(a.loop===entry.loop&&a.source===entry.source&&a.volumeDb===entry.volumeDb,entry.id+': metadata matches loaded game stream');
 const bytes=Buffer.from(a.audio,'base64');
 check(bytes.length>44,entry.id+': nonempty media');
 if(a.mime==='audio/wav') {
  check(bytes.toString('ascii',0,4)==='RIFF'&&bytes.toString('ascii',8,12)==='WAVE',entry.id+': WAV header');
  if(a.source.startsWith('res://scripts/Audio/SoundRecipes.cs#')) {
   const byteRate=bytes.readUInt32LE(28),payload=bytes.readUInt32LE(40);
   check(payload===bytes.length-44&&Math.abs(payload/byteRate-a.duration)<.001,entry.id+': PCM duration agrees');
   let peak=0;for(let i=44;i<bytes.length;i+=2)peak=Math.max(peak,Math.abs(bytes.readInt16LE(i)));
   check(peak>0&&Math.abs(peak/32768-a.peak)<.0001,entry.id+': audible nonzero samples and measured peak');
  }
 }
 if(entry.id==='shot_rifle') await writeFile(path.join(root,'studio/output/media-shot-rifle.wav'),bytes);
}
const targets=process.argv.includes('--all')?catalog.effects:catalog.effects.slice(0,3);
for(const entry of targets) {
 const [r,a]=await post('/api/effects/render',{id:entry.id});
 check(r.ok&&a.id===entry.id&&a.frames.length>3&&a.duration>0,entry.id+': actual animated capture');
 check(a.frames.every((f,i)=>Number.isFinite(f.time)&&f.time>=0&&(i===0||f.time>a.frames[i-1].time)),entry.id+': ordered timestamps');
 check(new Set(a.frames.map(f=>f.png)).size>2,entry.id+': animation changes visually');
 for(const [i,f] of a.frames.entries()) {
  const png=Buffer.from(f.png,'base64');
  assert.equal(png.readUInt32BE(16),a.width);assert.equal(png.readUInt32BE(20),a.height);
  if([0,Math.floor(a.frames.length/3),a.frames.length-1].includes(i)) await writeFile(path.join(root,'studio/output',`media-${entry.id.replaceAll(':','-')}-${i}.png`),png);
 }
}
for(const url of ['/api/sounds/preview','/api/effects/render']) {
 const [r]=await post(url,{id:'../../project.godot'});check(!r.ok,url+': unknown resource rejected');
 const [bad]=await post(url,{id:32});check(bad.status===400,url+': malformed ID rejected');
}
assert.deepEqual(await hashes(),before);checks++;console.log('PASS game sources unchanged');
console.log(`STUDIO_MEDIA_PASS ${checks} sounds=${catalog.sounds.length} effects=${targets.length}`);
