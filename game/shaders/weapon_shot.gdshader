shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform bool muzzle = false;
uniform float strength : hint_range(0.0, 1.0) = 1.0;
uniform float brightness = 2.4;
uniform vec4 edge_color : source_color = vec4(1.0, 0.48, 0.055, 1.0);
uniform vec4 core_color : source_color = vec4(1.0, 0.96, 0.78, 1.0);

void fragment() {
    float x = UV.x;
    float y = abs(UV.y * 2.0 - 1.0);
    float width;
    float envelope;
    if (muzzle) {
        // A forward spear with two short side tongues, not a round fireball.
        float spear = 0.35 * pow(max(0.0, 1.0 - x), 1.3);
        float wings = 0.9 * max(0.0, 1.0 - abs(x - 0.28) / 0.18);
        width = max(spear, wings);
        envelope = smoothstep(0.0, 0.07, x) * (1.0 - smoothstep(0.75, 1.0, x));
    } else {
        // Fine fading tail and a hot, compact leading edge.
        width = mix(0.12, 0.8, pow(x, 0.55));
        envelope = smoothstep(0.0, 0.22, x) * (1.0 - smoothstep(0.94, 1.0, x));
    }
    float edge = 1.0 - smoothstep(width * 0.65, max(width, 0.001), y);
    float core = 1.0 - smoothstep(0.0, max(width * 0.5, 0.001), y);
    ALBEDO = mix(edge_color.rgb, core_color.rgb, core) * brightness;
    ALPHA = edge * envelope * strength;
}
