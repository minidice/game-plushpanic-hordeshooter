import assert from 'node:assert/strict';
import {mkdir,writeFile,readFile} from 'node:fs/promises';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
const out=new URL('../output/reindeer-zombie/',import.meta.url);await mkdir(out,{recursive:true});
const source=new URL('../../game/content/data/common_roster.json',import.meta.url);
const before=await readFile(source,'utf8');let checks=0;
const check=(ok,label)=>{assert.ok(ok,label);checks++;};
async function render(body,name){
  const response=await fetch(base+'/api/roster/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({variant:0,gear:0,mode:'individual',rotation:25,zoom:1.15,...body})});
  const result=await response.json();check(response.ok&&result.png?.length>1000,'render '+name);
  check(result.members[0].identity.species===body.species,'actual identity '+name);
  check(!result.members[0].visible_modules.some(n=>/Glasses|Eyepatch/.test(n)),'no eyewear '+name);
  await writeFile(new URL(name+'.png',out),Buffer.from(result.png,'base64'));
  await writeFile(new URL(name+'.json',out),JSON.stringify({...result,png:undefined},null,2));return result;
}
for(const weapon of ['club','knife','woodbow','mp5','ak47','m4']){
  const hunter=['club','knife','woodbow'].includes(weapon);
  const result=await render({faction:hunter?'hunter':'armed',species:'reindeer',palette:'ochre',weapon,variant:3,gear:hunter?0:1},'reindeer-'+weapon);
  check(result.members[0].brows?.attachment.id==='reindeer','fitted reindeer brows');
  check(result.members[0].visible_modules.includes('RightForearm'),'actual runtime weapon hand');
}
for(const palette of ['red','green','slate'])await render({faction:'armed',species:'reindeer',palette,weapon:'m4',variant:9,gear:1},'reindeer-'+palette);
for(const palette of ['green','ochre'])for(let variant=0;variant<10;variant++){
  const result=await render({faction:'zombie',species:'human',palette,weapon:'unarmed',variant},`zombie-${palette}-${variant}`);
  check(result.members[0].brows===null,'human uses authored face, not animal brows');
  check(result.members[0].weapon_meshes===0,'ordinary infected remains unarmed');
}
check(await readFile(source,'utf8')===before,'render leaves source unchanged');
console.log('STUDIO_REINDEER_ZOMBIE_PASS '+checks);
