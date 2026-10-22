"""Detailed, original foliage/rock/shore geometry for the isolated outdoor kit.
All helpers use Blender Z-up. Semantic canopy groups survive export unchanged.
"""
import math
import random
import bpy
from mathutils import Vector

def leaf_spray(b, parent, angle, radius, base, height, width, material, scale=1):
    """A thick, domed bough with a drooping point, not a flat alpha leaf card."""
    rows=[(.32,.81,.08),(.55,.64,.64),(.82,.33,1),(1.02,.08,.70),(1.13,-.06,.04)]
    verts=[]
    for lower in [False,True]:
        for rr,zz,ww in rows:
            for side in [-1,-.5,0,.5,1]:
                r=rr*radius;tangent=side*ww*width
                z=base+height*zz + (1-side*side)*.12*height
                if lower:z-=.045*height
                verts.append(((math.cos(angle)*r-math.sin(angle)*tangent)*scale,
                              (math.sin(angle)*r+math.cos(angle)*tangent)*scale,z*scale))
    faces=[]
    for row in range(4):
        for side in range(4):
            n=row*5+side
            # radial direction x tangent direction gives outward/up top normal.
            faces.append((n,n+5,n+6,n+1))
            faces.append((n+25,n+26,n+31,n+30))
    border=[0,1,2,3,4,9,14,19,24,23,22,21,20,15,10,5]
    for i,n in enumerate(border):
        nxt=border[(i+1)%len(border)];faces.append((n,nxt,nxt+25,n+25))
    obj=b.mesh(parent,'OverlappingBough',verts,faces,material)
    # Soft domes with a separate, still readable hanging edge.
    for face in obj.data.polygons:face.use_smooth=True
    return obj

