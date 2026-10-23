// Matches WoodlandCover.Density. Used on both the painted floor and its far continuation.
float woodland_density(vec2 p) {
    float wave=.5+.25*sin(p.x*.53+p.y*.17+.8*sin(p.y*.31))+.25*sin(p.y*.29-p.x*.24);
    return smoothstep(.25,.78,wave);
}
vec3 woodland_grass(vec3 color,vec2 p) {
    float density=woodland_density(p);
    vec2 q=mat2(vec2(.8,-.6),vec2(.6,.8))*p;
    // Short directional strokes, filtered by their screen footprint to avoid distant shimmer.
    vec2 grid=q*18.0;
    vec2 cell=floor(grid),f=fract(grid);
    float hash=fract(sin(dot(cell,vec2(127.1,311.7)))*43758.5453);
    float line=abs(f.x-.5-(f.y-.5)*.45);
    float aa=max(fwidth(grid.x),.02);
    float blade=(1.0-smoothstep(.045,.045+aa,line))*smoothstep(.05,.25,f.y)*(1.0-smoothstep(.65,.95,f.y));
    float fade=1.0-smoothstep(.35,1.1,length(fwidth(grid)));
    return color*mix(.89,1.03,density)*(1.0+blade*fade*(hash>.45?.13:-.16));
}
