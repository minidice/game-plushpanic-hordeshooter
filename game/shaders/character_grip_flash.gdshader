shader_type spatial;
render_mode unshaded, blend_mix;
#include "res://shaders/character_grip_deform.gdshaderinc"
uniform vec4 flash_color : source_color = vec4(1.0);
void vertex() { deform_grip(VERTEX, NORMAL); }
void fragment() { ALBEDO=flash_color.rgb; ALPHA=flash_color.a; }
