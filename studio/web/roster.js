const $=id=>document.getElementById(id);
let catalog, busy=false, generation=0, rendered=false;
let clip=null, playing=false, position=0, last=0, soundUrl=null;
const factionNames={zombie:'감염무리',hunter:'수렵단',armed:'무장단'};
const speciesNames={human:'인간 좀비',eagle:'독수리',parrot:'앵무새',lion:'사자',tiger:'호랑이',reindeer:'순록',rhino:'코뿔소'};
const weaponNames={unarmed:'비무장',club:'몽둥이',knife:'칼',woodbow:'나무활',mp5:'MP5',ak47:'AK47',m4:'M4'};
const paletteNames={red:'붉은 무리',green:'녹색 무리',ochre:'황토색 무리',slate:'청회색 무리'};
const presetNames={jeans_shirt:'셔츠와 청바지',chinos_shirt:'셔츠와 면바지',bare_legs:'하의 없음',torn_pants:'찢어진 바지',overalls:'멜빵',suit:'양복',stained_jeans:'얼룩진 청바지 차림',striped_chinos:'줄무늬 셔츠와 면바지',torn_stained:'얼룩과 해진 바지',striped_jeans:'줄무늬 셔츠와 청바지'};
async function api(url,data){
  const response=await fetch(url,data===undefined?{}:{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});
  const result=await response.json();if(!response.ok)throw new Error(result.error||'요청 실패');return result;
}
function lock(value){busy=value;$('settings').disabled=value||!catalog;$('camera').disabled=value||!catalog;$('retry').disabled=value;$('reload').disabled=value;$('attack-playback').disabled=value||!clip;}
function pause(){playing=false;$('attack-play').textContent='재생';}
function stopSound(){const audio=$('attack-audio');audio.pause();audio.removeAttribute('src');audio.load();audio.hidden=true;if(soundUrl)URL.revokeObjectURL(soundUrl);soundUrl=null;}
function clearClip(){pause();stopSound();clip=null;position=0;$('attack-playback').hidden=true;$('attack-playback').disabled=true;}
function showFrame(index){
  if(!clip)return;
  const frame=clip.frames[index];$('preview').src='data:image/png;base64,'+frame.png;$('attack-frame').value=index;
  const phase={windup:'준비',impact:'타격',release:'발사',recovery:'복귀',idle:'대기'}[frame.phase];
  $('attack-time').textContent=`${frame.time.toFixed(3)} / ${clip.duration.toFixed(3)}초 · ${phase}`;
}
async function previewSound(sound){
  if(busy||!clip)return;
  pause();stopSound();const token=generation;lock(true);$('error').hidden=true;
  try{
    const result=await api('/api/sounds/preview',{id:sound.id});if(token!==generation)return;
    const bytes=Uint8Array.from(atob(result.audio),c=>c.charCodeAt(0));soundUrl=URL.createObjectURL(new Blob([bytes],{type:result.mime}));
    const audio=$('attack-audio');audio.src=soundUrl;audio.hidden=false;$('sound-note').textContent=`${sound.label} · ${result.id} · ${result.duration.toFixed(2)}초\n${result.note}`;
    try{await audio.play();}catch{$('sound-note').textContent+='\n브라우저의 재생 버튼을 눌러 주세요.';}
  }catch(e){if(token===generation)error(e);}finally{lock(false);if(catalog)constrain();}
}
function error(e){$('error').textContent=e.message;$('error').hidden=false;$('status').textContent='요청에 실패했습니다. 목록 다시 연결 또는 로컬 데이터 다시 읽기로 복구하세요.';}
const entries=value=>Array.isArray(value)?value:Object.values(value||{});
const rawEntry=(key,id)=>entries(catalog.raw[key]).find(v=>v.id===id);
const name=(key,id)=>rawEntry(key,id)?.name||(key==='palettes'?paletteNames[id]:null)||id;
function options(id,values,label){const previous=$(id).value;$(id).replaceChildren(...[...new Set(values)].map(value=>new Option(label(value),value)));if(values.includes(previous))$(id).value=previous;}
function selection(){return {faction:$('faction').value,species:$('species').value,palette:$('palette').value,weapon:$('weapon').value,variant:Number($('variant').value),gear:($('vest').checked?1:0),mode:$('mode').value,pack:$('mode').value==='pack'?$('pack').value:'',rotation:Number($('rotation').value),zoom:Number($('zoom').value)};}
function constrain(){
  const combos=catalog.combinations;
  const profile=$('mode').value==='pack'&&$('pack').value?rawEntry('packs',$('pack').value):null;
  options('faction',combos.map(c=>c.faction),id=>factionNames[id]||id);
  if(profile)$('faction').value=profile.faction;
  const faction=$('faction').value;
  $('attack-option').disabled=faction!=='hunter';
  if(faction!=='hunter'&&$('mode').value==='attack')$('mode').value='individual';
  options('species',combos.filter(c=>c.faction===faction).map(c=>c.species),id=>speciesNames[id]||name('species',id));
  if(profile)$('species').value=profile.species;
  const species=$('species').value;
  options('palette',combos.filter(c=>c.faction===faction&&c.species===species).map(c=>c.palette),id=>name('palettes',id));
  if(profile)$('palette').value=profile.palette;
  options('weapon',combos.filter(c=>c.faction===faction&&c.species===species&&c.palette===$('palette').value).map(c=>c.weapon),id=>weaponNames[id]||id);
  if(profile)$('weapon').value=profile.weapons[0];
  options('variant',Array.from({length:10},(_,i)=>String(i)),id=>{
    const preset=entries(catalog.raw.presets)[Number(id)];
    return faction==='zombie'?`${Number(id)+1}. ${preset?.name||presetNames[preset?.id]||preset?.id||'프리셋'}`:`${Number(id)+1}. 테마 안의 개체 차이`;
  });
  $('gear-row').hidden=faction!=='armed';
  if(faction!=='armed'){$('vest').checked=false;}
  const pack=$('mode').value==='pack';
  $('variant').disabled=pack;$('weapon').disabled=pack&&faction==='armed';$('pack').disabled=!pack;
  for(const key of ['faction','species','palette'])$(key).disabled=!!profile;
  if(profile)$('weapon').disabled=true;
  $('vest').disabled=pack;
  $('pack-note').textContent=profile?`실제 조우 프로필: ${profile.id}\n무기 순서를 반복해 10마리 구성 · 장구류도 공통 생성기 배정`:pack?(faction==='armed'?'직접 조합 예시 · MP5 4마리 + AK47 6마리 · 외형 10종 · 장구류 공통 생성기 배정':'같은 종족·색 테마·선택 무기 · 외형 프리셋 1–10'):$('mode').value==='attack'?'개체 한 마리 · 실제 게임 공격 포즈 12장 · 전투 시뮬레이션 아님':'선택한 외형 프리셋과 무기를 가진 개체 한 마리';
}
function changed(){
  generation++;clearClip();constrain();
  $('rotation-value').textContent=$('rotation').value+'°';$('zoom-value').textContent=Number($('zoom').value).toFixed(2)+'×';
  const chosen=selection();
  $('data').textContent=JSON.stringify({selection:chosen,source:catalog.source,runtime_sources:catalog.runtime_sources,species:rawEntry('species',chosen.species),palette:rawEntry('palettes',chosen.palette),weapon:rawEntry('weapons',chosen.weapon),preset:chosen.faction==='zombie'?entries(catalog.raw.presets)[chosen.variant]:undefined},null,2);
  if(rendered)$('status').textContent='설정이 변경되었습니다. 아래 수치·이미지는 직전 렌더 결과입니다. 조합 렌더를 누르세요.';
}
function showResult(result,chosen){
  $('preview').src='data:image/png;base64,'+result.png;$('preview').hidden=false;$('placeholder').hidden=true;rendered=true;
  $('meta').textContent='실제 게임 초기화 · '+(chosen.mode==='pack'?'10마리':'개체');
  if(result.mode==='attack'){
    clip=result;position=0;$('attack-playback').hidden=false;$('attack-frame').max=clip.frames.length-1;
    $('meta').textContent='실제 게임 공격 포즈 · '+clip.frames.length+'장';
    $('attack-timing').textContent=`준비 ${clip.attack.windup.toFixed(3)}초 · ${chosen.weapon==='woodbow'?'발사':'타격'} ${clip.attack.impact_time.toFixed(3)}초 · 복귀 ${clip.attack.recovery.toFixed(3)}초\n전체 포즈 범위에 고정한 카메라 · 전투 판정·투사체·자동 소리 없음`;
    $('attack-sounds').replaceChildren(...clip.attack.sounds.map(sound=>{const button=document.createElement('button');button.type='button';button.textContent=sound.label+' 듣기';button.onclick=()=>previewSound(sound);return button;}));
    $('sound-note').textContent='소리는 별도 버튼으로만 재생합니다. 명중 소리는 표본이며 실제 명중을 시뮬레이션하지 않습니다.';
    $('data').textContent=JSON.stringify({selection:chosen,attack:clip.attack,frames:clip.frames.map(({png,...frame})=>frame)},null,2);showFrame(0);
  }
  $('sample').textContent=`${factionNames[chosen.faction]} · ${speciesNames[chosen.species]} · ${name('palettes',chosen.palette)}\n${result.composition}\n${result.note}`;
  const labels={hp:'기본 체력',move_speed:'이동 속도 (m/s)',mass:'질량 계수',attack_damage:'공격 피해',attack_interval:'공격 간격 (s)',attack_windup:'공격 준비 (s)',turn_rate:'회전 계수',attack_reach:'근접 도달 거리 (m)',ranged_range:'원거리 사거리 (m)',projectile_speed:'투사체 속도 (m/s)',wake_range:'각성 거리 (m)',behavior:'행동 유형',collider_radius:'충돌 반경 (m)',collider_height:'실제 충돌 높이 (m)'};
  $('stats').replaceChildren();
  for(const [key,label] of Object.entries(labels)){
    const distinct=[...new Set(result.members.map(m=>m.stats[key]))];
    const row=document.createElement('tr'),th=document.createElement('th'),td=document.createElement('td');th.textContent=label;
    td.textContent=distinct.map(v=>typeof v==='number'?Number(v.toFixed(3)):v).join(' / ');row.append(th,td);$('stats').append(row);
  }
  $('members').replaceChildren();
  for(const [i,member] of result.members.entries()){
    const details=document.createElement('details'),summary=document.createElement('summary'),code=document.createElement('code');
    summary.textContent=`${i+1}. ${weaponNames[member.identity.weapon]} · 외형 ${member.identity.variant+1} · ${member.runtime_type} · 활성 메시 ${member.visible_modules.length}`;
    code.textContent=JSON.stringify(member,null,2);details.append(summary,code);$('members').append(details);
  }
  $('status').textContent='렌더 완료 · 실제 초기화 수치와 활성 모듈을 확인하세요.';
}
async function render(){
  if(busy||!catalog)return;
  const token=generation,chosen=selection();clearClip();lock(true);$('error').hidden=true;$('status').textContent='공통 데이터로 실제 몬스터 렌더 중…';
  try{const result=await api('/api/roster/render',chosen);if(token===generation)showResult(result,chosen);}
  catch(e){if(token===generation)error(e);}finally{lock(false);if(catalog)constrain();}
}
async function load(reload=false){
  if(busy)return;generation++;clearClip();lock(true);$('error').hidden=true;$('status').textContent=reload?'현재 코드 빌드·에셋 다시 읽기 중…':'공통 카탈로그 연결 중…';
  try{
    if(reload)await api('/api/reload',{});
    const loaded=await api('/api/roster');
    if(loaded.errors.length)throw new Error(loaded.errors.join('\n'));
    if(!loaded.combinations.length)throw new Error('사용 가능한 공통 몬스터 조합이 없습니다.');
    catalog=loaded;rendered=false;
    $('pack').replaceChildren(new Option('직접 조합 · 무장단 MP5 4 + AK47 6',''),...catalog.raw.packs.map(p=>new Option(`${factionNames[p.faction]} · ${speciesNames[p.species]} · ${paletteNames[p.palette]} (${p.id})`,p.id)));
    changed();$('catalog').textContent=JSON.stringify(catalog.raw,null,2);
    $('preview').hidden=true;$('preview').removeAttribute('src');$('placeholder').hidden=false;$('stats').replaceChildren();$('members').replaceChildren();
    $('status').textContent=catalog.note;
  }catch(e){catalog=null;error(e);}finally{lock(false);if(catalog)constrain();}
}
for(const id of ['faction','species','palette','weapon','variant','vest','mode'])$(id).addEventListener('change',changed);
$('pack').addEventListener('change',changed);
for(const id of ['rotation','zoom']){$(id).addEventListener('input',changed);$(id).addEventListener('change',render);}
$('render').onclick=render;$('retry').onclick=()=>load();$('reload').onclick=()=>load(true);
$('attack-play').onclick=()=>{if(!clip)return;if(playing){pause();return;}if(position>=clip.duration)position=0;playing=true;last=performance.now();$('attack-play').textContent='일시정지';};
$('attack-frame').oninput=()=>{if(!clip)return;pause();const index=Number($('attack-frame').value);position=clip.frames[index].time;showFrame(index);};
function tick(now){
  if(playing&&!busy&&clip){
    position+=(now-last)/1000*Number($('attack-speed').value);
    if(position>=clip.duration){if($('attack-loop').checked)position%=clip.duration;else{position=clip.duration;pause();}}
    let index=0;while(index+1<clip.frames.length&&clip.frames[index+1].time<=position)index++;showFrame(index);
  }
  last=now;requestAnimationFrame(tick);
}
window.addEventListener('pagehide',()=>{generation++;clearClip();});
requestAnimationFrame(tick);
load();