def build_trees(b):
    for key,color in [('PineMid','48784f'),('PineTip','6c9358'),('LeafShadow','446e43'),('OakLeaf','62874c'),('OakTip','83a15e'),('LindenLeaf','789657'),('LindenTip','9bb66e')]:
        b.M[key]=b.mat(key,color,.9)
    for asset_id,name,s in [('pine_tall','겹잎 침엽수 · 대',1),('pine_small','겹잎 침엽수 · 소',.68)]:
        r,k,d=b.start(asset_id,name,'식생','겹친 가지·잎 묶음 5층, 수관 4구역. 줄기/뿌리는 유지.',b.coll('cylinder',radius=.32*s,height=1.5*s))
        b.cyl(k,'Trunk',(0,0,1*s),.25*s,2*s,'Bark',.14*s)
        for i in range(6):
            a=i*math.tau/6
            b.beam(k,'Root',(0,0,.16*s),(.46*math.cos(a)*s,.46*math.sin(a)*s,.04*s),.10*s,'Bark')
            b.beam(k,'BarkRidge',(.21*math.cos(a)*s,.21*math.sin(a)*s,.25*s),(.15*math.cos(a+.08)*s,.15*math.sin(a+.08)*s,1.8*s),.018*s,'BarkLight',5)
        for q,label in enumerate(['NE','NW','SW','SE']):
            p=b.occ(r,d,'Canopy_'+label)
            for tier,(z,rad,h,n) in enumerate([(1.02,1.2,1.16,20),(1.59,1.04,1.1,20),(2.15,.84,.98,16),(2.67,.61,.88,16),(3.13,.35,.8,12)]):
                # Discrete overlapping fans at staggered heights avoid the cone-stack profile.
                for j in range(n//4):
                    i=q*(n//4)+j;a=(i+.45+(tier%2)*.22)*math.tau/n
                    variation=1+.06*math.sin(i*3.1+tier)
                    shade=['Pine','PineMid','PineTip'][(i+tier)%3]
                    leaf_spray(b,p,a,rad*variation,z+.05*math.sin(i*1.9),h,rad*math.pi/n*1.32,shade,s)
                # Recessed core belongs to its own quadrant, so fading does not expose a solid cone.
                vs=[(0,0,(z+h*.95)*s),(0,0,(z+.1)*s)]
                for j in range(9):
                    a=q*math.pi/2+j*math.pi/16
                    vs.append((math.cos(a)*rad*.70*s,math.sin(a)*rad*.70*s,(z+.17)*s))
                fs=[(0,j,j+1) for j in range(2,10)]+[(1,j+1,j) for j in range(2,10)]
                b.mesh(p,'RecessedNeedles',vs,fs,'PineDark')
        b.finish_asset(r,d)

    for asset_id,name,wide,hue in [('broadleaf','겹잎 활엽수',1,'Leaf'),('round_oak','둥근 참나무 · 넓은 수관',1.22,'OakLeaf'),('round_linden','둥근 피나무 · 작은 수관',.80,'LindenLeaf')]:
        r,k,d=b.start(asset_id,name,'식생','둥근 잎 덩어리와 겹친 잎판. 수관 4구역 독립, 줄기/큰 가지 유지.',b.coll('cylinder',radius=.36*wide,height=1.7))
        b.cyl(k,'Trunk',(0,0,1.05),.29*wide,2.1,'Bark',.16*wide)
        for q in range(4):
            a=q*math.pi/2+.35
            b.beam(k,'Root',(0,0,.12),(.58*wide*math.cos(a),.58*wide*math.sin(a),.06),.12,'Bark')
            b.beam(k,'Branch',(0,0,1.12),(.85*wide*math.cos(a),.85*wide*math.sin(a),2.38),.11*wide,'Bark')
            p=b.occ(r,d,'Canopy_'+str(q))
            for cluster in range(5):
                aa=a+(cluster-2)*.25
                rad=[.55,.95,1.05,.83,.35][cluster]*wide
                z=[2.28,2.42,2.63,2.93,3.12][cluster]*(.92 if wide<1 else 1)
                center=Vector((math.cos(aa)*rad,math.sin(aa)*rad,z))
                size=Vector((.72*wide,.68*wide,.69 if wide>1 else .73))
                o=b.ico(p,'SoftFoliageCore',center,size,'LeafShadow',2)
                for f in o.data.polygons:f.use_smooth=True
                # Low, convex leaf rosettes cover each rounded volume without a spike crown.
                for ring in range(3):
                    lat=.2+ring*.36
                    count=8 if ring<2 else 5
                    for j in range(count):
                        angle=j*math.tau/count+ring*.4+q*.2
                        surface=center+Vector((math.cos(angle)*size.x*math.cos(lat),math.sin(angle)*size.y*math.cos(lat),size.z*math.sin(lat)))
                        o=b.ico(p,'LeafPad',surface,(.37*wide,.27*wide,.105),hue if (j+ring)%3 else ('LindenTip' if wide<1 else 'OakTip'),1)
                        o.rotation_euler=(.32*math.sin(angle),-.32*math.cos(angle),angle)
                        for f in o.data.polygons:f.use_smooth=True
                o=b.ico(p,'CrownCap',center+Vector((0,0,size.z*.88)),(.39*wide,.37*wide,.20),hue,2)
                for f in o.data.polygons:f.use_smooth=True
        b.finish_asset(r,d)

def rock(b,p,at,scale,seed=0,moss=True):
    """Irregular stratified rock with bevelled fracture planes and conforming moss."""
    rng=random.Random(seed);n=9
    angular=[i*math.tau/n+rng.uniform(-.08,.08) for i in range(n)]
    radial=[rng.uniform(.85,1.12) for _ in range(n)]
    verts=[]
    for ring,(z,r) in enumerate([(-.5,.66),(-.28,.98),(.22,1),(.58,.78),(.73,.42)]):
        for i,a in enumerate(angular):
            verts.append((at[0]+math.cos(a)*radial[i]*r*scale[0]+ring*.014*scale[0],at[1]+math.sin(a)*radial[i]*r*scale[1],at[2]+(z+rng.uniform(-.07,.07))*scale[2]))
    faces=[tuple(reversed(range(n))),tuple(range(4*n,5*n))]
    for row in range(4):
        for j in range(n):faces.append((row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j))
    obj=b.mesh(p,'FracturedStone',verts,faces,'Rock')
    for key in ['RockLight','StoneWarm','StoneDark']:obj.data.materials.append(b.M[key])
    for poly in obj.data.polygons:
        poly.material_index=1 if poly.normal.z>.5 else (2 if rng.random()<.28 else (3 if poly.normal.z<-.25 else 0))
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bevel=obj.modifiers.new('Soft chipped edges','BEVEL');bevel.width=.035*min(scale);bevel.segments=2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    if moss:
        mv=[];mf=[]
        for f in obj.data.polygons:
            if f.normal.z>.52 and f.center.z>at[2]+.25*scale[2]:
                start=len(mv)
                for v in f.vertices:mv.append(tuple(obj.data.vertices[v].co+Vector((0,0,.012))))
                mf.append(tuple(range(start,len(mv))))
        if mf:
            cover=b.mesh(p,'SurfaceMoss',mv,mf,'Moss')
            cover.data.materials.append(b.M['MossLight'])
            for f in cover.data.polygons:f.material_index=1 if rng.random()<.18 else 0
    return obj

def build_rocks(b):
    for key,color in [('StoneWarm','7c8278'),('StoneDark','47585a'),('MossLight','7d9659')]:b.M[key]=b.mat(key,color,.95)
    for asset_id,name in [('boulder','이끼 바위 · 균열면'),('rock_cluster','갈라진 바위 무리'),('cliff_segment','층진 절벽 경계')]:
        tall=asset_id=='cliff_segment'
        r,k,d=b.start(asset_id,name,'지형','비대칭 절단면·작은 파편·표면을 따르는 이끼. '+('상부 암석은 별도 제어.' if tall else '낮은 바위는 원형 유지.'),b.coll('box',size=[3.5,3,1.5],center=[0,1.5,0]) if tall else b.coll('cylinder',radius=1.05,height=1.2))
        p=b.occ(r,d,'UpperRock') if tall else k
        if tall:
            specs=[(-1.16,0,.9,.76,.75,1.5),(0,.12,1.10,.92,.85,1.8),(1.22,.08,.9,.80,.78,1.5),(-.65,.30,2.08,.73,.70,1),( .68,.42,2.38,.80,.7,1.12)]
            rock(b,k,(0,0,.24),(1.72,.74,.55),90,False)
        elif asset_id=='rock_cluster':specs=[(-.7,0,.55,.86,.72,1.0),(.56,.24,.73,.91,.74,1.2),(.12,-.61,.30,.58,.42,.55)]
        else:specs=[(0,0,.59,1.08,.85,1.05),(.95,-.3,.22,.36,.36,.4)]
        for i,(x,y,z,sx,sy,sz) in enumerate(specs):rock(b,p,(x,y,z),(sx,sy,sz),i+33)
        for i in range(7):
            a=i*.78;rad=1.25 if not tall else 1.7
            rock(b,k,(math.cos(a)*rad,math.sin(a)*.85,.11),(.12+i%3*.055,.11,.18),70+i,False)
        b.finish_asset(r,d)

def river_path(t,bend):
    if bend:
        a=-math.pi/2+t*math.pi/2
        return Vector((4*math.cos(a)-2,4*math.sin(a)+2)),Vector((-math.sin(a),math.cos(a)))
    # Zero lateral derivative at endpoints, so copies have identical seams.
    x=.28*math.sin(math.tau*t)*math.sin(math.pi*t)**2
    dx=.28*(math.tau*math.cos(math.tau*t)*math.sin(math.pi*t)**2+math.pi*math.sin(math.tau*t)**2)
    return Vector((x,-4+8*t)),Vector((dx,8)).normalized()

def river_width(t):
    return 1.45 + math.sin(math.pi*t)**2*(.13*math.sin(t*31)+.08*math.sin(t*71))

def strip(b,p,name,rows,material,colors=None):
    verts=[v for row in rows for v in row];n=len(rows[0]);faces=[]
    for i in range(len(rows)-1):
        for j in range(n-1):
            a=i*n+j;faces.append((a,a+1,a+n+1,a+n))
    obj=b.mesh(p,name,verts,faces,material)
    if colors:
        attribute=obj.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        obj.data.color_attributes.active_color=attribute
        for i,c in enumerate(colors):attribute.data[i].color=(*c,1)
        bs=b.M[material].node_tree.nodes.get('Principled BSDF')
        nodes=b.M[material].node_tree.nodes
        node=nodes.get('RiverVertexColor') or nodes.new('ShaderNodeVertexColor')
        node.name='RiverVertexColor';node.layer_name='Color'
        b.M[material].node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
    return obj

def build_streams(b):
    for key,color,rough in [('BankEarth','655d43',.97),('WetSilt','495d4d',.72),('ShoreGrass','6a7b45',.95),('StreamWater','ffffff',.22),('ShoreFoam','bdd4b5',.55)]:b.M[key]=b.mat(key,color,rough)
    for asset_id,name,bend in [('stream_straight','자연 하천 · 직선 연결',False),('stream_curve','자연 하천 · 90도 굽이',True)]:
        r,k,d=b.start(asset_id,name,'지형','불규칙 둑·젖은 경계·얕은 물색·부분 포말. 양 끝 동일 단면 연결. 지형 프리팹.',b.coll('none'))
        d['waterline_m']=.24;d['connectors']=[]
        for idx,t in enumerate([0,1]):
            center,tangent=river_path(t,bend)
            socket=b.empty('Connection_Start' if idx==0 else 'Connection_End',r);socket.location=(center.x,center.y,.24)
            d['connectors'].append(dict(node=socket.name.split('.')[0],position=[round(center.x,6),.24,round(-center.y,6)],forward=[round(tangent.x,6),0,round(-tangent.y,6)],half_width_m=1.45,bank_height_m=.62))
        count=65;water_rows=[];colors=[]
        deep=(.018,.075,.095);shallow=(.05,.16,.14)
        for i in range(count):
            t=i/(count-1);center,tangent=river_path(t,bend);normal=Vector((tangent.y,-tangent.x));w=river_width(t)
            row=[]
            for u in [-1.08,-.91,-.60,0,.60,.91,1.08]:
                pt=center+normal*w*u;row.append((pt.x,pt.y,.24))
                s=abs(u)**3;colors.append(tuple(a+(bb-a)*min(1,s) for a,bb in zip(deep,shallow)))
            water_rows.append(row)
        water=strip(b,k,'WaterSurface',water_rows,'StreamWater',colors)
        for side in [-1,1]:
            # Inner wet toe lies under water; the dry bank visibly overlaps the surface edge.
            for name,profile,material in [
                ('WetShore',[(1.0,.17),(1.11,.30),(1.22,.42)],'WetSilt'),
                ('BankSlope',[(1.22,.42),(1.46,.60)],'BankEarth'),
                ('GrassBank',[(1.46,.60),(1.67,.62),(2.40,.62)],'ShoreGrass'),
                ('BankSkirt',[(2.40,.62),(2.40,0)],'BankEarth')]:
                rows=[]
                for i in range(count):
                    t=i/(count-1);c,tangent=river_path(t,bend);n=Vector((tangent.y,-tangent.x));w=river_width(t)
                    row=[]
                    for u,z in profile:
                        # Only the inside lip is irregular; the outer edge remains level for terrain joins.
                        dist=(w+(u-1)*1.45) if u<1.67 else u*1.45
                        point=c+n*side*dist
                        row.append((point.x,point.y,z))
                    if side<0:row.reverse()
                    rows.append(row)
                strip(b,k,name,rows,material)
            # Interrupted foam hugs the contact zone, rather than outlining the whole tile.
            for start,end in [(5,13),(22,29),(37,43),(51,58)]:
                rows=[]
                for i in range(start,end+1):
                    t=i/(count-1);c,tangent=river_path(t,bend);n=Vector((tangent.y,-tangent.x));w=river_width(t)
                    taper=math.sin(math.pi*(i-start)/(end-start));width=.018+.018*taper
                    rows.append([(*(c+n*side*(w*1.05-width)),.247),(*(c+n*side*(w*1.05+width)),.247)])
                    if side<0:rows[-1].reverse()
                strip(b,k,'BrokenFoam',rows,'ShoreFoam')
            for i in [13,38,54]:
                t=i/64;c,tangent=river_path(t,bend);n=Vector((tangent.y,-tangent.x));pt=c+n*side*(river_width(t)+.30)
                rock(b,k,(pt.x,pt.y,.34),(.24,.31,.40),i+(21 if side<0 else 0))
        b.finish_asset(r,d)
