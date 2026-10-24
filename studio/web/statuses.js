const $ = id => document.getElementById(id);
let catalog, clip, busy = false, playing = false, position = 0, last = 0;
const entries = new Map();
async function api(url, data) {
  const response = await fetch(url, data === undefined ? {} : { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(data) });
  const result = await response.json();
  if (!response.ok) throw new Error(result.error || '요청 실패');
  return result;
}
function pause() { playing = false; $('play').textContent = '재생'; }
function lock(value) { busy=value; $('settings').disabled=value; $('reload').disabled=value; $('playback').disabled=value||!clip; }
function error(e) { $('error').textContent=e.message; $('error').hidden=false; $('status').textContent='오류를 확인한 뒤 다시 시도하세요.'; }
function selected() { return [...entries].filter(([,e])=>e.check.checked).map(([id,e])=>({id,stacks:Number(e.stacks.value)})); }
function changed() {
  const ids=selected().map(s=>s.id);
  $('data').textContent=JSON.stringify({source:catalog.source,statuses:catalog.statuses.filter(d=>ids.includes(d.id)).map(({svg,...d})=>d)},null,2);
  if(clip)$('status').textContent='설정이 변경되었습니다. 새 조합을 렌더하세요.';
}
function show(frame) {
  if(!clip)return;const index=Math.max(0,Math.min(clip.frames.length-1,frame));
  $('preview').src='data:image/png;base64,'+clip.frames[index].png;$('preview').hidden=false;$('placeholder').hidden=true;
  $('frame').value=index;$('time').textContent=`${clip.frames[index].time.toFixed(2)} / ${clip.duration.toFixed(2)}초`;
}
async function load(reload=false) {
  pause();lock(true);$('error').hidden=true;
  try {
    if(reload)await api('/api/reload',{});
    catalog=await api('/api/statuses');entries.clear();$('choices').replaceChildren();$('target').replaceChildren();
    for(const target of catalog.targets) {const option=new Option(target.name,target.id);$('target').add(option);}
    for(const d of catalog.statuses) {
      const row=document.createElement('div');row.className='status-choice';
      const check=document.createElement('input');check.type='checkbox';check.id='status-'+d.id;check.checked=d.id==='burn';
      const icon=document.createElement('img');icon.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(d.svg);icon.alt='';
      const label=document.createElement('label');label.htmlFor=check.id;label.append(d.name);
      const badge=document.createElement('small');badge.textContent=d.binding==='live'?'실제 상태 연결':'시각 시제품';label.append(badge);
      const stacks=document.createElement('input');stacks.type='number';stacks.min=1;stacks.max=5;stacks.value=1;stacks.setAttribute('aria-label',d.name+' 중첩 수');
      check.addEventListener('change',changed);stacks.addEventListener('input',changed);
      row.append(check,icon,label,stacks);$('choices').append(row);entries.set(d.id,{check,stacks});
    }
    clip=null;$('preview').hidden=true;$('preview').removeAttribute('src');$('placeholder').hidden=false;changed();
    $('status').textContent=`${catalog.statuses.length}종 · 현재 로컬 데이터 연결 완료`;
  } catch(e){error(e);} finally{lock(false);}
}
$('render').onclick=async()=>{
  const states=selected();
  if(states.some(s=>!Number.isInteger(s.stacks)||s.stacks<1||s.stacks>5)){error(new Error('중첩 수는 1–5의 정수로 입력하세요.'));return;}
  pause();lock(true);$('error').hidden=true;$('status').textContent='실제 게임 프레임 렌더 중…';
  const target=$('target').value, targetName=$('target').selectedOptions[0].textContent;
  try {
    clip=await api('/api/statuses/render',{target,states,hit:$('hit').checked,release:$('release').checked});
    $('frame').max=clip.frames.length-1;position=0;show(0);$('meta').textContent=clip.renderer;
    $('sample').textContent=`${targetName} · ${states.map(s=>catalog.statuses.find(d=>d.id===s.id).name+(s.stacks>1?' ×'+s.stacks:'')).join(', ')||'상태 없음'}\n${$('hit').checked?'0.8초 피격 · ':''}${$('release').checked?'2.0초 해제':'지속 상태'} · 15fps 실제 캡처`;
    $('status').textContent='렌더 완료 · 재생하거나 슬라이더로 피격과 해제 전후를 확인하세요.';
  }catch(e){error(e);}finally{lock(false);}
};
$('clear').onclick=()=>{for(const e of entries.values())e.check.checked=false;changed();};
$('reload').onclick=()=>load(true);
for(const id of ['target','hit','release'])$(id).addEventListener('change',changed);
$('play').onclick=()=>{if(playing){pause();return;}if(position>=clip.duration)position=0;playing=true;last=performance.now();$('play').textContent='일시정지';};
$('frame').oninput=()=>{pause();position=Number($('frame').value)/clip.fps;show(Number($('frame').value));};
function tick(now){if(playing&&!busy&&clip){position+=(now-last)/1000*Number($('speed').value);if(position>=clip.duration){if($('loop').checked)position%=clip.duration;else{position=clip.duration;pause();}}show(Math.floor(position*clip.fps+1e-6));}last=now;requestAnimationFrame(tick);}
requestAnimationFrame(tick);load();
