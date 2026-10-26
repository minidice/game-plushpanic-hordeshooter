shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;
uniform sampler2D flow : repeat_enable, filter_linear_mipmap;
uniform sampler2D detail : repeat_enable, filter_linear_mipmap;
uniform sampler2D scorch : source_color, filter_linear_mipmap;
uniform vec4 color : source_color;
uniform vec4 accent : source_color;
uniform float clock = 0.0;
uniform float active = 1.0;
uniform float residue = 0.0;
uniform bool fire = true;
void fragment() {
    float r = length(UV * 2.0 - 1.0);
    float n = texture(flow, UV * 1.8 + vec2(clock * .025, -clock * .018)).r;
    float d = texture(detail, UV * 3.0 + vec2(-clock * .035, clock * .022)).r;
    float edge = 1.0 - smoothstep(.75 + n * .18, 1.0, r);
    float wisps = smoothstep(.43, .69, n * .7 + d * .3);
    // A faint continuous edge conveys the full damaging radius immediately.
    float boundary = smoothstep(.955, .975, r) * (1.0 - smoothstep(.985, 1.0, r));
    vec4 charred = texture(scorch, UV);
    float hot = active * (edge * (.035 + wisps * .24) + boundary * .32);
    float smoke = active * edge * wisps * .14;
    float ash = charred.a * residue * .42;
    ALBEDO = fire ? mix(charred.rgb, mix(color.rgb, accent.rgb, wisps * .6), step(.001, active)) : color.rgb;
    EMISSION = fire ? mix(color.rgb, accent.rgb, wisps) * hot * .65 : vec3(0.0);
    ALPHA = fire ? max(hot, ash) : smoke;
}
