extends SceneTree
const ASSETS = preload("res://preview/canonical_assets.gd")

func _initialize() -> void:
    call_deferred("run_checks")

func run_checks() -> void:
    var manifest: Dictionary = ASSETS.manifest()
    var checked := 0
    var river_section: Array[String] = []
    for a in manifest.assets:
        var instance := ASSETS.model(a.file)
        if instance == null:
            push_error("Cannot load "+a.id)
            quit(2)
            return
        root.add_child(instance)
        if instance.find_child("Keep",true,false) == null:
            push_error("Missing Keep: "+a.id)
            quit(2)
            return
        for part in a.occlusion_parts:
            if instance.find_child(part,true,false) == null:
                push_error("Missing "+part+" in "+a.id)
                quit(2)
                return
        var bounds := measure(instance)
        var expected := Vector3(a.dimensions_m[0],a.dimensions_m[1],a.dimensions_m[2])
        if absf(bounds.position.y)>0.015 or (bounds.size-expected).length()>0.02:
            push_error("Incorrect scale or ground pivot: "+a.id+" "+str(bounds))
            quit(2)
            return
        for socket in a.get("connectors",[]):
            var connection := instance.find_child(socket.node,true,false) as Node3D
            if connection == null or connection.global_position.distance_to(Vector3(socket.position[0],socket.position[1],socket.position[2])) > .001:
                push_error("Invalid river connector: "+a.id+" "+socket.node)
                quit(2)
                return
            var forward := Vector3(socket.forward[0],socket.forward[1],socket.forward[2])
            var across := Vector3(-forward.z,0,forward.x)
            var section: Array[String] = []
            for mesh_node in instance.find_children("*","MeshInstance3D",true,false):
                for surface in mesh_node.mesh.get_surface_count():
                    var material := mesh_node.mesh.surface_get_material(surface) as StandardMaterial3D
                    if material.resource_name not in ["StreamWater","WetSilt","BankEarth","ShoreGrass"]: continue
                    if material.resource_name == "StreamWater":
                        assert(material.vertex_color_use_as_albedo,"Water depth colour was lost on import")
                    var arrays: Array = mesh_node.mesh.surface_get_arrays(surface)
                    for vertex in arrays[Mesh.ARRAY_VERTEX]:
                        var relative: Vector3 = mesh_node.global_transform * vertex - connection.global_position
                        if absf(relative.dot(forward)) < .0001:
                            var key := "%s/%.3f/%.3f" % [material.resource_name, snappedf(relative.dot(across),.001)+0.0, snappedf(relative.y,.001)+0.0]
                            if key not in section: section.append(key)
            section.sort()
            assert(section.size() > 15,"Missing river cross section")
            if river_section.is_empty(): river_section = section
            else: assert(section == river_section,"River endpoint sections do not match: "+a.id)
        checked += 1
        instance.free()
    print("PASS GODOT IMPORT: ",checked," assets; role hierarchy, dimensions and ground pivots")
    quit(0)

func measure(node: Node3D) -> AABB:
    var found := false
    var bounds := AABB()
    for child in node.find_children("*","MeshInstance3D",true,false):
        # Transform each mesh vertex, not the already axis-aligned local AABB.
        for surface in child.mesh.get_surface_count():
            var arrays: Array = child.mesh.surface_get_arrays(surface)
            for v in arrays[Mesh.ARRAY_VERTEX]:
                var point: Vector3 = child.global_transform * v
                if not found:
                    bounds = AABB(point,Vector3.ZERO)
                    found = true
                else:
                    bounds = bounds.expand(point)
    return bounds
