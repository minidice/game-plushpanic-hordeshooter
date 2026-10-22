extends Node3D

const SURFACE = preload("res://pack/shaders/outdoor_preview.gdshader")
const WATER = preload("res://pack/shaders/water_preview.gdshader")
var elapsed := 0.0
var manifest: Dictionary
var stage: Node3D
var camera: Camera3D
var sun: DirectionalLight3D
var env: Environment
var mat_records: Array = []
var targets: Array[Node3D] = []
var ghost := false
var preset := "day"
var heading: Label

func _ready() -> void:
    manifest = JSON.parse_string(FileAccess.get_file_as_string("res://pack/manifest.json"))
    setup()
    if "--validate-preview" in OS.get_cmdline_user_args():
        validate_material_contract()
    elif "--capture" in OS.get_cmdline_user_args():
        await capture_all()
        get_tree().quit()
    else:
        build_diorama()
        controls()

func setup() -> void:
    env = Environment.new()
    env.background_mode = Environment.BG_COLOR
    env.background_color = Color("26343b")
    env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    env.ambient_light_color = Color("c6dce1")
    env.ambient_light_energy = 0.3
    env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
    env.ssao_enabled = true
    env.ssao_radius = 1.2
    env.ssao_intensity = 1.1
    var world := WorldEnvironment.new()
    world.environment = env
    add_child(world)
    sun = DirectionalLight3D.new()
    sun.rotation_degrees = Vector3(-48,-32,0)
    sun.light_color = Color("fff2d6")
    sun.light_energy = 1.6
    sun.shadow_enabled = true
    sun.light_angular_distance = 1.5
    sun.directional_shadow_max_distance = 70
    add_child(sun)
    camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.far = 160
    add_child(camera)
    camera.current = true
    stage = Node3D.new()
    add_child(stage)

func frame(center: Vector3, size: float) -> void:
    camera.size = size
    var pitch := deg_to_rad(55.0)
    var yaw := deg_to_rad(-30.0)
    camera.position = center + Vector3(sin(yaw)*cos(pitch),sin(pitch),cos(yaw)*cos(pitch))*35
    camera.look_at(center)

func asset(id: String, pos := Vector3.ZERO, yaw := 0.0) -> Node3D:
    var scene := load("res://pack/models/"+id+".glb") as PackedScene
    var model := scene.instantiate() as Node3D
    stage.add_child(model)
    model.position = pos
    model.rotation_degrees.y = yaw
    prepare_materials(model, "")
    return model

func prepare_materials(node: Node, role: String) -> void:
    if not node is MeshInstance3D and str(node.name).begins_with("Occlude_"):
        role = str(node.name)
    if node is MeshInstance3D:
        for i in node.mesh.get_surface_count():
            var original := node.get_active_material(i) as StandardMaterial3D
            if original == null:
                continue
            var material := ShaderMaterial.new()
            material.shader = WATER if original.resource_name == "StreamWater" else SURFACE
            material.set_shader_parameter("use_vertex_color", original.vertex_color_use_as_albedo)
            material.set_shader_parameter("base_color",original.albedo_color)
            material.set_shader_parameter("base_roughness",original.roughness)
            material.set_shader_parameter("base_metallic",original.metallic)
            material.set_shader_parameter("weather_eligible", original.resource_name not in ["Water","StreamWater","ShoreFoam","LampGlass"])
            node.set_surface_override_material(i,material)
            mat_records.append({"material":material,"node":node,"role":role})
    for child in node.get_children():
        prepare_materials(child,role)

func clear_stage() -> void:
    for child in stage.get_children():
        child.free()
    mat_records.clear()
    targets.clear()

func ground(size: Vector3, color: Color) -> void:
    var m := MeshInstance3D.new()
    var b := BoxMesh.new()
    b.size = size
    m.mesh = b
    m.position.y = -size.y*.5 - 0.015
    var material := ShaderMaterial.new()
    material.shader = SURFACE
    material.set_shader_parameter("base_color",color)
    material.set_shader_parameter("base_roughness",.95)
    m.material_override = material
    stage.add_child(m)
    mat_records.append({"material":material,"node":m,"role":""})

