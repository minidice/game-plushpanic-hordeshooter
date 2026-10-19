import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import * as THREE from 'three';
import { createCharacter } from '../src/model.mjs';

function readGlb(url){
  const bytes=fs.readFileSync(url);
  assert.equal(bytes.readUInt32LE(0),0x46546c67);
  return JSON.parse(bytes.subarray(20,20+bytes.readUInt32LE(12)).toString());
}

for(const kind of ['goose','duck','raccoon','redpanda']){
  test(`${kind} source arm split preserves the original world pose and collider`,()=>{
    const source=readGlb(new URL(`../output/roster/${kind}-source.glb`,import.meta.url));
    const character=createCharacter(kind);
    const world=index=>{
      const node=source.nodes[index], local=node.matrix?new THREE.Matrix4().fromArray(node.matrix):new THREE.Matrix4().compose(new THREE.Vector3().fromArray(node.translation??[0,0,0]),new THREE.Quaternion().fromArray(node.rotation??[0,0,0,1]),new THREE.Vector3().fromArray(node.scale??[1,1,1]));
      const parent=source.nodes.findIndex(n=>(n.children??[]).includes(index));
      return parent<0?local:world(parent).multiply(local);
    };
    for(const side of [-1,1]){
      const name=`${kind}_upper_arm_${side}`, index=source.nodes.findIndex(n=>n.name===name);
      const original=character.root.getObjectByName(name).matrixWorld.elements;
      assert.ok(world(index).elements.every((v,i)=>Math.abs(v-original[i])<1e-6),'reparented arm retains world pose');
      for(const [part,channel] of [[`eye_${side}`,'custom_eyes_'],[`eye_glint_${side}`,'detail_']]){
        const node=source.nodes.find(n=>n.name===`${kind}_${part}`);
        assert.ok(source.meshes[node.mesh].primitives.every(p=>source.materials[p.material].name.startsWith(channel)),`${part} channel excludes the glint`);
      }
      for(const [part,pivot] of [[`eye_${side}`,side<0?'leftEye':'rightEye'],[`eye_glint_${side}`,side<0?'leftEye':'rightEye'],[`brow_${side}`,side<0?'leftBrow':'rightBrow']]){
        const name=`${kind}_${part}`,index=source.nodes.findIndex(n=>n.name===name);
        const anchor=source.nodes.findIndex(n=>n.name===`${kind}_${pivot}`);
        assert.ok(anchor>=0,`${pivot} exists`);
        assert.ok(source.nodes[anchor].children.includes(index),`${part} moves with ${pivot}`);
        if(!part.includes('glint')){
          const mesh=character.parts.head.getObjectByName(name);
          mesh.geometry.computeBoundingBox();
          const center=part.startsWith('eye_')?mesh.position:mesh.geometry.boundingBox.getCenter(new THREE.Vector3()).applyMatrix4(mesh.matrix);
          const position=new THREE.Vector3().setFromMatrixPosition(world(anchor));
          assert.ok(position.distanceTo(character.parts.head.localToWorld(center.clone()))<1e-6,`${pivot} scales around its facial feature`);
        }
        const original=character.root.getObjectByName(name).matrixWorld.elements;
        assert.ok(world(index).elements.every((v,i)=>Math.abs(v-original[i])<1e-6),`${part} retains world pose`);
      }
    }
    if(kind==='raccoon'||kind==='redpanda')for(const part of ['muzzle','nose','smile']){
      const index=source.nodes.findIndex(n=>n.name===`${kind}_${part}`);
      const anchor=source.nodes.findIndex(n=>n.name===`${kind}_snout`);
      assert.ok(anchor>=0,'snout exists');
      assert.ok(source.nodes[anchor].children.includes(index),`${part} moves with snout`);
      const original=character.parts.head.getObjectByName(`${kind}_${part}`).matrixWorld.elements;
      assert.ok(world(index).elements.every((v,i)=>Math.abs(v-original[i])<1e-6),`${part} retains world pose`);
    }
    const root=source.nodes.find(n=>n.name===`${kind}_root`);
    assert.deepEqual(root.extras.physics,character.root.userData.physics);
  });
  test(`${kind} export preserves independent outfit and arm pivots, color channels and body paint`,()=>{
    const dir=new URL(`../../../game/assets/characters/${kind}/`,import.meta.url);
    const glb=readGlb(new URL(`${kind}.glb`,dir));
    const nodes=glb.nodes, find=suffix=>nodes.findIndex(n=>n.name===`${kind}_${suffix}`);
    const parent=index=>nodes.findIndex(n=>(n.children??[]).includes(index));
    const descendants=index=>[index,...(nodes[index].children??[]).flatMap(descendants)];
    const primitives=index=>descendants(index).flatMap(i=>nodes[i].mesh===undefined?[]:glb.meshes[nodes[i].mesh].primitives);
    const faceParts=['leftEye','rightEye','leftBrow','rightBrow','crestShort','crestSwept',...(['raccoon','redpanda'].includes(kind)?['snout']:['beak'])];
    for(const [suffix,anchor] of [['leftArm','torso'],['rightArm','torso'],['vest','torso'],['vestPadded','torso'],['goggles','head'],['headset','head'],...faceParts.map(s=>[s,'head'])]){
      const index=find(suffix);
      assert.ok(index>=0,`${suffix} pivot exists`);
      assert.equal(parent(index),find(anchor),`${suffix} parent`);
      assert.ok(primitives(index).length>0,`${suffix} has independently visible geometry`);
    }
    const materials=glb.materials.map(m=>m.name);
    for(const channel of ['body','beak','hat','vest','pack','wing','tail','feet','eyes','hair'])
      assert.ok(materials.some(n=>n.startsWith(`custom_${channel}_`)),`${channel} channel`);
    for(const [suffix,channel] of [['leftArm','wing'],['rightArm','wing'],['tail','tail'],['leftFoot','feet'],['rightFoot','feet'],['vest','vest'],['vestPadded','vest']])
      assert.ok(primitives(find(suffix)).every(p=>materials[p.material].startsWith(`custom_${channel}_`)),`${suffix} surfaces stay in their own channel`);
    for(const suffix of ['crestShort','crestSwept']){
      assert.equal(nodes[find(suffix)].extras.hiddenByDefault,true,`${suffix} defaults hidden`);
      assert.ok(primitives(find(suffix)).every(p=>materials[p.material].startsWith('custom_hair_')),`${suffix} surfaces use hair tint`);
    }
    for(const suffix of ['leftEye','rightEye']){
      const channels=primitives(find(suffix)).map(p=>materials[p.material]);
      assert.ok(channels.some(m=>m.startsWith('custom_eyes_')),`${suffix} has tinted eye`);
      assert.ok(channels.some(m=>m.startsWith('detail_')),`${suffix} retains untinted glint`);
    }
    const headPrimitives=primitives(find('head'));
    assert.ok(headPrimitives.some(p=>materials[p.material].startsWith('custom_eyes_')));
    assert.ok(headPrimitives.some(p=>materials[p.material].startsWith('detail_')),'eye glints and facial details remain untinted');
    if(kind==='raccoon'||kind==='redpanda')assert.equal(new Set(primitives(find('tail')).map(p=>p.material)).size,2,'striped tail keeps both source colors for multiplier tinting');
    assert.ok(glb.meshes.flatMap(m=>m.primitives).some(p=>materials[p.material].startsWith('custom_body_')&&p.attributes.COLOR_0!==undefined),'body vertex paint survives');
    assert.equal(glb.skins,undefined,'object pivots only, no skeletal rig');
    const triangles=glb.meshes.flatMap(m=>m.primitives).reduce((total,p)=>total+glb.accessors[p.indices].count/3,0);
    assert.ok(triangles<30000,`${triangles} triangles exceeds the player budget`);
  });
}
