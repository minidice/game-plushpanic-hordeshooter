shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform bool death = false;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform vec4 tint : source_color = vec4(1.0);

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float angle = atan(p.y, p.x) + 0.25;
    float count = death ? 8.0 : 6.0;
    float sector = 6.2831853 / count;
    float off = mod(angle + sector * 0.5, sector) - sector * 0.5;
    float travel = sqrt(progress);
    float start = mix(death ? 0.22 : 0.36, 0.74, travel);
    float finish = min(0.98, start + mix(0.25, 0.08, progress));
    float along = smoothstep(start, start + 0.035, r) * (1.0 - smoothstep(finish - 0.06, finish, r));
    float width = mix(0.027, 0.008, progress);
    float ray = (1.0 - smoothstep(width * 0.4, width, abs(sin(off)) * r)) * along;

    // Compact four-point impact, gone before the outward strokes finish. The body stays readable.
    float star_edge = 0.11 + 0.11 * pow(abs(cos(angle * 2.0)), 10.0);
    float star = (1.0 - smoothstep(star_edge * 0.65, star_edge, r)) * pow(1.0 - progress, 5.0);
    float ring = 0.0;
    if (death) {
        float ring_radius = mix(0.20, 0.88, travel);
        ring = (1.0 - smoothstep(0.012, 0.03, abs(r - ring_radius))) * 0.45;
        ring *= smoothstep(0.10, 0.40, abs(sin(angle * 3.0)));
    }
    float shape = max(ray, max(star, ring));
    ALBEDO = tint.rgb * (death ? 3.2 : 2.8);
    ALPHA = 0.9 * shape * pow(1.0 - progress, 1.5);
}
