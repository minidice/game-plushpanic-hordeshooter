shader_type spatial;
render_mode diffuse_burley;
uniform bool terrain_rock = false;
varying vec3 world;
varying vec3 world_normal;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;world_normal=MODEL_NORMAL_MATRIX*NORMAL;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
#include "res://shaders/forest_patch.gdshaderinc"
#include "res://shaders/forest_rock_surface.gdshaderinc"
void fragment(){
    float grain=.5;
    if(!graphics_surface_low)grain=noise(world.xz*2.6);
    float patch=noise(world.xz*.17);
    vec3 grass=mix(vec3(.22,.29,.17),vec3(.35,.39,.24),patch);
    vec3 gravel=mix(vec3(.30,.30,.25),vec3(.39,.38,.31),patch);
    vec3 ground=mix(grass,gravel,smoothstep(246.,264.,world.x));
    if(world.x<40.) ground=mix(gravel,grass,smoothstep(28.,40.,world.x));
    if(world.x>580.) ground=mix(gravel,grass,.55);
    // The canyon floor is exposed rock/soil. Vertical tier faces read as geology, not colored boxes.
    if(world.x>185. && world.x<215. && world.y<1.) ground=mix(gravel,vec3(.28,.31,.25),.5);
    vec3 rock=mix(vec3(.22,.25,.22),vec3(.38,.39,.32),patch);
    float strata=.9;
    if(graphics_surface_low){
        if(terrain_rock){
            rock=mix(vec3(.105,.112,.070),vec3(.205,.195,.125),patch);
            strata=.95;
        }
    } else {
        rock=mix(vec3(.22,.25,.22),vec3(.38,.39,.32),noise(world.xz*.7+world.y*3.));
        strata=.9+.1*sin(world.y*11.+noise(world.xz*.3)*4.);
        if(terrain_rock){
            // Earthy rock planes, not the pale horizontal bands of the original block walls.
            rock=mix(vec3(.105,.112,.070),vec3(.205,.195,.125),noise(world.xz*1.3+world.y*.8));
            strata=.95+.05*sin(world.y*4.+noise(world.xz*.6)*5.);
        }
    }
    ground=mix(rock*strata,ground,smoothstep(.45,.85,world_normal.y));
    // Container deck tops remain the original physical floor, with an industrial roof surface.
    bool container_deck=world.x>270. && world.x<384. && world.y>3.4 && world.y<3.7 && world_normal.y>.8;
    if(container_deck)
        ground=vec3(.34,.40,.39)*(.90+.10*step(.18,fract(world.z*2.)));
    ALBEDO=ground*(.92+.16*grain);
    float study=container_deck?0.:forest_weight(world.xz)*smoothstep(.8,.95,world_normal.y);
    if(study>0.) ALBEDO=mix(ALBEDO,forest_ground(world.xz),study);
    if(terrain_rock) ALBEDO=forest_rock_surface(world,normalize(world_normal),ALBEDO);
    ROUGHNESS=.94;
}
