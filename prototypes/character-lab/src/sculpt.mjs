import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';

const materials=new Map();
export function surface(color,finish='cloth'){
  const key=color+finish;
  if(!materials.has(key))materials.set(key,new THREE.MeshStandardMaterial({color,roughness:{cloth:.92,fur:.73,rubber:.82,metal:.38,lens:.22,beak:.48}[finish]??.75,metalness:finish==='metal'?.65:finish==='lens'?.35:0}));
  return materials.get(key);
}
function add(parent,name,geometry,material,pos=[0,0,0]){
  const o=new THREE.Mesh(geometry,material);o.name=name;o.position.set(...pos);o.castShadow=o.receiveShadow=true;o.userData.uniqueGeometry=true;parent.add(o);return o;
}
export function rounded(parent,name,pos,size,color,r=.025,finish='cloth'){
  return add(parent,name,new RoundedBoxGeometry(...size,3,Math.min(r,...size.map(s=>s*.4))),surface(color,finish),pos);
}
export function line(parent,name,points,radius,color,finish='cloth',closed=false){
  const curve=new THREE.CatmullRomCurve3(points.map(p=>new THREE.Vector3(...p)),closed);
  return add(parent,name,new THREE.TubeGeometry(curve,Math.max(12,points.length*3),radius,6,closed),surface(color,finish));
}
export function oval(parent,name,pos,size,color,finish='cloth'){
  const o=add(parent,name,new THREE.SphereGeometry(1,24,16),surface(color,finish),pos);o.scale.set(...size);return o;
}
export function ear(parent,name,color,inner){
  const s=new THREE.Shape();s.moveTo(-.12,0);s.bezierCurveTo(-.18,.13,-.10,.29,-.015,.30);s.bezierCurveTo(.07,.29,.16,.10,.12,0);s.quadraticCurveTo(0,-.06,-.12,0);
  const g=new THREE.ExtrudeGeometry(s,{depth:.055,bevelEnabled:true,bevelThickness:.025,bevelSize:.025,bevelSegments:3,steps:1,curveSegments:16});
  add(parent,name,g,surface(color,'fur'),[0,0,-.04]);
  const insert=add(parent,name+'_inner',g.clone(),surface(inner,'fur'),[0,.018,.025]);insert.scale.set(.63,.69,.45);
}
export function webfoot(parent,name,color){
  const s=new THREE.Shape();s.moveTo(-.06,-.09);s.quadraticCurveTo(-.11,-.02,-.15,.12);s.quadraticCurveTo(-.16,.18,-.075,.14);s.quadraticCurveTo(-.04,.19,0,.205);s.quadraticCurveTo(.04,.19,.075,.14);s.quadraticCurveTo(.16,.18,.15,.12);s.quadraticCurveTo(.11,-.02,.06,-.09);s.closePath();
  const o=add(parent,name,new THREE.ExtrudeGeometry(s,{depth:.027,bevelEnabled:true,bevelThickness:.012,bevelSize:.012,bevelSegments:3,curveSegments:12}),surface(color,'beak'),[0,0,0]);o.rotation.x=Math.PI/2;
  oval(parent,name+'_ankle',[0,.065,-.035],[.045,.10,.048],color,'beak');
  for(const x of [-.055,.055])line(parent,name+'_toe'+x,[[x*.3,.015,.00],[x*.7,.015,.07],[x,.015,.13]],.004,'#b37631');
}

