import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
test('game goose export is compact and preserves movement pivots and muzzle',()=>{
  const dir=new URL('../../goose-playtest/assets/',import.meta.url);
  const report=JSON.parse(fs.readFileSync(new URL('goose-report.json',dir),'utf8'));
  assert.ok(report.bytes<2_500_000,`asset is ${report.bytes} bytes`);
  assert.ok(report.triangles<35_000,`asset has ${report.triangles} triangles`);
  assert.ok(report.meshes<20,`asset has ${report.meshes} meshes`);
  for(const name of ['goose_root','goose_body','goose_deform','goose_leftFoot','goose_rightFoot','goose_weapon','Muzzle'])assert.ok(report.nodes.includes(name),name);
  assert.ok(fs.statSync(new URL('goose.glb',dir)).size===report.bytes);
});
