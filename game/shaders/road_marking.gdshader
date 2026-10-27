// The road's dashed centre line (decisions.md A-30): faded off-white paint, worn through in patches.
shader_type spatial;
render_mode depth_draw_opaque, cull_disabled;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
void fragment(){
    float wear=noise(world.xz*2.2)*.7+noise(world.xz*9.)*.3;
    ALBEDO=vec3(.84,.82,.72);
    ALPHA=smoothstep(.28,.45,wear)*.85;
    ROUGHNESS=.9;
}
