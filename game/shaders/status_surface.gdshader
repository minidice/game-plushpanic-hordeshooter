shader_type spatial;
render_mode cull_back, depth_draw_never, shadows_disabled;
#include "res://shaders/character_grip_deform.gdshaderinc"
uniform vec4 tint : source_color = vec4(1.0);
uniform float strength = 0.0;
uniform int pattern = 0;
uniform float clock = 0.0;
uniform vec3 actor_origin = vec3(0.0);
uniform vec3 actor_up = vec3(0.0,1.0,0.0);
uniform float actor_height = 1.5;
varying vec3 local_pos;
varying float height_fraction;
void vertex() {
    deform_grip(VERTEX, NORMAL);
    VERTEX += NORMAL * 0.0015;
    local_pos = VERTEX;
    height_fraction = dot((MODEL_MATRIX*vec4(VERTEX,1.0)).xyz-actor_origin,actor_up)/max(actor_height,0.05);
}
void fragment() {
    float n = sin(local_pos.x*19.0+sin(local_pos.z*13.0))*sin(local_pos.y*14.0+local_pos.z*17.0);
    float spots = smoothstep(0.1,0.65,n);
    float rim = pow(1.0-max(0.0,dot(NORMAL,VIEW)),2.0);
    float mask = pattern==4 ? max(spots*0.7,rim) : spots;
    // Keep the top/face clear, and leave most original fabric visible.
    mask *= 1.0-smoothstep(0.65,0.9,height_fraction);
    ALBEDO = tint.rgb;
    ROUGHNESS = pattern==4 ? 0.38 : 0.9;
    EMISSION = tint.rgb * (pattern==4 ? 0.12 : 0.0);
    ALPHA = mask*strength*(0.92+0.08*sin(clock*2.0));
}
