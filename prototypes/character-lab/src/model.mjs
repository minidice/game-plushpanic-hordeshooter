import * as THREE from 'three';
import { faceRegions } from './face-pattern.mjs';
import { mergeVertices } from 'three/addons/utils/BufferGeometryUtils.js';
import { equipment, ear, webfoot, oval, line, rounded } from './sculpt.mjs';

export const species = ['raccoon', 'redpanda', 'duck', 'goose'];
export const names = { raccoon: '라쿤', redpanda: '랫서판다', duck: '오리', goose: '거위' };
export const defaults = Object.fromEntries(species.map(s => [s, { head: 1, width: 1, height: 1, tail: 1, beak: 1, neck: 1, helmet: 1, bounce: 1 }]));
const palette = {
  raccoon: { fur: '#78858a', cream: '#e8e5dc', dark: '#30383b', accent: '#a6ada2' },
  redpanda: { fur: '#aa4b2c', cream: '#f3e4cb', dark: '#382e2a', accent: '#c5743e' },
  duck: { fur: '#bb7934', cream: '#edbd68', dark: '#b87124', accent: '#df9c34' },
  goose: { fur: '#e5e1cf', cream: '#f5ebd0', dark: '#c58429', accent: '#e0a343' },
};
const materials = new Map();
function mat(color) {
  if (!materials.has(color)) materials.set(color, new THREE.MeshStandardMaterial({ color, roughness: 0.72, metalness: 0 }));
  return materials.get(color);
}
function group(name, parent, pos = [0, 0, 0]) {
  const o = new THREE.Group(); o.name = name; o.position.set(...pos); parent?.add(o); return o;
}
function mesh(geo, color, parent, name, pos, scale = [1, 1, 1]) {
  const o = new THREE.Mesh(geo, mat(color)); o.name = name; o.position.set(...pos); o.scale.set(...scale);
  o.castShadow = true; o.receiveShadow = true; parent.add(o); return o;
}
const ballGeo = new THREE.SphereGeometry(1, 24, 16);
const ball = (p, n, xyz, size, c) => mesh(ballGeo, c, p, n, xyz, size);
function beanSkin(kind, p, c) {
  // One closed surface: no separately intersecting head, neck or torso spheres.
  const g = new THREE.SphereGeometry(1, 80, 64), pos = g.attributes.position, colors = [];
  const extra = kind === 'goose' ? .12 * p.neck : 0;
  const bottom = .22, total = 1.62 * p.height + extra;
  const fur = new THREE.Color(c.fur), belly = new THREE.Color(kind === 'redpanda' ? c.dark : c.cream);
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i), t = (y + 1) / 2;
    const round = Math.sqrt(Math.max(0, 1 - y*y));
    const radius = .47 * p.width * Math.pow(round, .62) * (1.08 - .14*t) * (1 + (p.head-1)*THREE.MathUtils.smoothstep(t,.35,.85));
    const factor = round > .00001 ? radius / round : 0;
    pos.setXYZ(i,x*factor,bottom+t*total,z*factor*.86);
    const front=THREE.MathUtils.smoothstep(z/(round||1),.2,.7);
    const mix = front * (1-THREE.MathUtils.smoothstep(t,.48,.66)) * .65;
    const color = fur.clone().multiplyScalar(.88+.12*t).lerp(belly,mix);
    if(kind==='raccoon'||kind==='redpanda'){
      const u=x*factor/(p.head*p.width),v=(bottom+t*total-(.22+1.12*p.height))/(p.head*p.width);
      const [cream,dark]=faceRegions(kind,u,v);
      const faceFront=THREE.MathUtils.smoothstep(z/(round||1),.02,.35);
      color.lerp(new THREE.Color(c.cream),cream*faceFront);
      const ink=kind==='raccoon'?new THREE.Color(c.dark):new THREE.Color(c.fur).lerp(new THREE.Color(c.dark),.5);
      color.lerp(ink,dark*faceFront);
    }
    colors.push(color.r,color.g,color.b);
  }
  g.setAttribute('color',new THREE.Float32BufferAttribute(colors,3));
  g.deleteAttribute('normal');g.deleteAttribute('uv');
  const welded=mergeVertices(g,1e-5);welded.computeVertexNormals();g.dispose();
  const material = mat('#ffffff').clone();material.vertexColors=true;material.userData.owned=true;
  const skin = new THREE.Mesh(welded,material);skin.name=`${kind}_continuous_skin`;skin.castShadow=skin.receiveShadow=true;
  return skin;
}
function stripedTail(p, kind, colors) {
  const vertices = [], indices = [], bands = [];
  const red = kind === 'redpanda', length = red ? 1.08 : .88;
  const rings = 48, radial = 24;
  for (let i = 0; i <= rings; i++) {
    const t = i / rings;
    const r = (red ? .255 : .20) * (.65+.35*Math.sin(Math.PI*Math.min(1,t*2))) * Math.sqrt(Math.max(0,1-Math.pow(t,6))) + .003;
    for (let j = 0; j <= radial; j++) {
      const a = j / radial * Math.PI * 2;
      vertices.push(.18*t*t+Math.cos(a)*r,.12*t+.24*t*t+Math.sin(a)*r,-t*length);
    }
  }
  for (let i = 0; i < rings; i++) {
    const start = indices.length;
    for (let j = 0; j < radial; j++) {
      const a = i * (radial + 1) + j, b = a + radial + 1;
      indices.push(a, b, a + 1, b, b + 1, a + 1);
    }
    bands.push([start, indices.length - start, Math.floor(i / 7) % 2]);
  }
  const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  geo.setIndex(indices); geo.computeVertexNormals(); bands.forEach(v => geo.addGroup(...v));
  const tail = new THREE.Mesh(geo, [mat(colors.accent), mat(red ? '#88402b' : '#343b45')]);
  tail.name = `${kind}_striped_tail`; tail.castShadow = true; tail.receiveShadow = true; p.add(tail);
}

