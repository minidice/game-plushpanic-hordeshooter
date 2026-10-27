// The urban Stage 1's ground (decisions.md A-30, LayoutMapUrbanGround.cs). The baked map gives, per floor cell, the
// distance to the nearest wall (R, 0..edge_range m), whether the cell is a road lane (G) and its pavement width (B * 3 m).
// Roads: worn asphalt with repair patches, cracks and oil stains, a paved pavement along the walls behind a kerb and a
// darker gutter. Yards, pockets and bases: concrete slabs with joints. With `lot` the ground past the walls: a gravel lot.
// Colours are written in sRGB like the kit palette and turned linear at the end.
shader_type spatial;
render_mode diffuse_burley;
uniform sampler2D map : filter_linear, repeat_disable;
uniform vec4 area;
uniform float edge_range = 6.0;
uniform bool lot = false;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
float fbm(vec2 p){return .55*noise(p)+.3*noise(p*2.1+7.3)+.15*noise(p*4.3+1.7);}
vec3 asphalt(vec2 xz){
    float n=fbm(xz*.25), fine=noise(xz*9.), grit=hash(floor(xz*14.));
    vec3 c=vec3(.30,.31,.32)*(.86+.22*n)+(grit-.5)*.035+(fine-.5)*.02;
    // Repair patches: some 6 m cells hold a darker, smoother rectangle.
    vec2 cellId=floor(xz/6.), inCell=fract(xz/6.);
    float patch_on=step(.78,hash(cellId+3.1));
    vec2 lo=vec2(.15,.2)+.15*vec2(hash(cellId+1.),hash(cellId+2.)), hi=lo+vec2(.35,.3)+.2*vec2(hash(cellId+4.),hash(cellId+5.));
    float in_patch=patch_on*step(lo.x,inCell.x)*step(inCell.x,hi.x)*step(lo.y,inCell.y)*step(inCell.y,hi.y);
    c=mix(c,c*.82,in_patch*(.6+.4*noise(xz*1.7)));
    // Cracks: thin dark lines along a noise ridge, only in some stretches.
    float ridge=abs(fbm(xz*.42+11.)-.5);
    float crack=(1.-smoothstep(.003,.009,ridge))*smoothstep(.62,.72,noise(xz*.07+5.));
    c*=1.-.22*crack;
    // Oil stains.
    float stain=smoothstep(.70,.80,fbm(xz*.31+31.));
    return c*(1.-.25*stain);
}
vec3 concrete(vec2 xz){
    vec2 slab=floor(xz/3.), f=fract(xz/3.);
    float joint=max(1.-smoothstep(.0,.025,min(f.x,1.-f.x)),1.-smoothstep(.0,.025,min(f.y,1.-f.y)));
    vec3 c=vec3(.50,.50,.47)*(.88+.12*hash(slab))*(.9+.12*fbm(xz*.35))+(hash(floor(xz*12.))-.5)*.03;
    float stain=smoothstep(.66,.8,fbm(xz*.22+17.));
    c*=1.-.18*stain;
    return mix(c,c*.62,joint);
}
vec3 paving(vec2 xz){
    vec2 t=xz/vec2(.6,.4);
    t.x+=step(.5,fract(floor(t.y)*.5))*.5;
    vec2 f=fract(t);
    float grout=max(1.-smoothstep(.0,.07,min(f.x,1.-f.x)),1.-smoothstep(.0,.1,min(f.y,1.-f.y)));
    vec3 c=vec3(.60,.58,.54)*(.88+.16*hash(floor(t)))*(.92+.1*fbm(xz*.4));
    return mix(c,c*.66,grout);
}
vec3 gravel(vec2 xz){
    float n=fbm(xz*.3), speck=noise(xz*6.)*.6+noise(xz*17.)*.4;
    vec3 c=mix(vec3(.33,.32,.29),vec3(.43,.41,.37),n);
    return c*(.86+.24*speck);
}
void fragment(){
    vec2 xz=world.xz;
    if(lot){ALBEDO=pow(gravel(xz),vec3(2.2));ROUGHNESS=.97;}
    else{
        vec4 m=texture(map,(xz-area.xy)/area.zw);
        float d=m.r*edge_range, pave=m.b*3.;
        vec3 c;
        if(m.g>.5){
            c=asphalt(xz);
            if(pave>.1){
                float wobble=(noise(xz*.8)-.5)*.06;
                float kerb_in=pave+wobble, kerb_out=pave+.3+wobble;
                // Gutter just off the kerb, then the kerb stone, then the pavement against the wall.
                c=mix(c,c*.78,1.-smoothstep(kerb_out,kerb_out+.35,d));
                c=mix(c,vec3(.66,.65,.61)*(.9+.1*noise(xz*3.)),smoothstep(kerb_in-.03,kerb_in,d)*(1.-smoothstep(kerb_out-.03,kerb_out,d)));
                c=mix(c,paving(xz),1.-smoothstep(kerb_in-.03,kerb_in,d));
            }
        }else c=concrete(xz);
        // Grime along the walls.
        c*=mix(.72,1.,smoothstep(.0,.7,d));
        ALBEDO=pow(c,vec3(2.2));
        ROUGHNESS=.93;
    }
}
