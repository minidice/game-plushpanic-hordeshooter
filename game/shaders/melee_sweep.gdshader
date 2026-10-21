shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform float age = 0.0;

void fragment() {
    // UV.x follows the hand from its right-side windup to its left-side follow-through.
    float head = mix(0.5, 1.05, clamp(age / 0.06, 0.0, 1.0));
    float behind = head - UV.x;
    float sweep = smoothstep(0.0, 0.08, behind) * (1.0 - smoothstep(0.25, 0.65, behind));
    float edge = smoothstep(0.0, 0.25, UV.y) * (1.0 - smoothstep(0.85, 1.0, UV.y));
    float core = smoothstep(0.35, 0.72, UV.y);
    float fade = pow(max(0.0, 1.0 - age / 0.16), 1.4);
    ALBEDO = mix(vec3(0.12, 0.6, 1.0), vec3(0.85, 0.98, 1.0), core) * 2.8;
    ALPHA = sweep * edge * fade;
}
