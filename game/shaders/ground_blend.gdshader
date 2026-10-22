shader_type spatial;
// Outdoor ground in one pass (decisions.md A-24): a baked blend map says where dirt, asphalt and a gravel shoulder lie
// (R dirt, G asphalt, B gravel); small world-space noise breaks the edges finer than the map, and speckles stand in
// for the kit tiles' pebbles. Flat toy colours from the outdoor kit palette, no textures.
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D blend : filter_linear, repeat_disable;
// World rectangle the blend map covers: x0, z0, width, depth.
uniform vec4 area;
uniform vec3 grass_color : source_color = vec3(0.424, 0.502, 0.325);
uniform vec3 dirt_color : source_color = vec3(0.588, 0.463, 0.325);
uniform vec3 asphalt_color : source_color = vec3(0.282, 0.337, 0.361);
uniform vec3 gravel_color : source_color = vec3(0.553, 0.565, 0.522);
uniform vec3 paint_color : source_color = vec3(0.835, 0.788, 0.635);
uniform vec3 speck_color : source_color = vec3(0.400, 0.478, 0.486);
// One straight dashed centre line: x0, x1, z, half width (0 = none).
uniform vec4 lane = vec4(0.0);

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Value noise; callers turn the domain (ROT) so its grid does not read as square tone patches.
const mat2 ROT = mat2(vec2(0.8, -0.6), vec2(0.6, 0.8));

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 p = world_pos.xz;
	vec4 w = texture(blend, (p - area.xy) / area.zw);
	float n = vnoise(p * 1.7) * 0.6 + vnoise(p * 4.3) * 0.4 - 0.5;
	// Dirt frays wide, gravel a little, asphalt keeps a firm edge.
	float dirt = smoothstep(0.32, 0.68, w.r + n * 0.38);
	float gravel = smoothstep(0.30, 0.70, w.b + n * 0.45);
	float asphalt = smoothstep(0.45, 0.55, w.g + n * 0.10);
	vec2 q = ROT * p;
	float patch = 0.93 + 0.14 * (vnoise(q * 0.33) * 0.6 + vnoise(ROT * q * 0.9) * 0.4);
	vec3 col = grass_color * patch;
	col = mix(col, gravel_color * (0.94 + 0.12 * vnoise(q * 2.1)), gravel);
	col = mix(col, dirt_color * (0.95 + 0.10 * (vnoise(q * 0.45 + 7.0) * 0.6 + vnoise(ROT * q * 1.3) * 0.4)), dirt);
	col = mix(col, asphalt_color * (0.96 + 0.08 * vnoise(q * 1.3 + 3.0)), asphalt);
	// Centre line: dashes 1 m long every 1.8 m.
	if (lane.w > 0.0 && p.x > lane.x && p.x < lane.y) {
		float across = abs(p.y - lane.z);
		float dash = step(fract((p.x - lane.x) / 1.8), 0.56);
		col = mix(col, paint_color, (1.0 - smoothstep(lane.w * 0.8, lane.w, across)) * dash * asphalt);
	}
	// Pebble speckles on grass and dirt (not on asphalt).
	vec2 cell = floor(p * 3.0);
	vec2 off = fract(p * 3.0) - vec2(hash(cell + 3.1), hash(cell + 7.7));
	float speck = step(hash(cell), 0.16) * (1.0 - smoothstep(0.05, 0.09, length(off)));
	col = mix(col, speck_color, speck * 0.75 * (1.0 - asphalt));
	ALBEDO = col;
	ROUGHNESS = mix(0.96, 0.9, asphalt);
}
