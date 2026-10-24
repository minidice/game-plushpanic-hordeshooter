shader_type spatial;
render_mode cull_back, shadows_disabled;
uniform vec4 tint : source_color = vec4(1.0);
uniform vec4 accent : source_color = vec4(1.0);
uniform float clock = 0.0;
uniform float heat = 1.0;
instance uniform float visibility_alpha = 1.0;
uniform bool flame = false;
varying vec3 local;
void vertex() {
    local = VERTEX;
    float wobble = sin(VERTEX.x*7.0+clock*5.0)*sin(VERTEX.y*6.0-clock*3.0)*sin(VERTEX.z*8.0+1.5);
    VERTEX += NORMAL * wobble * 0.085;
    if (flame) {
        float h = clamp(local.y+0.5,0.0,1.0);
        VERTEX.xz *= 1.25-h*0.95;
        VERTEX.y += h*h*h*0.35;
        VERTEX.x += h*h*sin(clock*8.0+local.z*3.0)*0.18;
    }
}
void fragment() {
    float folds = sin(local.x*8.0+clock*2.0)*sin(local.z*7.0-clock)*0.12;
    float bright = smoothstep(-0.55,0.45,local.y+folds);
    vec3 col = heat > 0.5 ? mix(accent.rgb,tint.rgb,smoothstep(-0.4,0.3,local.y+folds))
        : mix(tint.rgb,accent.rgb,bright*0.4);
    ALBEDO = col;
    EMISSION = col * heat * (0.18+(1.0-bright)*0.25);
    ROUGHNESS = 0.85;
    ALPHA = visibility_alpha;
}
