"""Blender --background --python tools/optimize_game_roster.py.

Run export-game-roster.mjs first. Authoring .blend and previews stay outside game/.
"""
import bpy
import bmesh
import json
import sys
from pathlib import Path
from mathutils import Vector

BASE=Path(__file__).resolve().parents[1]
ROOT=BASE.parents[1]
OUT=BASE/'output'/'roster'
PLAYERS=['goose','duck','raccoon','redpanda']
ENEMIES=['humanoid','crawler','tether','leaper','spitter']
BOSS_ONLY='--boss-only' in sys.argv
NAMED_ONLY='--named-only' in sys.argv
NAMED=['bull','balloon']
MIDBOSSES=['boss_balloon_king','boss_spitter_mother']
MIDBOSS_ONLY='--midboss-only' in sys.argv
SELECTED=ENEMIES+NAMED+['boss']+MIDBOSSES if '--monsters-only' in sys.argv else ['boss'] if BOSS_ONLY else MIDBOSSES if MIDBOSS_ONLY else NAMED if NAMED_ONLY else PLAYERS if '--players-only' in sys.argv else ENEMIES if '--enemies-only' in sys.argv else PLAYERS+ENEMIES

def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        if material.users==0: bpy.data.materials.remove(material)

def triangles(obj): return sum(len(p.vertices)-2 for p in obj.data.polygons)

def connect_surface(mesh):
    # glTF duplicates positions at UV/normal seams. Decimating these as independent
    # borders opens cracks. Weld within each authored part, preserving loop UVs and
    # material slots; never fill intentional open cloth/hood edges.
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    count=sum(edge.is_boundary for edge in bm.edges)
    bm.to_mesh(mesh);bm.free()
    return count

def surface_boundaries(mesh):
    bm=bmesh.new();bm.from_mesh(mesh)
    count=sum(edge.is_boundary for edge in bm.edges)
    bm.free();return count

def named_dimensions(kind, head_only):
    points=[]
    pivots=[f'{kind}_leftArm',f'{kind}_rightArm',f'{kind}_leftLeg',f'{kind}_rightLeg',f'{kind}_tail','LeftFoot','RightFoot']
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH': continue
        ancestors=[]
        node=obj
        while node:
            ancestors.append(node.name)
            node=node.parent
        in_head='Head' in ancestors
        is_limb=any(name in ancestors for name in pivots)
        if head_only != in_head or (not head_only and (is_limb or 'Deform' not in ancestors)): continue
        for vertex in obj.data.vertices:
            points.append(obj.matrix_world @ vertex.co)
    assert points, f'{kind}: missing {"Head" if head_only else "body"} geometry'
    # Blender imports glTF with model width on X and forward depth on Y.
    xs=[point.x for point in points]
    ys=[point.y for point in points]
    return {'width':max(xs)-min(xs),'depth':max(ys)-min(ys)}


def midboss_dimensions(kind, head_only):
    points=[]
    limb_pivots={'boss_leftArm','boss_rightArm',f'{kind}_leftLeg',f'{kind}_rightLeg','LeftFoot','RightFoot'}
    core_body={'boss_balloon_king':'round_plush_body','boss_spitter_mother':'pear_shaped_body'}[kind]
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH': continue
        ancestors=[]
        node=obj
        while node:
            ancestors.append(node.name)
            node=node.parent
        in_head='Head' in ancestors
        is_limb=any(name in ancestors for name in limb_pivots)
        if head_only != in_head or (not head_only and (is_limb or 'Deform' not in ancestors or obj.name!=core_body)): continue
        for vertex in obj.data.vertices:
            points.append(obj.matrix_world @ vertex.co)
    assert points, f'{kind}: missing {"Head" if head_only else "body"} geometry'
    # Blender imports glTF with model width on X and forward depth on Y.
    xs=[point.x for point in points]
    ys=[point.y for point in points]
    return {'width':max(xs)-min(xs),'depth':max(ys)-min(ys)}

