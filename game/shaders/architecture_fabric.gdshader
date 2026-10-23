shader_type spatial;
render_mode diffuse_burley, cull_disabled;
uniform vec4 fabric_color : source_color = vec4(0.16,0.29,0.37,1.0);
void fragment() {
    float fold = sin(UV.x*24.0+sin(UV.y*3.0))*0.035;
    float hem = smoothstep(0.0,0.025,min(min(UV.x,1.0-UV.x),min(UV.y,1.0-UV.y)));
    ALBEDO=fabric_color.rgb*(0.96+fold)*mix(0.65,1.0,hem);
    ROUGHNESS=0.97;
}
