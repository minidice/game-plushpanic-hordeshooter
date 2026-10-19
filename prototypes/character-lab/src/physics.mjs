import RAPIER from '@dimforge/rapier3d-compat';
await RAPIER.init();

// Fixed 60 Hz, recomputed when proportions change. Visual deformation never alters the collider.
export function simulateFlight(collider) {
  const world=new RAPIER.World({x:0,y:-9.81,z:0}),events=new RAPIER.EventQueue(true);
  world.timestep=1/60;
  world.createCollider(RAPIER.ColliderDesc.cuboid(30,.1,30).setTranslation(0,-.1,0).setFriction(.9));
  const rb=world.createRigidBody(RAPIER.RigidBodyDesc.dynamic().setTranslation(0,collider.center+.04,0).setLinearDamping(.55).setAngularDamping(.8).setCcdEnabled(true));
  world.createCollider(RAPIER.ColliderDesc.capsule(collider.halfHeight,collider.radius).setMass(1).setFriction(.9).setRestitution(.32).setActiveEvents(RAPIER.ActiveEvents.COLLISION_EVENTS),rb);
  rb.applyImpulse({x:1.4,y:6.5,z:.35},true);rb.applyTorqueImpulse({x:.6,y:.15,z:.8},true);
  const frames=[];let squash=0;
  for(let i=0;i<=480;i++){
    const before=rb.linvel();world.step(events);squash*=.86;
    events.drainCollisionEvents((a,b,started)=>{if(started)squash=Math.min(.28,Math.abs(before.y)*.045);});
    const v=rb.linvel();frames.push({position:{...rb.translation()},rotation:{...rb.rotation()},squash,speed:Math.hypot(v.x,v.y,v.z)});
  }
  events.free();world.free();return frames;
}

export function applyFlight(character,frames,time){
  const f=frames[Math.min(frames.length-1,Math.floor((time%8)*60))],p=character.parts;
  p.body.position.set(f.position.x,f.position.y,f.position.z);
  p.body.quaternion.set(f.rotation.x,f.rotation.y,f.rotation.z,f.rotation.w);
  const stretch=f.position.y>2?Math.min(.10,f.speed*.012):0;
  const y=1-f.squash+stretch,radial=1/Math.sqrt(y);
  p.deform.scale.set(radial,y,radial);
  character.root.updateMatrixWorld(true);
}