reports=[]
for kind in SELECTED:
    clear()
    bpy.ops.import_scene.gltf(filepath=str(OUT/f'{kind}-source.glb'))
    player=kind in PLAYERS
    if not player and kind not in ['boss']+NAMED+MIDBOSSES:
        # Preserve authored limbs instead of baking them into Deform and adding duplicate arms at runtime.
        deform=bpy.data.objects['Deform']
        low=kind=='crawler';leap=kind=='leaper';fat=kind=='spitter'
        for side,label in [(-1,'left'),(1,'right')]:
            arm=bpy.data.objects.new(f'{kind}_{label}Arm',None)
            bpy.context.collection.objects.link(arm)
            arm.parent=deform
            # Source glTF is Y-up/+Z-forward; Blender imports it as Z-up/-Y-forward.
            arm.location=(side*(.39 if fat else .32),-.08 if low else 0,.46 if low else .78 if leap else 1.02)
            bpy.context.view_layer.update()
            for part in ['puffy_sleeve','short_forearm','mitten','thumb']:
                obj=bpy.data.objects[f'{part}_{side}']
                world=obj.matrix_world.copy();obj.parent=arm;obj.matrix_world=world
                assert all(abs(obj.matrix_world[r][c]-world[r][c])<1e-6 for r in range(4) for c in range(4))
    keep={f'{kind}_{p}' for p in ['root','body','deform','torso','head','helmet','beret','goggles','headset','vest','vestPadded','pack','weapon','tail','beak','leftFoot','rightFoot','leftArm','rightArm','leftEar','rightEar']}
    keep.update({'Muzzle','Body','Deform','Head','LeftFoot','RightFoot','Weapon'})
    if kind=='boss': keep.update({'boss_leftLeg','boss_rightLeg'})
    if kind in MIDBOSSES:
        keep.update({f'{kind}_leftLeg',f'{kind}_rightLeg','boss_leftArm','boss_rightArm'})
    if kind in NAMED:
        keep.update({f'{kind}_leftArm',f'{kind}_rightArm',f'{kind}_leftLeg',f'{kind}_rightLeg'})
        if kind=='bull': keep.add('bull_tail')
    keep.update(f'{kind}_{p}' for p in ['leftEye','rightEye','leftBrow','rightBrow','snout','crestShort','crestSwept'])
    groups={}
    before=0
    source_open_edges=0
    dimensions=({'body':named_dimensions(kind,False),'head':named_dimensions(kind,True)} if kind in NAMED else {'body':midboss_dimensions(kind,False),'head':midboss_dimensions(kind,True)} if kind in MIDBOSSES else None)
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH': continue
        before+=triangles(obj)
        bpy.context.view_layer.objects.active=obj
        if not player:
            original_open_edges=connect_surface(obj.data)
            source_open_edges+=original_open_edges
        if player and len(obj.data.polygons)>100:
            mod=obj.modifiers.new('Game simplification','DECIMATE')
            # Faces are inspected close up in the workshop. Preserve the silhouette of eyes,
            # muzzles and bird bills instead of applying the equipment's aggressive reduction.
            face_detail=any(part in obj.name for part in ['_eye_', '_brow_', '_muzzle', '_nose', '_smile', '_bill_', '_nostril_'])
            mod.ratio=.75 if obj.name.endswith('continuous_skin') else .70 if face_detail or '_headset_band' in obj.name else .50 if '_mic_boom' in obj.name else .19
            bpy.ops.object.modifier_apply(modifier=mod.name)
        elif not player and len(obj.data.polygons)>60:
            mod=obj.modifiers.new('Crowd budget','DECIMATE')
            mod.ratio=(.25 if kind=='bull' else .32) if kind in NAMED else .8 if kind=='boss_balloon_king' and obj.name=='great_balloon_head' else (.44 if kind=='boss_balloon_king' else .5) if kind in MIDBOSSES else .38
            bpy.ops.object.modifier_apply(modifier=mod.name)
        if not player:
            assert surface_boundaries(obj.data)<=original_open_edges,(kind,obj.name,'simplification opened surface')
        anchor=obj.parent
        while anchor and anchor.name not in keep: anchor=anchor.parent
        world=obj.matrix_world.copy();obj.parent=anchor;obj.matrix_world=world
        groups.setdefault(anchor,[]).append(obj)
    for anchor,objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join()
        bpy.context.object.name=(anchor.name if anchor else kind)+'_mesh'
    # Remove empty authoring detail pivots after their meshes were merged.
    for obj in list(bpy.context.scene.objects):
        if obj.type=='EMPTY' and obj.name not in keep and not obj.children:
            bpy.data.objects.remove(obj,do_unlink=True)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for obj in meshes:
        tri=obj.modifiers.new('Stable triangles','TRIANGULATE')
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=tri.name)
    target=ROOT/'game'/'assets'/('characters' if player else 'enemies')/kind
    target.mkdir(parents=True,exist_ok=True)
    path=target/f'{kind}.glb'
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',export_animations=False,export_extras=True)
    report={'id':kind,'source_triangles':before,'triangles':sum(triangles(o) for o in meshes),'meshes':len(meshes),'materials':sorted({m.name for o in meshes for m in o.data.materials if m}),'bytes':path.stat().st_size,'nodes':[o.name for o in bpy.context.scene.objects],'forward':'+Z','notes':'No bones. Runtime object pivots. Beret included for runtime visibility. Source vertex colors preserved.'}
    if not player:
        report['source_open_edges']=source_open_edges
        report['open_edges']=sum(surface_boundaries(o.data) for o in meshes)
        assert report['open_edges']<=source_open_edges,report
    if kind in NAMED+MIDBOSSES:
        report['body_dimensions']=dimensions['body']
        report['head_dimensions']=dimensions['head']
        report['notes']='No bones. Named arm, leg, foot and tail pivots retained for runtime object animation.' if kind in NAMED else 'No bones. Boss arm, leg and foot pivots retained for runtime object animation.'
    (target/f'{kind}-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    reports.append(report)
    print('ROSTER_ASSET '+json.dumps({k:v for k,v in report.items() if k not in ['nodes','materials']}))
    assert report['triangles'] < (30000 if player else 4500),report
    if player: assert all(any(m.startswith(f'custom_{channel}_') for m in report['materials']) for channel in ['body','beak','hat','vest','pack','wing','tail','feet','eyes','hair'])
    elif kind!='boss' and kind not in MIDBOSSES:
        if kind in NAMED:
            budget=3000 if kind=='bull' else 2500
            assert report['triangles']<budget,report
            required=[f'{kind}_root','Body','Deform','Head','Muzzle','Weapon',f'{kind}_leftArm',f'{kind}_rightArm',f'{kind}_leftLeg',f'{kind}_rightLeg','LeftFoot','RightFoot']
            if kind=='bull': required.append('bull_tail')
            assert all(name in report['nodes'] for name in required),report
            body=dimensions['body'];head=dimensions['head']
            assert body['depth']>=body['width']*.8,(kind,body)
            assert head['depth']>=head['width']*.85,(kind,head)
            assert all(f'{kind}_{side}Arm_mesh' in report['nodes'] and f'{kind}_{side}Leg_mesh' in report['nodes'] for side in ['left','right']),report
            if kind=='bull': assert 'bull_tail_mesh' in report['nodes'],report
        else:
            assert all(f'{kind}_{side}Arm' in report['nodes'] and f'{kind}_{side}Arm_mesh' in report['nodes'] for side in ['left','right'])
    elif kind in MIDBOSSES:
        assert report['triangles'] < 4000,report
        required=[f'{kind}_root','Body','Deform','Head','Muzzle','Weapon','boss_leftArm','boss_rightArm',f'{kind}_leftLeg',f'{kind}_rightLeg','LeftFoot','RightFoot','boss_leftArm_mesh','boss_rightArm_mesh',f'{kind}_leftLeg_mesh',f'{kind}_rightLeg_mesh','LeftFoot_mesh','RightFoot_mesh']
        assert all(name in report['nodes'] for name in required),(kind,[name for name in required if name not in report['nodes']])
        body=dimensions['body'];head=dimensions['head']
        assert body['depth']>=body['width']*.8,(kind,body)
        assert head['depth']>=head['width']*.85,(kind,head)
        # The balloon king keeps cream and ink materials of its own for the face, fur and glint.
        assert len(report['materials'])<=(6 if kind=='boss_balloon_king' else 5),(kind,report['materials'])
    else:
        assert report['triangles'] < 4500,report
        assert all(name in report['nodes'] for name in ['boss_leftArm','boss_rightArm','boss_leftArm_mesh','boss_rightArm_mesh']),report
        assert all(name in report['nodes'] for name in ['boss_leftLeg','boss_rightLeg','LeftFoot','RightFoot']),report

if '--no-preview' in sys.argv:
    contact='boss-contact' if BOSS_ONLY else 'midboss-contact' if MIDBOSS_ONLY else 'named-contact' if NAMED_ONLY else 'players-contact' if '--players-only' in sys.argv else 'enemies-contact' if '--enemies-only' in sys.argv else 'roster-contact'
    (OUT/f'{contact}-summary.json').write_text(json.dumps(reports,indent=2),encoding='utf8')
    sys.exit(0)

def material(name,color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.85
    return m

def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

clear()
floor=material('Studio floor',(.045,.06,.075))
label=material('Labels',(.8,.86,.9))
for row,kinds in enumerate([MIDBOSSES] if MIDBOSS_ONLY else [PLAYERS,ENEMIES,NAMED] if NAMED_ONLY else [PLAYERS,ENEMIES]):
    for i,kind in enumerate(kinds):
        if kind not in SELECTED: continue
        path=ROOT/'game'/'assets'/('characters' if kind in PLAYERS else 'enemies')/kind/f'{kind}.glb'
        bpy.ops.import_scene.gltf(filepath=str(path))
        root=bpy.data.objects.get(f'{kind}_root')
        root.location=( (i-(len(kinds)-1)/2)*2.3, 2.0-row*3.7, 0)
        if kind in PLAYERS:
            for suffix in ['beret','weapon_mesh','vestPadded','headset','crestShort','crestSwept']:
                o=bpy.data.objects.get(f'{kind}_{suffix}')
                if o:
                    for part in [o]+list(o.children_recursive):part.hide_render=True
        bpy.ops.object.text_add(location=(root.location.x,root.location.y-1.0,.02))
        text=bpy.context.object;text.name=f'label_{kind}';text.data.body=kind.upper();text.data.align_x='CENTER';text.data.size=.18;text.data.materials.append(label)
if BOSS_ONLY:
    path=ROOT/'game'/'assets'/'enemies'/'boss'/'boss.glb'
    bpy.ops.import_scene.gltf(filepath=str(path))
    root=bpy.data.objects.get('boss_root')
    root.location=(0,0,0)
    bpy.ops.object.text_add(location=(0,-1.0,.02))
    text=bpy.context.object;text.name='label_boss';text.data.body='BOSS';text.data.align_x='CENTER';text.data.size=.24;text.data.materials.append(label)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.04));bpy.context.object.data.materials.append(floor)
