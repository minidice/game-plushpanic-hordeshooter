import fs from 'node:fs/promises';
import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { createCharacter, poseCharacter } from '../src/model.mjs';
import { line } from '../src/sculpt.mjs';
globalThis.FileReader=class{readAsArrayBuffer(blob){blob.arrayBuffer().then(b=>{this.result=b;this.onloadend?.();});}readAsDataURL(blob){blob.arrayBuffer().then(b=>{this.result=`data:${blob.type};base64,${Buffer.from(b).toString('base64')}`;this.onloadend?.();});}};
const c=createCharacter('goose');poseCharacter(c,0,'idle',0,true);
const w=c.parts.weapon;
for(const side of [-1,1])w.remove(c.root.getObjectByName(`goose_forearm_${side}`));
w.rotation.y=0;w.position.x=0;c.root.updateMatrixWorld(true);
for(const [hand,side] of [[c.parts.leftHand,-1],[c.parts.rightHand,1]]){
  const elbow=c.parts.torso.localToWorld(new THREE.Vector3(side*.43,.78,.29));w.worldToLocal(elbow);
  const grip=hand.position.clone(),mid=elbow.clone().lerp(grip,.5);mid.y-=.025;
  line(w,`goose_forearm_${side}`,[elbow.toArray(),mid.toArray(),grip.toArray()],.074,'#e5e1cf','fur');
}
const muzzle=new THREE.Group();muzzle.name='Muzzle';muzzle.position.set(0,.035,.81);w.add(muzzle);
c.root.userData.asset={forward:'+Z',units:'metres',physics:c.collider,version:1};
const scene=new THREE.Scene();scene.add(c.root);
const binary=await new GLTFExporter().parseAsync(scene,{binary:true,onlyVisible:true});
await fs.writeFile(new URL('../output/goose-source.glb',import.meta.url),Buffer.from(binary));
console.log(`Goose source: ${binary.byteLength} bytes; forward +Z; no baked root rotation.`);
