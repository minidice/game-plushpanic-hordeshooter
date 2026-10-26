shader_type particles;
render_mode disable_force, disable_velocity;

uniform float root_radius = 1.0;
uniform int root_count = 24;
uniform float card_height = 1.0;
uniform float clock = 0.0;
uniform float active_lifetime = 5.0;
uniform float tail_seconds = 2.0;

float variation(float i) {
    return fract(sin(i * 127.1 + 311.7) * 43758.5453);
}

void pose(uint index, inout mat4 transform, inout vec4 color, inout vec4 custom) {
    // INDEX identifies the persistent slot; NUMBER would change on each respawn.
    float i = float(index);
    float phase = variation(i + 2.0) * 6.2831853;
    float angle = i * 2.399963 + (variation(i + 7.0) - .5) * .7;
    float radial = sqrt((i + .5) / float(root_count));
    float radius = radial * root_radius;
    float pace = min(1.0, active_lifetime / 2.0);
    float ignition_at = max(0.0, radial - sqrt(.5 / float(root_count))) * .75 * pace;
    float ignite = smoothstep(ignition_at, ignition_at + .48 * pace, clock);
    float fade_end = active_lifetime + min(.8, tail_seconds) * (.3 + .7 * variation(i + 21.0));
    float fade_begin = max(0.0, active_lifetime - .65 * pace + .2 * pace * variation(i + 25.0));
    float burn = ignite * (1.0 - smoothstep(fade_begin, max(fade_begin + .01, fade_end), clock));
    float size = mix(.65, 1.1, variation(i + 11.0));
    float pulse = 1.0 + .12 * sin(clock * (3.8 + variation(i)) + phase);
    float height = size * pulse * mix(.18, 1.0, sqrt(burn));
    // Height breathes around the bottom of the card, not its centre.
    transform = mat4(vec4(size, 0.0, 0.0, 0.0),
        vec4(0.0, height, 0.0, 0.0), vec4(0.0, 0.0, size, 0.0),
        vec4(cos(angle) * radius, card_height * .5 * (height - 1.0), sin(angle) * radius, 1.0));
    color = vec4(1.0, 1.0, 1.0, (.54 + .07 * sin(clock * 4.7 + phase)) * burn);
    // Existing particle billboard material reads z as the atlas phase.
    custom = vec4(0.0, 0.0, fract(clock * mix(.65, 1.0, variation(i + 5.0)) + phase), 1.0);
}

void start() {
    pose(INDEX, TRANSFORM, COLOR, CUSTOM);
    VELOCITY = vec3(0.0);
}
void process() {
    pose(INDEX, TRANSFORM, COLOR, CUSTOM);
    VELOCITY = vec3(0.0);
}
