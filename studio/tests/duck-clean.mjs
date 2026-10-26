// Render the actual duck model through private Studio and guard saved appearance.
import assert from 'node:assert/strict';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import path from 'node:path';

const base=process.env.STUDIO_URL||'http://127.0.0.1:4332';
const out=new URL('../output/duck-clean/',import.meta.url);await mkdir(out,{recursive:true});
const protectedFiles=[path.join(process.env.APPDATA,'Godot/app_userdata/Horde Shooter Art Review Duck/appearance.cfg'),
  new URL('../../game/content/data/appearance_presets.json',import.meta.url)];
async function hashes(){return Promise.all(protectedFiles.map(async p=>{try{return createHash('sha256').update(await readFile(p)).digest('hex');}catch(e){if(e.code==='ENOENT')return null;throw e;}}));}
const before=await hashes();let checks=0;
const check=(ok,msg)=>{assert.ok(ok,msg);checks++;};
const catalog=await (await fetch(base+'/api/catalog')).json();
const duck=catalog.species.find(s=>s.id==='duck');check(!!duck,'duck catalog entry');
for(const key of ['body','body_secondary','beak','eyes','hair','feet','eye_size','eye_spacing','beak_size','tail_style','tail_size','wing_size','feet_size','body_width','body_height','face_style','beak_style','hat','vest','goggles'])
  check(duck.fields.some(f=>f.key===key),'retained duck control '+key);
async function render(name,fields={},options={}){
  const response=await fetch(base+'/api/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({species:'duck',fields,rotation:25,zoom:1.15,gear:false,weapon:false,...options})});
  const result=await response.json();check(response.ok&&result.png?.length>1000,'render '+name);
  const bytes=Buffer.from(result.png,'base64');check(bytes.subarray(1,4).toString()==='PNG'&&bytes.readUInt32BE(16)===900,'real PNG '+name);
  await writeFile(new URL(name+'.png',out),bytes);return bytes;
}
const first=await render('default');
for(const [name,rotation] of [['front',0],['side',90],['back',180]])await render(name,{}, {rotation});
await render('weapon',{}, {weapon:true});
await render('equipped',{}, {gear:true,weapon:true});
await render('vest-padded',{vest_style:1,hat:2},{gear:true,weapon:true});
for(const style of [1,2])await render('bill-'+style,{beak_style:style,beak_size:style===1?.75:1.25,face_style:style},{rotation:0});
for(const style of [1,2,3,4])await render('tail-'+style,{tail_style:style,tail_size:1.3},{rotation:140});
for(const [name,fields] of Object.entries({
  small:{body_width:.88,body_height:1.1,wing_size:.8,feet_size:.8,eye_size:.7,eye_spacing:.85,beak_size:.75},
  large:{body_width:1.12,body_height:.9,wing_size:1.25,feet_size:1.25,eye_size:1.45,eye_spacing:1.12,beak_size:1.25},
  recolored:{body:'#c27e39',body_secondary:'#327a9c',beak:'#eed26a',feet:'#315f71',eyes:'#18212f'},
}))await render(name,fields,{weapon:true});
const reset=await render('reset');
check(first.length>1000&&reset.length>1000,'default/reset captures available');
assert.deepEqual(await hashes(),before);checks++;
console.log('STUDIO_DUCK_CLEAN_PASS '+checks);
