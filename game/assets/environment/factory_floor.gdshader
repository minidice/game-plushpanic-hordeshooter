shader_type spatial;
render_mode diffuse_burley;

uniform vec4 tile_color : source_color = vec4(0.18, 0.24, 0.32, 1.0);
uniform float tile_size = 1.0;
varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 grid = world_position.xz / tile_size;
    vec2 cell = floor(grid);
    vec2 edge = min(fract(grid), 1.0 - fract(grid));
    float distance_to_edge = min(edge.x, edge.y);
    float aa = max(fwidth(distance_to_edge), 0.001);
    float grout = smoothstep(0.012 - aa, 0.012 + aa, distance_to_edge);
    float bevel = smoothstep(0.018, 0.035, distance_to_edge);
    float variation = fract(sin(dot(cell, vec2(12.9898, 78.233))) * 43758.5453);
    vec3 tile = tile_color.rgb * (0.91 + variation * 0.18);
    tile += (1.0 - bevel) * 0.025;
    ALBEDO = mix(tile_color.rgb * 0.35, tile, grout);
    ROUGHNESS = 0.74 + variation * 0.12;
    METALLIC = 0.08;
}
