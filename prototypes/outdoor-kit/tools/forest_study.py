"""Blender-authored warm forest. Only replaces its own six manifest records."""
import bpy, json, math, random
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[3]
PACK=ROOT/'game/assets/stage2-props'
OUT=ROOT/'game/output/forest-blender'
IDS=('forest_pine','forest_broadleaf','forest_oval','forest_fan','forest_grass_a','forest_grass_b')

def linear(hexcode):
    c=[int(hexcode[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c)

def empty(name,parent=None):
    n=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(n);n.parent=parent;return n

def mesh(parent,name,verts,faces,colors,grass=False):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
    o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);o.parent=parent
    m.materials.append(MAT_GRASS if grass else MAT_TREE)
    attr=m.color_attributes.new(name='ForestTint',type='FLOAT_COLOR',domain='POINT')
    for i,c in enumerate(colors):attr.data[i].color=(*c,1)
    for p in m.polygons:p.use_smooth=True
    return o

def trunk(parent,height,radius):
    vs=[];cs=[];fs=[]
    for j,z in enumerate((0,.12,height*.7,height)):
        r=radius*(1.45 if j==0 else 1 if j==1 else .67 if j==2 else .45)
        for i in range(10):
            a=math.tau*i/10;vs.append((math.cos(a)*r,math.sin(a)*r,z))
            cs.append(linear('77613e' if i%3 else '8b7248'))
    for j in range(3):
        for i in range(10):a=j*10+i;b=j*10+(i+1)%10;fs.append((a,b,b+10,a+10))
    fs.extend([tuple(range(9,-1,-1)),tuple(range(30,40))]);mesh(parent,'TaperedBark',vs,fs,cs)

def pine():
    root=empty('forest_pine');keep=empty('Keep',root);crown=empty('Occlude_Canopy',root)
    trunk(keep,3.55,.19)
    for layer,(base,top,radius) in enumerate(((.85,2.65,1.58),(1.60,3.25,1.35),(2.37,3.88,1.03),(3.12,4.55,.77))):
        vs=[];cs=[];fs=[];count=48;profile=(0,.045,.16,.38,.65,.86,.98)
        for j,t in enumerate(profile):
            for i in range(count):
                a=i*math.tau/count+.31*layer
                scallop=math.sin(a*12+layer)*.035+math.sin(a*5+layer)*.045
                r=radius*((1-t)**.83)*(1+.07*math.sin(a*3+layer)+.045*math.cos(a*2))
                r+=radius*scallop*(1-t)**5
                z=base+(top-base)*t+radius*scallop*(1-t)**6
                if i%4==0:
                    r-=radius*.065*(1-t)**15
                    z+=.12*(1-t)**15
                vs.append((r*math.cos(a)+.08*math.sin(layer*2)*t,r*math.sin(a)+.10*math.cos(layer)*t,z))
                lower=linear('607830');upper=linear('aabd54')
                tint=.17+.55*t+.08*math.sin(a*3+layer)
                cs.append(tuple(x*(1-tint)+y*tint for x,y in zip(lower,upper)))
        for j in range(len(profile)-1):
            for i in range(count):a=j*count+i;b=j*count+(i+1)%count;fs.append((a,b,b+count,a+count))
        vs.append((.08*math.sin(layer*2),.10*math.cos(layer),top));cs.append(linear('b6c45d'));tip=len(vs)-1
        for i in range(count):fs.append(((len(profile)-1)*count+i,(len(profile)-1)*count+(i+1)%count,tip))
        # The recessed underside is a separate dark surface, not a flat black disk.
        vs.append((0,0,base+.22));cs.append(linear('465c2b'));under=len(vs)-1
        for i in range(count):fs.append(((i+1)%count,i,under))
        mesh(crown,'LeafSkirt'+str(layer),vs,fs,cs)
    return root

def branch(parent,start,end,radius):
    start,end=Vector(start),Vector(end);axis=(end-start).normalized()
    u=axis.cross(Vector((0,0,1))).normalized();v=axis.cross(u)
    vs=[];cs=[];fs=[]
    for t in (0,.55,1):
        center=start.lerp(end,t)+Vector((0,0,.10*math.sin(t*math.pi)))
        for i in range(8):
            a=i*math.tau/8;vs.append(tuple(center+(u*math.cos(a)+v*math.sin(a))*radius*(1-.65*t)))
            cs.append(linear('8b7248' if i<4 else '77613e'))
    for j in range(2):
        for i in range(8):a=j*8+i;b=j*8+(i+1)%8;fs.append((a,b,b+8,a+8))
    fs.extend([tuple(range(7,-1,-1)),tuple(range(16,24))]);mesh(parent,'Fork',vs,fs,cs)

def leaf_dome(parent,layer,base,top,radius,oval=1):
    # Broad rounded shoulders, medium leaf lobes and a recessed underside.
    # Offset tiers leave real shadow gaps, rather than painted horizontal stripes.
    vs=[];cs=[];fs=[];count=64;profile=(0,.07,.22,.44,.68,.88,.98)
    cx=.10*math.sin(layer*2);cy=.08*math.cos(layer*1.7)
    for j,t in enumerate(profile):
        for i in range(count):
            a=i*math.tau/count+.29*layer
            lobe=(1-math.cos(a*8+layer*.6))*.5
            r=radius*math.cos(t*math.pi/2)**.76
            r*=1+.065*math.sin(a*3+layer)+.045*math.sin(a*5-layer)+.065*lobe*(1-t)
            z=base+(top-base)*math.sin(t*math.pi/2)-.15*lobe*(1-t)**3
            # Small notches between the eight broad lobes keep the rim from reading as a smooth disc.
            if i%8==0:r-=radius*.045*(1-t)**12;z+=.065*(1-t)**12
            vs.append((cx+r*math.cos(a),cy+r*math.sin(a)*oval,z))
            shade=.18+.60*t+.10*lobe+.045*math.sin(a*3+layer)
            cs.append(tuple(lo*(1-shade)+hi*shade for lo,hi in zip(linear('536c2f'),linear('b0bd59'))))
    for j in range(len(profile)-1):
        for i in range(count):a=j*count+i;b=j*count+(i+1)%count;fs.append((a,b,b+count,a+count))
    vs.append((cx,cy,top));cs.append(linear('adb958'));tip=len(vs)-1
    for i in range(count):fs.append(((len(profile)-1)*count+i,(len(profile)-1)*count+(i+1)%count,tip))
    # Duplicate edge vertices keep the top normals and warm rim separate from the dark inner face.
    edge=len(vs)
    for p in vs[:count]:vs.append(p);cs.append(linear('48612d'))
    vs.append((cx,cy,base+.23));cs.append(linear('3f5429'));under=len(vs)-1
    for i in range(count):fs.append((edge+(i+1)%count,edge+i,under))
    mesh(parent,'RoundedTier'+str(layer),vs,fs,cs)

def broadleaf(oval=False):
    root=empty('forest_oval' if oval else 'forest_broadleaf');keep=empty('Keep',root);crown=empty('Occlude_Canopy',root)
    trunk(keep,3.4,.23)
    for i in range(3):
        a=i*math.tau/3+.4
        branch(keep,(0,0,1.4+i*.12),(.75*math.cos(a),.75*math.sin(a),2.2+i*.18),.12)
    tiers=((1.75,2.85,1.14),(2.35,3.45,1.32),(2.98,4.03,1.22),(3.57,4.48,.90)) if oval else ((1.75,3.02,1.73),(2.55,3.73,1.47),(3.28,4.38,1.12))
    for layer,(base,top,radius) in enumerate(tiers):leaf_dome(crown,layer,base,top,radius,.89 if oval else .94)
    return root

def fan_tree():
    root=empty('forest_fan');keep=empty('Keep',root);crown=empty('Occlude_Canopy',root);trunk(keep,2.9,.23)
    for i in range(4):
        a=i*math.tau/4;branch(keep,(0,0,1.65),(.5*math.cos(a),.5*math.sin(a),2.65),.10)
    for layer,count in enumerate((8,6)):
        for leaf in range(count):
            angle=leaf*math.tau/count+layer*.43
            d=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-d.y,d.x,0))
            length=(1.70 if layer==0 else 1.27)*(1+.07*math.sin(leaf*2.3))
            vs=[];cs=[];fs=[]
            for underside in (False,True):
                for j in range(9):
                    t=j/8;center=d*(.07+length*t)+Vector((0,0,2.86+layer*.36+.52*math.sin(t*math.pi)-.80*t*t))
                    width=(.50 if layer==0 else .44)*math.sin(math.pi*t)**.8
                    for q in (-1,-.45,0,.45,1):
                        p=center+side*(width*q)+Vector((0,0,-abs(q)*.13*math.sin(math.pi*t)-(.025 if underside else 0)))
                        vs.append(tuple(p));shade=.44+.25*math.sin(math.pi*t)+.10*(1-abs(q))
                        cs.append(linear('485f2d') if underside else tuple(lo*(1-shade)+hi*shade for lo,hi in zip(linear('607831'),linear('b0bd59'))))
                offset=45 if underside else 0
                for j in range(8):
                    for k in range(4):
                        a=offset+j*5+k;face=(a,a+5,a+6,a+1);fs.append(tuple(reversed(face)) if underside else face)
            # Close the two rims; explicit undersides remain visible with opaque back-face culling.
            for j in range(8):
                a=j*5;b=(j+1)*5;fs.append((a,a+45,b+45,b));a+=4;b+=4;fs.append((b,b+45,a+45,a))
            mesh(crown,'BroadBlade',vs,fs,cs)
    return root

