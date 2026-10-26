shader_type particles;
render_mode disable_force, disable_velocity;

uniform float root_radius = 1.0;
uniform int root_count = 24;
uniform float card_height = 1.0;
uniform float clock = 0.0;

float variation(float i) {
    return fract(sin(i * 127.1 + 311.7) * 43758.5453);
}

void pose(uint index, inout mat4 transform, inout vec4 color, inout vec4 custom) {
    // INDEX identifies the persistent slot; NUMBER would change on each respawn.
    float i = float(index);
    float phase = variation(i + 2.0) * 6.2831853;
    float angle = i * 2.399963 + (variation(i + 7.0) - .5) * .7;
    float radius = sqrt((i + .5) / float(root_count)) * root_radius;
    float size = mix(.65, 1.1, variation(i + 11.0));
    float pulse = 1.0 + .12 * sin(clock * (3.8 + variation(i)) + phase);
    float height = size * pulse;
    // Height breathes around the bottom of the card, not its centre.
    transform = mat4(vec4(size, 0.0, 0.0, 0.0),
        vec4(0.0, height, 0.0, 0.0), vec4(0.0, 0.0, size, 0.0),
        vec4(cos(angle) * radius, card_height * .5 * (height - 1.0), sin(angle) * radius, 1.0));
    color = vec4(1.0, 1.0, 1.0, .54 + .07 * sin(clock * 4.7 + phase));
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
