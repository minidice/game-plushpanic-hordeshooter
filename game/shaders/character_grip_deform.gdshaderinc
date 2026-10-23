instance uniform bool grip_surface : instance_index(12) = false;
instance uniform vec4 fur_color : source_color, instance_index(4) = vec4(1.0);
instance uniform vec3 grip : instance_index(5) = vec3(0.54, 1.15, 0.65);
instance uniform float side : instance_index(6) = 1.0;
instance uniform float wing_size : instance_index(7) = 1.0;
instance uniform float running : instance_index(8) = 0.0;
instance uniform bool holding : instance_index(9) = true;
instance uniform bool bird : instance_index(10) = true;

vec3 shoulder() { return vec3(side * 0.435, 1.12, 0.15); }
vec3 curve(float t) {
    vec3 a = shoulder();
    vec3 b = vec3(side * 0.640, 0.98, 0.165);
    vec3 c = grip + vec3(side * 0.140, 0.005, 0.025);
    vec3 p = pow(1.0-t,3.0)*a + 3.0*pow(1.0-t,2.0)*t*b + 3.0*(1.0-t)*t*t*c + t*t*t*grip;
    float bulge = grip.y < 1.0 ? (bird ? 0.065 : 0.085) : (bird ? 0.0 : 0.020);
    p.x += side * bulge * sin(3.14159265*t);
    return p;
}
vec3 deform(vec3 p) {
    if (!holding) {
        vec3 q = (p - shoulder()) * wing_size;
        float angle = -side * running * 0.70;
        return shoulder() + vec3(q.x*cos(angle)-q.y*sin(angle), q.x*sin(angle)+q.y*cos(angle), q.z);
    }
    float v = (1.150-p.y)/1.13;
    float u = (p.z-0.145-0.08*v)/1.15;
    float s = clamp((v-0.01)/0.357,0.0,1.0);
    float depth = abs(p.x)-(0.455+0.035*v/0.37-0.030*exp(-pow(v/0.065,2.0)));
    float at = clamp(s,0.005,0.995);
    vec3 tangent = normalize(curve(at+0.005)-curve(at-0.005));
    vec3 width = vec3(0,-1,0);
    width = normalize(width-tangent*dot(width,tangent));
    vec3 normal = normalize(cross(tangent,width))*side;
    float taper = 1.0-(bird?0.40:0.15)*clamp((s-0.55)/0.45,0.0,1.0);
    return curve(s)+width*(u-(bird?0.007:0.0))*1.15*taper*wing_size+normal*depth*0.85*wing_size;
}
void deform_grip(inout vec3 position, inout vec3 surface_normal) {
    if (!grip_surface) return;
    // Transform the authored normal using two surface tangents; works for both mirrored wings.
    vec3 n = normalize(surface_normal);
    vec3 axis = abs(n.y)<0.9 ? vec3(0,1,0) : vec3(1,0,0);
    vec3 t = normalize(cross(n,axis));
    vec3 b = cross(n,t);
    vec3 dt = deform(position+t*0.0005)-deform(position-t*0.0005);
    vec3 db = deform(position+b*0.0005)-deform(position-b*0.0005);
    vec3 moved_normal = cross(dt,db);
    if (dot(moved_normal,moved_normal)>0.000000000001) surface_normal=normalize(moved_normal);
    position=deform(position);
}
