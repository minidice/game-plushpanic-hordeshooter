shader_type spatial;
render_mode diffuse_burley;
#include "res://shaders/woodland_surface.gdshaderinc"
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
uniform vec3 grass : source_color=vec3(0.424,0.502,0.325);
void fragment(){
    vec2 p=mat2(vec2(.8,-.6),vec2(.6,.8))*world.xz;
    float n=noise(p*.33)*.6+noise(p*.9)*.4;
    ALBEDO=woodland_grass(grass*(.93+.14*n),world.xz);
    ROUGHNESS=.97;
}
