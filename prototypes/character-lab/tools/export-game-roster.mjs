// Export authoring snapshots. Run optimize_game_roster.py in Blender next.
import fs from 'node:fs/promises';
import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { createCharacter } from '../src/model.mjs';
import { oval, line } from '../src/sculpt.mjs';
import { createInfected, infectedKinds } from './infected-models.mjs';

globalThis.FileReader = class {
  readAsArrayBuffer(blob) { blob.arrayBuffer().then(b => { this.result=b; this.onloadend?.(); }); }
  readAsDataURL(blob) { blob.arrayBuffer().then(b => { this.result=`data:${blob.type};base64,${Buffer.from(b).toString('base64')}`; this.onloadend?.(); }); }
};
const out = new URL('../output/roster/', import.meta.url);
await fs.mkdir(out, {recursive:true});
async function save(kind, root) {
  root.userData.asset={forward:'+Z',units:'metres',version:2,animation:'runtime object pivots'};
  const scene=new THREE.Scene(); scene.add(root);
  const binary=await new GLTFExporter().parseAsync(scene,{binary:true,onlyVisible:true});
  await fs.writeFile(new URL(`${kind}-source.glb`,out),Buffer.from(binary));
  console.log(`${kind}: ${binary.byteLength} bytes`);
}
for (const kind of (process.argv.includes('--enemies-only') ? [] : ['goose','duck','raccoon','redpanda'])) {
  const c=createCharacter(kind), bird=['goose','duck'].includes(kind), w=c.parts.weapon;
  // Export optional pieces; runtime and authoring previews control their visibility.
  c.root.traverse(o=>{if(o.userData.hiddenByDefault)o.visible=true;});
  // Keep the original upper arms independently hideable without changing their pose.
  for(const [side,key] of [[-1,'leftArm'],[1,'rightArm']]){
    const arm=c.root.getObjectByName(`${kind}_upper_arm_${side}`);
    const pivot=new THREE.Group();pivot.name=`${kind}_${key}`;pivot.position.copy(arm.position);c.parts.torso.add(pivot);
    pivot.attach(arm);
  }
  // Facial controls scale around their own centers, with glints following the eye.
  const head=c.parts.head;
  const facePivot=(key,objects,center)=>{
    const pivot=new THREE.Group();pivot.name=`${kind}_${key}`;pivot.position.copy(center);head.add(pivot);
    for(const object of objects)pivot.attach(object);
    return pivot;
  };
  for(const [side,label] of [[-1,'left'],[1,'right']]){
    const eye=head.getObjectByName(`${kind}_eye_${side}`),glint=head.getObjectByName(`${kind}_eye_glint_${side}`);
    facePivot(`${label}Eye`,[eye,glint],eye.position);
    const brow=head.getObjectByName(`${kind}_brow_${side}`);
    brow.geometry.computeBoundingBox();
    const center=brow.geometry.boundingBox.getCenter(new THREE.Vector3()).applyMatrix4(brow.matrix);
    facePivot(`${label}Brow`,[brow],center);
  }
  if(!bird){
    const snout=['muzzle','nose','smile'].map(part=>head.getObjectByName(`${kind}_${part}`));
    facePivot('snout',snout,snout[0].position);
  }
  // Both optional tufts share a crown anchor, rooted slightly inside the bean.
  const hairColor={goose:'#e5e1cf',duck:'#bb7934',raccoon:'#78858a',redpanda:'#aa4b2c'}[kind];
  for(const style of ['crestShort','crestSwept']){
    const crest=new THREE.Group();crest.name=`${kind}_${style}`;crest.position.set(0,.455,-.02);head.add(crest);
    crest.userData.hiddenByDefault=true;
    if(style==='crestShort'){
      oval(crest,`${kind}_${style}_base`,[0,.008,0],[.15,.065,.115],hairColor,'fur');
      for(const [i,x,y,z] of [[0,-.07,.055,.015],[1,0,.095,-.01],[2,.07,.065,-.025]]){
        const tuft=oval(crest,`${kind}_${style}_${i}`,[x,y,z],[.065,.105,.07],hairColor,'fur');tuft.rotation.z=-x*3;
      }
    }else{
      oval(crest,`${kind}_${style}_base`,[0,.015,-.015],[.16,.075,.14],hairColor,'fur');
      for(const [i,x,y,z] of [[0,-.085,.055,-.045],[1,0,.09,-.07],[2,.085,.06,-.035]]){
        const tuft=oval(crest,`${kind}_${style}_${i}`,[x,y,z],[.072,.085,.19],hairColor,'fur');tuft.rotation.x=.45;tuft.rotation.y=-.25;
      }
    }
  }
  // Gun meshes, hands and grip forearms merge under the existing weapon pivot.
  // Only the independent upper arms remain when the game replaces this gun.
  for(const side of [-1,1]) w.remove(c.root.getObjectByName(`${kind}_forearm_${side}`));
  w.rotation.y=0; w.position.x=0; c.root.updateMatrixWorld(true);
  for(const [hand,side] of [[c.parts.leftHand,-1],[c.parts.rightHand,1]]) {
    const elbow=c.parts.torso.localToWorld(new THREE.Vector3(side*.43,.78,.29)); w.worldToLocal(elbow);
    const grip=hand.position.clone(), mid=elbow.clone().lerp(grip,.5); mid.y-=.025;
    line(w,`${kind}_forearm_${side}`,[elbow.toArray(),mid.toArray(),grip.toArray()],.074,bird?(kind==='goose'?'#e5e1cf':'#bb7934'):'#30383b','fur');
  }
  const muzzle=new THREE.Group(); muzzle.name='Muzzle'; muzzle.position.set(0,.035,.81); w.add(muzzle);
  const beret=new THREE.Group(); beret.name=`${kind}_beret`; beret.position.set(0,.37,-.025); c.parts.head.add(beret);
  oval(beret,`${kind}_beret_crown`,[.065,.055,0],[.48,.15,.40],'#46534e');
  oval(beret,`${kind}_beret_band`,[0,-.025,0],[.395,.055,.34],'#283631');
  oval(beret,`${kind}_beret_badge`,[-.18,.045,.36],[.055,.052,.013],'#b89f68');
  beret.userData.hiddenByDefault=true;
  const cloned=new Map();
  c.root.traverse(o=>{
    if(!o.isMesh) return;
    const ancestors=[]; for(let p=o;p;p=p.parent) ancestors.push(p.name);
    let channel=null;
    if(ancestors.includes(`${kind}_helmet`)||ancestors.includes(`${kind}_beret`)) channel='hat';
    else if(ancestors.includes(`${kind}_crestShort`)||ancestors.includes(`${kind}_crestSwept`)) channel='hair';
    else if(ancestors.includes(`${kind}_vest`)||ancestors.includes(`${kind}_vestPadded`)) channel='vest';
    else if(ancestors.includes(`${kind}_pack`)) channel='pack';
    else if(ancestors.some(n=>n===`${kind}_leftArm`||n===`${kind}_rightArm`)) channel='wing';
    else if(ancestors.includes(`${kind}_tail`)) channel='tail';
    else if(ancestors.some(n=>n.endsWith('Foot'))) channel='feet';
    else if(o.name===`${kind}_eye_-1`||o.name===`${kind}_eye_1`) channel='eyes';
    else if(ancestors.includes(`${kind}_beak`)) channel='beak';
    else if(o===c.skin||(!bird&&o.name.includes('_ear_')&&!o.name.endsWith('_inner'))) channel='body';
    else if(!bird&&(o.name===`${kind}_muzzle`||o.name.endsWith('_inner'))) channel='beak';
    const convert=m=>{
      const key=`${channel??'detail'}_${m.uuid}`;
      if(!cloned.has(key)) { const copy=m.clone(); copy.name=channel?`custom_${channel}_${cloned.size}`:`detail_${cloned.size}`; cloned.set(key,copy); }
      return cloned.get(key);
    };
    o.material=Array.isArray(o.material)?o.material.map(convert):convert(o.material);
  });
  await save(kind,c.root);
}

for (const kind of (process.argv.includes('--players-only') ? [] : infectedKinds)) await save(kind, createInfected(kind));
