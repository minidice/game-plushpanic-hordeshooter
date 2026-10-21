import * as THREE from 'three';

// Small, round plush named enemies. +Z faces forward and the root sits on the floor.
const material=(name,color,roughness=.83)=>{
  const value=new THREE.MeshStandardMaterial({color,roughness});
  value.name=name;
  return value;
};
const group=(name,parent,xyz=[0,0,0])=>{
  const value=new THREE.Group();
  value.name=name;
  value.position.set(...xyz);
  parent?.add(value);
  return value;
};
const ell=(parent,name,xyz,size,mat)=>{
  const value=new THREE.Mesh(new THREE.SphereGeometry(1,16,12),mat);
  value.name=name;
  value.position.set(...xyz);
  value.scale.set(...size);
  parent.add(value);
  return value;
};
const limb=(parent,name,a,b,radius,mat)=>{
  const start=new THREE.Vector3(...a),end=new THREE.Vector3(...b);
  const direction=end.clone().sub(start);
  const value=new THREE.Mesh(new THREE.CapsuleGeometry(radius,Math.max(.001,direction.length()-2*radius),4,10),mat);
  value.name=name;
  value.position.copy(start.add(end).multiplyScalar(.5));
  value.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),direction.normalize());
  parent.add(value);
  return value;
};
const cone=(parent,name,xyz,radius,height,rotation,mat)=>{
  const value=new THREE.Mesh(new THREE.ConeGeometry(radius,height,12),mat);
  value.name=name;
  value.position.set(...xyz);
  value.rotation.set(...rotation);
  parent.add(value);
  return value;
};

export function createBull() {
  const fur=material('bull_fur','#8c6147');
  const cream=material('bull_cream','#f1e6c8');
  const ink=material('bull_ink','#2a1f1a');
  const pink=material('bull_pink','#dcb1a1');
  const root=group('bull_root'),body=group('Body',root),deform=group('Deform',body);

  // A deep, forward-leaning plush torso; the belly patch sits proud of the fabric.
  ell(deform,'bull_torso',[0,.91,-.06],[.57,.66,.54],fur).rotation.x=-.08;
  ell(deform,'bull_belly_patch',[0,.87,.455],[.35,.37,.13],cream);

  // Short planted legs with hip pivots and independent round hooves.
  for(const side of [-1,1]) {
    const label=side<0?'Left':'Right';
    const leg=group(`bull_${side<0?'left':'right'}Leg`,deform,[side*.34,.46,-.035]);
    limb(leg,`${label}LegPlush`,[0,0,0],[0,-.31,.025],.17,fur);
    const foot=group(`${label}Foot`,leg,[0,-.32,.08]);
    ell(foot,`${label}Hoof`,[0,-.055,.045],[.205,.14,.25],ink);

    const arm=group(`bull_${side<0?'left':'right'}Arm`,deform,[side*.49,1.24,.08]);
    limb(arm,`${label}ShortArm`,[0,0,0],[side*.07,-.27,.11],.145,fur);
    ell(arm,`${label}Mitten`,[side*.075,-.32,.14],[.17,.145,.16],fur);

  }

  // The head sits ahead of the chest and pitches down into the charge pose.
  const head=group('Head',deform,[0,1.36,.29]);
  head.rotation.x=-.06;
  ell(head,'bull_head',[0,.015,0],[.47,.46,.46],fur);
  for(const side of [-1,1]) {
    ell(head,`ear_${side}`,[side*.48,.27,-.05],[.13,.115,.12],fur).rotation.z=side*-.25;
    ell(head,`ear_inset_${side}`,[side*.50,.275,.04],[.065,.06,.035],pink);
    // Thick, outward-sweeping horns curl up at the tips; no metal trim.
    limb(head,`horn_${side}_root`,[side*.34,.25,-.015],[side*.55,.42,.015],.12,cream);
    limb(head,`horn_${side}_sweep`,[side*.55,.42,.015],[side*.68,.53,.08],.105,cream);
    limb(head,`horn_${side}_tip`,[side*.68,.53,.08],[side*.64,.69,.16],.075,cream);
    ell(head,`horn_${side}_round_tip`,[side*.64,.69,.16],[.075,.08,.075],cream);

    const brow=ell(head,`angry_brow_${side}`,[side*.205,.16,.409],[.205,.07,.055],ink);
    brow.rotation.z=side*-.19;
    ell(head,`eye_${side}`,[side*.19,.055,.445],[.079,.082,.052],ink);
    ell(head,`eye_glint_${side}`,[side*.17,.083,.491],[.021,.024,.012],cream);
    ell(head,`nostril_${side}`,[side*.19,-.185,.785],[.067,.09,.035],ink);
  }
  ell(head,'broad_cream_muzzle',[0,-.225,.62],[.39,.255,.255],cream);
  ell(head,'muzzle_lower_lip',[0,-.405,.60],[.25,.075,.18],cream);
  group('Muzzle',head,[0,-.22,.825]);
  group('Weapon',deform,[0,.91,.58]);

  // Short rump tail with a soft tuft, on its own future ragdoll pivot.
  const tail=group('bull_tail',deform,[0,.83,-.51]);
  limb(tail,'tail_cord',[0,0,0],[0,-.22,-.13],.055,fur);
  limb(tail,'tail_end',[0,-.22,-.13],[.015,-.38,-.17],.05,fur);
  ell(tail,'tail_tuft',[.015,-.43,-.18],[.12,.115,.105],pink);
  root.userData.style='plush-comedy-v3';
  root.userData.asset={forward:'+Z',units:'metres',version:2,animation:'runtime object pivots'};
  return root;
}

