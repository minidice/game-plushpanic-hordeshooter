shader_type spatial;
render_mode diffuse_burley;

uniform bool along_x = true;
varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    float horizontal = along_x ? world_position.x : world_position.z;
    vec2 cell = vec2(horizontal / 1.25, world_position.y / 0.72);
    vec2 edge = min(fract(cell), 1.0 - fract(cell));
    float d = min(edge.x, edge.y);
    float aa = max(fwidth(d), 0.001);
    float seam = smoothstep(0.008 - aa, 0.008 + aa, d);
    vec3 upper = vec3(0.53, 0.60, 0.61);
    vec3 lower = vec3(0.12, 0.34, 0.35);
    vec3 paint = mix(lower, upper, step(1.18, world_position.y));
    float variation = fract(sin(floor(horizontal / 1.25) * 12.9898) * 43758.5453);
    ALBEDO = paint * (0.94 + 0.09 * variation) * mix(0.68, 1.0, seam);
    ROUGHNESS = 0.82;
}