export function createCharacter(kind, config = {}, suffix = '') {
  if (!species.includes(kind)) throw new Error(`Unknown species: ${kind}`);
  const p = { ...defaults[kind], ...config }, c = palette[kind], bird = kind === 'duck' || kind === 'goose';
  const prefix = `${kind}${suffix}`, root = group(`${prefix}_root`), parts = {};
  const pivot = (key, parent, pos) => (parts[key] = group(`${prefix}_${key}`, parent, pos));
  const center = .22 + (1.62 * p.height + (kind === 'goose' ? .12*p.neck : 0)) / 2;
  const body = pivot('body', root, [0, center, 0]);
  const deform = pivot('deform', body, [0,0,0]);
  const torso = pivot('torso', deform, [0,-center,0]);
  const skin = beanSkin(kind,p,c);skin.name=`${prefix}_continuous_skin`;torso.add(skin);
  const headY = .22 + 1.12*p.height + (kind === 'goose' ? .12*p.neck : 0);
  // 'head' is now an attachment anchor for facial features, not a separate skull.
  const head = pivot('head', torso, [0, headY, 0.02]); head.scale.setScalar(p.head*p.width);
  const faceDepth=(x,dy)=>{
    const scale=p.head*p.width,total=1.62*p.height+(kind==='goose'?.12*p.neck:0),t=(headY+dy*scale-.22)/total;
    const r=.47*p.width*Math.pow(Math.sqrt(Math.max(0,1-(t*2-1)**2)),.62)*(1.08-.14*t)*(1+(p.head-1)*THREE.MathUtils.smoothstep(t,.35,.85));
    return (Math.sqrt(Math.max(.0001,r*r-(x*scale)**2))*.86-.02)/scale;
  };
  if (!bird) {
    for (const side of [-1, 1]) {
      const e = pivot(side < 0 ? 'leftEar' : 'rightEar', head, [side * .32,.27,-.04]);
      e.rotation.z=-side*.18;ear(e,`${prefix}_ear_${side}`,c.fur,c.cream);
    }
    const mz=faceDepth(0,-.12);
    oval(head,`${prefix}_muzzle`,[0,-.12,mz+.025],[.18,.10,.095],c.cream,'fur');
    oval(head,`${prefix}_nose`,[0,-.075,mz+.117],[.062,.038,.033],'#292c29','rubber');
    line(head,`${prefix}_smile`,[[-.065,-.16,mz+.106],[0,-.174,mz+.116],[.065,-.16,mz+.106]],.006,c.dark,'fur');
    const tail = pivot('tail', torso, [0, .60*p.height, -.30*p.width]); tail.scale.setScalar(p.tail); stripedTail(tail, kind, c);
  } else {
    const beak = pivot('beak', head, [0,-.075,faceDepth(0,-.075)]); beak.scale.set(1,1,p.beak);
    const bw=kind==='goose'?.16:.225;
    oval(beak,`${prefix}_bill_lower`,[0,-.035,.105],[bw*.95,.042,.18],'#bd7427','beak',48,24);
    oval(beak,`${prefix}_bill_upper`,[0,.005,.105],[bw,.058,.19],c.accent,'beak',48,24);
    line(beak,`${prefix}_bill_lip`,[[-bw*.87,-.012,.18],[0,-.018,.29],[bw*.87,-.012,.18]],.006,'#8a5429','beak');
    for(const side of [-1,1])oval(beak,`${prefix}_nostril_${side}`,[side*.075,.056,.10],[.012,.004,.020],'#8c5c2c','beak');
    const tail = pivot('tail', torso, [0, .55*p.height, -.30*p.width]); tail.scale.setScalar(p.tail);
    ball(tail, `${prefix}_tail_feathers`, [0, 0.08, -0.14], [0.22, 0.14, 0.25], c.cream);
  }
  for (const side of [-1, 1]) {
    const ex=bird?.225:.20,ey=.065,ez=faceDepth(side*ex,ey);
    oval(head,`${prefix}_eye_${side}`,[side*ex,ey,ez+.018],[.043,kind==='goose'?.043:.056,.028],'#242924','lens');
    oval(head,`${prefix}_eye_glint_${side}`,[side*ex-.01,ey+.019,ez+.042],[.009,.010,.004],'#eee1bf','lens');
    const by=kind==='goose'?.11:.14;
    if(kind==='redpanda'){
      const brow=oval(head,`${prefix}_brow_${side}`,[side*ex,.19,faceDepth(side*ex,.19)+.009],[.060,.031,.018],c.cream,'fur');
      brow.rotation.z=side*.15;
    }else{
    line(head,`${prefix}_brow_${side}`,[[side*(ex-.055),by-.018,faceDepth(side*(ex-.055),by)+.025],[side*ex,by,faceDepth(side*ex,by)+.025],[side*(ex+.055),by+.005,faceDepth(side*(ex+.055),by)+.025]],bird?.015:.010,bird?c.fur:c.cream,'fur');
    }
    const foot=pivot(side<0?'leftFoot':'rightFoot',torso,[side*.20*p.width,bird?.055:.105,.055]);
    if(bird)webfoot(foot,`${prefix}_webfoot_${side}`,c.accent);
    else{
      oval(foot,`${prefix}_ankle_${side}`,[0,.12,-.025],[.085,.17,.085],c.dark,'fur');
      rounded(foot,`${prefix}_paw_sole_${side}`,[0,-.055,.03],[.23,.055,.30],'#272e2c',.025,'rubber');
      oval(foot,`${prefix}_paw_${side}`,[0,0,.01],[.12,.085,.15],c.dark,'fur');
      for(const x of [-.045,.045])line(foot,`${prefix}_toe_${side}_${x}`,[[x,.035,.09],[x,.012,.15]],.004,'#676c60');
    }
    pivot(side<0?'leftHand':'rightHand',torso,[0,0,0]);
    const arm=oval(torso,`${prefix}_upper_arm_${side}`,[side*.43*p.width,.89*p.height,.20],[.115,.17,.13],bird?c.fur:c.dark,'fur');arm.rotation.z=side*.28;arm.rotation.x=-.65;
  }
  equipment({kind,head,torso,p,parts,prefix,pivot,fur:c.fur,dark:c.dark,bird});
  root.updateMatrixWorld(true);
  for(const [hand,side] of [[parts.leftHand,-1],[parts.rightHand,1]]){
    const elbow=torso.localToWorld(new THREE.Vector3(side*.43*p.width,.78*p.height,.29));
    parts.weapon.worldToLocal(elbow);
    const grip=hand.position.clone(),mid=elbow.clone().lerp(grip,.5);mid.y-=.025;
    line(parts.weapon,`${prefix}_forearm_${side}`,[elbow.toArray(),mid.toArray(),grip.toArray()],.074,bird?c.fur:c.dark,'fur');
  }
  const base = new Map();
  Object.values(parts).forEach(o => base.set(o, { position: o.position.clone(), rotation: o.rotation.clone(), scale: o.scale.clone() }));
  const radius = .48*p.width*Math.max(1,p.head);
  const collider = { type:'capsule', radius, halfHeight:Math.max(.10,center-radius), center, mass:1 };
  root.userData.physics = { ...collider, deformation:'visual-only', attachments:'single-visual-hierarchy' };
  return { root, parts, base, kind, config: p, skin, collider };
}