function shellGeometry(rx,rz,bottom,top,helmet=false){
  const vertices=[],indices=[],N=64,M=12;
  for(let layer=0;layer<2;layer++)for(let j=0;j<=M;j++)for(let i=0;i<=N;i++){
    const a=i/N*Math.PI*2,t=j/M,sn=Math.sin(a),cs=Math.cos(a),thickness=layer*.026;
    let x,y,z;
    if(helmet){const phi=.02+t*Math.PI*.48;const edge=-.035-.08*Math.pow(Math.abs(sn),4)-.045*Math.max(0,-cs);x=(rx-thickness)*Math.sin(phi)*sn;y=(top-thickness)*Math.cos(phi)+edge*t*t;z=(rz-thickness)*Math.sin(phi)*cs;}
    else{const upper=top-.10*Math.pow(Math.abs(sn),6);const bulge=1+.045*Math.sin(t*Math.PI);x=(rx-thickness)*bulge*sn;y=bottom+(upper-bottom)*t;z=(rz-thickness)*bulge*cs;}
    vertices.push(x,y,z);
  }
  const stride=N+1,layerStride=(M+1)*stride;
  for(let l=0;l<2;l++)for(let j=0;j<M;j++)for(let i=0;i<N;i++){
    const a=l*layerStride+j*stride+i,b=a+stride;
    if((l===0)!==helmet)indices.push(a,a+1,b,a+1,b+1,b);else indices.push(a,b,a+1,a+1,b,b+1);
  }
  for(const j of [0,M])for(let i=0;i<N;i++){
    const a=j*stride+i,b=a+layerStride;
    if(j===M)indices.push(a,a+1,b,a+1,b+1,b);else indices.push(a,b,a+1,a+1,b,b+1);
  }
  const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3));g.setIndex(indices);g.computeVertexNormals();return g;
}

