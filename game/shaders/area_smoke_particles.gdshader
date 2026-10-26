shader_type particles;
render_mode disable_force, disable_velocity;
uniform float root_radius = 1.0;
uniform int root_count = 20;
uniform float card_height = 1.0;
uniform float clock = 0.0;
uniform float active_lifetime = 5.0;
uniform float tail_seconds = 0.0;

float variation(float i) { return fract(sin(i * 127.1 + 911.7) * 43758.5453); }

void pose(uint index, inout mat4 transform, inout vec4 color, inout vec4 custom) {
    float i = float(index);
    float phase = variation(i + 2.0) * 6.2831853;
    float radial = sqrt((i + .5) / float(root_count));
    float pace = min(1.0, active_lifetime / 2.0);
    float begin = max(0.0, radial - sqrt(.5 / float(root_count))) * .9 * pace;
    float appear = smoothstep(begin, begin + .65 * pace, clock);
    float spread = smoothstep(0.0, 1.25 * pace, clock);
    float angle = i * 2.399963 + (variation(i + 7.0) - .5) * .8;
    float radius = radial * root_radius * mix(.12, 1.0, spread);
    float end = tail_seconds > 0.0 ? active_lifetime + tail_seconds * (.55 + .4 * variation(i + 31.0))
        : active_lifetime - min(.4, active_lifetime * .1) * variation(i + 31.0);
    float fade_time = min(1.15 + .65 * variation(i + 37.0), active_lifetime * .45 + tail_seconds * .5);
    float dissolve = 1.0 - smoothstep(max(0.0, end - fade_time), end, clock);
    float size = mix(.7, 1.2, variation(i + 11.0)) * mix(.62, 1.0, spread);
    size *= 1.0 + .06 * sin(clock * .7 + phase);
    vec3 at = vec3(cos(angle) * radius, .07 * sin(clock * .8 + phase), sin(angle) * radius);
    at.xz += vec2(sin(clock * .5 + phase), cos(clock * .4 + phase)) * root_radius * .035 * spread;
    transform = mat4(vec4(size, 0.0, 0.0, 0.0), vec4(0.0, size, 0.0, 0.0),
        vec4(0.0, 0.0, size, 0.0), vec4(at, 1.0));
    color = vec4(1.0, 1.0, 1.0, appear * dissolve * (.58 + .08 * sin(clock * .8 + phase)));
    custom = vec4(.15 * sin(clock * .3 + phase), 0.0, (mod(i, 4.0) + .5) / 4.0, 1.0);
}
void start() { pose(INDEX, TRANSFORM, COLOR, CUSTOM); VELOCITY = vec3(0.0); }
void process() { pose(INDEX, TRANSFORM, COLOR, CUSTOM); VELOCITY = vec3(0.0); }