export function poseCharacter(c, time, mode = 'walk', direction = 0.45, equipped = true) {
  const { parts: p, config: cfg, kind } = c;
  for (const [o, b] of c.base) { o.position.copy(b.position); o.rotation.copy(b.rotation); o.scale.copy(b.scale); }
  c.root.rotation.set(0, direction, 0);
  p.helmet.visible = p.pack.visible = p.vest.visible = equipped;
  p.weapon.visible = true; p.flash.visible = false;
  if (p.leftEar && equipped) for (const [ear, side] of [[p.leftEar, -1], [p.rightEar, 1]]) {
    ear.position.set(side*.43,.16,-.06);ear.rotation.z=-side*.55;ear.scale.set(.72,.60,.80);
  }
  const cycle = time * Math.PI * 4, step = Math.sin(cycle), bounce = cfg.bounce;
  if (mode === 'walk' || mode === 'strafe') {
    p.body.position.y += Math.abs(step) * 0.055 * bounce;
    p.body.rotation.z = Math.cos(cycle) * 0.045 * bounce;
    p.body.rotation.x = 0.07;
    p.deform.scale.set(1+Math.abs(step)*.012*bounce,1-Math.abs(step)*.018*bounce,1+Math.abs(step)*.012*bounce);
    p.leftFoot.position.z += step * 0.14; p.rightFoot.position.z -= step * 0.14;
    p.leftFoot.position.y += Math.max(0, step) * 0.11; p.rightFoot.position.y += Math.max(0, -step) * 0.11;
    p.leftFoot.rotation.x = step * 0.22; p.rightFoot.rotation.x = -step * 0.22;
    p.tail.rotation.y = Math.sin(cycle - 0.8) * 0.14 * bounce;
    p.tail.rotation.x = Math.cos(cycle - 0.8) * 0.045 * bounce;
    if (mode === 'strafe') {
      p.leftFoot.position.x += step * 0.10; p.rightFoot.position.x -= step * 0.10;
      p.body.position.x += Math.sin(time * 1.7) * 0.18;
    }
  } else if (mode === 'recoil') {
    const phase = (time % 0.5) / 0.5, kick = Math.exp(-phase * 14);
    p.weapon.position.z -= kick * 0.10; p.weapon.rotation.x -= kick * 0.12;
    p.body.rotation.x = -kick * 0.12 * bounce;
    p.deform.scale.set(1+kick*.035,1-kick*.065,1+kick*.035);
    p.flash.visible = phase < 0.1;
  } else if (mode === 'fall') {
    const phase = time % 3, ease = THREE.MathUtils.smoothstep(phase, 0.15, 0.8) * (1 - THREE.MathUtils.smoothstep(phase, 2.1, 2.8));
    p.body.rotation.z = ease * 1.30; p.body.position.y -= ease * .42;
    p.body.position.x += ease * 0.38;
    p.leftFoot.rotation.z = ease * 0.6; p.rightFoot.rotation.z = ease * 0.4;
    p.tail.rotation.y = Math.sin(phase * 13) * 0.18 * ease;
  } else if (mode === 'tumble') {
    const phase=time%4;
    if(phase<.5){const squash=Math.sin(phase/.5*Math.PI)*.30;p.deform.scale.set(1+squash*.6,1-squash,1+squash*.6);p.body.position.y-=squash*.4;}
    else if(phase<2.5){const t=(phase-.5)/2;p.body.position.y+=Math.sin(t*Math.PI)*1.6;p.body.position.x=Math.sin(t*Math.PI)*.5;p.body.rotation.z=t*Math.PI*4;p.deform.scale.set(.92,1.16,.92);}
    else{const squash=Math.exp(-(phase-2.5)*5)*.32;p.deform.scale.set(1+squash*.6,1-squash,1+squash*.6);p.body.position.y-=squash*.4;}
  } else {
    p.body.position.y += Math.sin(time * 2) * 0.012;
    p.tail.rotation.y = Math.sin(time * 1.6) * 0.07;
  }
  c.root.updateMatrixWorld(true);
}