func mannequin(at: Vector3) -> void:
    # Scale reference only; not a game character or an x-ray rendering pass.
    for part in [[Vector3(0,.68,0),Vector3(.42,.72,.3),Color("e7b658")], [Vector3(0,1.18,0),Vector3(.36,.36,.36),Color("e6d7ad")], [Vector3(-.13,.2,0),Vector3(.16,.4,.19),Color("3a5059")], [Vector3(.13,.2,0),Vector3(.16,.4,.19),Color("3a5059")]]:
        var m := MeshInstance3D.new()
        var b := BoxMesh.new()
        b.size = part[1]
        m.mesh = b
        m.position = at+part[0]
        var material := StandardMaterial3D.new()
        material.albedo_color = part[2]
        m.material_override = material
        stage.add_child(m)

func set_look(name_: String) -> void:
    preset = name_
    sun.light_color = Color("fff2d6")
    sun.light_energy = 1.1
    sun.rotation_degrees = Vector3(-48,-32,0)
    env.ambient_light_energy = .3
    env.ambient_light_color = Color("c6dce1")
    env.background_color = Color("26343b")
    if preset == "sunset":
        sun.light_color = Color("ffbb78")
        sun.rotation_degrees = Vector3(-17,-65,0)
        env.ambient_light_color = Color("a8b6d6")
        env.ambient_light_energy = .25
    elif preset == "night":
        sun.light_color = Color("93b7ed")
        sun.light_energy = .45
        env.ambient_light_energy = .16
        env.ambient_light_color = Color("8da9cc")
        env.background_color = Color("141e2b")
    elif preset == "wet":
        sun.light_energy = .9
        sun.light_color = Color("b6cbd5")
    elif preset == "snow":
        sun.light_color = Color("d7e4ee")
    for rec in mat_records:
        rec.material.set_shader_parameter("wetness", .85 if preset=="wet" else 0.0)
        rec.material.set_shader_parameter("snow", 1.0 if preset=="snow" else 0.0)

func set_ghost(enabled: bool) -> void:
    ghost = enabled
    for rec in mat_records:
        var chosen := false
        for target in targets:
            if target.is_ancestor_of(rec.node):
                chosen = true
        # Only camera-side crown sectors / front wall / roof of selected instances.
        var selected_part: bool = rec.role in ["Occlude_Canopy_SW","Occlude_Canopy_SE","Occlude_Canopy_2","Occlude_Canopy_3","Occlude_Roof","Occlude_WallFront","Occlude_RoofL","Occlude_RoofR","Occlude_FrontWall","Occlude_SideWallL"]
        rec.material.set_shader_parameter("fade",1.0 if enabled and chosen and selected_part else 0.0)

func build_diorama() -> void:
    clear_stage()
    ground(Vector3(24,.3,22),Color("667150"))
    for z in [-8,-4,0,4,8]:
        asset("path_dirt",Vector3(1,-.15,z))
    for p in [Vector3(-8,0,-7),Vector3(-5,0,-8),Vector3(7,0,-7),Vector3(9,0,-4),Vector3(-9,0,4),Vector3(6,0,6),Vector3(8,0,8)]:
        asset("pine_tall",p)
    for p in [Vector3(-7,0,-4),Vector3(8,0,3),Vector3(-8,0,7)]:
        asset("pine_small",p)
    targets.append(asset("pine_tall",Vector3(-3.1,0,3.1)))
    mannequin(Vector3(-2.75,0,2.15))
    targets.append(asset("container_open",Vector3(-4.7,0,-1.8)))
    asset("container_closed",Vector3(5.4,0,-3),90)
    asset("pallet",Vector3(-2.4,0,-.7))
    asset("wooden_crate",Vector3(-3.7,0,.3))
    asset("wooden_crate",Vector3(-4.7,0,.6))
    asset("barrel",Vector3(3.7,0,-.4))
    asset("barrel",Vector3(4.4,0,-.8))
    asset("sandbags",Vector3(4.4,0,3.2),15)
    asset("streetlamp",Vector3(.3,0,-4.2))
    for x in [-6,-3,0,3,6]:asset("corrugated_fence",Vector3(x,0,-9.7))
    for z in [-6,-2,2]:asset("cliff_segment",Vector3(-10.4,0,z),90)
    for p in [Vector3(8,0,0),Vector3(-6,0,4),Vector3(5,0,-8)]:asset("rock_cluster",p)
    for i in range(45):
        var x := sin(i*13.1)*9.3
        var z := cos(i*7.3)*8.8
        if absf(x-1)>2.3:asset("grass_patch",Vector3(x,0,z),i*41)
    for z in [4,8]:asset("water_tile",Vector3(9,-.025,z))
    asset("reeds",Vector3(7,0,6))
    asset("timber_bridge",Vector3(9,0,6))
    frame(Vector3(0,.6,0),24.5)
    set_look(preset)
    set_ghost(ghost)

