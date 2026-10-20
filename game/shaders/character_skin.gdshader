shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Analytic region masks: no UV seams, vertex-color interpolation or repainting on color edits.
// Coordinates stay in the authored skin space, so body scaling and corpse poses carry the paint.
uniform vec4 primary_color : source_color = vec4(1.0);
uniform vec4 secondary_color : source_color = vec4(1.0);
uniform vec4 marking_color : source_color = vec4(1.0);
uniform int species = 0; // 0 bird, 1 raccoon, 2 red panda
uniform float body_height = 1.62;
uniform float marking_softness : hint_range(0.0,1.0) = 0.25;
uniform float eye_spacing = 1.0;
varying vec3 skin_position;

void vertex() { skin_position = VERTEX; }

float oval_distance(vec2 p,vec2 center,vec2 radius) {
    return (length((p-center)/radius)-1.0)*min(radius.x,radius.y);
}
float smooth_union(float a,float b,float k) {
    float h=max(k-abs(a-b),0.0)/k;
    return min(a,b)-h*h*k*0.25;
}
float fill(float distance_to_edge) {
    float edge=max(0.002+0.022*marking_softness,fwidth(distance_to_edge)*0.65);
    return 1.0-smoothstep(-edge,edge,distance_to_edge);
}

void fragment() {
    float t = clamp((skin_position.y-0.22)/body_height,0.0,1.0);
    float y = 2.0*t-1.0;
    float radius = 0.47*pow(sqrt(max(0.0,1.0-y*y)),0.62)*(1.08-0.14*t);
    float front = smoothstep(0.2,0.7,skin_position.z/max(0.00001,0.86*radius));
    float belly = front*(1.0-smoothstep(0.48,0.66,t))*0.65;
    vec3 color = mix(primary_color.rgb*(0.88+0.12*t),secondary_color.rgb,belly);
    if (species > 0) {
        vec2 p = vec2(abs(skin_position.x)/eye_spacing,skin_position.y-1.34);
        float cream, dark;
        float face_front = smoothstep(0.02,0.35,skin_position.z/max(0.00001,0.86*radius));
        if (species == 1) {
            cream = fill(oval_distance(p,vec2(0.0,-0.045),vec2(0.445,0.26)));
            // One upswept eye band, narrow over the bridge and tapered at the temples.
            float center=0.045+0.070*smoothstep(0.08,0.38,p.x);
            float thick=0.042+0.050*smoothstep(0.035,0.17,p.x);
            float vertical=(p.y-center)/thick;
            float q=pow(p.x/0.445,6.0)+vertical*vertical;
            dark=fill((sqrt(q)-1.0)*0.065);
        } else {
            // Cheeks flow into the muzzle. Small eyebrow spots and downward tear markings
            // distinguish a red panda from a raccoon's horizontal black band.
            float cheek=smooth_union(oval_distance(p,vec2(0.285,-0.075),vec2(0.18,0.175)),oval_distance(p,vec2(0.0,-0.155),vec2(0.31,0.105)),0.075);
            cream=fill(cheek);
            float tear_x=0.20+0.035*clamp((0.06-p.y)/0.22,0.0,1.0);
            dark=fill(oval_distance(p,vec2(tear_x,-0.025),vec2(0.032,0.100)));
        }
        color=mix(color,species==1?secondary_color.rgb:marking_color.rgb,cream*face_front);
        color=mix(color,species==1?marking_color.rgb:mix(primary_color.rgb,secondary_color.rgb,0.50),dark*face_front);
    }
    ALBEDO = color;
    ROUGHNESS = 0.86;
    SPECULAR = 0.22;
}
