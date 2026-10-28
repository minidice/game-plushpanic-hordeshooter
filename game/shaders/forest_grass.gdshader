shader_type spatial;
render_mode cull_disabled, diffuse_burley;
#include "res://shaders/graphics_surface_quality.gdshaderinc"
uniform bool forest_surface_quality = false;
varying vec4 tint;
varying float surface_weight;
float grass_surface_weight(vec2 p) {
    if(!forest_surface_quality)return 0.;
    return smoothstep(64.,68.,p.x)*(1.-smoothstep(104.,108.,p.x))
        *smoothstep(-42.,-38.,p.y)*(1.-smoothstep(-6.,-2.,p.y));
}
void vertex() {
    tint=COLOR;
    vec3 at=(MODEL_MATRIX*vec4(VERTEX,1.)).xyz;
    vec2 root=(MODEL_MATRIX*vec4(0.,0.,0.,1.)).xz;
    surface_weight=grass_surface_weight(root);
    float bend=VERTEX.y*VERTEX.y;
    float original_wind=0.;
    if(!graphics_surface_low)original_wind=sin(TIME*1.4+at.x*.7+at.z*.6)*.10*bend;
    if(surface_weight>0.) {
        // Retain authored blade colours; a broad wash avoids high-frequency shimmer.
        float tip=smoothstep(.015,.36,VERTEX.y);
        float patch=.5+.5*sin(root.x*.37+sin(root.y*.43));
        vec3 blade=tint.rgb*mix(vec3(.80,.86,.72),vec3(1.10,1.04,.87),tip);
        blade*=mix(.94,1.04,patch);
        tint.rgb=mix(tint.rgb,blade,surface_weight);
        // Shared slow gust, with zero motion at every blade root.
        if(!graphics_surface_low) {
            float gust=sin(TIME*1.15+root.x*.36+root.y*.29);
            VERTEX.x+=mix(original_wind,gust*.12*bend,surface_weight);
            VERTEX.z+=cos(TIME*.83+root.y*.31-root.x*.21)*.055*bend*surface_weight;
        }
    } else {
        VERTEX.x+=original_wind;
    }
}
void fragment() {
    ALBEDO=tint.rgb;
    ROUGHNESS=mix(.95,.98,surface_weight);
    SPECULAR=mix(.12,.08,surface_weight);
}
