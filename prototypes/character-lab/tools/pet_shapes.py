"""Authored quadruped silhouettes; helpers are supplied by the Blender builder.

All coordinates are metres, +Y up, +Z forward. Each articulation is sculpted
independently so the common sockets and rigid walking rig survive both tiers.
"""
import math
import bpy


def coat_material(g,name):
    mat=g.material(name,'ffffff',rough=.78)
    color=mat.node_tree.nodes.new('ShaderNodeVertexColor');color.layer_name='Coat'
    mat.node_tree.links.new(color.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    return mat


def surface(g,parent,name,vertices,faces,material,colors=None):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    g.finish(obj,name,parent,(0,0,0),(1,1,1),material)
    if colors:
        attribute=mesh.color_attributes.new(name='Coat',type='FLOAT_COLOR',domain='CORNER')
        mesh.color_attributes.active_color=attribute
        for loop in mesh.loops:attribute.data[loop.index].color=(*colors[loop.vertex_index],1)
    return obj


def grounded(g,obj,pivot_y):
    floor=min(v.co.y for v in obj.data.vertices)
    for vertex in obj.data.vertices:vertex.co.y+=-pivot_y-floor


def eyes(g,head,mesh,m,width,y,size=1):
    for side in [-1,1]:
        x=side*width
        candidates=[v.co.z for v in mesh.data.vertices if abs(v.co.x-x)<.035 and abs(v.co.y-y)<.035]
        z=max(candidates)
        # The eye sits into its cheek instead of on an isolated stalk.
        g.ball(head,'Eyelid',(x,y,z-.01),(.080*size,.097*size,.027),m['accent'],segments=24,rings=16)
        g.ball(head,'Eye',(x,y,z+.008),(.061*size,.076*size,.026),m['ink'],segments=24,rings=16)
        g.ball(head,'EyeGlint',(x-.017*size,y+.025*size,z+.033),(.016*size,.020*size,.008),m['white'],segments=12,rings=8)


def turtle(g,root,body,m):
    skin=coat_material(g,'Turtle skin');shell=coat_material(g,'Turtle shell')
    green=g.linear_hex('7eaa76');light=g.linear_hex('c3cf96');dark=g.linear_hex('3f6555')
    upper=g.linear_hex('7f9e68');rim=g.linear_hex('b9bc81')
    belly=g.sculpt(body,'PlastronAndNeck',[
        g.ball(body,'Plastron',(0,.31,-.10),(.53,.20,.53),skin,segments=32,rings=20),
        g.ball(body,'Shoulder',(0,.36,.34),(.35,.22,.25),skin),
        g.ball(body,'Neck',(0,.47,.47),(.27,.20,.21),skin),
    ],skin,voxel=.012,triangles=2700)
    g.paint(belly,lambda v:g.blend(light,green,(v.z-.29)/.27))
    # One curved shell with recessed polygonal sutures, not plates intersecting a sphere.
    vertices=[];colors=[];faces=[];rows=40;sides=112
    seeds=[(0,-.12),(-.34,-.40),(.34,-.40),(-.35,.15),(.35,.15),(0,-.65),(0,.46)]
    for row in range(rows+1):
        t=(row/rows)*math.pi/2
        for j in range(sides):
            a=2*math.pi*j/sides;x=.66*math.sin(t)*math.cos(a);z=-.12+.64*math.sin(t)*math.sin(a)
            distances=sorted(math.hypot((x-sx)*1.05,z-sz) for sx,sz in seeds)
            seam=max(0,1-(distances[1]-distances[0])/.065)
            y=.42+.605*math.cos(t)-.014*seam
            vertices.append((x,y,z))
            base=g.blend(upper,dark,seam*.9)
            colors.append(g.blend(base,rim,max(0,(row/rows-.88)/.12)))
    for row in range(rows):
        for j in range(sides):
            a=row*sides+j;b=row*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    surface(g,body,'SculptedShell',vertices,faces,shell,colors)
    # A thin continuous rolled lip follows the shell perimeter.
    for band,(height,rx,rz,radius) in enumerate([(.42,.65,.63,.037),(.39,.61,.59,.024)]):
        pts=[(rx*math.cos(a),height,-.12+rz*math.sin(a)) for a in [2*math.pi*j/24 for j in range(25)]]
        g.line(body,'ShellLip'+str(band),pts,m['rim'],radius)
    head=g.empty('Head',body,(0,.50,.58))
    skull=g.sculpt(head,'TurtleFace',[
        g.ball(head,'Cranium',(0,.075,.03),(.43,.33,.33),skin,segments=32,rings=20),
        g.ball(head,'CheekL',(-.19,-.07,.21),(.25,.19,.17),skin),
        g.ball(head,'CheekR',(.19,-.07,.21),(.25,.19,.17),skin),
        g.ball(head,'Jaw',(0,-.12,.25),(.29,.13,.13),skin),
    ],skin,voxel=.011,triangles=4200)
    g.paint(skull,lambda v:g.blend(green,light,(-v.y+.01)/.22))
    eyes(g,head,skull,m,.255,.075,1.22)
    for side in [-1,1]:
        g.ball(head,'Nostril',(side*.065,.002,.373),(.010,.009,.006),m['accent'],segments=12,rings=8)
        g.line(head,'Mouth',[(0,-.12,.386),(side*.11,-.135,.374),(side*.18,-.11,.350)],m['accent'],.005)
    for front in [True,False]:
        for side in [-1,1]:
            pivot=g.empty(('Front' if front else 'Rear')+('Left' if side<0 else 'Right'),body,(side*.40,.21,.29 if front else -.40))
            parts=[g.ball(pivot,'UpperLeg',(side*.015,.005,-.03),(.18,.16,.18),skin),g.ball(pivot,'BentAnkle',(side*.025,-.075,.025),(.17,.12,.16),skin),g.ball(pivot,'Foot',(side*.03,-.125,.07),(.19,.085,.18),skin)]
            for j in [-1,0,1]:parts.append(g.ball(pivot,'Toe',(side*.03+j*.075,-.13,.19),(.06,.065,.065),skin))
            paw=g.sculpt(pivot,'TurtleLeg',parts,skin,voxel=.010,triangles=1300);grounded(g,paw,.21)
            g.paint(paw,lambda v:g.blend(green,light,(-v.y-.12)/.25))
            for j in [-1,0,1]:g.ball(pivot,'Claw',(side*.03+j*.075,-.135,.242),(.020,.018,.023),m['cream'],segments=12,rings=8)
    tail=g.empty('Tail',body,(0,.29,-.63))
    tip=g.sculpt(tail,'TaperedTail',[g.ball(tail,'Tail',(0,-i*.006,-i*.043),(.085-i*.011,.075-i*.010,.08),skin) for i in range(6)],skin,voxel=.010,triangles=700)
    g.paint(tip,lambda v:green)
    g.empty('BackSocket',body,(0,1.025,-.23))['mount_contract']='quadruped_back_v1'
    return root


def rabbit(g,root,body,m):
    fur=coat_material(g,'Rabbit fur')
    main=g.linear_hex('d8d5cd');cream=g.linear_hex('f4eadb');pink=g.linear_hex('d8a09d')
    torso=g.sculpt(body,'RabbitTorso',[
        g.ball(body,'Rump',(0,.54,-.20),(.405,.405,.35),fur,segments=32,rings=20),
        g.ball(body,'Waist',(0,.52,.04),(.33,.31,.30),fur),
        g.ball(body,'Shoulder',(0,.57,.26),(.31,.30,.24),fur),
        g.ball(body,'Neck',(0,.69,.34),(.28,.23,.20),fur),
    ],fur,voxel=.010,triangles=4300)
    # Slightly fuller saddle supports the shared .95 m socket.
    for v in torso.data.vertices:
        if v.co.y>.7:v.co.y+=.014*(v.co.y-.7)/.25
    g.paint(torso,lambda v:g.blend(main,cream,(-v.y+.56)/.28))
    head=g.empty('Head',body,(0,.70,.51))
    skull=g.sculpt(head,'RabbitFace',[
        g.ball(head,'Skull',(0,.075,.025),(.445,.33,.33),fur,segments=32,rings=20),
        g.ball(head,'CheekL',(-.18,-.07,.20),(.25,.20,.17),fur),
        g.ball(head,'CheekR',(.18,-.07,.20),(.25,.20,.17),fur),
        g.ball(head,'Chin',(0,-.17,.22),(.24,.095,.13),fur),
    ],fur,voxel=.010,triangles=5200)
    g.paint(skull,lambda v:g.blend(main,cream,(v.z-.12)/.20))
    eyes(g,head,skull,m,.25,.075,1.22)
    for side in [-1,1]:
        vertices=[];faces=[];colors=[];rows=36;sides=32
        for row in range(rows+1):
            t=row/rows;profile=max(.018,math.sin(math.pi*t)**.64)
            for j in range(sides):
                a=j*2*math.pi/sides;c=math.cos(a);s=math.sin(a)
                x=side*(.26+.12*t)+c*.14*profile;y=.26+.65*t
                z=.065+.05*t+.085*s*profile-.04*max(0,s)**3*math.sin(math.pi*t)
                vertices.append((x,y,z))
                inset=max(0,min(1,(t-.12)/.10))*max(0,min(1,(.93-t)/.10))*max(0,min(1,(s-.52)/.22))
                colors.append(g.blend(main,pink,inset))
        for row in range(rows):
            for j in range(sides):
                a=row*sides+j;b=row*sides+(j+1)%sides
                faces.append((a+sides,b+sides,b,a))
        faces.extend([tuple(reversed(range(sides))),tuple(rows*sides+j for j in range(sides))])
        surface(g,head,'SculptedEar',vertices,faces,fur,colors)
    nose=g.ball(head,'RabbitNose',(0,-.030,.382),(.048,.034,.023),m['pink'],segments=24,rings=16)
    for v in nose.data.vertices:v.co.x*=.7+.3*(v.co.y+1)/2
    g.line(head,'MouthSplit',[(0,-.06,.391),(0,-.11,.379)],m['accent'],.004)
    for side in [-1,1]:
        g.line(head,'MouthSmile',[(0,-.11,.379),(side*.055,-.13,.369),(side*.095,-.11,.349)],m['accent'],.004)
        g.box(head,'Incisor',(side*.022,-.16,.348),(.038,.055,.022),m['white'],.009)
    for front in [True,False]:
        for side in [-1,1]:
            pivot=g.empty(('Front' if front else 'Rear')+('Left' if side<0 else 'Right'),body,(side*(.28 if front else .32),.24,.31 if front else -.29))
            parts=[g.ball(pivot,'UpperLeg',(0,.06,-.035),(.14,.18,.15) if front else (.20,.25,.23),fur),g.ball(pivot,'Ankle',(0,-.07,.02),(.12,.13,.13) if front else (.16,.15,.17),fur),g.ball(pivot,'RoundPaw',(0,-.15,.08),(.155,.095,.18) if front else (.20,.10,.23),fur)]
            paw=g.sculpt(pivot,'RabbitLeg',parts,fur,voxel=.010,triangles=1300);grounded(g,paw,.24)
            g.paint(paw,lambda v:g.blend(main,cream,(-v.y-.02)/.22))
            for j in [-1,1]:g.line(pivot,'ToeCrease',[(j*.045,-.13,.23 if front else .28),(j*.045,-.10,.20 if front else .25)],m['cream'],.004)
    tail=g.empty('Tail',body,(0,.48,-.51))
    tail_parts=[g.ball(tail,'Puff',(0,.025,-.11),(.19,.18,.19),fur,segments=24,rings=16)]
    for x,y,z in [(-.09,.10,-.18),(.09,.11,-.18),(0,-.055,-.22)]:tail_parts.append(g.ball(tail,'SoftTuft',(x,y,z),(.10,.10,.10),fur))
    puff=g.sculpt(tail,'CottonTail',tail_parts,fur,voxel=.009,triangles=1200);g.paint(puff,lambda v:cream)
    g.empty('BackSocket',body,(0,.95,-.23))['mount_contract']='quadruped_back_v1'
    return root
