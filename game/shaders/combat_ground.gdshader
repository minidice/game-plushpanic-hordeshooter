shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 tint : source_color = vec4(1.0);
uniform vec4 accent : source_color = vec4(1.0);
uniform float clock = 0.0;
uniform float fade = 1.0;
// 0 shock ring, 1 fire, 2 poison; radius always matches the caller's gameplay radius.
uniform int mode = 0;
float hash(vec2 p) {return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p) {
    vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    vec2 p = UV*2.0-1.0;
    float r = length(p);
    float a = atan(p.y,p.x);
    vec2 drift = p*5.0+vec2(clock*0.13,-clock*0.09);
    float wave = noise(drift+vec2(noise(p*4.0+clock*0.1),noise(p*3.0-clock*0.12))*1.7);
    float edge = 0.98 + sin(a*11.0+clock)*0.012;
    float inside = 1.0-smoothstep(edge-0.025,edge,r);
    float boundary = smoothstep(0.92,0.965,r)*inside;
    float alpha;
    vec3 col;
    if (mode == 0) {
        float ring = min(0.96,0.13+sqrt(clamp(clock/0.38,0.0,1.0))*0.82);
        alpha = (1.0-smoothstep(0.015,0.05,abs(r-ring)))*max(0.0,1.0-clock/0.42);
        col = accent.rgb*1.3;
    } else {
        float veins = 1.0-smoothstep(0.018,0.065,abs(wave-0.53));
        col = mix(tint.rgb,accent.rgb,veins*0.38+boundary*0.45);
        alpha = mode == 1 ? inside*(0.025+veins*0.075)+boundary*0.35 : inside*(0.56+veins*0.08)+boundary*0.22;
    }
    ALBEDO = col;
    ALPHA = alpha*fade;
}
