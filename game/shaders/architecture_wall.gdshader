shader_type spatial;
render_mode diffuse_burley;
uniform bool along_x = true;
uniform int finish = 0;
uniform vec4 upper_color : source_color = vec4(0.53, 0.59, 0.59, 1.0);
uniform vec4 lower_color : source_color = vec4(0.22, 0.38, 0.37, 1.0);
varying vec3 world;
void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    float h = along_x ? world.x : world.z;
    vec2 uv = vec2(h, world.y);
    vec2 tile = uv / (finish == 1 ? vec2(1.1,0.5) : vec2(1.65,0.85));
    if (finish == 1) tile.x += mod(floor(tile.y),2.0)*0.5;
    vec2 e = min(fract(tile),1.0-fract(tile));
    float d = min(e.x,e.y);
    float seam = smoothstep(0.005,0.005+max(fwidth(d),0.002),d);
    if (finish == 2) seam = 0.96 + 0.04*sin(h*44.0);
    float grain = noise(uv*9.0)*0.04 + noise(uv*0.7)*0.07;
    float foot = smoothstep(0.0,0.45,world.y);
    vec3 base = mix(lower_color.rgb,upper_color.rgb,smoothstep(1.12,1.15,world.y));
    if (finish == 2) base = upper_color.rgb;
    ALBEDO = base * (0.93+grain) * mix(0.78,1.0,seam) * mix(0.79,1.0,foot);
    ROUGHNESS = finish == 2 ? 0.64 : 0.91;
}