export function createBalloon() {
  const bodyMat=material('balloon_body','#f27fac');
  const balloonMat=material('balloon_head','#f7a9c8',.38);
  const white=material('balloon_white','#fff2e4');
  const ink=material('balloon_ink','#4d1a2e');
  const root=group('balloon_root'),body=group('Body',root),deform=group('Deform',body);

  ell(deform,'soft_body',[0,.60,0],[.36,.43,.33],bodyMat);
  // Neck bow stays with the body when the runtime hides Head on pop.
  ell(deform,'neck_knot',[0,1.035,.01],[.09,.09,.09],bodyMat);
  ell(deform,'neck_bow_left',[-.13,1.035,.015],[.14,.075,.055],bodyMat).rotation.z=-.3;
  ell(deform,'neck_bow_right',[.13,1.035,.015],[.14,.075,.055],bodyMat).rotation.z=.3;

  for(const side of [-1,1]) {
    const label=side<0?'Left':'Right';
    const arm=group(`balloon_${side<0?'left':'right'}Arm`,deform,[side*.34,.85,.015]);
    limb(arm,`${label}Stub`,[0,0,0],[side*.055,-.19,.035],.095,bodyMat);
    ell(arm,`${label}Mitten`,[side*.06,-.21,.045],[.105,.09,.105],bodyMat);

    const leg=group(`balloon_${side<0?'left':'right'}Leg`,deform,[side*.19,.31,-.015]);
    limb(leg,`${label}LegPlush`,[0,0,0],[0,-.17,.015],.09,bodyMat);
    const foot=group(`${label}Foot`,leg,[0,-.18,.055]);
    ell(foot,`${label}FootPuff`,[0,0,.025],[.12,.075,.15],bodyMat);
  }

  const head=group('Head',deform,[0,1.0,.01]);
  ell(head,'round_balloon',[0,.28,0],[.56,.56,.56],balloonMat);
  // Tied tip is part of Head, so it disappears with the balloon on pop.
  ell(head,'balloon_neck',[0,.83,0],[.085,.075,.09],balloonMat);
  cone(head,'balloon_knot',[0,.91,0],.13,.20,[0,0,0],balloonMat);
  // A small satin dot reads as a highlight from the game's oblique camera.
  ell(head,'balloon_glint',[-.23,.55,.462],[.065,.075,.026],white);
  for(const side of [-1,1]) {
    const x=side*.205;
    ell(head,`eye_white_${side}`,[x,.34,.484],[.125,.15,.075],white);
    ell(head,`eye_pupil_${side}`,[x+side*.018,.325,.55],[.058,.075,.035],ink);
    ell(head,`eye_glint_${side}`,[x-.012,.365,.579],[.019,.023,.012],white);
  }
  ell(head,'tiny_mouth',[0,.105,.548],[.055,.038,.025],ink);
  group('Muzzle',head,[0,.105,.575]);
  group('Weapon',deform,[0,.70,.39]);
  root.userData.style='plush-comedy-v3';
  root.userData.asset={forward:'+Z',units:'metres',version:2,animation:'runtime object pivots'};
  return root;
}
