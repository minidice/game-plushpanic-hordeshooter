using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Species selection and a live appearance workshop with fixed launch controls.</summary>
public partial class MainMenu : Control
{
    private List<CharacterDefinition> _characters = new();
    private int _index;
    private Label _name = null!, _description = null!, _details = null!;
    private Node3D _turntable = null!;
    private Camera3D _previewCamera = null!;
    private CharacterRig? _rig;
    private readonly List<Button> _buttons = new();
    private Sfx _sfx = null!;
    private float _time;
    private AppearanceEditor _appearance = null!;
    private int _posePreview;
    private HSlider _rotation = null!, _zoom = null!;
    private CheckButton _autoRotate = null!;
    private bool _previewGear = true, _previewWeapon = true;
    private bool _starting;

    public IReadOnlyList<CharacterDefinition> Characters => _characters;
    public CharacterDefinition Selected => _characters[_index];
    public string AppearancePath {get;set;}=AppearanceStore.Path;

    public override void _Ready()
    {
        GameInput.Ensure();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Theme = UiTheme.Get();
        SetAnchorsPreset(LayoutPreset.FullRect);
        var speciesOrder = new[] { "goose", "duck", "raccoon", "redpanda" };
        _characters = CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir)
            .Where(c => !c.Id.StartsWith("test_"))
            .OrderBy(c => { int order = System.Array.IndexOf(speciesOrder, c.Id); return order < 0 ? int.MaxValue : order; }).ToList();
        _sfx = new Sfx { Pool3DSize = 0, Pool2DSize = 4 };
        AddChild(_sfx);
        var bg = new ColorRect { Color = new Color("0d1214") };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 24);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 24);
        heading.AddChild(UiTheme.Label(Loc.Text("ui_menu_title"), 34, UiTheme.Accent));
        heading.AddChild(UiTheme.Label(Loc.Text("ui_menu_subtitle"), 17, UiTheme.Muted));
        column.AddChild(heading);

        var species = new HBoxContainer { Name = "SpeciesSelection" };
        species.AddThemeConstantOverride("separation", 10);
        species.AddChild(UiTheme.Label(Loc.Text("custom_species_step"), 20, UiTheme.Accent));
        for (int i = 0; i < _characters.Count; i++)
        {
            int index = i;
            var button = new Button { Name = "Species_" + _characters[i].Id, Text = Loc.Text(_characters[i].DisplayName), CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            button.Pressed += () => Select(index);
            species.AddChild(button);
            _buttons.Add(button);
        }
        column.AddChild(species);

        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 24);
        column.AddChild(row);
        var previewColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .95f };
        previewColumn.AddThemeConstantOverride("separation", 8);
        row.AddChild(previewColumn);
        previewColumn.AddChild(BuildPreview());
        var poses = new HBoxContainer();
        var poseGroup = new ButtonGroup();
        previewColumn.AddChild(poses);
        for (int pose = 0; pose < 3; pose++)
        {
            int value = pose;
            var choice = new Button { Name = "PreviewPose_" + pose, Text = Loc.Text(new[] { "preview_idle", "preview_run", "preview_fire" }[pose]), SizeFlagsHorizontal = SizeFlags.ExpandFill, ToggleMode = true, ButtonGroup = poseGroup, ButtonPressed = pose == 0 };
            choice.Pressed += () => _posePreview = value;
            poses.AddChild(choice);
        }
        _rotation = PreviewSlider(previewColumn, "PreviewRotation", "preview_rotation", -180, 180, 1, 0);
        _rotation.ValueChanged += value => { _autoRotate.ButtonPressed = false; _turntable.Rotation = new Vector3(0, Mathf.DegToRad((float)value), 0); };
        _zoom = PreviewSlider(previewColumn, "PreviewZoom", "preview_zoom", .75, 1.45, .01, 1);
        _zoom.ValueChanged += _ => UpdatePreviewCamera();
        var previewOptions = new HBoxContainer();
        previewColumn.AddChild(previewOptions);
        _autoRotate = new CheckButton { Name = "PreviewAutoRotate", Text = Loc.Text("preview_auto_rotate"), ButtonPressed = true };
        previewOptions.AddChild(_autoRotate);
        var gear = new CheckButton { Name = "PreviewGear", Text = Loc.Text("preview_gear"), ButtonPressed = true };
        gear.Toggled += on => { _previewGear = on; _appearance.RefreshPreview(); };
        previewOptions.AddChild(gear);
        var weapon = new CheckButton { Name = "PreviewWeapon", Text = Loc.Text("preview_weapon"), ButtonPressed = true };
        weapon.Toggled += on => { _previewWeapon = on; ApplyPreviewVisibility(); };
        previewOptions.AddChild(weapon);
        previewColumn.AddChild(UiTheme.Label(Loc.Text("preview_only_hint"), 13, UiTheme.Muted));

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.05f };
        right.AddThemeConstantOverride("separation", 8);
        row.AddChild(right);
        var nameRow = new HBoxContainer();
        _name = UiTheme.Label("", 26, UiTheme.Accent);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameRow.AddChild(_name);
        _details = UiTheme.Label("", 15, UiTheme.Muted);
        nameRow.AddChild(_details);
        right.AddChild(nameRow);
        _description = UiTheme.Label("", 16);
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(_description);
        right.AddChild(UiTheme.Label(Loc.Text("custom_shape_hint"), 13, UiTheme.Muted));
        _appearance = new AppearanceEditor { Name = "AppearanceEditor", SizeFlagsVertical = SizeFlags.ExpandFill,ProfilePath=AppearancePath };
        _appearance.AppearanceChanged += ApplyPreviewVisibility;
        right.AddChild(_appearance);

        var launchOptions = new HBoxContainer();
        var theme = new OptionButton { Name = "StageThemeChoice", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 38), TooltipText = Loc.Text("custom_stage_theme") };
        var themes = new[] { "zombie", "animals", "armed" };
        foreach (var id in themes) theme.AddItem(Loc.Text("theme_" + id));
        theme.Select(System.Math.Max(0, System.Array.IndexOf(themes, GameSession.SelectedTheme)));
        theme.ItemSelected += i => GameSession.SelectedTheme = themes[(int)i];
        launchOptions.AddChild(theme);
        var escape = new CheckButton { Name = "DebugEscape", Text = Loc.Text("debug_escape_option"), ButtonPressed = GameSession.DebugEscapeEnabled };
        escape.AddThemeFontSizeOverride("font_size", 14);
        escape.Toggled += on => GameSession.DebugEscapeEnabled = on;
        launchOptions.AddChild(escape);
        right.AddChild(launchOptions);
        var start = new Button { Name = "StartGame", Text = Loc.Text("ui_menu_start"), CustomMinimumSize = new Vector2(0, 50) };
        start.AddThemeFontSizeOverride("font_size", 24);
        start.Pressed += StartGame;
        right.AddChild(start);
        column.AddChild(UiTheme.Label(Hud.ControlsText, 14, UiTheme.Muted));
        int saved = _characters.FindIndex(c => c.Id == GameSession.SelectedCharacterId);
        Select(saved >= 0 ? saved : 0);
        start.GrabFocus();
    }

    private HSlider PreviewSlider(VBoxContainer parent, string name, string labelKey, double min, double max, double step, double value)
    {
        var row = new HBoxContainer();
        var label = UiTheme.Label(Loc.Text(labelKey), 15, UiTheme.Muted);
        label.CustomMinimumSize = new Vector2(90, 0);
        row.AddChild(label);
        var slider = new HSlider { Name = name, MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 28) };
        row.AddChild(slider);
        parent.AddChild(row);
        return slider;
    }

    private Control BuildPreview()
    {
        var container = new SubViewportContainer { Name = "CharacterPreview", Stretch = true, CustomMinimumSize = new Vector2(560, 360), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var viewport = new SubViewport { OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X };
        container.AddChild(viewport);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("1a2224"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c6dddf"),
            AmbientLightEnergy = .45f, TonemapMode = Godot.Environment.ToneMapper.Agx,
        };
        viewport.AddChild(new WorldEnvironment { Environment = env });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -35, 0), LightEnergy = 1.2f, LightColor = new Color("ffe2b8"), ShadowEnabled = true });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 150, 0), LightEnergy = .35f, LightColor = new Color("a8c8ff") });
        Mat.Mesh(viewport, new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.6f, Height = .1f }, new Vector3(0, -.05f, 0), Mat.Solid("3a4440"));
        _turntable = new Node3D();
        viewport.AddChild(_turntable);
        _previewCamera = new Camera3D { Fov = 35 };
        viewport.AddChild(_previewCamera);
        return container;
    }

    private void Select(int index)
    {
        if (_characters.Count == 0) return;
        _index = (index % _characters.Count + _characters.Count) % _characters.Count;
        var def = Selected;
        _name.Text = Loc.Text(def.DisplayName);
        if (_rig != null) _sfx.Play("ui_click");
        _description.Text = Loc.Text(def.Description);
        _details.Text = $"HP {def.MaxHealth}    ·    {Loc.Text("custom_speed")} {def.MoveSpeed:0.0}";
        for (int i = 0; i < _buttons.Count; i++) _buttons[i].Modulate = i == _index ? UiTheme.Accent : Colors.White;
        if (_rig != null) { _turntable.RemoveChild(_rig); _rig.QueueFree(); }
        _rig = CharacterRig.Create(def);
        _turntable.AddChild(_rig);
        _appearance.ShowRig(_rig);
        _rig.HideBuiltInWeapon();
        var heldWeapon = WeaponVisual.Build(ArmoryCatalog.Weapon("m4"), _rig.HandColor);
        _rig.Weapon.AddChild(heldWeapon.Root);
        _rig.BindWeaponGrip(heldWeapon);
        ApplyPreviewVisibility();
        UpdatePreviewCamera();
    }

    private void UpdatePreviewCamera()
    {
        if (_rig == null) return;
        float h = Selected.ColliderHeight;
        _previewCamera.Position = new Vector3(0, h * .75f, (h * 2.6f + 1.5f) / (float)_zoom.Value);
        _previewCamera.LookAt(new Vector3(0, h * .45f, 0), Vector3.Up);
    }

    private void ApplyPreviewVisibility()
    {
        _rig?.SetPreviewVisibility(_previewGear,_previewWeapon);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (_autoRotate.ButtonPressed)
        {
            _turntable.RotateY(dt * .22f);
            _rotation.SetValueNoSignal(Mathf.Wrap(Mathf.RadToDeg(_turntable.Rotation.Y), -180, 180));
        }
        float movement = _posePreview == 1 ? 1 : 0;
        _rig?.SetLocomotionPose(dt, movement, _posePreview == 2);
        _rig?.Animate(dt, movement, _posePreview == 2 ? Mathf.Max(0, Mathf.Sin(_time * 15)) * .5f : 0, 0, Vector3.Zero);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(GameInput.Confirm)) StartGame();
        else if (e.IsActionPressed(GameInput.MoveRight) || e.IsActionPressed(GameInput.MoveDown) || e.IsActionPressed("ui_right")) Select(_index + 1);
        else if (e.IsActionPressed(GameInput.MoveLeft) || e.IsActionPressed(GameInput.MoveUp) || e.IsActionPressed("ui_left")) Select(_index - 1);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitHygiene.Collect();
    }

    private async void StartGame()
    {
        if (_starting || _characters.Count == 0) return;
        _starting = true;
        _sfx.Play("ui_click");
        GameSession.SelectedCharacterId = Selected.Id;
        await ToSignal(GetTree().CreateTimer(.09f), SceneTreeTimer.SignalName.Timeout);
        GetTree().ChangeSceneToFile("res://scenes/Stage1.tscn");
    }
}