bpy.ops.object.camera_add(location=(2,-17,13));camera=bpy.context.object;aim(camera,(0,0,1));camera.data.type='ORTHO';camera.data.ortho_scale=12.5
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.world.color=(.23,.23,.23)
for name,pos,power,size in [('Key',(-5,-5,10),1600,7),('Fill',(6,-2,6),1000,6),('Rim',(1,6,9),1800,5)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;aim(o,(0,0,1))
scene.view_settings.view_transform='AgX'
contact='boss-contact' if BOSS_ONLY else 'midboss-contact' if MIDBOSS_ONLY else 'players-contact' if '--players-only' in sys.argv else 'enemies-contact' if '--enemies-only' in sys.argv else 'roster-contact'
if '--players-only' in sys.argv:
    camera.location=(2,-13,9);aim(camera,(0,2,1));camera.data.ortho_scale=10
    scene.render.resolution_y=700
scene.render.filepath=str(OUT/f'{contact}.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{contact}.blend'))
bpy.ops.render.render(write_still=True)
if '--players-only' in sys.argv:
    camera.location=(2,-12,6);aim(camera,(0,2,1.25));camera.data.ortho_scale=9.5
    for style in ['crestShort','crestSwept']:
        for kind in PLAYERS:
            for suffix in ['helmet','goggles','crestShort','crestSwept']:
                obj=bpy.data.objects.get(f'{kind}_{suffix}')
                if obj:
                    for part in [obj]+list(obj.children_recursive):part.hide_render=suffix!=style
        scene.render.filepath=str(OUT/f'players-{style}.png')
        bpy.ops.render.render(write_still=True)
(OUT/f'{contact}-summary.json').write_text(json.dumps(reports,indent=2),encoding='utf8')