func controls() -> void:
    var layer := CanvasLayer.new()
    add_child(layer)
    var panel := PanelContainer.new()
    panel.position = Vector2(24,24)
    layer.add_child(panel)
    var col := VBoxContainer.new()
    panel.add_child(col)
    heading = Label.new()
    heading.text = "  OUTDOOR ASSET LAB / 32 MODELS  "
    col.add_child(heading)
    var row := HBoxContainer.new()
    col.add_child(row)
    for p in ["day","sunset","night","wet","snow"]:
        var button := Button.new()
        button.text = p.capitalize()
        button.pressed.connect(func(): set_look(p))
        row.add_child(button)
    var scene_row := HBoxContainer.new()
    col.add_child(scene_row)
    for label in ["Outdoor", "Nature", "Shore"]:
        var scene_button := Button.new()
        scene_button.text = label
        scene_button.pressed.connect(func():
            if label == "Shore": build_shore_study()
            elif label == "Nature": build_nature_study()
            else: build_diorama()
        )
        scene_row.add_child(scene_button)
    var toggle := CheckButton.new()
    toggle.text = "Selected canopy / roof: ghost silhouette"
    toggle.toggled.connect(set_ghost)
    col.add_child(toggle)
    var note := Label.new()
    note.text = "  Preview only: manual selection, no automatic occlusion or gameplay.  "
    note.add_theme_font_size_override("font_size",13)
    col.add_child(note)

func save_image(path: String) -> void:
    await get_tree().process_frame
    await get_tree().process_frame
    await RenderingServer.frame_post_draw
    var error := get_viewport().get_texture().get_image().save_png(path)
    if error != OK:
        push_error("Capture failed: "+path)
        get_tree().quit(2)
    print("CAPTURE ",path)

func capture_all() -> void:
    DirAccess.make_dir_recursive_absolute("res://gallery/images")
    for item in manifest.assets:
        clear_stage()
        var model := asset(item.id,Vector3.ZERO,180.0 if str(item.id).begins_with("container_") else 0.0)
        var dims: Array = item.dimensions_m
        ground(Vector3(200,.1,200),Color("5b686b"))
        var span := maxf(maxf(dims[0],dims[2]),dims[1])
        frame(Vector3(0,dims[1]*.45,0),maxf(1.1,span*1.45))
        set_look("day")
        await save_image("res://gallery/images/"+item.id+".png")
        if item.occlusion_parts.size()>0:
            # Catalog demonstrates independent groups. Leave half of each tree crown solid.
            for rec in mat_records:
                var role: String = rec.role
                var fade_part := role.begins_with("Occlude_")
                if role.begins_with("Occlude_Canopy_"):
                    fade_part = role in ["Occlude_Canopy_SW","Occlude_Canopy_SE","Occlude_Canopy_2","Occlude_Canopy_3"]
                rec.material.set_shader_parameter("fade",1.0 if fade_part else 0.0)
            await save_image("res://gallery/images/"+item.id+"-ghost.png")
    clear_stage()
    ground(Vector3(10,.1,9),Color("657352"))
    targets.append(asset("pine_tall",Vector3(-1.6,0,0)))
    mannequin(Vector3(-1.15,0,-.7))
    targets.append(asset("utility_shed",Vector3(2.2,0,0)))
    mannequin(Vector3(2.2,.25,.6))
    frame(Vector3(.6,1.0,0),10)
    set_look("day")
    set_ghost(false)
    await save_image("res://gallery/images/occlusion-solid.png")
    set_ghost(true)
    await save_image("res://gallery/images/occlusion-ghost.png")
    build_nature_study()
    set_look("day")
    set_ghost(false)
    await save_image("res://gallery/images/nature-lineup.png")
    set_ghost(true)
    await save_image("res://gallery/images/nature-ghost.png")
    build_shore_study()
    set_ghost(false)
    for look in ["day", "sunset"]:
        set_look(look)
        await save_image("res://gallery/images/shore-"+look+".png")
    frame(Vector3(0,.5,.5),9.5)
    set_look("day")
    await save_image("res://gallery/images/shore-close.png")
    build_diorama()
    set_ghost(false)
    for p in ["day","sunset","night","wet","snow"]:
        set_look(p)
        await save_image("res://gallery/images/scene-"+p+".png")
    set_look("day")
    set_ghost(true)
    await save_image("res://gallery/images/scene-ghost.png")

