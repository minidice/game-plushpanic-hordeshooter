import test from 'node:test';
import assert from 'node:assert/strict';
import { createCharacter, defaults, species } from '../src/model.mjs';
import { simulateFlight, applyFlight } from '../src/physics.mjs';

test('capsules launch, land, squash and settle without invalid transforms', () => {
  for(const kind of species){
    const c=createCharacter(kind,defaults[kind]);
    const flight=simulateFlight(c.collider);
    assert.ok(flight.some(f=>f.position.y>2));
    assert.ok(flight.some(f=>f.squash>.04));
    assert.ok(flight.every(f=>f.position.y>.1&&Object.values(f.position).every(Number.isFinite)));
    const last=flight.at(-1);
    assert.ok(last.speed<.2,`${kind} should settle, speed=${last.speed}`);
    applyFlight(c,flight,1);
    assert.ok(c.parts.deform.scale.toArray().every(v=>v>0));
    assert.equal(c.parts.helmet.parent,c.parts.head);
  }
});

test('wide-short and narrow-tall proportions keep their collision simulation bounded',()=>{
  for(const kind of species)for(const config of [{width:1.3,height:.75,head:1.35},{width:.75,height:1.3,head:.75,neck:1.6}]){
    const c=createCharacter(kind,config),frames=simulateFlight(c.collider);
    assert.ok(frames.every(f=>f.position.y>0&&f.position.y<5&&Number.isFinite(f.rotation.w)));
    assert.ok(frames.at(-1).speed<.2);
  }
});
