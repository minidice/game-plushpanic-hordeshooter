using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public enum MenuPage { Home, Customize, Single, Multi, Settings }

/// <summary>
/// Main menu (doc/menu-multiplayer-plan.md 1장): home → customize / single / multi / settings.
/// The character preview stays on the left for every page; only the right-hand page changes.
/// </summary>
public partial class MainMenu : Control
{
    private List<CharacterDefinition> _characters = new();
    private int _index;
    private Label _name = null!, _description = null!, _details = null!, _singleCharacter = null!;
    private Node3D _turntable = null!;
    private Camera3D _previewCamera = null!;
    private CharacterRig? _rig;
    private readonly List<Button> _buttons = new();
    private readonly Dictionary<MenuPage, Control> _pages = new();
    private Control _previewTools = null!;
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
    public string AppearancePath { get; set; } = AppearanceStore.Path;
    public MenuPage Page { get; private set; }

    public override void _Ready()
    {
        GameInput.Ensure();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        // The real game boots into this scene; test scenes add the menu under their own root and keep defaults.
        if (!GameSettings.Loaded && GetTree().CurrentScene == this) GameSettings.Load();
        Theme = UiTheme.Get();
        SetAnchorsPreset(LayoutPreset.FullRect);
        var speciesOrder = new[] { "goose", "duck", "raccoon", "redpanda" };
        _characters = CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir)
            .Where(c => !c.Id.StartsWith("test_"))
            .OrderBy(c => { int order = Array.IndexOf(speciesOrder, c.Id); return order < 0 ? int.MaxValue : order; }).ToList();
        Build(MenuPage.Home);
    }

    private void Build(MenuPage page)
    {
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        _buttons.Clear();
        _pages.Clear();
        _rig = null;
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

        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 24);
        column.AddChild(row);
        row.AddChild(BuildPreviewColumn());

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.05f };
        row.AddChild(right);
        AddPage(right, MenuPage.Home, BuildHome());
        AddPage(right, MenuPage.Customize, BuildCustomize());
        AddPage(right, MenuPage.Single, BuildSingle());
        AddPage(right, MenuPage.Multi, BuildMulti());
        AddPage(right, MenuPage.Settings, BuildSettings());
        column.AddChild(UiTheme.Label(Hud.ControlsText, 14, UiTheme.Muted));

        int saved = _characters.FindIndex(c => c.Id == GameSession.SelectedCharacterId);
        Select(saved >= 0 ? saved : 0, playSound: false);
        ShowPage(page);
    }

    private void AddPage(VBoxContainer parent, MenuPage page, VBoxContainer body)
    {
        body.Name = "Page_" + page;
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 10);
        _pages[page] = body;
        parent.AddChild(body);
    }

    public void ShowPage(MenuPage page)
    {
        // Keyboard changes to a slider never fire DragEnded, so leaving the page also saves.
        if (Page == MenuPage.Settings && page != MenuPage.Settings) GameSettings.Save();
        Page = page;
        foreach (var (key, control) in _pages) control.Visible = key == page;
        _previewTools.Visible = page == MenuPage.Customize;
        _description.Visible = page != MenuPage.Customize;
        if (page == MenuPage.Single && _characters.Count > 0) _singleCharacter.Text = Loc.Format("fmt_menu_single_character", Loc.Text(Selected.DisplayName));
        // Enter presses the focused button, so each page focuses the action Enter should take.
        var focus = page switch
        {
            MenuPage.Customize when _buttons.Count > 0 => _buttons[_index],
            MenuPage.Home => _pages[page].FindChild("Menu_Single", true, false) as Button,
            MenuPage.Single => _pages[page].FindChild("StartGame", true, false) as Button,
            _ => _pages[page].FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => !b.Disabled),
        };
        focus?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Open(MenuPage page)
    {
        _sfx.Play("ui_click");
        ShowPage(page);
    }

    // ── Pages ──────────────────────────────────────────────────────────────

    private VBoxContainer BuildHome()
    {
        var page = new VBoxContainer();
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        foreach (var (name, key, target) in new (string, string, MenuPage?)[]
        {
            ("Menu_Customize", "menu_home_customize", MenuPage.Customize),
            ("Menu_Single", "menu_home_single", MenuPage.Single),
            ("Menu_Multi", "menu_home_multi", MenuPage.Multi),
            ("Menu_Settings", "menu_home_settings", MenuPage.Settings),
            ("Menu_Quit", "menu_home_quit", null),
        })
        {
            var button = BigButton(name, Loc.Text(key));
            if (target is { } open) button.Pressed += () => Open(open);
            else button.Pressed += Quit;
            page.AddChild(button);
            if (Loc.Has(key + "_desc")) page.AddChild(Hint(Loc.Text(key + "_desc")));
        }
        return page;
    }

    private VBoxContainer BuildCustomize()
    {
        var page = new VBoxContainer();
        page.AddChild(Title("menu_customize_title"));
        var species = new HBoxContainer { Name = "SpeciesSelection" };
        species.AddThemeConstantOverride("separation", 10);
        species.AddChild(UiTheme.Label(Loc.Text("custom_species_step"), 18, UiTheme.Accent));
        for (int i = 0; i < _characters.Count; i++)
        {
            int index = i;
            var button = new Button { Name = "Species_" + _characters[i].Id, Text = Loc.Text(_characters[i].DisplayName), CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            button.Pressed += () => Select(index);
            species.AddChild(button);
            _buttons.Add(button);
        }
        page.AddChild(species);
        page.AddChild(UiTheme.Label(Loc.Text("custom_shape_hint"), 13, UiTheme.Muted));
        _appearance = new AppearanceEditor { Name = "AppearanceEditor", SizeFlagsVertical = SizeFlags.ExpandFill, ProfilePath = AppearancePath };
        _appearance.AppearanceChanged += ApplyPreviewVisibility;
        page.AddChild(_appearance);
        page.AddChild(BackButton(MenuPage.Customize));
        return page;
    }

    private VBoxContainer BuildSingle()
    {
        var page = new VBoxContainer();
        page.AddChild(Title("menu_single_title"));
        _singleCharacter = UiTheme.Label("", 17);
        page.AddChild(_singleCharacter);

        page.AddChild(Section("menu_mode_label"));
        var modes = new HBoxContainer();
        var group = new ButtonGroup();
        var modeHint = Hint("");
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            string id = mode.ToString().ToLowerInvariant();
            bool playable = GameSession.PlayableModes.Contains(mode);
            var button = new Button
            {
                Name = "Mode_" + id, ToggleMode = true, ButtonGroup = group, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 44), Disabled = !playable, ButtonPressed = mode == GameSession.Mode,
                Text = playable ? Loc.Text("mode_" + id) : Loc.Format("fmt_menu_coming_soon", Loc.Text("mode_" + id)),
                TooltipText = Loc.Text("mode_" + id + "_desc"),
            };
            button.Pressed += () => { GameSession.Mode = mode; modeHint.Text = Loc.Text("mode_" + id + "_desc"); };
            modes.AddChild(button);
        }
        page.AddChild(modes);
        modeHint.Text = Loc.Text("mode_" + GameSession.Mode.ToString().ToLowerInvariant() + "_desc");
        page.AddChild(modeHint);
        page.AddChild(Hint(Loc.Text("menu_stage_auto")));

        page.AddChild(Section("menu_squad_label"));
        var squad = new OptionButton { Name = "SquadChoice", CustomMinimumSize = new Vector2(0, 38) };
        foreach (int size in GameSession.SquadChoices) squad.AddItem(Loc.Text("squad_" + size), size);
        squad.Select(Array.IndexOf(GameSession.SquadChoices, GameSession.ValidSquad(GameSession.SquadSize)));
        squad.ItemSelected += i => { GameSession.SquadSize = GameSession.SquadChoices[(int)i]; GameSettings.Save(); };
        page.AddChild(squad);
        page.AddChild(Hint(Loc.Text("menu_squad_hint")));

        page.AddChild(Section("menu_test_options"));
        var tests = new HBoxContainer();
        var theme = new OptionButton { Name = "StageThemeChoice", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 38), TooltipText = Loc.Text("custom_stage_theme") };
        var themes = new[] { "zombie", "animals", "armed" };
        foreach (var id in themes) theme.AddItem(Loc.Text("theme_" + id));
        theme.Select(Math.Max(0, Array.IndexOf(themes, GameSession.SelectedTheme)));
        theme.ItemSelected += i => { GameSession.SelectedTheme = themes[(int)i]; GameSettings.Save(); };
        tests.AddChild(theme);
        var escape = new CheckButton { Name = "DebugEscape", Text = Loc.Text("debug_escape_option"), ButtonPressed = GameSession.DebugEscapeEnabled };
        escape.AddThemeFontSizeOverride("font_size", 14);
        escape.Toggled += on => GameSession.DebugEscapeEnabled = on;
        tests.AddChild(escape);
        page.AddChild(tests);

        page.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var start = new Button { Name = "StartGame", Text = Loc.Text("ui_menu_start"), CustomMinimumSize = new Vector2(0, 50) };
        start.AddThemeFontSizeOverride("font_size", 24);
        start.Pressed += StartGame;
        page.AddChild(start);
        page.AddChild(BackButton(MenuPage.Single));
        return page;
    }

    private VBoxContainer BuildMulti()
    {
        var page = new VBoxContainer();
        page.AddChild(Title("menu_multi_title"));
        page.AddChild(Hint(Loc.Text("menu_multi_rules")));
        page.AddChild(BigButton("Multi_Host", Loc.Format("fmt_menu_coming_soon", Loc.Text("menu_multi_host")), disabled: true));
        var join = new HBoxContainer();
        join.AddChild(new LineEdit { Name = "Multi_Code", PlaceholderText = Loc.Text("menu_multi_code"), Editable = false, MaxLength = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48) });
        join.AddChild(new Button { Name = "Multi_Join", Text = Loc.Format("fmt_menu_coming_soon", Loc.Text("menu_multi_join")), Disabled = true, CustomMinimumSize = new Vector2(220, 48) });
        page.AddChild(join);
        page.AddChild(BigButton("Multi_Public", Loc.Format("fmt_menu_coming_soon", Loc.Text("menu_multi_public")), disabled: true));
        page.AddChild(Hint(Loc.Text("menu_multi_soon_hint")));
        page.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        page.AddChild(BackButton(MenuPage.Multi));
        return page;
    }

    private VBoxContainer BuildSettings()
    {
        var page = new VBoxContainer();
        page.AddChild(Title("menu_settings_title"));

        page.AddChild(Section("settings_language"));
        var languages = GameSettings.Languages;
        var language = new OptionButton { Name = "Settings_Language", CustomMinimumSize = new Vector2(0, 38) };
        foreach (var id in languages) language.AddItem(GameSettings.LanguageName(id));
        language.Select(Math.Max(0, languages.ToList().IndexOf(Loc.Language)));
        language.ItemSelected += i => SetLanguage(languages[(int)i]);
        page.AddChild(language);

        page.AddChild(Section("settings_sound"));
        PercentSlider(page, "Settings_MasterVolume", "settings_volume_master", GameSettings.MasterVolume, v => { GameSettings.MasterVolume = v; GameSettings.ApplyAudio(); });
        PercentSlider(page, "Settings_SfxVolume", "settings_volume_sfx", GameSettings.SfxVolume, v => { GameSettings.SfxVolume = v; GameSettings.ApplyAudio(); });
        PercentSlider(page, "Settings_AmbienceVolume", "settings_volume_ambience", GameSettings.AmbienceVolume, v => { GameSettings.AmbienceVolume = v; GameSettings.ApplyAudio(); });

        page.AddChild(Section("settings_gameplay"));
        PercentSlider(page, "Settings_Recoil", "settings_recoil", GameSettings.RecoilScale, v => GameSettings.RecoilScale = v);
        page.AddChild(Hint(Loc.Text("settings_recoil_hint")));
        var revive = new CheckButton { Name = "Settings_Revive", Text = Loc.Text("settings_revive"), ButtonPressed = GameSettings.ReviveEnabled };
        revive.Toggled += on => { GameSettings.ReviveEnabled = on; GameSettings.Save(); };
        page.AddChild(revive);
        page.AddChild(Hint(Loc.Text("settings_revive_hint")));

        page.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        page.AddChild(Hint(Loc.Text(GameSettings.Loaded ? "settings_saved_hint" : "settings_not_saved_hint")));
        page.AddChild(BackButton(MenuPage.Settings));
        return page;
    }

    /// <summary>Every label was built in the old language, so the whole menu is rebuilt on the settings page.</summary>
    public void SetLanguage(string language)
    {
        if (language == Loc.Language) return;
        Loc.Language = language;
        GameSettings.Save();
        // Deferred: this runs inside the language OptionButton's own signal, which Build() removes from the tree.
        Callable.From(() => Build(MenuPage.Settings)).CallDeferred();
    }

    // ── Building blocks ────────────────────────────────────────────────────

    private static Button BigButton(string name, string text, bool disabled = false)
    {
        var button = new Button { Name = name, Text = text, Disabled = disabled, CustomMinimumSize = new Vector2(0, 54), Alignment = HorizontalAlignment.Left };
        button.AddThemeFontSizeOverride("font_size", 22);
        return button;
    }

    private static Label Title(string key) => UiTheme.Label(Loc.Text(key), 26, UiTheme.Accent);
    private static Label Section(string key) => UiTheme.Label(Loc.Text(key), 17, UiTheme.Accent);

    private static Label Hint(string text)
    {
        var label = UiTheme.Label(text, 14, UiTheme.Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private Button BackButton(MenuPage from)
    {
        var back = new Button { Name = "Back_" + from, Text = Loc.Text("menu_back"), CustomMinimumSize = new Vector2(0, 40) };
        back.Pressed += () => Open(MenuPage.Home);
        return back;
    }

    private static void PercentSlider(VBoxContainer parent, string name, string labelKey, float value, Action<float> changed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = UiTheme.Label(Loc.Text(labelKey), 16);
        label.CustomMinimumSize = new Vector2(170, 0);
        row.AddChild(label);
        var slider = new HSlider { Name = name, MinValue = 0, MaxValue = 100, Step = 1, Value = Mathf.Round(value * 100), SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 30) };
        var shown = UiTheme.Label($"{slider.Value:0}%", 15, UiTheme.Muted);
        shown.CustomMinimumSize = new Vector2(56, 0);
        slider.ValueChanged += v => { shown.Text = $"{v:0}%"; changed((float)v / 100f); };
        // Save once the drag ends rather than on every step.
        slider.DragEnded += _ => GameSettings.Save();
        row.AddChild(slider);
        row.AddChild(shown);
        parent.AddChild(row);
    }

    // ── Character preview ──────────────────────────────────────────────────

    private VBoxContainer BuildPreviewColumn()
    {
        var previewColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .95f };
        previewColumn.AddThemeConstantOverride("separation", 8);
        previewColumn.AddChild(BuildPreview());
        var nameRow = new HBoxContainer();
        _name = UiTheme.Label("", 26, UiTheme.Accent);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameRow.AddChild(_name);
        _details = UiTheme.Label("", 15, UiTheme.Muted);
        nameRow.AddChild(_details);
        previewColumn.AddChild(nameRow);
        _description = UiTheme.Label("", 16);
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        previewColumn.AddChild(_description);

        var tools = new VBoxContainer { Name = "PreviewTools" };
        tools.AddThemeConstantOverride("separation", 8);
        _previewTools = tools;
        previewColumn.AddChild(tools);
        var poses = new HBoxContainer();
        var poseGroup = new ButtonGroup();
        tools.AddChild(poses);
        for (int pose = 0; pose < 3; pose++)
        {
            int value = pose;
            var choice = new Button { Name = "PreviewPose_" + pose, Text = Loc.Text(new[] { "preview_idle", "preview_run", "preview_fire" }[pose]), SizeFlagsHorizontal = SizeFlags.ExpandFill, ToggleMode = true, ButtonGroup = poseGroup, ButtonPressed = pose == _posePreview };
            choice.Pressed += () => _posePreview = value;
            poses.AddChild(choice);
        }
        _rotation = PreviewSlider(tools, "PreviewRotation", "preview_rotation", -180, 180, 1, 0);
        _rotation.ValueChanged += value => { _autoRotate.ButtonPressed = false; _turntable.Rotation = new Vector3(0, Mathf.DegToRad((float)value), 0); };
        _zoom = PreviewSlider(tools, "PreviewZoom", "preview_zoom", .75, 1.45, .01, 1);
        _zoom.ValueChanged += _ => UpdatePreviewCamera();
        var previewOptions = new HBoxContainer();
        tools.AddChild(previewOptions);
        _autoRotate = new CheckButton { Name = "PreviewAutoRotate", Text = Loc.Text("preview_auto_rotate"), ButtonPressed = true };
        previewOptions.AddChild(_autoRotate);
        var gear = new CheckButton { Name = "PreviewGear", Text = Loc.Text("preview_gear"), ButtonPressed = _previewGear };
        gear.Toggled += on => { _previewGear = on; _appearance.RefreshPreview(); };
        previewOptions.AddChild(gear);
        var weapon = new CheckButton { Name = "PreviewWeapon", Text = Loc.Text("preview_weapon"), ButtonPressed = _previewWeapon };
        weapon.Toggled += on => { _previewWeapon = on; ApplyPreviewVisibility(); };
        previewOptions.AddChild(weapon);
        tools.AddChild(UiTheme.Label(Loc.Text("preview_only_hint"), 13, UiTheme.Muted));
        return previewColumn;
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

    private void Select(int index, bool playSound = true)
    {
        if (_characters.Count == 0) return;
        _index = (index % _characters.Count + _characters.Count) % _characters.Count;
        var def = Selected;
        _name.Text = Loc.Text(def.DisplayName);
        if (playSound) _sfx.Play("ui_click");
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
        if (GameSession.SelectedCharacterId != def.Id)
        {
            GameSession.SelectedCharacterId = def.Id;
            GameSettings.Save();
        }
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
        _rig?.SetPreviewVisibility(_previewGear, _previewWeapon);
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
        if (e.IsActionPressed("ui_cancel") && Page != MenuPage.Home) Open(MenuPage.Home);
        else if (e.IsActionPressed(GameInput.Confirm) && Page == MenuPage.Home) Open(MenuPage.Single);
        else if (e.IsActionPressed(GameInput.Confirm) && Page == MenuPage.Single) StartGame();
        else if (Page != MenuPage.Customize) return;
        else if (e.IsActionPressed(GameInput.MoveRight) || e.IsActionPressed(GameInput.MoveDown) || e.IsActionPressed("ui_right")) Select(_index + 1);
        else if (e.IsActionPressed(GameInput.MoveLeft) || e.IsActionPressed(GameInput.MoveUp) || e.IsActionPressed("ui_left")) Select(_index - 1);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        GameSettings.Save();
        QuitHygiene.Collect();
    }

    private void Quit()
    {
        GameSettings.Save();
        QuitHygiene.Collect();
        GetTree().Quit();
    }

    private async void StartGame()
    {
        if (_starting || _characters.Count == 0 || !GameSession.PlayableModes.Contains(GameSession.Mode)) return;
        _starting = true;
        _sfx.Play("ui_click");
        GameSession.SelectedCharacterId = Selected.Id;
        GameSettings.Save();
        await ToSignal(GetTree().CreateTimer(.09f), SceneTreeTimer.SignalName.Timeout);
        GetTree().ChangeSceneToFile("res://scenes/Stage1.tscn");
    }
}
