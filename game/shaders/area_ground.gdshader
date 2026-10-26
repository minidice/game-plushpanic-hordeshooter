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
    // Feather noisy heat into the ground; no continuous circle or bright rim.
    // The final mask still contains the heat inside the actual hazard radius.
    float heat_edge = (1.0 - smoothstep(.73, .99, r + (n - .5) * .14))
        * (1.0 - smoothstep(.97, 1.0, r));
    float heat = smoothstep(.30, .73, n * .7 + d * .3);
    vec4 charred = texture(scorch, UV);
    float hot = active * heat_edge * (.018 + heat * .25);
    float smoke = active * edge * wisps * .14;
    float ash = charred.a * residue * .42;
    ALBEDO = fire ? mix(charred.rgb, mix(color.rgb, accent.rgb, wisps * .6), step(.001, active)) : color.rgb;
    EMISSION = fire ? mix(color.rgb, accent.rgb, wisps) * hot * .65 : vec3(0.0);
    ALPHA = fire ? max(hot, ash) : smoke;
}
