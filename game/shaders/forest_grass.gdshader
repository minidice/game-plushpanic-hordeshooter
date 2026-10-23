shader_type spatial;
render_mode cull_disabled, diffuse_burley;
varying vec4 tint;
void vertex() {
    tint=COLOR;
    vec3 at=(MODEL_MATRIX*vec4(VERTEX,1.)).xyz;
    float bend=VERTEX.y*VERTEX.y;
    VERTEX.x+=sin(TIME*1.4+at.x*.7+at.z*.6)*.10*bend;
}
void fragment() {
    ALBEDO=tint.rgb;
    ROUGHNESS=.95;
    SPECULAR=.12;
}
