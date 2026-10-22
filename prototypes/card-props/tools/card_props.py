"""Three card props; standalone Blender bpy script, metres, Z up, front -Y.

Reviewer runs:
  blender --background --python prototypes/card-props/tools/card_props.py
Authoring check only: python -m py_compile prototypes/card-props/tools/card_props.py
Helpers follow transition_props.py / ground_bits.py; neither is imported.
Outputs are generated only by main(), never by the authoring syntax check.
"""
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game' / 'assets' / 'card-props'
EXPECTED = {
    'button_missile': (.20, .20, .06),
    'toy_train': (.9, 2.4, 1.0),
    'jack_box': (.75, .75, .8),
}
BUDGET = dict(button_missile=160, toy_train=2600, jack_box=2400)
REQUIRED = {
    'button_missile': {'Body'},
    'toy_train': {*(f'Wheel_{i}' for i in range(8)), 'Smoke'},
    'jack_box': {'Lid', 'Jack', 'Crank'},
}
PALETTE = {
    'WoodDark': '8a6240', 'Red': 'e5484d', 'Blue': '3f6fd8',
    'Yellow': 'ffd23f', 'White': 'f3f4f1', 'Rubber': '2b2f36',
    'Orange': 'ff9b4a',
}
M = {}


def mat(name, color):
    rgb = [int(color[i:i+2], 16)/255 for i in (0, 2, 4)]
    rgb = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*rgb, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = .78
    shader.inputs['Metallic'].default_value = 0
    return material


def empty(name, parent=None, at=(0, 0, 0), rotating=False):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = at
    if rotating:
        # These parts expose their undersides while rolling/opening/turning.
        obj['retain_underside'] = True
    return obj


