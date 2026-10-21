import * as THREE from 'three';

// Plush midbosses use the same named runtime pivots as the regular boss.
const material=(name,color,roughness=.83)=>{const m=new THREE.MeshStandardMaterial({color,roughness});m.name=name;return m;};
const tintVertices=(mesh,color)=>{const tint=new THREE.Color(color),values=[];for(let i=0;i<mesh.geometry.attributes.position.count;i++)values.push(tint.r,tint.g,tint.b);mesh.geometry.setAttribute('color',new THREE.Float32BufferAttribute(values,3));return mesh;};
const group=(name,parent,position=[0,0,0])=>{const g=new THREE.Group();g.name=name;g.position.set(...position);parent?.add(g);return g;};
const ellipsoid=(parent,name,position,radii,mat,segments=[10,7])=>{const geometry=new THREE.SphereGeometry(1,...segments);if(mat.vertexColors){const values=[];for(let i=0;i<geometry.attributes.position.count;i++)values.push(1,1,1);geometry.setAttribute('color',new THREE.Float32BufferAttribute(values,3));}const mesh=new THREE.Mesh(geometry,mat);mesh.name=name;mesh.position.set(...position);mesh.scale.set(...radii);parent.add(mesh);return mesh;};
const capsule=(parent,name,start,end,radius,mat)=>{const a=new THREE.Vector3(...start),b=new THREE.Vector3(...end),direction=b.clone().sub(a);const geometry=new THREE.CapsuleGeometry(radius,Math.max(.001,direction.length()-2*radius),4,10);if(mat.vertexColors){const values=[];for(let i=0;i<geometry.attributes.position.count;i++)values.push(1,1,1);geometry.setAttribute('color',new THREE.Float32BufferAttribute(values,3));}const mesh=new THREE.Mesh(geometry,mat);mesh.name=name;mesh.position.copy(a.add(b).multiplyScalar(.5));mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),direction.normalize());parent.add(mesh);return mesh;};
const torus=(parent,name,position,major,minor,mat,rotation=[Math.PI/2,0,0])=>{const mesh=new THREE.Mesh(new THREE.TorusGeometry(major,minor,8,24),mat);mesh.name=name;mesh.position.set(...position);mesh.rotation.set(...rotation);parent.add(mesh);return mesh;};
const cone=(parent,name,position,radius,height,mat,rotation=[0,0,0])=>{const mesh=new THREE.Mesh(new THREE.ConeGeometry(radius,height,8),mat);mesh.name=name;mesh.position.set(...position);mesh.rotation.set(...rotation);parent.add(mesh);return mesh;};

