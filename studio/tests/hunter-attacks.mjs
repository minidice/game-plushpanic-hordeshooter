import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
const base=process.env.STUDIO_URL||'http://127.0.0.1:4317';
let checks=0;
const check=(ok,label)=>{assert.ok(ok,label);checks++;console.log('PASS '+label);};
const post=body=>fetch(base+'/api/roster/render',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
const defaults={faction:'hunter',species:'eagle',palette:'red',weapon:'club',variant:0,gear:0,mode:'attack',rotation:25,zoom:1};
const source=new URL('../../game/content/data/common_roster.json',import.meta.url);
const hash=async()=>createHash('sha256').update(await readFile(source)).digest('hex');
const before=await hash();
const out=new URL('../output/hunter-attacks/',import.meta.url);await mkdir(out,{recursive:true});
for(const weapon of ['club','knife','woodbow']){
  const response=await post({...defaults,weapon});const clip=await response.json();
  check(response.ok&&clip.frames?.length===12,'twelve runtime attack frames '+weapon);
  check(clip.attack.source==='res://scripts/Characters/CharacterHunterPose.cs','pose implementation source '+weapon);
  check(clip.mode==='attack'&&clip.members.length===1,'individual attack preview '+weapon);
  check(clip.attack.weapon===weapon&&clip.attack.windup>0&&clip.attack.recovery>0,'runtime timeline '+weapon);
  check(Math.abs(clip.duration-clip.attack.windup-clip.attack.recovery)<1e-5,'full recovery duration '+weapon);
  check(clip.frames[0].time===0&&Math.abs(clip.frames.at(-1).time-clip.duration)<1e-5,'start and end sampled '+weapon);
  check(clip.frames.some(f=>f.time===clip.attack.windup&&f.phase===(weapon==='woodbow'?'release':'impact')),'exact impact boundary '+weapon);
  check(clip.frames.at(-1).phase==='idle','idle reset after recovery '+weapon);
  check(clip.frames.every((f,i)=>f.png.length>1000&&(i===0||f.time>clip.frames[i-1].time)),'ordered rendered frames '+weapon);
  check(new Set(clip.frames.map(f=>f.png)).size>=5,'visible pose variation '+weapon);
  check(clip.attack.camera==='fixed_union_bounds'&&clip.attack.combat_simulated===false,'stable framing and explicit preview scope '+weapon);
  check(clip.attack.sounds.length===3,'three explicit sound cues '+weapon);
  for(const sound of clip.attack.sounds){
    const r=await fetch(base+'/api/sounds/preview',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({id:sound.id})});const result=await r.json();
    check(r.ok&&result.id===sound.id&&result.audio?.length>100&&result.duration>0,'actual sound preview '+sound.id);
  }
  for(const [i,frame] of clip.frames.entries())await writeFile(new URL(`${weapon}-${String(i).padStart(2,'0')}.png`,out),Buffer.from(frame.png,'base64'));
  await writeFile(new URL(`${weapon}.json`,out),JSON.stringify({...clip,png:undefined,frames:clip.frames.map(({png,...frame})=>frame)},null,2));
}
for(const change of [{faction:'armed',weapon:'knife'},{faction:'zombie',species:'human',weapon:'unarmed'},{weapon:'mp5'}]){
  check(!(await post({...defaults,...change})).ok,'unsupported attack rejected '+JSON.stringify(change));
}
check((await post({...defaults,mode:'individual'})).ok,'static renderer restored after clips');
check(await hash()===before,'source unchanged');
console.log('STUDIO_HUNTER_ATTACKS_PASS '+checks);
