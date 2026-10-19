import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { createCharacter, poseCharacter, defaults, species, names, makeClips } from './model.mjs';
import { simulateFlight, applyFlight } from './physics.mjs';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';

const $ = id => document.getElementById(id), key = 'horde-character-lab-v2';
const fields = [
  ['head','윗부분 볼륨',0.75,1.35],['width','몸체 너비',0.75,1.3],['height','몸체 높이',0.75,1.3],
  ['tail','꼬리 크기',0.5,1.5],['beak','부리 길이',0.6,1.6],['neck','상체 길이 · 거위',0.5,1.6],['helmet','헬멧 크기',0.95,1.2],['bounce','움직임 과장',0,1.8]
];
let configs = structuredClone(defaults), selected = 'raccoon', paired = false, paused = false, time = 0;
function validateConfigs(data) {
  if (!data || ![1,2].includes(data.version) || !data.characters) throw new Error('이 비교실에서 저장한 비율 파일을 선택해 주세요.');
  const result = structuredClone(defaults);
  for (const s of species) for (const [field,, min,max] of fields) {
    const v = data.characters[s]?.[field];
    if (typeof v !== 'number' || !Number.isFinite(v) || v < min || v > max) throw new Error(`${names[s]}의 ${field} 값이 범위를 벗어났습니다.`);
    result[s][field] = v;
  }
  return result;
}
try { const cached = localStorage.getItem(key); if (cached) configs = validateConfigs(JSON.parse(cached)); } catch {}
function settings() { return { version: 2, characters: configs }; }
function persist() { try { localStorage.setItem(key, JSON.stringify(settings())); } catch { $('status').textContent = '자동 저장이 제한됩니다. 비율 저장 버튼을 사용하세요.'; } }
const renderer = new THREE.WebGLRenderer({ canvas: $('view'), antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2)); renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.VSMShadowMap; renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.05;
const room=new RoomEnvironment(),pmrem=new THREE.PMREMGenerator(renderer);
const environment=pmrem.fromScene(room,.04).texture;room.dispose();pmrem.dispose();
const shadowCanvas=document.createElement('canvas');shadowCanvas.width=shadowCanvas.height=128;
const ctx=shadowCanvas.getContext('2d'),gradient=ctx.createRadialGradient(64,64,3,64,64,64);
gradient.addColorStop(0,'rgba(14,18,16,.42)');gradient.addColorStop(1,'rgba(14,18,16,0)');ctx.fillStyle=gradient;ctx.fillRect(0,0,128,128);
const contactMap=new THREE.CanvasTexture(shadowCanvas);
renderer.setScissorTest(true);
let views = [];
const english = { raccoon: 'RACCOON', redpanda: 'RED PANDA', duck: 'DUCK', goose: 'GOOSE' };
const traits = { raccoon:'복면 · 줄무늬 꼬리', redpanda:'넓은 볼 · 큰 꼬리', duck:'넓은 부리 · 콩 체형', goose:'세로 실루엣 · 좁은 부리' };
function disposeCharacter(c) { const geos = new Set(); c.root.traverse(o => { if (o.isMesh && (o.userData.uniqueGeometry||o===c.skin || !['SphereGeometry','BoxGeometry','CylinderGeometry'].includes(o.geometry.type))) geos.add(o.geometry);if(o.isMesh&&o.material.userData?.owned)o.material.dispose(); }); geos.forEach(g => g.dispose()); }
function terrain(){
  const g=new THREE.Group(),dirt=new THREE.MeshStandardMaterial({color:'#746a4c',roughness:1}),grass=new THREE.MeshStandardMaterial({color:'#354a36',roughness:1}),rock=new THREE.MeshStandardMaterial({color:'#535f56',roughness:1});
  const ground=new THREE.Mesh(new THREE.PlaneGeometry(60,60),grass);ground.rotation.x=-Math.PI/2;ground.position.y=-.02;ground.receiveShadow=true;g.add(ground);
  const path=new THREE.Mesh(new THREE.PlaneGeometry(4.8,60),dirt);path.rotation.x=-Math.PI/2;path.rotation.z=.28;path.position.y=-.012;path.receiveShadow=true;g.add(path);
  for(let i=0;i<18;i++){
    const angle=i*2.399,dist=3.5+(i%5)*1.2,x=Math.cos(angle)*dist,z=Math.sin(angle)*dist;
    const o=new THREE.Mesh(new THREE.DodecahedronGeometry(.35+(i%3)*.15,0),i%3?grass:rock);o.position.set(x,.18,z);o.scale.y=.8;o.castShadow=true;o.receiveShadow=true;g.add(o);
  }
  for(const [x,z] of [[-3,-2],[3.3,3],[4,-3]]){
    const box=new THREE.Mesh(new THREE.BoxGeometry(.8,.8,.8),new THREE.MeshStandardMaterial({color:'#8a7151',roughness:1}));box.position.set(x,.4,z);box.rotation.y=.3;box.castShadow=box.receiveShadow=true;g.add(box);
  }
  return g;
}
function makeScene(s, suffix) {
  const scene = new THREE.Scene(), character = createCharacter(s, configs[s], suffix);
  scene.add(character.root);
  scene.environment=environment;scene.environmentIntensity=.45;
  const hemi = new THREE.HemisphereLight('#f1e7cf', '#686657', 1); scene.add(hemi);
  const light = new THREE.DirectionalLight('#ffdab0', 3.2); light.position.set(-3, 6, 4); light.castShadow = true;
  light.shadow.mapSize.set(1024,1024); light.shadow.camera.left = -4; light.shadow.camera.right = 4;
  light.shadow.camera.top = 5; light.shadow.camera.bottom = -4; light.shadow.normalBias = .012;light.shadow.bias=-.00015;light.shadow.radius=4;light.shadow.blurSamples=8;scene.add(light);
  const rim = new THREE.DirectionalLight('#ead9bd',1.4); rim.position.set(3,4,-3); scene.add(rim);
  const ground=terrain();scene.add(ground);
  const studio=new THREE.Mesh(new THREE.PlaneGeometry(200,200),new THREE.MeshStandardMaterial({color:'#746955',roughness:1}));studio.rotation.x=-Math.PI/2;studio.position.y=-.025;studio.receiveShadow=true;scene.add(studio);
  const contact=new THREE.Mesh(new THREE.PlaneGeometry(1.9,1.9),new THREE.MeshBasicMaterial({map:contactMap,transparent:true,depthWrite:false}));contact.rotation.x=-Math.PI/2;contact.position.y=-.005;scene.add(contact);
  const camera = new THREE.OrthographicCamera(-2,2,2,-2,.1,100);
  return { scene, character, camera, hemi, light, rim, ground, studio, contact, s, flight:null };
}
function rebuildCards() {
  for (const v of views) { disposeCharacter(v.character);v.ground.traverse(o=>{if(o.isMesh){o.geometry.dispose();o.material.dispose();}});v.studio.geometry.dispose();v.studio.material.dispose();v.contact.geometry.dispose();v.contact.material.dispose();v.light.shadow.map?.dispose(); }
  const kinds = paired ? [selected,selected] : species;
  $('cards').classList.toggle('paired',paired);
  $('cards').innerHTML=kinds.map((s,i)=>`<button type="button" class="model-card" data-species="${s}" aria-pressed="${s===selected}" aria-label="${names[s]} ${paired?(i?'장비 착용':'맨몸'):'선택'}"><div class="card-top"><span class="card-number">0${i+1} / ${paired?(i?'EQUIPPED':'BASE'):'SPECIMEN'}</span><span class="card-indicator">${paired?(i?'장비 착용':'맨몸'):(s===selected?'선택됨':'비교')}</span></div><div class="viewport"></div><div class="card-bottom"><div><strong>${names[s]}</strong><small>${english[s]}</small></div><span class="trait">${traits[s]}</span></div></button>`).join('');
  views=kinds.map((s,i)=>({...makeScene(s,`_view${i}`),element:$('cards').children[i].querySelector('.viewport'),equipped:paired?!!i:$('gear').checked}));
  $('cards').querySelectorAll('button').forEach(b=>b.onclick=()=>{selected=b.dataset.species; renderSliders(); if(paired)rebuildCards();else updateSelection();});
}
function updateSelection(){ $('cards').querySelectorAll('button').forEach(b=>{const active=b.dataset.species===selected;b.setAttribute('aria-pressed',active);b.querySelector('.card-indicator').textContent=active?'선택됨':'비교';}); }
function renderSliders(){
  $('selected-name').textContent=names[selected];
  $('sliders').innerHTML=fields.map(([field,label,min,max])=>{
    const disabled=(field==='beak'&&!['duck','goose'].includes(selected))||(field==='neck'&&selected!=='goose');
    return `<div class="slider ${disabled?'disabled':''}"><label for="param-${field}">${label}<output id="out-${field}">${configs[selected][field].toFixed(2)}×</output></label><input id="param-${field}" type="range" min="${min}" max="${max}" step="0.05" value="${configs[selected][field]}" ${disabled?'disabled':''}></div>`;
  }).join('');
  for(const [field] of fields)$(`param-${field}`).oninput=e=>{
    configs[selected][field]=Number(e.target.value);$(`out-${field}`).textContent=Number(e.target.value).toFixed(2)+'×';
    views.forEach((v,i)=>{if(v.s===selected){v.scene.remove(v.character.root);disposeCharacter(v.character);v.character=createCharacter(v.s,configs[v.s],`_view${i}`);v.scene.add(v.character.root);v.flight=null;}});persist();
  };
}
$('roster').onclick=()=>{paired=false;$('roster').setAttribute('aria-pressed',true);$('paired').setAttribute('aria-pressed',false);rebuildCards();};
$('paired').onclick=()=>{paired=true;$('paired').setAttribute('aria-pressed',true);$('roster').setAttribute('aria-pressed',false);rebuildCards();};
$('pause').onclick=()=>{paused=!paused;$('pause').setAttribute('aria-pressed',paused);$('pause').textContent=paused?'재생':'일시정지';};
$('motion').onchange=()=>{time=0;$('turn').checked=$('motion').value==='walk';};
$('direction').oninput=()=>{$('turn').checked=false;$('direction-value').textContent=$('direction').value+'°';};
$('elevation').oninput=()=>{$('elevation-value').textContent=$('elevation').value+'°';};
$('zoom').oninput=()=>{$('zoom-value').textContent=Number($('zoom').value).toFixed(1)+'×';};
for(const [id,elevation,zoom] of [['camera-play',55,1],['camera-close',35,4.8],['camera-portrait',12,6]])$(id).onclick=()=>{
  $('elevation').value=elevation;$('zoom').value=zoom;$('elevation').oninput();$('zoom').oninput();
  $('studio').checked=id!=='camera-play';
  document.querySelectorAll('.camera-presets button').forEach(b=>b.setAttribute('aria-pressed',b.id===id));
};
$('reset').onclick=()=>{configs[selected]={...defaults[selected]};persist();renderSliders();rebuildCards();};
function download(blob,name){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),5000);}
$('export-json').onclick=()=>download(new Blob([JSON.stringify(settings(),null,2)],{type:'application/json'}),'character-proportions.json');
$('import-json').onclick=()=>$('file').click();
$('file').onchange=async e=>{try{if(!e.target.files.length)return;configs=validateConfigs(JSON.parse(await e.target.files[0].text()));persist();renderSliders();rebuildCards();$('status').textContent='비율을 불러왔습니다.';}catch(e){$('status').textContent=e.message;}finally{$('file').value='';}};
$('export-glb').onclick=async()=>{
  $('export-glb').disabled=true;$('status').textContent='4종 모델과 동작을 저장하고 있습니다…';
  const scene=new THREE.Scene(),characters=species.map((s,i)=>{const c=createCharacter(s,configs[s]);c.root.position.x=(i-1.5)*2.6;scene.add(c.root);return c;});
  try{const clips=makeClips(characters,$('gear').checked);scene.userData=settings();const buffer=await new GLTFExporter().parseAsync(scene,{binary:true,animations:clips,onlyVisible:true});download(new Blob([buffer],{type:'model/gltf-binary'}),'character-comparison.glb');$('status').textContent='GLB 저장 완료. Blender의 파일 → 가져오기 → glTF로 열 수 있습니다.';}catch(e){$('status').textContent=`저장 실패: ${e.message}`;}finally{characters.forEach(disposeCharacter);$('export-glb').disabled=false;}
};
renderSliders();rebuildCards();
let last=performance.now();
function animate(now){
  requestAnimationFrame(animate);if(!paused)time+=Math.min((now-last)/1000,.1);last=now;
  const bounds=$('stage').getBoundingClientRect(),ratio=renderer.getPixelRatio();
  if(renderer.domElement.width!==Math.round(bounds.width*ratio)||renderer.domElement.height!==Math.round(bounds.height*ratio))renderer.setSize(bounds.width,bounds.height,false);
  const direction=$('turn').checked?Math.floor(time%8)*Math.PI/4+.45:Number($('direction').value)*Math.PI/180;
  $('direction-value').textContent=Math.round(direction*180/Math.PI)%360+'°';
  const night=$('night').checked,el=Number($('elevation').value)*Math.PI/180;
  for(let i=0;i<views.length;i++){
    const v=views[i],r=v.element.getBoundingClientRect(),w=r.width,h=r.height;
    const size=18/Number($('zoom').value);v.camera.left=-size*w/h/2;v.camera.right=size*w/h/2;v.camera.top=size/2;v.camera.bottom=-size/2;
    const studio=$('studio').checked;v.studio.visible=studio;v.ground.visible=!studio;
    v.scene.background=new THREE.Color(night?'#171e25':studio?'#746955':'#27332f');v.hemi.intensity=night?.25:1;v.light.intensity=night?.8:3.2;v.rim.intensity=night?.9:1.4;v.scene.environmentIntensity=night?.15:.45;
    poseCharacter(v.character,time,$('motion').value,direction,paired?!!i:$('gear').checked);
    const physical=$('motion').value==='physics';
    if(physical){v.flight??=simulateFlight(v.character.collider);applyFlight(v.character,v.flight,time);}
    const bodyPosition=new THREE.Vector3();v.character.parts.body.getWorldPosition(bodyPosition);v.contact.position.set(bodyPosition.x,-.005,bodyPosition.z);v.contact.material.opacity=Math.max(.10,1-Math.max(0,bodyPosition.y-1)*.4);
    const target=new THREE.Vector3(0,1,0);
    if(physical){v.character.parts.body.getWorldPosition(target);target.y=Math.max(1,target.y*.75);}
    v.camera.position.set(target.x,target.y+Math.sin(el)*8,target.z+Math.cos(el)*8);v.camera.lookAt(target);v.camera.updateProjectionMatrix();
    renderer.setViewport(r.left-bounds.left,bounds.bottom-r.bottom,w,h);renderer.setScissor(r.left-bounds.left,bounds.bottom-r.bottom,w,h);renderer.render(v.scene,v.camera);
  }
}
requestAnimationFrame(animate);
window.addEventListener('error',e=>{$('error').hidden=false;$('error').textContent='미리보기 오류: '+e.message;});
