import assert from 'node:assert/strict';
import {mkdir,writeFile,readFile} from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4334';
const out=new URL('../output/shared-poses/',import.meta.url);await mkdir(out,{recursive:true});
const source=new URL('../../game/content/data/weapons.csv',import.meta.url),before=await readFile(source,'utf8');
let checks=0;
async function render(route,body,name){
  const response=await fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
  const result=await response.json();assert.ok(response.ok&&result.png?.length>1000,name);checks++;
  if(route==='/api/render') {
    assert.equal(result.weapon_id,body.weapon_id);checks++;
    assert.equal(result.pose_family,body.weapon_id==='m1911'?'Pistol':'Shouldered');checks++;
  }
  await writeFile(new URL(name+'.png',out),Buffer.from(result.png,'base64'));
  await writeFile(new URL(name+'.json',out),JSON.stringify({...result,png:undefined},null,2));
}
for(const species of ['goose','duck','raccoon','redpanda'])for(const weapon_id of ['m4','mp5','ak47','m1911'])
  for(const rotation of [25,-90])await render('/api/render',{species,fields:{},rotation,zoom:1.15,gear:true,weapon:true,weapon_id},`${species}-${weapon_id}-${rotation}`);
for(const species of ['parrot','eagle','lion','tiger','reindeer','rhino'])for(const weapon of ['m4','mp5','ak47'])
  for(const rotation of [25,-90])await render('/api/roster/render',{faction:'armed',species,palette:'ochre',weapon,variant:0,gear:1,mode:'individual',rotation,zoom:1.15},`${species}-${weapon}-${rotation}`);
assert.equal(await readFile(source,'utf8'),before);checks++;
console.log('STUDIO_SHARED_POSES_PASS '+checks);