def grass(which):
    root=empty('forest_grass_'+which);keep=empty('Keep',root);rng=random.Random(711 if which=='a' else 729)
    vs=[];cs=[];fs=[]
    for blade in range(14 if which=='a' else 10):
        angle=rng.random()*math.tau;d=Vector((math.cos(angle),math.sin(angle),0));cross=Vector((-d.y,d.x,0))
        base=Vector((rng.uniform(-.20,.20),rng.uniform(-.20,.20),0));height=rng.uniform(.23,.48 if which=='a' else .32);width=rng.uniform(.018,.037);bend=rng.uniform(.06,.19)
        start=len(vs)
        for j,t in enumerate((0,.34,.70,1)):
            center=base+Vector((0,0,height*t))+d*bend*t*t
            for side in (-1,1):
                p=center+cross*width*(1-t)*side;vs.append(tuple(p))
                lo=linear('59662e');hi=linear('b1b859' if blade%3 else '94a347')
                cs.append(tuple(a*(1-t)+b*t for a,b in zip(lo,hi)))
        for j in range(3):a=start+j*2;fs.append((a,a+1,a+3,a+2))
    mesh(keep,'BentBlades',vs,fs,cs,True);return root

def material(name,double=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.use_backface_culling=not double
    shader=m.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.92;shader.inputs['Specular IOR Level'].default_value=.18
    node=m.node_tree.nodes.new('ShaderNodeVertexColor');node.layer_name='ForestTint';m.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color']);return m

def run():
    global MAT_TREE,MAT_GRASS
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    MAT_TREE=material('WarmForest');MAT_GRASS=material('WarmGrass',True)
    roots=[pine(),broadleaf(),broadleaf(True),fan_tree(),grass('a'),grass('b')];records=[]
    for root in roots:
        for obj in root.children_recursive:obj.name=root.name+'__'+obj.name.split('.')[0]
    for root in roots:
        # One joined mesh for each occlusion role, preserving vertex colors and smooth normals.
        for part in list(root.children):
            objects=[o for o in part.children if o.type=='MESH']
            bpy.ops.object.select_all(action='DESELECT')
            for o in objects:o.select_set(True)
            bpy.context.view_layer.objects.active=objects[0]
            if len(objects)>1:bpy.ops.object.join()
            objects[0].name=part.name+'_mesh'
        objects=[root]+list(root.children_recursive);bpy.context.view_layer.update()
        for obj in objects[1:]:obj.name=obj.name.split('__')[-1].split('.')[0]
        coords=[o.matrix_world@Vector(c) for o in objects if o.type=='MESH' for c in o.bound_box]
        lo=[min(v[i] for v in coords) for i in range(3)];hi=[max(v[i] for v in coords) for i in range(3)]
        tri=0
        for o in objects:
            if o.type=='MESH':o.data.calc_loop_triangles();tri+=len(o.data.loop_triangles)
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.ops.export_scene.gltf(filepath=str(PACK/(root.name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
        for obj in objects[1:]:obj.name=root.name+'__'+obj.name
        tree='grass' not in root.name
        records.append(dict(id=root.name,name=root.name,category='Stage 2 숲 품질 기준',file=root.name+'.glb',dimensions_m=[round(hi[0]-lo[0],4),round(hi[2]-lo[2],4),round(hi[1]-lo[1],4)],collision=dict(kind='cylinder',radius=.25,height=1.4) if tree else dict(kind='none'),occlusion_parts=['Occlude_Canopy'] if tree else [],triangles=tri,bounds_min_y=round(lo[2],5),note='Stage 2 warm forest; vertex color, opaque, Blender source forest-study.blend'))
    manifest=json.loads((PACK/'manifest.json').read_text(encoding='utf-8'));manifest['assets']=[a for a in manifest['assets'] if a['id'] not in IDS]+records
    (PACK/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'prototypes/outdoor-kit/source/forest-study.blend'))
    OUT.mkdir(parents=True,exist_ok=True)
    # Independently reimport each delivered GLB to validate its exported geometry and roles.
    for record in records:
        before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(PACK/record['file']));imported=set(bpy.data.objects)-before
        meshes=[o for o in imported if o.type=='MESH'];count=0
        for o in meshes:o.data.calc_loop_triangles();count+=len(o.data.loop_triangles)
        assert count==record['triangles'] and count<14000,(record['id'],count)
        assert any(o.name.split('.')[0]=='Keep' for o in imported)
        assert all(len(o.data.color_attributes)>0 for o in meshes)
        if record['id'] in ('forest_broadleaf','forest_oval','forest_fan'):
            assert count<=5000 and max(record['dimensions_m'][::2])<=4 and record['dimensions_m'][1]<=4.6
            assert any(o.name.split('.')[0]=='Occlude_Canopy' for o in imported)
            assert abs(record['bounds_min_y'])<.001
        for o in imported:bpy.data.objects.remove(o,do_unlink=True)
    print('FOREST_BLENDER_PASS 6 assets; triangles '+str([r['triangles'] for r in records]))
    # Contact sheet using the actual authored meshes.
    for i,root in enumerate(roots[:4]):root.location.x=(i-1.5)*4.5
    roots[4].location=(0,-2,0);roots[5].location=(1,-2,0)
    bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object
    fm=bpy.data.materials.new('PreviewSoil');fm.diffuse_color=(*linear('777952'),1);floor.data.materials.append(fm);floor.location.z=-.01
    bpy.ops.object.light_add(type='SUN',location=(0,0,9));sun=bpy.context.object;sun.rotation_euler=(.5,-.4,-.6);sun.data.energy=2;sun.data.angle=.12
    bpy.ops.object.camera_add(location=(6,-20,13));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,2))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=20
    scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.world.color=(.3,.3,.3);scene.render.resolution_x=1800;scene.render.resolution_y=850;scene.render.resolution_percentage=100
    scene.render.filepath=str(OUT/'models.png');bpy.ops.render.render(write_still=True)
    (OUT/'report.json').write_text(json.dumps(records,indent=2),encoding='utf-8')

if __name__=='__main__':run()
