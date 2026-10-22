shader_type spatial;
render_mode cull_back;
#include "res://shaders/character_grip_deform.gdshaderinc"
void vertex() { deform_grip(VERTEX, NORMAL); }
void fragment() { ALBEDO=fur_color.rgb; ROUGHNESS=0.85; }
