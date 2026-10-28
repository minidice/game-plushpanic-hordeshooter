#include "res://shaders/graphics_surface_quality.gdshaderinc"
// Dedicated terrain-wall treatment. The shared pilot mask leaves all other art intact.
vec3 forest_rock_surface(vec3 rock_position,vec3 normal,vec3 fallback) {
    float weight=forest_surface_weight(rock_position.xz);
    if(weight<=0.)return fallback;

    if(graphics_surface_low) {
        float broad=noise(rock_position.xz*.31+vec2(rock_position.y*.17));
        vec3 stone=mix(vec3(.105,.116,.105),vec3(.172,.180,.162),broad);
        float moss=smoothstep(.12,.48,normal.y)*smoothstep(.28,.64,broad);
        vec3 moss_color=mix(vec3(.125,.155,.042),vec3(.225,.255,.072),broad);
        return mix(fallback,mix(stone,moss_color,moss),weight);
    }

    // Meter-scale irregular stone blocks; the two nearest cells provide sparse cracks.
    // x+z follows either axis-aligned cliff without restarting at each mesh segment.
    vec2 p=vec2((rock_position.x+rock_position.z)*.72,rock_position.y*1.12);
    p.x+=rock_position.y*.22;
    vec2 cell=floor(p),f=fract(p);
    float nearest=10.,second=10.,stone_id=0.;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++) {
        vec2 neighbor=vec2(float(x),float(y));
        vec2 id=cell+neighbor;
        vec2 center=vec2(hash(id+vec2(13.,7.)),hash(id+vec2(37.,19.)));
        vec2 delta=neighbor+vec2(.18)+center*.64-f;
        float d=dot(delta,delta);
        if(d<nearest) {
            second=nearest;nearest=d;stone_id=hash(id+vec2(5.,41.));
        } else second=min(second,d);
    }
    float broad=noise(rock_position.xz*.31+vec2(rock_position.y*.17));
    vec3 stone=mix(vec3(.105,.116,.105),vec3(.172,.180,.162),stone_id);
    stone*=.96+.08*broad;
    // Broken, low-contrast recesses avoid drawing a polygon network over the rock.
    float joint=(1.-smoothstep(.008,.065,second-nearest))*smoothstep(.72,.88,stone_id);
    joint*=smoothstep(.38,.63,noise(rock_position.xz*.8+vec2(rock_position.y*.7)));
    stone=mix(stone,vec3(.080,.092,.067),joint*.25);

    // Moss follows the beveled shoulder and extends unevenly across the upper cap.
    // Large patches, with no fine grain, remain readable at the gameplay camera scale.
    float clump=noise(rock_position.xz*1.15+vec2(rock_position.y*.3));
    float moss=smoothstep(.035+clump*.20,.34+clump*.23,normal.y);
    moss*=smoothstep(.22,.52,clump+broad*.24);
    vec3 moss_color=mix(vec3(.125,.155,.042),vec3(.225,.255,.072),broad);
    moss_color*=.94+.10*clump;
    // A narrow subdued edge separates soft moss from the exposed stone beneath it.
    float moss_edge=smoothstep(.02,.16,moss)*(1.-smoothstep(.18,.42,moss));
    stone=mix(stone,vec3(.105,.125,.047),moss_edge*.35);
    return mix(fallback,mix(stone,moss_color,moss),weight);
}
