import assert from 'node:assert/strict';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
const out=new URL('../output/common-brows/',import.meta.url);
await mkdir(out,{recursive:true});
let checks=0;
const check=(ok,label)=>{assert.ok(ok,label);checks++;};
const source=new URL('../../game/content/data/common_brows.json',import.meta.url);
const before=await readFile(source,'utf8');
const catalog=await (await fetch(base+'/api/roster')).json();
check(catalog.errors.length===0,'catalog valid');
assert.deepEqual(catalog.brows,JSON.parse(before));checks++;
check(catalog.brow_source==='res://content/data/common_brows.json','source exposed');
check(catalog.brows.presets.length===10,'ten variants');
const defaults={faction:'hunter',palette:'ochre',weapon:'club',variant:0,gear:0,mode:'individual',rotation:0,zoom:1.25};
async function render(change,file){
  const response=await fetch(base+'/api/roster/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({...defaults,...change})});
  const result=await response.json();
  check(response.ok&&result.png?.length>1000,'actual render '+file);
  await writeFile(new URL(file+'.png',out),Buffer.from(result.png,'base64'));
  await writeFile(new URL(file+'.json',out),JSON.stringify({...result,png:undefined},null,2));
  return result;
}
for(const species of ['parrot','eagle','lion']){
  const images=new Set();
  for(let variant=0;variant<10;variant++){
    const result=await render({species,variant},`${species}-${variant}`);
    const member=result.members[0];
    assert.deepEqual(member.brows.preset,catalog.brows.presets[variant]);checks++;
    check(member.brows.attachment.id===species,'species fitted '+species);
    check(member.visible_modules.filter(n=>n.startsWith('common_brow_')).length===2,'two actual brow meshes');
    images.add(result.png);
  }
  check(images.size===10,'ten visually distinct variants '+species);
  for(const palette of ['green','red','slate'])await render({species,palette,variant:6,rotation:35},`${species}-${palette}`);
  const pack=await render({species,mode:'pack',zoom:1},`${species}-pack`);
  check(new Set(pack.members.map(m=>m.brows.preset.id)).size===10,'pack has all ten brows '+species);
  const repeated=await render({species,variant:6},`${species}-repeat`);
  assert.deepEqual(repeated.members[0].brows.preset,catalog.brows.presets[6]);checks++;
}
const unsupported=await render({species:'tiger'},'tiger-unchanged');
check(unsupported.members[0].brows===null,'other species unsupported');
check((await readFile(source,'utf8'))===before,'render never writes source');
console.log('STUDIO_COMMON_BROWS_PASS '+checks);
