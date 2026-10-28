shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
varying vec4 shadow_tint;
void vertex(){shadow_tint=COLOR;}
void fragment(){
    float radius=length((UV-.5)*2.);
    ALBEDO=vec3(.035,.045,.055);
    ALPHA=(1.-smoothstep(shadow_tint.r,1.,radius))*shadow_tint.a;
}