export function equipment({kind,head,torso,p,parts,prefix,pivot,fur,dark,bird}){
  const gear=kind==='redpanda'?'#36434b':kind==='duck'?'#303c44':'#46534e';
  const edge=kind==='duck'?'#182830':'#283631',webbing='#788075',metal='#343e43',lightMetal='#596566';
  const helmet=pivot('helmet',head,[0,.27,-.015]);helmet.scale.setScalar(p.helmet);
  add(helmet,prefix+'_helmet_shell',shellGeometry(.515,.445,0,.32,true),surface(gear));
  const rim=[];for(let i=0;i<64;i++){const a=i/64*Math.PI*2;rim.push([.515*Math.sin(a),-.035-.08*Math.pow(Math.abs(Math.sin(a)),4)-.045*Math.max(0,-Math.cos(a)),.445*Math.cos(a)]);}
  line(helmet,prefix+'_bound_rim',rim,.018,edge,'rubber',true);
  // Crown seams and side hardware; all curves follow the helmet rather than floating around it.
  for(const side of [-1,1]){
    line(helmet,prefix+'_crown_seam'+side,[[side*.18,.27,.19],[side*.22,.285,0],[side*.18,.22,-.27]],.006,'#788174');
    rounded(helmet,prefix+'_side_rail'+side,[side*.495,.005,-.01],[.035,.065,.25],edge,.012,'rubber');
    for(const z of [-.09,.07])oval(helmet,prefix+'_rivet'+side+z,[side*.518,.012,z],[.012,.012,.012],lightMetal,'metal');
  }
  const goggles=new THREE.Group();goggles.name=prefix+'_goggles';goggles.position.set(0,.095,.40);goggles.rotation.x=-.18;helmet.add(goggles);
  for(const side of [-1,1]){
    rounded(goggles,prefix+'_goggle_frame'+side,[side*.137,0,0],[.265,.16,.105],'#222b2e',.045,'rubber');
    rounded(goggles,prefix+'_goggle_lens'+side,[side*.137,.006,.058],[.205,.105,.018],'#56676a',.035,'lens');
    rounded(goggles,prefix+'_lens_glint'+side,[side*.137-.04,.035,.07],[.065,.008,.004],'#b2c1b5',.002,'lens');
  }
  rounded(goggles,prefix+'_bridge',[0,-.012,.008],[.065,.045,.08],edge,.012,'rubber');
  // Independent head attachment: changing hats must not remove the goggles.
  head.attach(goggles);parts.goggles=goggles;
  const headset=pivot('headset',head,[0,0,0]);
  line(headset,prefix+'_headset_band',[[-.48,.06,-.04],[-.46,.40,-.04],[0,.61,-.04],[.46,.40,-.04],[.48,.06,-.04]],.035,edge,'rubber');
  for(const side of [-1,1]){
    oval(headset,prefix+'_earmuff_pad'+side,[side*.47,.025,-.035],[.072,.16,.14],edge,'rubber');
    rounded(headset,prefix+'_earmuff_shell'+side,[side*.52,.025,-.035],[.11,.25,.21],gear,.065);
  }
  line(headset,prefix+'_mic_boom',[[-.52,-.025,.06],[-.48,-.13,.30],[-.29,-.16,.44]],.018,lightMetal,'metal');
  oval(headset,prefix+'_mic_tip',[-.27,-.16,.45],[.065,.037,.037],edge,'rubber');
  headset.userData.hiddenByDefault=true;
  headset.visible=false;
  rounded(helmet,prefix+'_helmet_patch',[-.25,.19,.31],[.12,.075,.023],'#b89f68',.014);
  rounded(helmet,prefix+'_patch_mark',[-.25,.19,.326],[.055,.012,.008],'#484c3d',.004);

  const vest=pivot('vest',torso,[0,0,0]);vest.scale.set(p.width,p.height,p.width);
  const vestPadded=pivot('vestPadded',torso,[0,0,0]);vestPadded.scale.copy(vest.scale);
  vestPadded.userData.hiddenByDefault=true;
  vestPadded.visible=false;
  for(const [wrap,label] of [[vest,''],[vestPadded,'_padded']]){
    add(wrap,prefix+label+'_wraparound_vest',shellGeometry(.495,.410,.43,.88),surface(gear));
    for(const [y,radius] of [[.455,.024],[.86,.026]]){
      const seam=[];for(let i=0;i<64;i++){const a=i/64*Math.PI*2;seam.push([.495*Math.sin(a),y-.055*Math.pow(Math.abs(Math.sin(a)),6),.412*Math.cos(a)]);}
      line(wrap,prefix+label+'_vest_binding'+y,seam,radius,edge,'cloth',true);
    }
    for(const side of [-1,1])line(wrap,prefix+label+'_shoulder_strap'+side,[[side*.33,.82,.31],[side*.37,1.00,.18],[side*.37,1.02,-.13],[side*.31,.83,-.33]],.031,gear);
  }
  for(const x of [-.245,0,.245]){
    const z=.432-Math.abs(x)*.25;
    const pouch=rounded(vest,prefix+'_pouch'+x,[x,.635,z],[.218,.29,.17],gear,.057);pouch.rotation.y=x*.8;
    const flap=rounded(vest,prefix+'_pouch_flap'+x,[x,.755,z+.078],[.205,.09,.038],webbing,.027);flap.rotation.y=x*.8;
    rounded(vest,prefix+'_pouch_tab'+x,[x,.637,z+.091],[.044,.085,.022],edge,.01);
  }
  for(const y of [.57,.73]){
    const band=[];for(let i=0;i<=24;i++){const a=-1.12+i/24*2.24;band.push([.502*Math.sin(a),y,.427*Math.cos(a)]);}
    line(vestPadded,prefix+'_padded_band'+y,band,.034,webbing);
  }

  const pack=pivot('pack',torso,[0,.85*p.height,-.35*p.width]);
  rounded(pack,prefix+'_pack_main',[0,0,-.105],[.54,.57,.24],'#665e4d',.085);
  rounded(pack,prefix+'_pack_lid',[0,.23,-.16],[.56,.15,.24],'#81745d',.045);
  rounded(pack,prefix+'_pack_pocket',[0,-.08,-.26],[.39,.27,.11],'#76684e',.035);
  for(const x of [-.16,.16]){
    rounded(pack,prefix+'_pack_strap'+x,[x,.03,-.327],[.035,.43,.015],edge,.007);
    rounded(pack,prefix+'_pack_clasp'+x,[x,.16,-.34],[.06,.045,.022],'#b4ac8f',.007,'metal');
  }
  const roll=rounded(pack,prefix+'_bedroll',[0,.37,-.09],[.62,.14,.16],'#64746a',.055);
  for(const x of [-.20,.20])rounded(roll,prefix+'_roll_tie'+x,[x,0,.075],[.035,.14,.015],edge,.006);

  const weapon=pivot('weapon',torso,[.09*p.width,.85*p.height,.56]);weapon.rotation.y=-.65;weapon.rotation.z=-.08;
  rounded(weapon,prefix+'_receiver',[0,.015,.06],[.16,.16,.42],metal,.025,'metal');
  rounded(weapon,prefix+'_handguard',[0,.02,.35],[.145,.14,.24],'#536058',.022);
  rounded(weapon,prefix+'_stock',[0,0,-.24],[.14,.17,.22],'#6b6456',.025);
  rounded(weapon,prefix+'_butt',[0,-.005,-.365],[.165,.19,.036],edge,.015,'rubber');
  rounded(weapon,prefix+'_rail',[0,.11,.13],[.075,.035,.53],metal,.008,'metal');
  for(let i=0;i<8;i++)rounded(weapon,prefix+'_rail_tooth'+i,[0,.135,-.07+i*.05],[.09,.015,.018],'#65716e',.003,'metal');
  const barrel=add(weapon,prefix+'_barrel',new THREE.CylinderGeometry(.031,.031,.27,20),surface(metal,'metal'),[0,.035,.59]);barrel.rotation.x=Math.PI/2;
  const muzzle=add(weapon,prefix+'_muzzle',new THREE.CylinderGeometry(.052,.049,.10,20),surface(edge,'metal'),[0,.035,.74]);muzzle.rotation.x=Math.PI/2;
  oval(weapon,prefix+'_bore',[0,.035,.793],[.028,.028,.003],'#10191c','rubber');
  const mag=rounded(weapon,prefix+'_magazine',[0,-.14,.08],[.10,.23,.15],'#555f56',.015);mag.rotation.x=-.18;
  for(const y of [-.06,.01,.07])rounded(mag,prefix+'_mag_rib'+y,[0,y,.078],[.08,.015,.008],edge,.003);
  rounded(weapon,prefix+'_grip',[0,-.14,-.13],[.09,.18,.09],edge,.015,'rubber');
  line(weapon,prefix+'_trigger_guard',[[0,-.08,-.15],[0,-.17,-.08],[0,-.14,-.01],[0,-.07,0]],.012,metal,'metal');
  rounded(weapon,prefix+'_ejection_port',[.083,.035,.06],[.008,.055,.10],'#151e23',.006,'metal');
  rounded(weapon,prefix+'_charging_handle',[.105,.07,-.03],[.06,.018,.035],'#89918b',.005,'metal');
  rounded(weapon,prefix+'_optic_base',[0,.16,-.05],[.09,.035,.13],edge,.008);
  rounded(weapon,prefix+'_optic_frame',[0,.21,-.05],[.10,.10,.07],metal,.025,'metal');
  rounded(weapon,prefix+'_optic_glass',[0,.21,-.008],[.065,.065,.008],'#677f78',.018,'lens');
  for(const side of [-1,1]){
    const hand=parts[side<0?'leftHand':'rightHand'];weapon.add(hand);
    hand.position.set(side<0?-.08:.07,side<0?-.035:-.10,side<0?.32:-.12);
    hand.rotation.set(0,side<0?.3:-.3,side<0?.3:-.2);
    // A mitten grip wraps the handguard / pistol grip instead of hovering beside the body.
    oval(hand,prefix+'_grip_hand'+side,[0,0,0],[.085,.10,.105],bird?fur:dark,'fur');
    oval(hand,prefix+'_thumb'+side,[side*.035,.045,.055],[.043,.06,.044],bird?fur:dark,'fur');
    for(const d of [-.025,.025])line(hand,prefix+'_finger'+side+d,[[side*.075,d,-.025],[side*.085,d,.02],[side*.06,d,.07]],.003,bird?'#bd945d':'#302b28');
  }
  const flash=pivot('flash',weapon,[0,.035,.86]);add(flash,prefix+'_flash_mesh',new THREE.OctahedronGeometry(.12),surface('#ffd275','beak'));flash.visible=false;
  return {helmet,goggles,headset,vest,vestPadded,pack,weapon};
}
