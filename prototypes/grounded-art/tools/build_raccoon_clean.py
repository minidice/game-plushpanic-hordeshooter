"""Build the clean raccoon from the immutable fitted GLB.

Blender: blender --background --python-exit-code 1 --python this_file.py
Imported Blender space is Z up and -Y forward; the exported GLB is Y up and +Z forward.
"""
import bpy
import math
import hashlib
import json
from pathlib import Path
from mathutils import Vector



def main():
    ROOT = Path(__file__).resolve().parents[3]
    SOURCE = ROOT / 'game/assets/characters/raccoon/raccoon-fitted.glb'
    TARGET = ROOT / 'game/assets/characters/raccoon/raccoon-clean.glb'
    OUT = ROOT / 'prototypes/grounded-art/output/raccoon-clean'
    OUT.mkdir(parents=True, exist_ok=True)
    IMMUTABLE = [SOURCE, *[p for p in (ROOT/'game/assets/characters').glob('*/*.glb') if p != TARGET and p != SOURCE]]
    before_hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in IMMUTABLE}
    (OUT/'immutable-before.json').write_text(json.dumps(before_hashes,indent=2),encoding='utf-8')

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    scene = bpy.context.scene
    root = bpy.data.objects['raccoon_root']
    GEAR = ('raccoon_beret', 'raccoon_goggles', 'raccoon_headset',
            'raccoon_helmet', 'raccoon_pack', 'raccoon_vest', 'raccoon_vestPadded')


    def camera_and_light():
        world = bpy.data.worlds.new('Review world')
        scene.world = world
        world.use_nodes = True
        world.node_tree.nodes['Background'].inputs['Color'].default_value = (.72, .77, .83, 1)
        world.node_tree.nodes['Background'].inputs['Strength'].default_value = .6
        camera_data = bpy.data.cameras.new('Review camera')
        camera = bpy.data.objects.new('Review camera', camera_data)
        scene.collection.objects.link(camera)
        camera_data.type = 'ORTHO'
        camera_data.ortho_scale = 2.75
        scene.camera = camera
        light_data = bpy.data.lights.new('Review key', 'AREA')
        light = bpy.data.objects.new('Review key', light_data)
        scene.collection.objects.link(light)
        light.location = (-3, -4, 6)
        light_data.energy = 450
        light_data.shape = 'DISK'
        light_data.size = 5
        scene.render.engine = 'BLENDER_EEVEE'
        scene.render.resolution_x = 640
        scene.render.resolution_y = 640
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        scene.render.film_transparent = False
        scene.view_settings.view_transform = 'AgX'
        neutral = bpy.data.materials.new('Review neutral')
        neutral.diffuse_color = (.57, .59, .60, 1)
        neutral.use_nodes = True
        neutral.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.57, .59, .60, 1)
        shadow = bpy.data.materials.new('Review silhouette')
        shadow.diffuse_color = (.035,.039,.043,1)
        shadow.use_nodes = True
        shadow.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.035,.039,.043,1)
        return camera, neutral, shadow


    camera, neutral, shadow = camera_and_light()


    def render_set(label):
        for mode in ('silhouette', 'neutral', 'color', 'gear'):
            scene.view_layers[0].material_override = shadow if mode == 'silhouette' else (neutral if mode == 'neutral' else None)
            for name in GEAR:
                obj=bpy.data.objects[name]
                for part in (obj,*obj.children_recursive): part.hide_render = mode != 'gear' or name not in ('raccoon_helmet','raccoon_vest','raccoon_pack')
            obj=bpy.data.objects['raccoon_weapon']
            for part in (obj,*obj.children_recursive): part.hide_render = True
            for angle, name in ((0, 'front'), (math.radians(35), 'quarter'),
                                (math.radians(90), 'side')):
                camera.location = (3.4*math.sin(angle), -3.4*math.cos(angle), 2.35)
                direction = Vector((0, 0, 1.03)) - camera.location
                camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
                scene.render.filepath = str(OUT / f'{label}-{mode}-{name}.png')
                bpy.ops.render.render(write_still=True)
        scene.view_layers[0].material_override = None
        for name in GEAR:
            obj=bpy.data.objects[name]
            for part in (obj,*obj.children_recursive): part.hide_render = False
        obj=bpy.data.objects['raccoon_weapon']
        for part in (obj,*obj.children_recursive): part.hide_render = False


    render_set('before')


    def replace_mesh(name, vertices, faces, material_ids=None):
        obj = bpy.data.objects[name]
        old = obj.data
        inverse = obj.matrix_world.inverted()
        mesh = bpy.data.meshes.new(name + '_clean')
        mesh.from_pydata([inverse @ Vector(v) for v in vertices], [], faces)
        mesh.update()
        for material in old.materials: mesh.materials.append(material)
        if material_ids:
            for polygon, index in zip(mesh.polygons, material_ids): polygon.material_index = index
        for polygon in mesh.polygons: polygon.use_smooth = True
        obj.data = mesh
        if old.users == 0: bpy.data.meshes.remove(old)
        return obj


    def oval_rings(rings, steps=16):
        """Closed loft of (x,y,z,x-radius,y-radius), with caps."""
        verts=[];faces=[]
        for x,y,z,rx,ry in rings:
            for k in range(steps):
                a=2*math.pi*k/steps
                verts.append((x+rx*math.cos(a), y+ry*math.sin(a), z))
        faces.append(tuple(reversed(range(steps))))
        for i in range(len(rings)-1):
            for k in range(steps):
                j=(k+1)%steps
                faces.append((i*steps+k,i*steps+j,(i+1)*steps+j,(i+1)*steps+k))
        faces.append(tuple((len(rings)-1)*steps+k for k in range(steps)))
        return verts,faces


    def palm_loft(x,sign):
        """One closed flattened palm with a thumb flowing from the same rings."""
        sections=(
            (.621,-.272,.038,.021),
            (.632,-.272,.056,.031),
            (.651,-.264,.067,.038),
            (.680,-.252,.078,.050),
            (.702,-.252,.082,.050),
            (.722,-.252,.075,.050),
            (.744,-.234,.058,.040),
            (.760,-.220,.047,.031),
            (.770,-.217,.033,.022))
        steps=32;verts=[];faces=[]
        for z,cy,rx,ry in sections:
            for k in range(steps):
                angle=math.tau*k/steps
                c=math.cos(angle);s=math.sin(angle)
                # A rounded rectangular section gives a broad dorsal plane,
                # unlike a sphere; end rings taper into the gripping fingers.
                dx=rx*math.copysign(abs(c)**.63,c)
                dy=ry*math.copysign(abs(s)**.63,s)
                # Localized inward/front lobe joins the palm without a seam.
                direction=max(0.0,(-sign*c-s)/math.sqrt(2))**6
                thumb=direction*math.exp(-((z-.695)/.024)**2)
                verts.append((x+dx-sign*.027*thumb,cy+dy-.016*thumb,z))
        faces.append(tuple(reversed(range(steps))))
        for row in range(len(sections)-1):
            for k in range(steps):
                j=(k+1)%steps
                faces.append((row*steps+k,row*steps+j,(row+1)*steps+j,(row+1)*steps+k))
        faces.append(tuple((len(sections)-1)*steps+k for k in range(steps)))
        return verts,faces


    # Keep the torso coordinates untouched: the face marking shader samples that space.
    # The short cream muzzle is broader at its root and less spherical at the nose.
    snout=bpy.data.objects['raccoon_snout_mesh']
    for vertex in snout.data.vertices:
        world=snout.matrix_world@vertex.co
        root_weight=max(0.0,min(1.0,(world.y+.55)/.24))
        world.x *= 1.24+.13*root_weight
        world.y = -.420+(world.y+.420)*.50
        world.z = 1.220+(world.z-1.220)*.80
        vertex.co=snout.matrix_world.inverted()@world
    snout.data.update()


    # Existing eye and brow nodes stay available to the runtime sliders. Their meshes
    # become lower relief while their pivots, material channels and names stay intact.
    for side in ('left','right'):
        for part, factor in (('Eye',.78),('Brow',.55)):
            obj=bpy.data.objects[f'raccoon_{side}{part}_mesh']
            center=obj.parent.matrix_world.translation.copy()
            for vertex in obj.data.vertices:
                p=obj.matrix_world@vertex.co
                p.y=center.y+(p.y-center.y)*factor
                if part=='Brow': p.z=center.z+(p.z-center.z)*.78
                vertex.co=obj.matrix_world.inverted()@p
            obj.data.update()


    # One closed cupped ear surface. The cream center is topology-connected to its
    # gray rim; a radial drop toward the center creates the inset without an overlay.
    for side,sign in (('left',-1),('right',1)):
        cx=.34*sign
        count=32
        radii=(.20,.40,.58,.72,.86,1.0)
        vertices=[];faces=[];ids=[]
        center_z=1.725
        def point(radius,k,front):
            angle=math.tau*k/count
            s=math.sin(angle)
            z=center_z+radius*.145*s
            width=.145-.008*s
            x=cx+radius*width*math.cos(angle)+sign*.012*((z-1.58)/.29)
            y=(-.055+.037*(1-radius*radius)) if front else (.057+.022*(1-radius*radius))
            return (x,y,z)
        # Front faces toward -Y. Material 0 is cream, 1 is the gray fur rim.
        front_center=len(vertices);vertices.append((cx+sign*.006,-.018,center_z))
        front_offsets=[]
        for radius in radii:
            front_offsets.append(len(vertices))
            vertices.extend(point(radius,k,True) for k in range(count))
        for k in range(count):
            faces.append((front_center,front_offsets[0]+k,front_offsets[0]+(k+1)%count));ids.append(0)
        for r in range(len(radii)-1):
            a=front_offsets[r];b=front_offsets[r+1]
            for k in range(count):
                faces.append((a+k,b+k,b+(k+1)%count,a+(k+1)%count))
                ids.append(0 if radii[r+1]<=.72 else 1)
        # Closed gray back and perimeter band; no disconnected white plate.
        back_center=len(vertices);vertices.append((cx+sign*.006,.079,center_z))
        back_offsets=[]
        for radius in radii:
            back_offsets.append(len(vertices))
            vertices.extend(point(radius,k,False) for k in range(count))
        for k in range(count):
            faces.append((back_center,back_offsets[0]+(k+1)%count,back_offsets[0]+k));ids.append(1)
        for r in range(len(radii)-1):
            a=back_offsets[r];b=back_offsets[r+1]
            for k in range(count):
                faces.append((a+(k+1)%count,b+(k+1)%count,b+k,a+k));ids.append(1)
        for k in range(count):
            faces.append((front_offsets[-1]+k,back_offsets[-1]+k,
                          back_offsets[-1]+(k+1)%count,front_offsets[-1]+(k+1)%count));ids.append(1)
        replace_mesh(f'raccoon_{side}Ear_mesh',vertices,faces,ids)


    def empty(name,parent,world_at):
        obj=bpy.data.objects.new(name,None)
        scene.collection.objects.link(obj)
        obj.parent=parent
        obj.matrix_world.translation=Vector(world_at)
        return obj


    def new_mesh(name,parent,world_at,verts,faces,material):
        mesh=bpy.data.meshes.new(name)
        obj=bpy.data.objects.new(name,mesh)
        scene.collection.objects.link(obj)
        obj.parent=parent
        obj.matrix_world.translation=Vector(world_at)
        inv=obj.matrix_world.inverted()
        mesh.from_pydata([inv@Vector(v) for v in verts],[],faces)
        mesh.materials.append(material)
        for polygon in mesh.polygons:polygon.use_smooth=True
        return obj


    for side,sign in (('left',-1),('right',1)):
        arm=bpy.data.objects[f'raccoon_{side}Arm']
        arm_mesh=bpy.data.objects[f'raccoon_{side}Arm_mesh']
        material=arm_mesh.data.materials[0]
        x=.43*sign
        # The original Arm pivot is at Z=.89, near the middle of its old mesh.
        # The true top of the forearm gets an authored shoulder marker so runtime
        # can stretch from the surface endpoint without changing the old pivot.
        verts,faces=oval_rings([
            (x,-.195,.745,.061,.063),
            (x+sign*.019,-.180,.795,.074,.072),
            (x+sign*.030,-.155,.910,.086,.082),
            (x+sign*.023,-.145,1.020,.100,.095),
            (x+sign*.010,-.158,1.125,.086,.082)])
        replace_mesh(arm_mesh.name,verts,faces)
        empty(f'raccoon_{side}Shoulder',arm,(x+sign*.010,-.158,1.125))
        wrist=(x,-.207,.752)
        paw=empty(f'raccoon_{side}Paw',arm,wrist)
        verts,faces=palm_loft(x,sign)
        new_mesh(f'raccoon_{side}Paw_mesh',paw,wrist,verts,faces,material)
        empty(f'raccoon_{side}Wrist',paw,wrist)
        empty(f'raccoon_{side}Grip',paw,(x-sign*.040,-.315,.687))


    # Narrow ankle grows into a broad but shallow forefoot, with an actual flat sole.
    for side,sign in (('left',-1),('right',1)):
        x=.2*sign
        vertices,faces=oval_rings([
            (x,-.071,.000,.093,.146),
            (x,-.071,.020,.104,.155),
            (x,-.066,.065,.104,.153),
            (x,-.057,.108,.092,.134),
            (x,-.025,.161,.060,.080),
            (x,-.018,.285,.049,.058),
            (x,-.018,.385,.045,.049)])
        ids=[1]+[0]*(len(faces)-2)+[2]
        replace_mesh(f'raccoon_{side}Foot_mesh',vertices,faces,ids)


    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from mammal_tail_profile import replace_tail
    replace_tail("raccoon")

    # Export exactly the original hierarchy and its new paw children, not review objects.
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children_recursive:child.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.gltf(filepath=str(TARGET),export_format='GLB',use_selection=True,
                              export_apply=False,export_extras=True)
    render_set('after')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'raccoon-clean.blend'))
    assert {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in IMMUTABLE} == before_hashes, 'immutable source or other player GLB changed'
    print('BUILT',TARGET,flush=True)


if __name__ == "__main__":
    main()
