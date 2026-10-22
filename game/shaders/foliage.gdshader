shader_type spatial;
render_mode diffuse_burley;
varying vec3 local_position;
varying float canopy_up;
void vertex() { local_position=VERTEX; canopy_up=NORMAL.y; }
void fragment() {
    float leaf=COLOR.a;
    // Broad, low contrast variation follows the crown rather than individual triangles.
    float patch=0.97+0.025*sin(local_position.x*3.1+local_position.z*2.7)
        +0.015*sin(local_position.y*4.0-local_position.z*3.0);
    ALBEDO=COLOR.rgb*mix(1.0,patch*(.82+.25*smoothstep(-.5,.8,canopy_up)),leaf);
    ROUGHNESS=mix(0.96,0.88,leaf);
    SPECULAR=0.20;
}
