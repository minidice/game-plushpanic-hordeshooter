// The large Stage 1's walkway along its routes (doc/features/level/log/014): worn pale paving with faded
// yellow kerb lines near the edges, soft at the border so it reads as a way to follow, not a wall.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
void fragment(){
    float n=noise(world.xz*.7), fine=noise(world.xz*6.);
    vec3 paving=vec3(.60,.58,.52)*(.9+.12*n+.06*fine);
    float edge=min(UV.x,1.-UV.x);
    float kerb=smoothstep(.10,.13,edge)*(1.-smoothstep(.16,.19,edge))*step(.35,noise(world.xz*1.3));
    ALBEDO=mix(paving,vec3(.82,.68,.30),kerb*.75);
    ALPHA=smoothstep(.02,.2,edge+(n-.5)*.08)*.88;
    ROUGHNESS=.95;
}