def mesh(parent, name, vertices, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    data.materials.append(M[material])
    return obj


def finish(obj, parent, name, at, material):
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.data.materials.append(M[material])
    return obj


def box(parent, name, at, size, material, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = finish(bpy.context.object, parent, name, at, material)
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft toy corners', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def cyl(parent, name, at, radius, depth, material, axis='Z', sides=12, tip=None):
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius, radius2=radius if tip is None else tip,
        depth=depth)
    obj = finish(bpy.context.object, parent, name, at, material)
    if axis == 'X':
        obj.rotation_euler.y = math.pi/2
    elif axis == 'Y':
        obj.rotation_euler.x = math.pi/2
    return obj


def ico(parent, name, at, scale, material, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1)
    obj = finish(bpy.context.object, parent, name, at, material)
    obj.scale = scale
    return obj


def button_missile():
    root = empty('button_missile')
    body = empty('Body', root, (0, 0, .03), rotating=True)
    # Eight-sided turned profile: 48 side triangles + 6 bottom triangles.
    # The top has four actual recessed holes, not disks hidden in a solid cap.
    sides = 8
    profile = ((.088, -.03), (.100, -.021), (.100, .021), (.088, .03))
    vertices = [(r*math.cos(i*math.tau/sides), r*math.sin(i*math.tau/sides), z)
                for r, z in profile for i in range(sides)]
    faces = []
    for row in range(3):
        for i in range(sides):
            j = (i+1) % sides
            faces.append((row*sides+i, row*sides+j,
                          (row+1)*sides+j, (row+1)*sides+i))
    faces.append(tuple(reversed(range(sides))))
    mesh(body, 'RoundedDisc', vertices, faces, 'Red')
    rim = [(r*math.cos(i*math.tau/sides), r*math.sin(i*math.tau/sides), .03)
           for r in (.080, .088) for i in range(sides)]
    mesh(body, 'WhiteRim', rim,
         [(i, 8+i, 8+(i+1) % 8, (i+1) % 8) for i in range(8)], 'White')
    # Each quadrant is an annulus around a four-sided shallow well: 18 tris.
    # Combined total = 54 + 16 + 4*18 = 142, including rolling undersides.
    for quadrant in range(4):
        outer = [(0, 0), (.080, 0), (.080/math.sqrt(2), .080/math.sqrt(2)), (0, .080)]
        hole = []
        for x, y in outer:
            dx, dy = x-.027, y-.027
            length = math.hypot(dx, dy)
            hole.append((.027+.012*dx/length, .027+.012*dy/length))
        angle = quadrant*math.pi/2

        def turn(points, z):
            return [(x*math.cos(angle)-y*math.sin(angle),
                     x*math.sin(angle)+y*math.cos(angle), z) for x, y in points]

        vertices = turn(outer, .03)+turn(hole, .03)+turn(hole, .020)
        faces = []
        for i in range(4):
            j = (i+1) % 4
            faces.extend(((i, j, 4+j, 4+i), (4+i, 4+j, 8+j, 8+i)))
        mesh(body, 'HoleWall', vertices, faces, 'Red')
        mesh(body, 'HoleFloor', turn(hole, .020), [(0, 1, 2, 3)], 'Rubber')
    return root


def toy_train():
    root = empty('toy_train')
    # -Y is the engine nose; +Y is the single trailing freight wagon.
    box(root, 'EngineFrame', (0, -.55, .32), (.68, 1.30, .16), 'WoodDark')
    box(root, 'WagonFrame', (0, .75, .32), (.68, .90, .16), 'WoodDark')
    box(root, 'Coupler', (0, .20, .30), (.14, .30, .09), 'WoodDark', .015)
    cyl(root, 'Boiler', (0, -.74, .59), .23, .72, 'Red', axis='Y', sides=16)
    cyl(root, 'BoilerFront', (0, -1.105, .59), .19, .025, 'Yellow', axis='Y')
    box(root, 'Cab', (0, -.10, .62), (.56, .42, .44), 'Red', .045)
    box(root, 'CabRoof', (0, -.10, .88), (.70, .55, .10), 'Yellow', .035)
    for x in (-.284, .284):
        box(root, 'CabWindow', (x, -.10, .70), (.016, .25, .20), 'WoodDark', .008)
    cyl(root, 'Chimney', (0, -.94, .84), .085, .26, 'WoodDark')
    cyl(root, 'ChimneyLip', (0, -.94, .97), .115, .06, 'Yellow')
    empty('Smoke', root, (0, -.94, 1.0))
    box(root, 'FreightBox', (0, .75, .60), (.62, .80, .40), 'Blue', .045)
    box(root, 'FreightRim', (0, .75, .81), (.66, .84, .065), 'Yellow', .020)
    for index, (y, side) in enumerate((y, side) for y in (-.90, -.30, .43, .99)
                                      for side in (-1, 1)):
        wheel = empty(f'Wheel_{index}', root, (side*.375, y, .20), rotating=True)
        cyl(wheel, 'Tyre', (0, 0, 0), .20, .15, 'Rubber', axis='X', sides=16)
        # Outward hub on each side of the train; the tyre retains both caps.
        cyl(wheel, 'Hub', (side*.078, 0, 0), .095, .012, 'Yellow', axis='X')
        # An off-centre wooden peg makes rotation visible on the round hub.
        cyl(wheel, 'HubPeg', (side*.087, .045, 0), .018, .012,
            'WoodDark', axis='X', sides=6)
    return root


def spring(parent):
    """Continuous compressed seven-turn tube; no curves/modifiers in GLB."""
    turns, steps, sides = 7, 84, 5
    vertices, faces = [], []
    rise = .22
    for i in range(steps+1):
        angle = math.tau*turns*i/steps
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        tangent = Vector((-.09*math.sin(angle), .09*math.cos(angle),
                          rise/(math.tau*turns))).normalized()
        other = tangent.cross(radial).normalized()
        center = radial*.09+Vector((0, 0, .018+rise*i/steps))
        for j in range(sides):
            a = math.tau*j/sides
            vertices.append(tuple(center+.009*(math.cos(a)*radial+math.sin(a)*other)))
    for i in range(steps):
        for j in range(sides):
            a, b = i*sides+j, i*sides+(j+1) % sides
            faces.append((a, b, b+sides, a+sides))
    faces.extend((tuple(reversed(range(sides))),
                  tuple(steps*sides+j for j in range(sides))))
    mesh(parent, 'SevenCoilSpring', vertices, faces, 'White')


def jack_box():
    root = empty('jack_box')
    # Hollow shell; its floor top is z=.07 and inside walls are at +/- .315.
    # Round only the XY corners here: culling a lower Z bevel would lift the
    # lowest surviving vertex off z=0. Vertical skirt edges keep the floor seated.
    outline = [(-.35, -.375), (.35, -.375), (.375, -.35), (.375, .35),
               (.35, .375), (-.35, .375), (-.375, .35), (-.375, -.35)]
    vertices = [(x, y, z) for z in (0, .07) for x, y in outline]
    faces = [(i, (i+1) % 8, 8+(i+1) % 8, 8+i) for i in range(8)]
    faces.append(tuple(range(8, 16)))
    mesh(root, 'Floor', vertices, faces, 'Blue')
    box(root, 'Front', (0, -.345, .40), (.75, .06, .68), 'Red', .020)
    box(root, 'Back', (0, .345, .40), (.75, .06, .68), 'Blue', .020)
    for x in (-.345, .345):
        box(root, 'Side', (x, 0, .40), (.06, .64, .68), 'Yellow', .020)
    # A raised toy star uses the existing yellow material, visible from above/front.
    vertices = [(0, -.379, .42)]
    for i in range(10):
        a = math.pi/2+math.tau*i/10
        r = .14 if i % 2 == 0 else .065
        vertices.append((r*math.cos(a), -.379, .42+r*math.sin(a)))
    mesh(root, 'Star', vertices, [(0, i+1, (i+1) % 10+1) for i in range(10)], 'Yellow')
    lid = empty('Lid', root, (0, .375, .74), rotating=True)
    # Local -Y extends from the rear hinge to the front; X=-110deg opens upward.
    box(lid, 'LidPlate', (0, -.375, .03), (.75, .75, .06), 'Red', .020)
    box(lid, 'LidInlay', (0, -.375, .061), (.52, .52, .002), 'Yellow', 0)
    for x in (-.23, .23):
        cyl(lid, 'Hinge', (x, 0, 0), .028, .14, 'Yellow', axis='X', sides=8)
    crank = empty('Crank', root, (.375, 0, .43), rotating=True)
    cyl(crank, 'Axle', (.035, 0, 0), .038, .09, 'Blue', axis='X', sides=8)
    box(crank, 'Arm', (.08, 0, -.065), (.035, .045, .16), 'Yellow', .014)
    cyl(crank, 'Grip', (.113, 0, -.13), .043, .07, 'Red', axis='X', sides=10)
    jack = empty('Jack', root, (0, 0, .07))
    spring(jack)
    # Fully stowed geometry: local top < .64, below the lid underside (.74 world).
    ico(jack, 'GooseNeck', (0, .015, .295), (.075, .070, .09), 'White')
    ico(jack, 'GooseHead', (0, -.015, .405), (.145, .125, .14), 'White')
    box(jack, 'GooseBeak', (0, -.153, .377), (.17, .16, .065), 'Orange', .025)
    ico(jack, 'ClownNose', (0, -.183, .431), (.047, .047, .047), 'Red')
    for side in (-1, 1):
        ico(jack, 'Eye', (side*.087, -.111, .454), (.023, .018, .025), 'Blue', 1)
    cyl(jack, 'HatBrim', (0, .005, .521), .12, .025, 'Yellow')
    cyl(jack, 'PointedHat', (0, .005, .576), .10, .11, 'Yellow', tip=0)
    return root


def merge_parts(root):
    """Bake into each direct parent's local frame, merge by material per part.

    Root and animation Empty transforms survive unchanged. Cull by authoring
    WORLD Z, not a primitive cylinder's rotated local normal. Rotating parts
    retain their undersides. Triangles are explicit before budget validation.
    """
    bpy.context.view_layer.update()
    for group in [root]+[o for o in root.children_recursive if o.type == 'EMPTY']:
        originals = [o for o in group.children if o.type == 'MESH']
        buckets = {}
        inverse = group.matrix_world.inverted()
        for obj in originals:
            obj.data.calc_loop_triangles()
            for tri in obj.data.loop_triangles:
                world = [obj.matrix_world @ obj.data.vertices[i].co for i in tri.vertices]
                normal = (world[1]-world[0]).cross(world[2]-world[0])
                if normal.length < 1e-10:
                    continue
                if not group.get('retain_underside', False) and normal.normalized().z < -.5:
                    continue
                material = obj.data.materials[tri.material_index]
                vertices, faces = buckets.setdefault(material.name, ([], []))
                offset = len(vertices)
                vertices.extend(tuple(inverse @ point) for point in world)
                faces.append((offset, offset+1, offset+2))
        for obj in originals:
            data = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if data.users == 0:
                bpy.data.meshes.remove(data)
        for material, (vertices, faces) in sorted(buckets.items()):
            mesh(group, group.name+'_'+material, vertices, faces, material)


def bounds(objects):
    points = [o.matrix_world @ v.co for o in objects if o.type == 'MESH'
              for v in o.data.vertices]
    assert points, 'no mesh vertices'
    return (Vector(tuple(min(p[i] for p in points) for i in range(3))),
            Vector(tuple(max(p[i] for p in points) for i in range(3))))


def validate(root):
    asset_id = root.name
    bpy.context.view_layer.update()
    objects = list(root.children_recursive)
    meshes = [o for o in objects if o.type == 'MESH']
    nodes = {o.name: o for o in objects if o.type == 'EMPTY'}
    assert root.type == 'EMPTY' and root['card_prop'] is True, asset_id
    assert root.location.length < 1e-6 and root.rotation_euler.to_matrix() == Matrix.Identity(3)
    assert set(nodes) == REQUIRED[asset_id], (asset_id, set(nodes))
    assert all(o.parent == root for o in nodes.values()), asset_id
    materials, triangles = set(), 0
    for obj in meshes:
        assert len(obj.data.materials) == 1 and not obj.modifiers, obj.name
        material = obj.data.materials[0]
        materials.add(material.name)
        assert not any(n.type == 'TEX_IMAGE' for n in material.node_tree.nodes)
        assert all(len(face.vertices) == 3 for face in obj.data.polygons), obj.name
        triangles += len(obj.data.polygons)
        if not obj.parent.get('retain_underside', False):
            assert all(face.normal.z >= -.50001 for face in obj.data.polygons), obj.name
    assert 0 < triangles < BUDGET[asset_id], (asset_id, triangles)
    assert 0 < len(materials) <= 5, (asset_id, materials)
    low, high = bounds(meshes)
    assert abs(low.z) <= .002, (asset_id, 'lowest Z', low.z)
    assert all(.75*target <= actual <= 1.25*target
               for actual, target in zip(high-low, EXPECTED[asset_id])), (asset_id, tuple(high-low))
    for group in [root]+list(nodes.values()):
        mats = [o.data.materials[0].name for o in group.children if o.type == 'MESH']
        assert len(mats) == len(set(mats)), (asset_id, group.name, 'unmerged material')
    if asset_id == 'button_missile':
        assert (nodes['Body'].location-Vector((0, 0, .03))).length < 1e-6
        assert abs(low.x+high.x) < .002 and abs(low.y+high.y) < .002
    elif asset_id == 'toy_train':
        assert not nodes['Smoke'].children and abs(nodes['Smoke'].location.z-1.0) < 1e-6
        for i in range(8):
            wheel = nodes[f'Wheel_{i}']
            assert wheel.rotation_euler.to_matrix() == Matrix.Identity(3)
            lo, hi = bounds(list(wheel.children))
            assert abs((lo.y+hi.y)/2-wheel.location.y) < .002
            assert abs((lo.z+hi.z)/2-wheel.location.z) < .002
            assert abs(lo.z) < .002, (wheel.name, 'wheel must touch ground')
    else:
        assert (nodes['Lid'].location-Vector((0, .375, .74))).length < 1e-6
        assert (nodes['Jack'].location-Vector((0, 0, .07))).length < 1e-6
        assert (nodes['Crank'].location-Vector((.375, 0, .43))).length < 1e-6
        lo, hi = bounds(list(nodes['Jack'].children))
        assert lo.z >= .07 and hi.z < .74, ('Jack not stowed', tuple(lo), tuple(hi))
        assert min(lo.x, lo.y) > -.315 and max(hi.x, hi.y) < .315, 'Jack intersects walls'
        opened_front = Matrix.Rotation(math.radians(-110), 4, 'X') @ Vector((0, -.75, 0))
        assert opened_front.z > 0 and opened_front.y > 0, 'wrong lid opening direction'
    return triangles


def main():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # No imported builders, textures, external packages, manifests or game edits.
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    M.update({name: mat(name, color) for name, color in PALETTE.items()})
    assert all(material.name == name for name, material in M.items()), 'palette name collision'
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    roots = [builder() for builder in (button_missile, toy_train, jack_box)]
    counts = {}
    # Validate ALL assets before creating any export files.
    for root in roots:
        root['card_prop'] = True
        merge_parts(root)
        counts[root.name] = validate(root)
    assert set(counts) == set(EXPECTED) and len(roots) == 3
    PACK.mkdir(parents=True, exist_ok=True)
    (ROOT/'source').mkdir(parents=True, exist_ok=True)
    for root in roots:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in [root]+list(root.children_recursive):
            obj.select_set(True)
        bpy.context.view_layer.objects.active = root
        result = bpy.ops.export_scene.gltf(
            filepath=str(PACK/(root.name+'.glb')), export_format='GLB',
            use_selection=True, export_extras=True, export_yup=True, export_animations=False)
        assert result == {'FINISHED'}, (root.name, 'GLB export failed', result)
    # Source-only display spacing; exported roots were all at the bottom origin.
    for i, root in enumerate(roots):
        root.location.x = i*2.0
    result = bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source'/'card-props.blend'))
    assert result == {'FINISHED'}, ('blend save failed', result)
    for asset_id, triangles in counts.items():
        print('CARD_PROP_READY', asset_id, triangles)
    print('CARD_PROPS_BUILT 3')


if __name__ == '__main__':
    main()
