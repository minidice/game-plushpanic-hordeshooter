@tool
extends EditorScenePostImport

# Preserve the painted coat in the imported asset itself, including direct GLB use.
# Godot 4.7.2 imports the shared coat with COLOR_0 data but albedo use disabled.
func _post_import(scene):
	for instance in scene.find_children("*", "MeshInstance3D", true, false):
		for surface in range(instance.mesh.get_surface_count()):
			var material = instance.mesh.surface_get_material(surface)
			var colors = instance.mesh.surface_get_arrays(surface)[Mesh.ARRAY_COLOR]
			if material is StandardMaterial3D and colors != null and colors.size() > 0:
				material.vertex_color_use_as_albedo = true
	return scene
