shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
#include "res://shaders/forest_patch.gdshaderinc"
void fragment(){
    float n=noise(world.xz*.8), fine=noise(world.xz*7.);
    vec3 dirt=vec3(.43,.32,.20);
    vec3 service=vec3(.15,.16,.14);
    ALBEDO=mix(dirt,service,smoothstep(245.,266.,world.x))*(.88+.17*n+.08*fine);
    float edge=min(UV.x,1.-UV.x);
    ALPHA=smoothstep(.015,.22,edge+(n-.5)*.12)*.92;
    ALPHA*=1.-forest_weight(world.xz);
    ROUGHNESS=.98;
}
