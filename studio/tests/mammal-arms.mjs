// Uses Studio's actual Godot renderer. No new appearance keys or separate browser meshes.
import assert from 'node:assert/strict';
import {mkdir,writeFile} from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4334';
const out=new URL('../output/mammal-arms/',import.meta.url);await mkdir(out,{recursive:true});
let checks=0;
async function render(route,body,name){
  const response=await fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
  const result=await response.json();assert.ok(response.ok&&result.png?.length>1000,name);checks++;
  if(result.members)for(const member of result.members){
    assert.ok(member.visible_modules.includes('UpperArm')&&member.visible_modules.includes('Forearm'),name+' renders both sections');checks++;
    assert.equal(member.identity.weapon,body.weapon);checks++;
  }
  await writeFile(new URL(name+'.png',out),Buffer.from(result.png,'base64'));
  await writeFile(new URL(name+'.json',out),JSON.stringify({...result,png:undefined},null,2));
}
for(const species of ['tiger','lion','reindeer','rhino'])for(const weapon of ['m4','mp5','ak47'])
  await render('/api/roster/render',{faction:'armed',species,palette:'ochre',weapon,variant:0,gear:1,mode:'individual',rotation:25,zoom:1.15},species+'-'+weapon);
for(const rotation of [0,90,180])await render('/api/roster/render',{faction:'armed',species:'tiger',palette:'ochre',weapon:'m4',variant:0,gear:1,mode:'individual',rotation,zoom:1.15},'tiger-angle-'+rotation);
for(const species of ['redpanda','raccoon'])for(const [name,fields] of Object.entries({default:{},small:{wing_size:.8,body_width:.88,body_height:1.1},large:{wing_size:1.25,body_width:1.12,body_height:.9}}))
  await render('/api/render',{species,fields,rotation:25,zoom:1.15,gear:true,weapon:true},species+'-'+name);
console.log('STUDIO_MAMMAL_ARMS_PASS '+checks);