export function createBalloonKing(){
  // Face, fur and glint colors are their own materials rather than vertex tints on the body material.
  const bodyMat=material('king_body','#ef8db4');
  const gold=material('king_gold','#e2b34a',.55);
  const capeMat=material('king_cape','#c23b3b');
  const blueBalloon=material('king_blue_balloon','#75cbe8',.55);
  const cream=material('king_cream','#fff2e4');
  const ink=material('king_ink','#4d1a2e');
  const root=group('boss_balloon_king_root'),body=group('Body',root),deform=group('Deform',body);
  ellipsoid(deform,'round_plush_body',[0,.62,0],[.42,.58,.40],bodyMat);
  // A broad back cape and fur collar remain visible around the balloon from above.
  ellipsoid(deform,'wide_red_cape',[0,1.08,-.48],[.61,.36,.56],capeMat);
  torus(deform,'cape_fur_collar',[0,1.31,-.16],.48,.085,cream);
  torus(deform,'cape_collar_clasp',[0,1.36,.28],.105,.035,gold);

  for(const side of [-1,1]){
    const label=side<0?'left':'right',title=side<0?'Left':'Right';
    const arm=group('boss_'+label+'Arm',deform,[side*.40,1.18,.02]);
    capsule(arm,title+'PuffyArm',[0,.02,0],[side*.10,-.27,.06],.14,bodyMat);
    ellipsoid(arm,title+'Mitten',[side*.11,-.31,.11],[.16,.14,.15],bodyMat);
    const leg=group('boss_balloon_king_'+label+'Leg',deform,[side*.22,.32,-.015]);
    capsule(leg,title+'ShortLeg',[0,0,0],[0,-.18,.035],.115,bodyMat);
    const foot=group(title+'Foot',leg,[0,-.20,.055]);
    ellipsoid(foot,title+'RoundFoot',[0,0,.035],[.16,.105,.20],bodyMat);
  }

  const head=group('Head',deform,[0,1.32,.015]);
  ellipsoid(head,'great_balloon_head',[0,.23,0],[.66,.66,.66],bodyMat,[28,18]);
  // The tied nozzle and crown are separate, rounded toy shapes.
  capsule(head,'balloon_neck',[0,.78,-.01],[0,.88,-.02],.085,bodyMat);
  ellipsoid(head,'balloon_knot',[0,.89,-.02],[.12,.10,.12],bodyMat);
  // Seated on the top of the balloon (centre .23 + radius .66): a band of .25 m meets the surface at y .84.
  const crown=group('upright_gold_crown',head,[0,.84,0]);
  // A thick band and fat points: from the steep camera the crown is the first thing seen.
  torus(crown,'crown_band',[0,0,0],.25,.08,gold);
  torus(crown,'crown_band_top',[0,.07,0],.24,.055,gold);
  for(let i=0;i<5;i++){
    const angle=(i/5)*Math.PI*2-Math.PI/2,x=Math.cos(angle)*.24,z=Math.sin(angle)*.24;
    cone(crown,'crown_point_'+i,[x,.18,z],.1,.3,gold);
    ellipsoid(crown,'crown_tip_'+i,[x,.34,z],[.06,.06,.06],gold);
  }
  // Large, slightly upturned eyes sit above the balloon's midpoint for the steep camera.
  for(const side of [-1,1]){
    const x=side*.22;
    // On the balloon's surface (radius .66 around y .23), not inside it.
    const white=ellipsoid(head,'half_lidded_eye_white_'+side,[x,.43,.6],[.15,.15,.09],cream);white.rotation.x=-.44;
    const lid=ellipsoid(head,'sleepy_upper_lid_'+side,[x,.5,.6],[.16,.075,.07],ink);lid.rotation.x=-.44;
    const pupil=ellipsoid(head,'eye_pupil_'+side,[x+side*.018,.41,.675],[.055,.075,.035],ink);pupil.rotation.x=-.44;
  }
  torus(head,'puckered_round_mouth',[0,.20,.66],.075,.026,ink,[0,0,0]);
  ellipsoid(head,'balloon_glint',[-.36,.54,.46],[.055,.075,.025],cream);
  // Three material-colored 0.35 m balloons float beside the crown, above the right hand.
  const rightArm=root.getObjectByName('boss_rightArm');
  const bundle=group('balloon_minion_bundle',rightArm,[.08,.37,.13]);
  const balloonMats=[gold,blueBalloon,capeMat];
  for(const [i,x] of [-1,0,1].map((side,index)=>[index,side*.19])){
    const y=i===1?.14:.22,z=.20+(i===1?.02:0);
    ellipsoid(bundle,'minion_balloon_'+i,[x,y,z],[.175,.175,.175],balloonMats[i],[16,12]);
    capsule(bundle,'minion_string_'+i,[x,y-.17,z],[x*.7,-.07,.04],.012,bodyMat);
  }
  group('Muzzle',head,[0,.055,.69]);
  group('Weapon',deform,[0,.87,.43]);
  root.userData.style='plush-comedy-v3';root.userData.asset={forward:'+Z',units:'metres',version:2,animation:'runtime object pivots'};
  return root;
}

