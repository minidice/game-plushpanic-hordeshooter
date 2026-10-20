// Default authoring counterpart of CharacterFacePattern and character_skin.gdshader.
// The game evaluates these regions per pixel; exported vertex colors are a portable fallback.
export function faceRegions(species, u, v, softness=.25) {
  const x=Math.abs(u), edge=.002+.022*Math.max(0,Math.min(1,softness));
  const step=(a,b,t)=>{t=Math.max(0,Math.min(1,(t-a)/(b-a)));return t*t*(3-2*t);};
  const oval=(cx,cy,rx,ry)=>(Math.hypot((x-cx)/rx,(v-cy)/ry)-1)*Math.min(rx,ry);
  const fill=d=>1-step(-edge,edge,d);
  const smoothMin=(a,b,k)=>{const h=Math.max(k-Math.abs(a-b),0)/k;return Math.min(a,b)-h*h*k*.25;};
  if(species==='raccoon') {
    const center=.045+.070*step(.08,.38,x),thick=.042+.050*step(.035,.17,x);
    const q=(x/.445)**6+((v-center)/thick)**2;
    return [fill(oval(0,-.045,.445,.26)),fill((Math.sqrt(q)-1)*.065)];
  }
  if(species==='redpanda') {
    const cheek=smoothMin(oval(.285,-.075,.18,.175),oval(0,-.155,.31,.105),.075);
    const tearX=.20+.035*Math.max(0,Math.min(1,(.06-v)/.22));
    return [fill(cheek),fill(oval(tearX,-.025,.032,.100))];
  }
  return [0,0];
}
