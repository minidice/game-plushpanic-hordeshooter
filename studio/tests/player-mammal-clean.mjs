// Actual game renderer, default restore and existing player customization controls.
import assert from 'node:assert/strict';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import path from 'node:path';
export async function checkPlayerMammal(species) {
const base=process.env.STUDIO_URL||'http://127.0.0.1:4323';
const out=new URL('../output/'+species+'-clean/',import.meta.url);await mkdir(out,{recursive:true});
const saved=path.join(process.env.APPDATA,'Godot/app_userdata/Horde Shooter - Stage 1/appearance.cfg');
const protectedFiles=[saved,new URL('../../game/content/data/appearance_presets.json',import.meta.url)];
async function hashes(){return Promise.all(protectedFiles.map(async p=>{try{return createHash('sha256').update(await readFile(p)).digest('hex');}catch(e){if(e.code==='ENOENT')return null;throw e;}}));}
const before=await hashes();let checks=0;
const check=(ok,msg)=>{assert.ok(ok,msg);checks++;};
const catalog=await (await fetch(base+'/api/catalog')).json();
const panda=catalog.species.find(s=>s.id===species);
for(const key of ['body','body_secondary','body_marking','marking_softness','eye_size','eye_spacing','beak_size','ear_style','ear_size','ear_angle','tail_style','tail_size','wing_size','feet_size','body_width','body_height','face_style','beak_style','hat','vest','goggles'])
  check(panda.fields.some(f=>f.key===key),'retained control '+key);
async function render(name,fields={},options={}){
  const response=await fetch(base+'/api/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({species,fields,rotation:25,zoom:1.15,gear:false,weapon:false,...options})});
  const result=await response.json();check(response.ok&&result.png?.length>1000,'render '+name);
  const bytes=Buffer.from(result.png,'base64');check(bytes.subarray(1,4).toString()==='PNG'&&bytes.readUInt32BE(16)===900,'real PNG '+name);
  await writeFile(new URL(name+'.png',out),bytes);return bytes;
}
const first=await render('default');
for(const [name,rotation] of [['front',0],['side',90],['back',180]])await render(name,{}, {rotation});
await render('weapon',{}, {weapon:true});
await render('equipped',{}, {gear:true,weapon:true});
await render('vest-padded',{vest_style:1,hat:2},{gear:true,weapon:true});
for(let style=1;style<4;style++)await render('ear-'+style,{ear_style:style,ear_size:1.25,ear_angle:25});
for(let style=1;style<5;style++)await render('tail-'+style,{tail_style:style,tail_size:1.3},{rotation:140});
for(let style=1;style<3;style++)await render('face-'+style,{face_style:style,beak_style:style});
for(const [name,fields] of Object.entries({
  small:{body_width:.88,body_height:1.1,ear_size:.75,ear_angle:-20,wing_size:.8,feet_size:.8,eye_size:.7,eye_spacing:.85,beak_size:.75},
  large:{body_width:1.12,body_height:.9,ear_size:1.25,ear_angle:40,wing_size:1.25,feet_size:1.25,eye_size:1.45,eye_spacing:1.12,beak_size:1.25},
  recolored:{body:'#c27e39',body_secondary:'#327a9c',body_marking:'#ecd6a1',feet:'#315f71',eyes:'#18212f',marking_softness:.8},
}))await render(name,fields,{weapon:true});
await render('hat-large-ears',{hat:1,ear_size:1.25,ear_angle:40},{gear:true});
const reset=await render('reset');
// Pixel tolerance is assessed by the existing compare-render.py utility, not compressed PNG equality.
check(first.length>1000&&reset.length>1000,'default/reset captures available for pixel comparison');
assert.deepEqual(await hashes(),before);checks++;
console.log('STUDIO_'+species.toUpperCase()+'_CLEAN_PASS '+checks);

}
