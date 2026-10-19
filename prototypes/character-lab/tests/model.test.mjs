import test from 'node:test';
import assert from 'node:assert/strict';
import { createCharacter, poseCharacter, defaults, species, makeClips } from '../src/model.mjs';

test('each species has one continuous body skin and all attachments follow its deform pivot', () => {
  assert.equal(species.length, 4);
  for (const kind of species) {
    const c = createCharacter(kind, defaults[kind]);
    for (const name of ['head', 'body', 'leftFoot', 'rightFoot', 'helmet', 'pack', 'weapon']) assert.ok(c.parts[name], `${kind}: ${name}`);
    assert.ok(c.parts[kind === 'duck' || kind === 'goose' ? 'beak' : 'tail']);
    assert.equal(c.root.getObjectByName(`${kind}_head_fur`), undefined);
    assert.ok(c.skin?.isMesh);
    assert.equal(c.parts.torso.parent, c.parts.deform);
    assert.equal(c.parts.leftFoot.parent, c.parts.torso);
    assert.equal(c.collider.type, 'capsule');
    const normals=c.skin.geometry.attributes.normal;
    for(let i=0;i<normals.count;i++)assert.ok(Math.hypot(normals.getX(i),normals.getY(i),normals.getZ(i))>.99,'closed body needs valid normals at its poles');
    c.root.traverse(o => { if (o.isMesh) assert.ok(o.geometry.attributes.position.count > 0); });
  }
});

test('squash and tumble move the whole character without detaching its feet or face', () => {
  const c = createCharacter('duck', defaults.duck);
  poseCharacter(c, 0.3, 'tumble', 0.7, true);
  assert.notEqual(c.parts.deform.scale.y, 1);
  assert.ok(c.parts.deform.scale.toArray().every(v => Number.isFinite(v) && v > 0));
  poseCharacter(c, 0, 'idle', 0, true);
  assert.equal(c.parts.deform.scale.y, 1);
  assert.equal(c.parts.head.rotation.x, 0);
});

test('proportions change the intended geometry and equipment follows the head', () => {
  const c = createCharacter('raccoon', { ...defaults.raccoon, head: 1.25, tail: 1.4 });
  assert.equal(c.parts.head.scale.x, 1.25);
  assert.equal(c.parts.tail.scale.x, 1.4);
  assert.equal(c.parts.helmet.parent, c.parts.head);
});

test('motion is repeatable after mode changes, and strafe preserves aim', () => {
  const c = createCharacter('duck', defaults.duck);
  poseCharacter(c, 0.37, 'walk', 0.7, true);
  const first = c.parts.body.position.clone();
  poseCharacter(c, 0.6, 'fall', 0.7, true);
  poseCharacter(c, 0.37, 'walk', 0.7, true);
  assert.ok(first.distanceTo(c.parts.body.position) < 1e-8);
  poseCharacter(c, 0.37, 'strafe', 0.7, false);
  assert.equal(c.root.rotation.y, 0.7);
  assert.equal(c.parts.helmet.visible, false);
  assert.equal(c.parts.weapon.visible, true);
});

test('hands follow recoil with the weapon and helmet outer surfaces face outward',()=>{
  const c=createCharacter('duck',defaults.duck);
  assert.equal(c.parts.leftHand.parent.name,c.parts.weapon.name);
  assert.equal(c.parts.rightHand.parent.name,c.parts.weapon.name);
  const shell=c.root.getObjectByName('duck_helmet_shell');
  assert.ok(shell.geometry.attributes.normal.getY(0)>0,'helmet crown normal should face the light, not its interior');
});

test('exported animation contains all requested motions and finite values', () => {
  const c = createCharacter('redpanda', defaults.redpanda);
  const clips = makeClips([c], true);
  assert.deepEqual(clips.map(c => c.name), ['Walk_8_Directions', 'Strafe_Aim', 'Recoil', 'Hit_Fall', 'Tumble_Squash']);
  assert.ok(clips[4].tracks.some(t => t.name.endsWith('_deform.scale')));
  for (const clip of clips) {
    assert.ok(clip.duration > 0);
    for (const track of clip.tracks) assert.ok([...track.values].every(Number.isFinite));
  }
});