export function makeClips(characters, equipped) {
  const clips = [], modes = [['Walk_8_Directions', 'walk', 8], ['Strafe_Aim', 'strafe', 4], ['Recoil', 'recoil', 2], ['Hit_Fall', 'fall', 3], ['Tumble_Squash','tumble',4]];
  for (const [name, mode, duration] of modes) {
    const tracks = [], times = Array.from({ length: duration * 24 + 1 }, (_, i) => i / 24);
    for (const c of characters) {
      const objects = [c.root, ...Object.values(c.parts)], data = objects.map(o => ({ o, pos: [], rot: [], scale: [] }));
      for (const t of times) {
        const direction = mode === 'walk' ? Math.floor(Math.min(t, duration - 0.001)) * Math.PI / 4 : 0.45;
        poseCharacter(c, t, mode, direction, equipped);
        data.forEach(d => { d.pos.push(...d.o.position); d.rot.push(...d.o.quaternion); d.scale.push(...d.o.scale); });
      }
      for (const d of data) {
        tracks.push(new THREE.VectorKeyframeTrack(`${d.o.name}.position`, times, d.pos));
        tracks.push(new THREE.QuaternionKeyframeTrack(`${d.o.name}.quaternion`, times, d.rot));
        if(d.o===c.parts.deform)tracks.push(new THREE.VectorKeyframeTrack(`${d.o.name}.scale`,times,d.scale));
      }
      poseCharacter(c, 0, 'idle', 0.45, equipped);
    }
    clips.push(new THREE.AnimationClip(name, duration, tracks));
  }
  return clips;
}
