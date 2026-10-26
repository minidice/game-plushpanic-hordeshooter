// Renders the same runtime character as the game, including style/size/color reset.
import assert from 'node:assert/strict';
import {mkdir, writeFile, readFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import path from 'node:path';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4323';
const out=new URL('../output/mammal-tails/',import.meta.url);
await mkdir(out,{recursive:true});
const saved=path.join(process.env.APPDATA,'Godot/app_userdata/Horde Shooter - Stage 1/appearance.cfg');
const protectedFiles=[saved,new URL('../../game/content/data/appearance_presets.json',import.meta.url)];
async function hashes(){return Promise.all(protectedFiles.map(async p=>{
  try{return createHash('sha256').update(await readFile(p)).digest('hex');}
  catch(e){if(e.code==='ENOENT')return null;throw e;}
}));}
const before=await hashes();let checks=0;
for(const species of ['raccoon','redpanda']){
  async function render(name,fields={}){
    const response=await fetch(base+'/api/render',{method:'POST',headers:{'Content-Type':'application/json'},
      body:JSON.stringify({species,fields,rotation:140,zoom:1.05,gear:false,weapon:false})});
    const result=await response.json();assert.ok(response.ok&&result.png?.length>1000,name);checks++;
    const png=Buffer.from(result.png,'base64');assert.equal(png.subarray(1,4).toString(),'PNG');checks++;
    await writeFile(new URL(species+'-'+name+'.png',out),png);
  }
  await render('default');
  for(let style=0;style<5;style++)
    for(const size of [.65,1.3])await render(`style${style}-size${size}`,{tail_style:style,tail_size:size});
  await render('recolored',{body:'#c78438',body_secondary:'#3d8796',body_marking:'#dad5b8',tail:'#6d9fbe'});
  await render('reset');
}
assert.deepEqual(await hashes(),before);checks++;
console.log('STUDIO_MAMMAL_TAILS_PASS',checks,'26 PNGs; run compare-render.py on each default/reset pair');
