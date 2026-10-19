import * as THREE from 'three';

export const infectedKinds=['humanoid','crawler','tether','leaper','spitter'];
// Same plush, short-limbed shape language as the playable animals. +Z faces forward.
export function createInfected(kind) {
  const low=kind==='crawler', leap=kind==='leaper', fat=kind==='spitter';
  const colors={humanoid:['#a0b883','#58738c','#344454'],crawler:['#a9bb91','#89708d','#55465f'],tether:['#b7b49a','#80729c','#57466d'],leaper:['#b8b08b','#b57c57','#725344'],spitter:['#c1ca79','#688c77','#476654']}[kind];
  const mat=(name,color)=>{const m=new THREE.MeshStandardMaterial({color,roughness:.83});m.name=name;return m;};
  const skin=mat('infected_skin',colors[0]),cloth=mat('infected_cloth',colors[1]),trim=mat('infected_trim',colors[2]);
  const ink=mat('infected_ink','#30383d'),white=mat('infected_cream','#fff1c5'),pink=mat('infected_mouth','#d7928e');
  const group=(name,parent,xyz=[0,0,0])=>{const g=new THREE.Group();g.name=name;g.position.set(...xyz);parent?.add(g);return g;};
  const ell=(p,n,xyz,size,m)=>{const o=new THREE.Mesh(new THREE.SphereGeometry(1,16,10),m);o.name=n;o.position.set(...xyz);o.scale.set(...size);p.add(o);return o;};
  const limb=(p,n,a,b,r,m)=>{const av=new THREE.Vector3(...a),bv=new THREE.Vector3(...b),d=bv.clone().sub(av);const o=new THREE.Mesh(new THREE.CapsuleGeometry(r,Math.max(.001,d.length()-2*r),4,10),m);o.name=n;o.position.copy(av.add(bv).multiplyScalar(.5));o.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),d.normalize());p.add(o);return o;};
  const root=group(`${kind}_root`),body=group('Body',root),deform=group('Deform',body);
  const head=group('Head',deform,low?[0,.55,.43]:leap?[0,1.02,.18]:[0,1.36,.04]);
  const hs=low?.78:1;
  head.scale.setScalar(hs);
  ell(deform,'bean_torso',low?[0,.43,-.21]:leap?[0,.65,-.12]:[0,.77,-.04],low?[.35,.26,.48]:fat?[.47,.44,.34]:[.36,.42,.29],cloth);
  ell(head,'big_soft_face',[0,0,0],[.43,.41,.33],skin);
  ell(head,'chin',[0,-.245,.12],[.29,.15,.22],skin);
  for(const side of [-1,1]) {
    ell(head,`ear_${side}`,[side*.43,-.035,-.015],[.09,.12,.065],skin);
    ell(head,`ear_inner_${side}`,[side*.473,-.035,.025],[.036,.057,.022],pink);
    const ey=side<0?.07:.095, ex=side*.164;
    const eye=ell(head,`eye_socket_${side}`,[ex,ey,.291],[.138,side<0?.153:.136,.055],ink);eye.rotation.z=side*.12;
    ell(head,`eye_white_${side}`,[ex,ey,.329],[.108,side<0?.123:.105,.038],white);
    ell(head,`wandering_pupil_${side}`,[ex-side*.023,ey+(side<0?-.02:.015),.363],[.038,.05,.016],ink);
    ell(head,`eye_glint_${side}`,[ex-side*.023-.01,ey+.027,.377],[.012,.014,.008],white);
    const brow=ell(head,`brow_${side}`,[ex,.245,.276],[.13,.037,.038],trim);brow.rotation.z=side*(kind==='leaper'?.30:-.24);
    ell(head,`cheek_${side}`,[side*.27,-.105,.28],[fat?.145:.10,fat?.12:.055,.035],fat?skin:pink);
    const x=side*(fat?.27:.22), z=low?-.45:leap?-.22:-.015;
    const foot=group(side<0?'LeftFoot':'RightFoot',deform,[x,.10,z]);
    ell(foot,`boot_${side}`,[0,0,.055],[.145,.105,.20],trim);
    limb(deform,`short_leg_${side}`,[x,.39,z-.025],[x,.16,z],.105,skin);
    const shoulder=[side*(fat?.39:.32),low?.46:leap?.78:1.02,low?.08:0];
    const elbow=[side*(fat?.53:.47),low?.29:leap?.48:.78,low?.25:.09];
    const hand=[side*(fat?.51:.44),low?.12:leap?.18:.59,low?.48:leap?.40:.28];
    limb(deform,`puffy_sleeve_${side}`,shoulder,elbow,.14,cloth);
    limb(deform,`short_forearm_${side}`,elbow,hand,.105,skin);
    ell(deform,`mitten_${side}`,hand,[.145,.12,.14],skin);
    ell(deform,`thumb_${side}`,[hand[0]-side*.09,hand[1]+.025,hand[2]+.07],[.068,.065,.075],skin);
  }
  ell(head,'button_nose',[0,-.045,.346],[.065,.047,.052],skin);
  const mouth=ell(head,'silly_open_mouth',[.02,-.207,.318],[fat?.12:.165,fat?.12:.09,.045],ink);mouth.rotation.z=kind==='humanoid'?-.13:.08;
  if(!fat) {
    for(const [i,x] of [[0,-.05],[1,.035]]){const tooth=ell(head,`uneven_tooth_${i}`,[x,-.174,.357],[.035,i===0?.046:.033,.018],white);tooth.rotation.z=i===0?-.14:.16;}
  } else ell(head,'puckered_lips',[.02,-.207,.343],[.085,.078,.03],pink);
  if(kind==='tether') {
    const pts=[[.05,-.23,.35],[.12,-.29,.47],[.14,-.44,.54],[.05,-.50,.57]];
    for(let i=0;i<pts.length-1;i++)limb(head,`floppy_tongue_${i}`,pts[i],pts[i+1],.055,pink);
    ell(head,'tongue_tip',pts.at(-1),[.09,.047,.065],pink);
  }
  if(kind==='humanoid') {
    ell(head,'crooked_hair',[.07,.34,-.035],[.35,.105,.245],trim);
    ell(head,'single_cowlick',[-.21,.392,.04],[.075,.10,.07],trim).rotation.z=-.35;
    for(const y of [.65,.82,.99])ell(deform,`oversized_button_${y}`,[.025,y,.255],[.038,.039,.022],white);
    ell(deform,'shirt_pocket',[-.20,.93,.245],[.095,.09,.025],trim);
  } else if(kind==='crawler') {
    ell(head,'sleepy_forehead_patch',[.07,.32,-.015],[.26,.095,.22],trim);
    ell(deform,'round_back_patch',[0,.63,-.22],[.23,.055,.24],trim);
  } else if(kind==='leaper') {
    ell(head,'soft_hood_back',[0,.055,-.15],[.445,.405,.27],trim);
    for(const side of [-1,1])ell(head,`hood_nub_${side}`,[side*.28,.38,-.17],[.10,.12,.10],cloth);
  } else if(fat) {
    ell(deform,'round_belly',[0,.75,.22],[.39,.33,.17],skin);
    ell(deform,'belly_button',[.02,.65,.383],[.043,.035,.012],trim);
    for(const side of [-1,1])ell(head,`puffed_cheek_${side}`,[side*.29,-.135,.23],[.17,.15,.105],skin);
    ell(head,'tiny_hair_tuft',[.08,.39,-.03],[.12,.085,.10],trim);
  }
  const weapon=group('Weapon',deform,[0,.85,.56]);group('Muzzle',weapon,[0,.035,.81]);
  root.userData.style='plush-comedy-v3';
  return root;
}
