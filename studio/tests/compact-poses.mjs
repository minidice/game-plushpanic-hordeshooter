import assert from 'node:assert/strict';
import {mkdir,writeFile} from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4334';
const out=new URL('../output/compact-poses/',import.meta.url);await mkdir(out,{recursive:true});
let checks=0;
async function render(route,body,name){
 const response=await fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
 const result=await response.json();assert.ok(response.ok&&result.png?.length>1000,name);checks++;
 await writeFile(new URL(name+'.png',out),Buffer.from(result.png,'base64'));
}
await fetch(base+'/api/reload',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});
for(const species of ['raccoon','redpanda'])for(const weapon_id of ['m4','m1911','m82'])for(const rotation of [25,-90])
 await render('/api/render',{species,weapon_id,fields:{},gear:true,weapon:true,rotation,zoom:1.15},`${species}-${weapon_id}-${rotation}`);
for(const species of ['lion','tiger','reindeer','rhino'])for(const rotation of [25,-90])
 await render('/api/roster/render',{faction:'armed',species,palette:'ochre',weapon:'m4',variant:0,gear:1,mode:'individual',rotation,zoom:1.15},`${species}-m4-${rotation}`);
for(const species of ['raccoon','redpanda'])
 await render('/api/render',{species,weapon_id:'m4',fields:{wing_size:.8,body_width:1.12},gear:false,weapon:true,rotation:25,zoom:1.15},`${species}-small-unarmored`);
console.log('STUDIO_COMPACT_POSES_PASS '+checks);
