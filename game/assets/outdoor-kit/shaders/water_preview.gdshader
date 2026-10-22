shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Opaque, vertex-coloured art study. Banks conceal the water mesh edge.
// This is not a depth/refraction, hydrology or weather system.
uniform float preview_time = 0.0;
uniform float wetness = 0.0;
uniform float snow = 0.0;
uniform float fade = 0.0;
varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    vec2 p = world_position.xz;
    float a = dot(p, vec2(2.2, 5.1)) + preview_time * 1.2;
    float b = dot(p, vec2(-5.4, 3.6)) - preview_time * 0.8;
    vec3 ripple_normal = normalize(vec3(0.07 * cos(a), 1.0, 0.06 * sin(b)));
    NORMAL = normalize((VIEW_MATRIX * vec4(ripple_normal, 0.0)).xyz);
    float glint = smoothstep(0.76, 0.89, noise(p * vec2(6.0, 14.0) + vec2(preview_time * 0.16, preview_time * 0.25))) * smoothstep(0.3, 0.65, noise(p * 1.4));
    ALBEDO = COLOR.rgb * (0.92 + 0.08 * sin(a + b)) + vec3(0.11, 0.15, 0.13) * glint;
    ROUGHNESS = 0.22;
    METALLIC = 0.12;
    SPECULAR = 0.65;
}
