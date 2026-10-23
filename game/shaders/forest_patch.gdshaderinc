uniform bool forest_enabled = false;
uniform bool forest_expanded = false;
uniform int forest_route_count = 0;
uniform vec4 forest_routes[32];
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
vec3 forest_ground(vec2 p) {
    float broad=noise(p*.27),detail=noise(p*5.),fine=noise(p*31.);
    float road=1.-smoothstep(1.7,3.9,forest_path_distance(p)+(noise(p*1.2)-.5)*.9);
    vec3 moss=mix(vec3(.115,.137,.052),vec3(.185,.192,.078),broad);
    vec3 soil=mix(vec3(.175,.130,.078),vec3(.225,.174,.102),broad);
    vec3 ground=mix(moss,soil,road);
    ground*=.89+.13*detail+.07*fine;
    ground+=vec3(.035,.029,.018)*smoothstep(.76,.92,fine);
    return ground;
}