export function createSpitterMother(){
  const skin=material('mother_skin','#c1ca79');
  const hood=material('mother_hood','#688c77');
  const ink=material('mother_ink','#476654');
  const pouchMat=material('mother_minion_pouches','#a8d46f');
  const cream=material('mother_cream','#efe6c4');
  const root=group('boss_spitter_mother_root'),body=group('Body',root),deform=group('Deform',body);
  // Wide, pear-shaped plush body with a full front-to-back volume.
  ellipsoid(deform,'pear_shaped_body',[0,.83,0],[.72,.83,.66],skin);
  ellipsoid(deform,'round_belly_patch',[0,.77,.624],[.39,.43,.075],cream);
  // Three bright, raised summon pouches and tied knobs break the silhouette from above.
  for(const [i,x] of [-1,0,1].map((side,index)=>[index,side*.34])){
    const y=i===1?1.43:1.29;
    ellipsoid(deform,'summon_pouch_'+i,[x,y,-.50],[.22,.23,.20],pouchMat,[16,12]);
    ellipsoid(deform,'pouch_knot_'+i,[x,y+.235,-.50],[.065,.075,.065],pouchMat);
    ellipsoid(deform,'pouch_seam_'+i,[x,y,-.695],[.11,.12,.018],ink);
  }

  // Sewn cloth patches move to the upper shoulders so they remain visible above the hood.
  for(const [i,x] of [-1,0,1].map((side,index)=>[index,side*.32])){
    const y=i===1?1.58:1.48,z=.24;
    const patch=ellipsoid(deform,'stitched_shoulder_patch_'+i,[x,y,z],[.095,.13,.045],cream);patch.rotation.z=(i-1)*-.22;
    capsule(deform,'shoulder_patch_stitch_'+i,[x-.035,y-.025,z+.04],[x+.035,y+.025,z+.04],.012,ink);
  }

  for(const side of [-1,1]){
    const label=side<0?'left':'right',title=side<0?'Left':'Right';
    const arm=group('boss_'+label+'Arm',deform,[side*.61,1.27,.03]);
    capsule(arm,title+'ThickArm',[0,.03,0],[side*.10,-.28,.10],.19,hood);
    ellipsoid(arm,title+'BigMitten',[side*.12,-.32,.14],[.22,.20,.20],hood);
    ellipsoid(arm,title+'PalmPad',[side*.12,-.34,.315],[.105,.10,.035],cream);
    const leg=group('boss_spitter_mother_'+label+'Leg',deform,[side*.37,.30,0]);
    capsule(leg,title+'StubLeg',[0,0,0],[0,-.15,.025],.15,hood);
    const foot=group(title+'Foot',leg,[0,-.17,.04]);
    ellipsoid(foot,title+'WideFoot',[0,0,.06],[.22,.12,.25],hood);
  }

  const head=group('Head',deform,[0,1.25,.02]);
  ellipsoid(head,'partly_embedded_face',[0,.02,.34],[.46,.42,.45],skin);
  // A rear-half dome leaves the upturned eyes, cheeks and muzzle open to the camera.
  const dome=new THREE.Mesh(new THREE.SphereGeometry(.56,20,14,Math.PI,Math.PI,0,Math.PI/2),hood);dome.name='rear_half_hood_dome';dome.position.set(0,.18,-.08);dome.scale.set(1,.90,1);head.add(dome);
  const brim=new THREE.Mesh(new THREE.TorusGeometry(.48,.065,8,24,Math.PI),hood);brim.name='rear_half_hood_brim';brim.position.set(0,.045,-.04);brim.rotation.set(-Math.PI/2,0,Math.PI);head.add(brim);
  for(const side of [-1,1]){
    const x=side*.17;
    ellipsoid(head,'sleepy_eye_'+side,[x,.205,.716],[.083,.075,.047],ink).rotation.x=-.34;
    const brow=ellipsoid(head,'mischief_brow_'+side,[x,.31,.694],[.145,.052,.045],ink);brow.rotation.z=side*.20;brow.rotation.x=-.34;
    ellipsoid(head,'puffed_cheek_'+side,[side*.34,-.005,.68],[.30,.27,.26],skin);
  }
  // The 0.30 m funnel tilts upward so its circular opening is legible from the high camera.
  const funnel=new THREE.Mesh(new THREE.CylinderGeometry(.13,.19,.30,20,1,false),cream);funnel.name='round_mouth_funnel';funnel.position.set(0,-.035,.84);funnel.rotation.x=1.20;head.add(funnel);
  torus(head,'mouth_funnel_rim',[0,.02,.98],.17,.04,cream,[-.37,0,0]);
  ellipsoid(head,'mouth_interior',[0,.025,.975],[.13,.13,.025],ink).rotation.x=-.37;
  group('Muzzle',head,[0,.03,1.02]);
  group('Weapon',deform,[0,.86,.67]);
  root.userData.style='plush-comedy-v3';root.userData.asset={forward:'+Z',units:'metres',version:2,animation:'runtime object pivots'};
  return root;
}
