@tool
extends EditorScenePostImport
## Godot 4.7.2 retains COLOR_0 but does not enable it on this exported PBR material.
## Persist the flag in the imported scene so direct loads and MultiMesh share it.
func _post_import(scene: Node) -> Object:
    for part in scene.find_children("*", "MeshInstance3D", true, false):
        for surface in part.mesh.get_surface_count():
            var material := part.mesh.surface_get_material(surface) as BaseMaterial3D
            if material != null and part.mesh.surface_get_arrays(surface)[Mesh.ARRAY_COLOR] != null:
                material.vertex_color_use_as_albedo = true
    return scene
