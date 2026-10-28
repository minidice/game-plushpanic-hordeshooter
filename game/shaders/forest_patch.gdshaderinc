#include "res://shaders/graphics_surface_quality.gdshaderinc"
uniform bool forest_enabled = false;
uniform bool forest_expanded = false;
uniform bool forest_surface_quality = false;
uniform int forest_route_count = 0;
uniform vec4 forest_routes[32];
float forest_surface_weight(vec2 p) {
    if(!forest_surface_quality)return 0.;
    return smoothstep(64.,68.,p.x)*(1.-smoothstep(104.,108.,p.x))
        *smoothstep(-42.,-38.,p.y)*(1.-smoothstep(-6.,-2.,p.y));
}
float forest_weight(vec2 p) {
    if(!forest_enabled)return 0.;
    if(forest_expanded) {
        float west=smoothstep(32.,46.,p.x)*(1.-smoothstep(166.,192.,p.x));
        float east=smoothstep(208.,224.,p.x)*(1.-smoothstep(248.,282.,p.x));
        return max(west,east);
    }
    vec2 d=abs(p-vec2(94.,-24.));
    return forest_enabled?smoothstep(0.,3.,10.-max(d.x,d.y)):0.;
}
float forest_path_distance(vec2 p) {
    if(forest_expanded) {
        float best=1000.;
        for(int i=0;i<forest_route_count;i++) {
            vec2 a=forest_routes[i].xy,b=forest_routes[i].zw;
            best=min(best,distance(p,a+(b-a)*clamp(dot(p-a,b-a)/dot(b-a,b-a),0.,1.)));
        }
        return best;
    }
    vec2 a=vec2(60.,-6.),b=vec2(110.,-30.);
    return distance(p,a+(b-a)*clamp(dot(p-a,b-a)/dot(b-a,b-a),0.,1.));
}
vec3 forest_surface_ground(vec2 p) {
    // Broad, turned colour patches read as earth at the play camera, without sand-like grain.
    vec2 q=vec2(.8*p.x-.6*p.y,.6*p.x+.8*p.y);
    float broad=noise(q*.16),earth=noise(q*.52+vec2(7.,19.));
    float broken=noise(q*.9+vec2(21.,3.));
    float path_distance=forest_path_distance(p);
    // Retain the old path envelope; only its shoulder extends into the grass.
    float road=1.-smoothstep(1.7,3.9,path_distance+(noise(p*1.2)-.5)*.9);
    float shoulder=1.-smoothstep(3.,5.,path_distance+(broken-.5)*1.2);
    float worn=1.-smoothstep(1.1,2.5,path_distance+(earth-.5)*.5);
    vec3 olive=mix(vec3(.118,.145,.054),vec3(.183,.204,.080),broad);
    vec3 soil=mix(vec3(.170,.128,.073),vec3(.220,.171,.098),broad*.65+earth*.35);
    vec3 ground=mix(olive,soil*.83,shoulder*smoothstep(.28,.70,broken)*.65);
    ground=mix(ground,soil,road);
    vec3 center=mix(vec3(.222,.168,.098),vec3(.262,.204,.123),broad);
    ground=mix(ground,center,worn*.86);
    ground*=.97+.06*earth;
    // Keep the authored path/shoulder and broad earth colours, omit sub-pixel detail.
    if(graphics_surface_low) {
        float damp=smoothstep(.42,.76,earth*.65+broken*.35);
        return ground*(1.-.09*damp*mix(.5,1.,shoulder)*(1.-worn*.4));
    }
    // Soil retains a visible aggregate scale; suppress it as pixels outgrow the grain.
    float pixel=max(length(fwidth(p)),.001);
    float grain=noise(q*5.5+vec2(13.,29.))-.5;
    float grain_visible=1.-smoothstep(.45,1.,pixel*5.5);
    ground*=1.+grain*.12*grain_visible*mix(.65,1.,shoulder);
    float damp=smoothstep(.42,.76,earth*.65+broken*.35);
    ground*=1.-.09*damp*mix(.5,1.,shoulder)*(1.-worn*.4);
    // Occasional small stones, filtered in world units and faded below a pixel.
    vec2 cell=floor(p*1.3);
    vec2 offset=vec2(.2)+.6*vec2(hash(cell+vec2(4.,8.)),hash(cell+vec2(17.,2.)));
    float radius=mix(.035,.065,hash(cell+vec2(11.,6.)));
    float pebble=1.-smoothstep(radius-pixel*.5,radius+pixel*.5,length(fract(p*1.3)-offset)/1.3);
    pebble*=step(.94,hash(cell))* (1.-smoothstep(radius,radius*3.,pixel));
    vec3 stone=mix(vec3(.183,.171,.127),vec3(.265,.247,.184),earth);
    return mix(ground,stone,pebble*shoulder*.55);
}
vec3 forest_ground(vec2 p) {
    float quality=forest_surface_weight(p);
    if(quality>=1.)return forest_surface_ground(p);
    float broad=noise(p*.27);
    float road=1.-smoothstep(1.7,3.9,forest_path_distance(p)+(noise(p*1.2)-.5)*.9);
    vec3 moss=mix(vec3(.115,.137,.052),vec3(.185,.192,.078),broad);
    vec3 soil=mix(vec3(.175,.130,.078),vec3(.225,.174,.102),broad);
    vec3 ground=mix(moss,soil,road);
    if(graphics_surface_low) {
        ground*=.99;
        if(quality<=0.)return ground;
        return mix(ground,forest_surface_ground(p),quality);
    }
    float detail=noise(p*5.),fine=noise(p*31.);
    ground*=.89+.13*detail+.07*fine;
    ground+=vec3(.035,.029,.018)*smoothstep(.76,.92,fine);
    if(quality<=0.)return ground;
    return mix(ground,forest_surface_ground(p),quality);
}
