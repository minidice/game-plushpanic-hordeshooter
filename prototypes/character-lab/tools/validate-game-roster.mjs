// Structural validation of the final binary assets, independent of Blender/Godot.
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const players=['goose','duck','raccoon','redpanda'];
for(const kind of [...players,'humanoid','crawler','tether','leaper','spitter']) {
  const player=players.includes(kind);
  const path=new URL(`../../../game/assets/${player?'characters':'enemies'}/${kind}/${kind}.glb`,import.meta.url);
  const binary=await fs.readFile(path);
  assert.equal(binary.readUInt32LE(0),0x46546c67);
  assert.equal(binary.readUInt32LE(4),2);
  assert.equal(binary.readUInt32LE(8),binary.length);
  const doc=JSON.parse(binary.subarray(20,20+binary.readUInt32LE(12)).toString());
  const nodes=new Set(doc.nodes.map(n=>n.name));
  const required=player?['body','deform','head','leftFoot','rightFoot','weapon','weapon_mesh','helmet','beret','vest','pack'].map(n=>`${kind}_${n}`):['Body','Deform','Head','LeftFoot','RightFoot','Weapon'];
  for(const name of [...required,'Muzzle'])assert(nodes.has(name),`${kind}: missing ${name}`);
  assert(!doc.skins?.length,`${kind}: unexpected skeleton`);
  const primitives=doc.meshes.flatMap(m=>m.primitives);
  const triangles=primitives.reduce((n,p)=>n+doc.accessors[p.indices].count/3,0);
  assert(triangles<(player?30000:4500));
  if(player) {
    for(const channel of ['body','beak','hat','vest','pack'])assert(doc.materials.some(m=>m.name.startsWith(`custom_${channel}`)),`${kind}: missing ${channel}`);
    const body=primitives.filter(p=>doc.materials[p.material].name.startsWith('custom_body'));
    assert(body.some(p=>p.attributes.COLOR_0!==undefined&&doc.accessors[p.indices].count>10000),`${kind}: continuous skin lost its vertex colors`);
    for(const slot of ['helmet','beret','vest','pack']) {
      const parent=doc.nodes.find(n=>n.name===`${kind}_${slot}`);
      const children=(parent.children??[]).map(i=>doc.nodes[i]).filter(n=>n.mesh!==undefined);
      const channel=['helmet','beret'].includes(slot)?'hat':slot;
      assert(children.length>0,`${kind}: empty ${slot}`);
      for(const child of children)for(const p of doc.meshes[child.mesh].primitives)assert(doc.materials[p.material].name.startsWith(`custom_${channel}`),`${kind}: mixed material channels on ${slot}`);
    }
    if(!['goose','duck'].includes(kind))for(const ear of ['leftEar','rightEar'])assert(nodes.has(`${kind}_${ear}`));
  } else assert(doc.materials.length<=6,`${kind}: too many materials`);
  console.log(`${kind}: PASS | ${triangles} triangles | ${doc.meshes.length} mesh nodes | ${Math.round(binary.length/1024)} KiB`);
}
