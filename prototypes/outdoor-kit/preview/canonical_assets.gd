extends RefCounted
## The preview and game share one loadable model set; no copied legacy pack.

static func path(relative: String) -> String:
    return ProjectSettings.globalize_path("res://").path_join("../../game/assets/outdoor-kit").simplify_path().path_join(relative)

static func manifest() -> Dictionary:
    return JSON.parse_string(FileAccess.get_file_as_string(path("manifest.json")))

static func shader(relative: String) -> Shader:
    var result := Shader.new()
    result.code = FileAccess.get_file_as_string(path("shaders/" + relative))
    return result

static func model(relative: String) -> Node3D:
    var document := GLTFDocument.new()
    var state := GLTFState.new()
    var error := document.append_from_file(path(relative), state)
    assert(error == OK, "Cannot load canonical model: " + relative)
    var result := document.generate_scene(state) as Node3D
    for part in result.find_children("*", "MeshInstance3D", true, false):
        for surface in part.mesh.get_surface_count():
            var material := part.mesh.surface_get_material(surface) as BaseMaterial3D
            if material != null and part.mesh.surface_get_arrays(surface)[Mesh.ARRAY_COLOR] != null:
                material.vertex_color_use_as_albedo = true
    return result