func validate_material_contract() -> void:
    var count := 0
    for id in ["pine_tall","pine_small","broadleaf","round_oak","round_linden","container_open"]:
        clear_stage()
        var selected := asset(id)
        asset(id,Vector3(10,0,0))
        targets.append(selected)
        set_ghost(true)
        var faded := 0
        for rec in mat_records:
            var value: float = rec.material.get_shader_parameter("fade")
            if value > 0:
                assert(selected.is_ancestor_of(rec.node),"Fade leaked to another instance")
                assert(rec.role.begins_with("Occlude_"),"Keep mesh faded")
                assert(not "." in rec.role,"Mesh suffix replaced role")
                faded += 1
            count += 1
        assert(faded > 0,"No selected surfaces faded: "+id)
        set_ghost(false)
        for rec in mat_records:
            assert(rec.material.get_shader_parameter("fade") == 0.0,"Original state not restored")
    print("PASS PREVIEW MATERIALS: ",count," surfaces; selected roles, instance isolation and restoration")
    clear_stage()
    get_tree().quit(0)

func _process(delta: float) -> void:
    if "--capture" in OS.get_cmdline_user_args() or "--validate-preview" in OS.get_cmdline_user_args():
        return
    elapsed += delta
    for rec in mat_records:
        if rec.material.shader == WATER:
            rec.material.set_shader_parameter("preview_time",elapsed)

func build_nature_study() -> void:
    clear_stage()
    ground(Vector3(19,.15,10),Color("687659"))
    var names := ["pine_tall","pine_small","broadleaf","round_oak","round_linden"]
    for i in names.size():
        targets.append(asset(names[i],Vector3(-7+i*3.5,0,-1)))
    for i in 3:
        asset(["boulder","rock_cluster","cliff_segment"][i],Vector3(-4+i*4,0,3))
    frame(Vector3(0,1.1,0),20)
    set_look(preset)
    set_ghost(ghost)

func build_shore_study() -> void:
    clear_stage()
    # No full ground plane across the channel: banks are a physical profile.
    asset("stream_straight",Vector3(0,0,2))
    asset("stream_curve",Vector3(-2,0,-4),90)
    asset("timber_bridge",Vector3(0,.3,1),90)
    targets.append(asset("round_oak",Vector3(-2.8,.62,3.3)))
    targets.append(asset("round_linden",Vector3(2.9,.62,-1.2)))
    asset("pine_small",Vector3(-5.7,.62,-5.5))
    asset("boulder",Vector3(2.6,.60,4.3),25)
    asset("rock_cluster",Vector3(-2.7,.62,-.6),35)
    for pos in [Vector3(-2,.38,4.8),Vector3(1.9,.38,-.6),Vector3(-4,.38,-4)]:
        asset("reeds",pos)
    mannequin(Vector3(.3,.63,1))
    frame(Vector3(-1.4,.6,-.5),18)
    set_look(preset)
    set_ghost(ghost)
