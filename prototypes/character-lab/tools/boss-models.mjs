import * as THREE from 'three';

// Plush champion bull. +Z faces forward; the root sits on the floor.
export function createBoss() {
  const mat=(name,color)=>{const m=new THREE.MeshStandardMaterial({color,roughness:.83});m.name=name;return m;};
  const fur=mat('boss_fur','#6b4632');
  const cream=mat('boss_cream','#f3e3c4');
  const gold=mat('boss_gold','#e2b34a');
  const ink=mat('boss_ink','#2d2622');
  const leather=mat('boss_leather','#7a2f2a');
  const group=(name,parent,xyz=[0,0,0])=>{const g=new THREE.Group();g.name=name;g.position.set(...xyz);parent?.add(g);return g;};
  const ell=(p,n,xyz,size,m)=>{const o=new THREE.Mesh(new THREE.SphereGeometry(1,16,10),m);o.name=n;o.position.set(...xyz);o.scale.set(...size);p.add(o);return o;};
  const limb=(p,n,a,b,r,m)=>{const av=new THREE.Vector3(...a),bv=new THREE.Vector3(...b),d=bv.clone().sub(av);const o=new THREE.Mesh(new THREE.CapsuleGeometry(r,Math.max(.001,d.length()-2*r),4,10),m);o.name=n;o.position.copy(av.add(bv).multiplyScalar(.5));o.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),d.normalize());p.add(o);return o;};
  const root=group('boss_root'),body=group('Body',root),deform=group('Deform',body);

  // Broad bean-shaped chest, compact legs, and wide planted feet.
  ell(deform,'champion_torso',[0,1.06,-.04],[.66,.68,.48],fur);
  ell(deform,'round_belly',[0,.88,.31],[.49,.39,.25],fur);
  for(const side of [-1,1]) {
    const label=side<0?'Left':'Right';
    const leg=group(`boss_${side<0?'left':'right'}Leg`,deform,[side*.34,.49,-.02]);
    limb(leg,`${label}Leg`,[0,0,0],[0,-.35,.01],.17,fur);
    const foot=group(`${label}Foot`,leg,[0,-.37,.10]);
    ell(foot,`${label}Boot`,[0,0,.03],[.22,.12,.29],leather);
    ell(deform,`${label}ShoulderPad`,[side*.52,1.47,-.01],[.27,.27,.29],leather);

    // Runtime rotates these shoulder pivots for attacks. Oversized mittens make slams readable.
    const arm=group(`boss_${side<0?'left':'right'}Arm`,deform,[side*.52,1.39,.04]);
    limb(arm,`${label}UpperArm`,[0,.02,0],[side*.08,-.29,.10],.17,fur);
    ell(arm,`${label}Fist`,[side*.09,-.39,.19],[.29,.27,.25],fur);
    for(const i of [-1,0,1]) {
      const knuckle=ell(arm,`${label}Knuckle_${i}`,[side*.09+i*.115,-.37,.405],[.055,.065,.026],cream);
      knuckle.rotation.z=side*.12;
    }
  }

  ell(deform,'champion_belt',[0,.78,.35],[.50,.12,.18],leather);
  ell(deform,'belt_buckle',[0,.79,.514],[.20,.16,.045],gold);
  ell(deform,'buckle_center',[0,.79,.557],[.055,.055,.018],cream);

  const head=group('Head',deform,[0,1.52,.02]);
  ell(head,'bull_skull',[0,.02,0],[.49,.44,.42],fur);
  ell(head,'crown_band',[0,.40,-.01],[.25,.065,.20],gold);
  ell(head,'little_crown',[0,.49,-.015],[.22,.12,.17],gold);
  for(const [i,x] of [[0,-.13],[1,0],[2,.13]]) {
    const point=new THREE.Mesh(new THREE.CapsuleGeometry(.035,.06,2,4),gold);
    point.name=`crown_point_${i}`;point.position.set(x,.59,-.015);head.add(point);
  }
  for(const side of [-1,1]) {
    ell(head,`ear_${side}`,[side*.49,.11,-.03],[.13,.12,.10],fur).rotation.z=side*-.3;
    ell(head,`ear_inner_${side}`,[side*.52,.11,.045],[.067,.067,.025],leather);
    limb(head,`horn_base_${side}`,[side*.33,.25,-.01],[side*.49,.43,.015],.105,cream);
    limb(head,`horn_tip_${side}`,[side*.49,.43,.015],[side*.56,.58,.055],.072,cream);
    ell(head,`horn_round_${side}`,[side*.56,.58,.055],[.072,.072,.072],cream);
    const brow=ell(head,`angry_brow_${side}`,[side*.19,.18,.363],[.19,.065,.045],ink);
    brow.rotation.z=side*-.26;
    ell(head,`eye_${side}`,[side*.18,.07,.385],[.062,.055,.035],ink);
    ell(head,`nostril_${side}`,[side*.17,-.18,.573],[.07,.09,.035],ink);
  }

  ell(head,'broad_muzzle',[0,-.20,.39],[.39,.25,.23],cream);
  ell(head,'lower_lip',[0,-.39,.405],[.25,.07,.16],cream);
  const noseRing=new THREE.Mesh(new THREE.TorusGeometry(.17,.035,8,16),gold);
  noseRing.name='gold_nose_ring';noseRing.position.set(0,-.37,.61);head.add(noseRing);
  group('Muzzle',head,[0,-.24,.59]);
  group('Weapon',deform,[0,1.05,.55]);
  root.userData.style='plush-comedy-v3';
  root.userData.asset={forward:'+Z',units:'metres',version:2,animation:'runtime object pivots'};
  return root;
}
