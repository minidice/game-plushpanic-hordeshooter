import assert from 'node:assert/strict';
import {mkdir,writeFile,readFile} from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
const out=new URL('../output/rhino-clean/',import.meta.url);await mkdir(out,{recursive:true});
const source=new URL('../../game/content/data/common_roster.json',import.meta.url);
const before=await readFile(source,'utf8');let checks=0;
const check=(ok,label)=>{assert.ok(ok,label);checks++;};
async function render(body,name){
  const response=await fetch(base+'/api/roster/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({faction:'hunter',species:'rhino',palette:'ochre',weapon:'club',variant:0,gear:0,mode:'individual',rotation:25,zoom:1.15,...body})});
  const result=await response.json();check(response.ok&&result.png?.length>1000,'render '+name);
  for(const member of result.members){
    check(member.identity.species==='rhino','actual identity '+name);
    check(!member.visible_modules.some(n=>/Glasses|Eyepatch/.test(n)),'no eyewear '+name);
    check(member.visible_modules.includes('RightForearm'),'authored runtime hand '+name);
  }
  await writeFile(new URL(name+'.png',out),Buffer.from(result.png,'base64'));
  await writeFile(new URL(name+'.json',out),JSON.stringify({...result,png:undefined},null,2));return result;
}
for(const weapon of ['club','knife','woodbow','mp5','ak47','m4']){
  const hunter=['club','knife','woodbow'].includes(weapon);
  const result=await render({faction:hunter?'hunter':'armed',weapon,gear:hunter?0:1},weapon);
  check(result.members[0].brows===null,'rhino uses sculpted ridge, not new brow combinations');
}
for(const palette of ['red','green','slate'])await render({faction:'armed',palette,weapon:'m4',gear:1},palette);
for(const [name,rotation,zoom] of [['front',0,1.15],['side',90,1.15],['distant',25,.75]])await render({rotation,zoom},name);
const pack=await render({mode:'pack',pack:'armed_rhino'},'pack');
check(pack.members.length===10&&new Set(pack.members.map(m=>m.identity.palette)).size===1,'ten coherent actual pack members');
check(await readFile(source,'utf8')===before,'render leaves source unchanged');
console.log('STUDIO_RHINO_CLEAN_PASS '+checks);
