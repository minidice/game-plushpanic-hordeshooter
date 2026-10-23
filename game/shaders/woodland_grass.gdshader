shader_type spatial;
render_mode cull_disabled, diffuse_burley;
void fragment() {
    // A stylized upward lighting normal on both blade faces avoids black backfaces
    // when the camera sees the underside of a two-sided bent blade.
    NORMAL=normalize((VIEW_MATRIX*vec4(0.0,1.0,0.0,0.0)).xyz);
    ALBEDO=COLOR.rgb;
    ROUGHNESS=1.0;
    SPECULAR=0.05;
}
