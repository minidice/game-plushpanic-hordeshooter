"""Detailed, original foliage/rock/shore geometry for the isolated outdoor kit.
All helpers use Blender Z-up. Semantic canopy groups survive export unchanged.
"""
import math
import random
import bpy
from mathutils import Vector

# Trees are authored from the approved FoliageArt snapshot; no legacy tree recipe remains.
from revised_trees import build_trees

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
