extends SceneTree

func _initialize():
	for kind in ["goose", "duck", "raccoon", "redpanda"]:
		var scene = load("res://assets/characters/%s/%s.glb" % [kind, kind]).instantiate()
		for mesh in scene.find_children("*", "MeshInstance3D", true, false):
			for i in range(mesh.mesh.get_surface_count()):
				var mat = mesh.get_active_material(i)
				if mat.resource_name.begins_with("custom_body"):
					var arrays = mesh.mesh.surface_get_arrays(i)
					var colors = arrays[Mesh.ARRAY_COLOR]
					print(JSON.stringify({"species": kind, "mesh":mesh.name,"material":mat.resource_name,"vertex_albedo":mat.vertex_color_use_as_albedo,"vertex_srgb":mat.vertex_color_is_srgb,"albedo":str(mat.albedo_color),"color_count":colors.size() if colors else 0,"color0":str(colors[0]) if colors else "none","color_mid":str(colors[colors.size()/2]) if colors else "none"}))
		scene.free()
	quit()
