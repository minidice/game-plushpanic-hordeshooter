shader_type spatial;
render_mode diffuse_burley;
varying vec3 local_position;
varying float canopy_up;
uniform vec3 inner_green : source_color = vec3(0.15, 0.29, 0.22);
void vertex() { local_position=VERTEX; canopy_up=NORMAL.y; }
void fragment() {
    float leaf=COLOR.a;
    // Broad, low contrast variation follows the crown rather than individual triangles.
    float patch=0.97+0.025*sin(local_position.x*3.1+local_position.z*2.7)
        +0.015*sin(local_position.y*4.0-local_position.z*3.0);
    // Recessed layer undersides stay cool; broad shoulders and tips receive a warm green.
    // Interpolated normals carry this transition without horizontal colour stripes.
    vec3 canopy=mix(inner_green,COLOR.rgb*1.12,smoothstep(-.65,.75,canopy_up))*patch;
    ALBEDO=mix(COLOR.rgb,canopy,leaf);
    ROUGHNESS=mix(0.96,0.88,leaf);
    SPECULAR=0.20;
}
