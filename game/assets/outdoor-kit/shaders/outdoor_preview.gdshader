shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Demonstration material, assigned per surface/instance. GLB source stays opaque.
uniform vec4 base_color : source_color = vec4(0.5, 0.6, 0.4, 1.0);
uniform float base_roughness = 0.8;
uniform float base_metallic = 0.0;
uniform float fade : hint_range(0.0, 1.0) = 0.0;
uniform float wetness : hint_range(0.0, 1.0) = 0.0;
uniform float snow : hint_range(0.0, 1.0) = 0.0;
uniform bool use_vertex_color = false;
uniform bool weather_eligible = true;
varying vec3 world_normal;

void vertex() {
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
    float snow_mask = weather_eligible ? smoothstep(0.35, 0.85, normalize(world_normal).y) * snow : 0.0;
    vec3 surface = base_color.rgb * (use_vertex_color ? COLOR.rgb : vec3(1.0)) * (1.0 - wetness * 0.23);
    surface = mix(surface, vec3(0.84, 0.90, 0.92), snow_mask);
    float edge = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 2.0);
    // A fixed ordered pattern avoids alpha sorting between overlapping crowns.
    int x = int(FRAGCOORD.x) % 4;
    int y = int(FRAGCOORD.y) % 4;
    float pattern[16] = float[16](0.0,8.0,2.0,10.0,12.0,4.0,14.0,6.0,3.0,11.0,1.0,9.0,15.0,7.0,13.0,5.0);
    float coverage = mix(1.0, 0.12 + edge * 0.32, fade);
    // Full shadow is intentional for this visual-only preview. Runtime policy is deferred.
    if (!IN_SHADOW_PASS && (pattern[y * 4 + x] + 0.5) / 16.0 > coverage) { discard; }
    ALBEDO = mix(surface, vec3(0.48,0.64,0.59), fade * 0.80);
    ROUGHNESS = mix(mix(base_roughness, 0.25, wetness), 0.94, snow_mask);
    METALLIC = base_metallic * (1.0 - snow_mask);
    EMISSION = vec3(0.12,0.20,0.17) * fade * (0.25 + edge);
}
