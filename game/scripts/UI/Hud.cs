using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>In-game HUD, reticle, overlays, pause and end screens. Keeps running while the tree is paused.</summary>
public partial class Hud : CanvasLayer
{
    public static string ControlsText => Loc.Text("ui_controls");

    private const string VignetteShader = @"
shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float strength = 0.0;
uniform float inner = 0.3;
void fragment() {
    float d = distance(UV, vec2(0.5));
    COLOR = vec4(tint.rgb, tint.a * smoothstep(inner, 0.78, d) * strength);
}";

    private Stage Stage => GetParent<Stage>();
    public bool Paused { get; private set; }
    public bool GraphicsOpen { get; private set; }

    private Control _root = null!;
    private Label _objective = null!, _timer = null!, _notice = null!, _hp = null!;
    private Label _ammo = null!, _reserve = null!, _status = null!, _prompt = null!, _help = null!;
    private HudEquipmentSlot _slot1 = null!, _slot2 = null!, _slot3 = null!, _throwables = null!, _medkit = null!;
    private ColorRect _blind = null!;
    private Label _endTitle = null!, _endBody = null!;
    private PanelContainer _endPanel = null!, _pausePanel = null!, _graphicsPanel = null!;
    private Button _pauseGraphics = null!;
    private Control _pauseInputBlocker = null!;
    private GraphicsOptions _graphicsOptions = null!;
    private ShaderMaterial _darkVignette = null!, _hurtVignette = null!, _heliumVignette = null!;

    /// <summary>The card pick window is open (<see cref="CardWindow"/>): the mouse shows and only walking and leaping work.</summary>
    public bool CardWindowOpen { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 10;
        _root = new Control { Name = "HudRoot", Theme = UiTheme.Get(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _darkVignette = Overlay(new Color(0, 0, 0, 1), .25f, .28f);
        _hurtVignette = Overlay(new Color(.85f, .05f, .05f, 1), 0, .2f);
        _heliumVignette = Overlay(new Color(1f, .55f, .8f, 1), 0, .12f);

        var hitArcs = new Paint { Name = "HitArcs", Painter = DrawHitArcs };
        hitArcs.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(hitArcs);
        Stage.Player.HitFrom += dir => _hits.Add((dir, HitArcSeconds));

        // Survival's base health / Survivor's boss countdown and health, top center.
        var modeBar = Place(new Paint { Name = "ModeBar", Painter = DrawModeBar, CustomMinimumSize = new Vector2(440, 30) }, .5f, 0, new Vector2(0, 26), true, false, centerX: true);
        modeBar.Visible = Stage.ModeRules != null;
        // Onslaught's experience: a thin bar with the level under the top bar (decisions.md M-27).
        var subBar = Place(new Paint { Name = "SubBar", Painter = DrawSubBar, CustomMinimumSize = new Vector2(440, 14) }, .5f, 0, new Vector2(0, 62), true, false, centerX: true);
        subBar.Visible = Stage.ModeRules != null;

        var arrows = new Paint { Name = "OffscreenArrows", Painter = DrawOffscreenArrows };
        arrows.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(arrows);

        var reticle = new Paint { Name = "Reticle", Painter = DrawReticle };
        reticle.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(reticle);

        BuildCompactHud();
        BuildInteraction();

        // Flashbang: you looked at it.
        _blind = new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _blind.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_blind);
        BuildStatusEffects();
        BuildWeaponAction();

        _status = Place(HudLabel("", 17, UiTheme.Helium), .5f, 1, new Vector2(0, -196), true, true, centerX: true);
        _status.Name = "Status";
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt = Place(HudLabel("", 18, HudWhite), .5f, 1, new Vector2(0, -162), true, true, centerX: true);
        _prompt.Name = "InteractionPrompt";
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;

        BuildInventory();

        _endPanel = CenterPanel(out var endBox);
        _endTitle = UiTheme.Label("", 48, UiTheme.Accent);
        _endTitle.HorizontalAlignment = HorizontalAlignment.Center;
        endBox.AddChild(_endTitle);
        _endBody = UiTheme.Label("", 20);
        _endBody.HorizontalAlignment = HorizontalAlignment.Center;
        endBox.AddChild(_endBody);

        _pauseInputBlocker = new Control { Name = "PauseInputBlocker", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _pauseInputBlocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_pauseInputBlocker);
        _pausePanel = CenterPanel(out var pauseBox);
        _pausePanel.Name = "PausePanel";
        _pausePanel.MouseFilter = Control.MouseFilterEnum.Stop;
        var pauseTitle = UiTheme.Label(Loc.Text("ui_pause_title"), 40, UiTheme.Selection);
        pauseTitle.HorizontalAlignment = HorizontalAlignment.Center;
        pauseBox.AddChild(pauseTitle);
        // A host can read the join address again here (multiplayer-sync-design.md 10장).
        var hostInfo = Stage.Net is { HostInfo: { Length: > 0 } info } ? info + "\n\n" : "";
        var pauseBody = UiTheme.Label(hostInfo + SetupLine(Stage) + "\n\n" + Loc.Text(OnlineKey("ui_pause_keys")) + "\n\n" + ControlsText
            + "\n\n" + Loc.Text("hud_reticle_legend"), 18);
        pauseBody.Name = "PauseControls";
        pauseBody.CustomMinimumSize = new Vector2(880, 0);
        pauseBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        pauseBody.HorizontalAlignment = HorizontalAlignment.Center;
        pauseBox.AddChild(pauseBody);
        _pauseGraphics = new Button { Name = "Pause_Graphics", Text = Loc.Text("settings_graphics"), FocusMode = Control.FocusModeEnum.All, MouseFilter = Control.MouseFilterEnum.Stop };
        _pauseGraphics.Pressed += OpenGraphicsOptions;
        pauseBox.AddChild(_pauseGraphics);

        _graphicsPanel = CenterPanel(out var graphicsBox);
        _graphicsPanel.Name = "PauseGraphicsPanel";
        _graphicsPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        graphicsBox.CustomMinimumSize = new Vector2(650, 0);
        var graphicsTitle = UiTheme.Label(Loc.Text("settings_graphics"), 36, UiTheme.Selection);
        graphicsTitle.HorizontalAlignment = HorizontalAlignment.Center;
        graphicsBox.AddChild(graphicsTitle);
        _graphicsOptions = new GraphicsOptions { Name = "GraphicsOptions" };
        graphicsBox.AddChild(_graphicsOptions);
        var graphicsBack = new Button { Name = "Graphics_Back", Text = Loc.Text("menu_back"), CustomMinimumSize = new Vector2(0, 42) };
        graphicsBack.Pressed += CloseGraphicsOptions;
        graphicsBox.AddChild(graphicsBack);
    }

    public void OpenGraphicsOptions()
    {
        if (!Paused || Stage.State != StageState.Playing) return;
        GraphicsOpen = true;
        UpdatePausePanels();
        _graphicsOptions.FocusSelected();
    }

    private void CloseGraphicsOptions()
    {
        GraphicsOpen = false;
        UpdatePausePanels();
        // The pause root advertises Enter to restart/return to room. Do not leave that key on a button.
        if (Paused) GetViewport().GuiReleaseFocus();
    }

    private void UpdatePausePanels()
    {
        _pauseInputBlocker.Visible = Paused;
        _pausePanel.Visible = Paused && !GraphicsOpen;
        _graphicsPanel.Visible = Paused && GraphicsOpen;
    }

    private T Place<T>(T control, float ax, float ay, Vector2 offset, bool growLeft, bool growUp, bool centerX = false) where T : Control
    {
        control.AnchorLeft = control.AnchorRight = ax;
        control.AnchorTop = control.AnchorBottom = ay;
        control.OffsetLeft = control.OffsetRight = offset.X;
        control.OffsetTop = control.OffsetBottom = offset.Y;
        control.GrowHorizontal = centerX ? Control.GrowDirection.Both : growLeft ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        control.GrowVertical = growUp ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(control);
        return control;
    }

    private PanelContainer CenterPanel(out VBoxContainer box)
    {
        var panel = Place(new PanelContainer(), .5f, .5f, Vector2.Zero, true, false, centerX: true);
        panel.GrowVertical = Control.GrowDirection.Both;
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(UiTheme.Surface, .94f), 36, new Color(UiTheme.Selection, .3f)));
        box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 16);
        panel.AddChild(box);
        panel.Visible = false;
        return panel;
    }

    private ShaderMaterial Overlay(Color tint, float strength, float inner)
    {
        var mat = new ShaderMaterial { Shader = new Shader { Code = VignetteShader } };
        mat.SetShaderParameter("tint", tint);
        mat.SetShaderParameter("strength", strength);
        mat.SetShaderParameter("inner", inner);
        var rect = new ColorRect { Material = mat, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(rect);
        return mat;
    }

    public override void _Process(double delta)
    {
        var stage = Stage;
        var player = stage.Player;
        var gun = player.Gun;
        bool playing = stage.State == StageState.Playing;

        _objective.Text = stage.ObjectiveText;
        UpdateSquadPanel(stage);
        int t = (int)stage.Elapsed;
        _timer.Text = Loc.Format("fmt_hud_timer", t / 60, t % 60, stage.Kills, Loc.Text(PlaySetup.DifficultyKey(stage.Difficulty)));
        _notice.Text = stage.NoticeTime > 0 ? stage.Notice : "";
        _notice.Modulate = new Color(1, 1, 1, Mathf.Clamp(stage.NoticeTime, 0, 1));

        _hp.Text = player.IsHealing ? Loc.Format("fmt_hud_healing", (int)(player.HealProgress * 100)) : (player.DisplayLabel.Length > 0 ? player.DisplayLabel : Loc.Text(player.Definition.DisplayName)) + DownPips(stage, player);
        UpdateGear(stage, player);
        _medkit.UpdateSlot(Loc.Text("hud_medkit"), player.IsHealing ? $"{(int)(player.HealProgress * 100)}%" : $"×{player.Medkits}", "", "medkit", player.IsHealing, player.Medkits > 0);
        var loadout = player.Loadout;
        var secondary = loadout.Secondary;
        // Weapon tiles show no ammo of their own (M-42): the active one's ammo floats above its tile.
        _slot1.UpdateSlot(secondary.Def.DisplayName, "", secondary.Def.IconPath,
            secondary.IsMelee ? "melee" : "pistol", loadout.ActiveSlot == 1, true);
        var primary = loadout.Primary;
        _slot2.UpdateSlot(primary?.Def.DisplayName ?? Loc.Text("hud_empty"), "",
            primary?.Def.IconPath ?? "", "rifle", loadout.ActiveSlot == 2, primary != null);
        var special = loadout.Special;
        _slot3.UpdateSlot(special?.Def.DisplayName ?? Loc.Text("hud_special"), special?.CooldownLeft > 0 ? SlotAmmo(special) : "",
            special?.Def.IconPath ?? "", "rifle", loadout.ActiveSlot == 3, special != null);
        var throwable = loadout.CurrentThrowable;
        _throwables.UpdateSlot(throwable?.DisplayName ?? Loc.Text("hud_throwable"), throwable == null ? "—" : $"×{loadout.Count(throwable)}",
            throwable?.IconPath ?? "", "throwable", player.Weapons.AimingThrow, throwable != null);
        UpdateActiveInfo(player, player.Weapons.AimingThrow && throwable != null ? _throwables
            : loadout.ActiveSlot switch { 1 => _slot1, 3 => _slot3, _ => _slot2 });
        _blind.Color = new Color(1, 1, 1, Mathf.Clamp(player.BlindTime / 1.2f, 0, 1) * .95f);
        UpdateStatusEffects(player);

        _status.Text = player.IsDowned ? Loc.Format("fmt_hud_downed", Mathf.CeilToInt(Mathf.Max(0, player.BleedLeft)))
            : player.IsRestrained ? Loc.Text("restrained_status")
            : player.Weapons.AimingThrow && player.Loadout.CurrentThrowable is { } aiming
            ? Loc.Format("fmt_hud_throw_aim", aiming.DisplayName, player.Loadout.Count(aiming))
            : player.HeliumTime > 0 ? Loc.Format("fmt_hud_helium", Mathf.CeilToInt(player.HeliumTime))
            : player.IsStunned ? Loc.Text("ui_hud_stunned")
            : player.OnLastDown && stage.ReviveEnabled ? Loc.Text("hud_last_down") : "";
        _prompt.Text = player.IsRestrained ? Loc.Text(GameSession.DebugEscapeEnabled ? "restrained_cheat" : "restrained_rescue") : "";
        UpdateInteractionUi((float)delta);
        _help.Visible = stage.Objective == Objective.LeaveSafeRoom || stage.Elapsed < 20;

        _darkVignette.SetShaderParameter("strength", player.HeliumTime > 0 ? 1f : .25f);
        _hurtVignette.SetShaderParameter("strength", Mathf.Clamp(player.DamageFlash, 0, 1) * .8f + (player.Health < 30 && !player.IsDead ? .25f : 0));
        float helium = player.HeliumTime > 0 ? .35f + .15f * Mathf.Sin(stage.Elapsed * 6) : 0;
        _heliumVignette.SetShaderParameter("strength", helium);

        if (!playing && Paused) SetPaused(false);
        UpdateEndPanel(stage, player);
        UpdatePausePanels();
        if (InventoryOpen && (!playing || Paused || player.IsDead)) SetInventoryOpen(false);
        UpdateWeaponAction((float)delta);
        bool captured = playing && !Paused && !InventoryOpen && !CardWindowOpen && !stage.AllStopped && GetWindow().HasFocus();
        var mode = captured ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != mode) Input.MouseMode = mode;
    }

    /// <summary>"12 / 300", "30 / ∞", or a recharging weapon's seconds left.</summary>
    private static string SlotAmmo(WeaponState state) => state.CooldownLeft > 0
        ? $"{Mathf.CeilToInt(state.CooldownLeft)}s"
        : $"{state.Mag} / {(state.HasInfiniteReserve ? "∞" : state.Reserve.ToString())}";

    private void UpdateEndPanel(Stage stage, Player player)
    {
        bool ended = stage.State != StageState.Playing;
        _endPanel.Visible = ended && !Paused;
        if (!ended) return;
        int t = (int)stage.Elapsed;
        _endTitle.Text = Loc.Text(stage.State == StageState.Cleared ? "ui_end_clear" : "ui_end_over");
        _endTitle.AddThemeColorOverride("font_color", stage.State == StageState.Cleared ? UiTheme.Heal : UiTheme.Danger);
        var result = stage.ModeRules?.ResultText is { Length: > 0 } line ? line + "\n" : "";
        _endBody.Text =
            $"{Loc.Text(player.Definition.DisplayName)}\n" + result +
            Loc.Format("fmt_end_stats", t / 60, t % 60, stage.Kills, stage.SpecialKills, player.ShotsFired) + "\n\n" +
            Loc.Text(OnlineKey("ui_end_keys"));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        var stage = Stage;
        if (GraphicsOpen)
        {
            if (e.IsActionPressed(GameInput.Pause) || e.IsActionPressed("ui_cancel")) CloseGraphicsOptions();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e.IsActionPressed(GameInput.Zoom)) stage.Cam.Close = !stage.Cam.Close;
        if (e.IsActionPressed(GameInput.Inventory) && stage.State == StageState.Playing && !Paused)
        {
            SetInventoryOpen(!InventoryOpen);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (InventoryOpen && e.IsActionPressed(GameInput.Pause))
        {
            SetInventoryOpen(false);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (e.IsActionPressed(GameInput.Pause) || e.IsActionPressed("ui_cancel"))
        {
            if (stage.State != StageState.Playing) GoMenu();
            else SetPaused(!Paused);
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed(GameInput.Confirm) && (stage.State != StageState.Playing || Paused))
        {
            SetPaused(false);
            stage.Restart();
        }
        else if (Paused && e is InputEventKey { Pressed: true, PhysicalKeycode: Key.M })
            GoMenu();
    }

    /// <summary>Online, Enter means "back to the room" for the host and nothing for a client: their own key hints.</summary>
    private string OnlineKey(string key) => Stage.Role switch
    {
        NetRole.Host => key + "_host",
        NetRole.Client => key + "_client",
        _ => key,
    };

    private void GoMenu()
    {
        SetPaused(false);
        Stage.BackToMenu();
    }

    public void SetPaused(bool paused)
    {
        Stage.Player.SuppressMenuInput();
        Paused = paused && Stage.State == StageState.Playing;
        GraphicsOpen = false;
        UpdatePausePanels();
        if (Paused) SetInventoryOpen(false);
        // Online the world keeps running for everyone; only the menu opens (multiplayer-sync-design.md 3장).
        GetTree().Paused = (Paused && Stage.Role == NetRole.Offline) || Stage.AllStopped;
    }

    // ---------- painters ----------

    /// <summary>Arrows drawn on the last frame (checks): one per teammate or boss off screen.</summary>
    public IReadOnlyList<string> OffscreenArrows { get; private set; } = new List<string>();

    /// <summary>
    /// Teammates (and the boss) off screen: a translucent arrow on the screen edge pointing their way, with the name
    /// (decisions.md M-26). In a wide field the team spreads out of view.
    /// </summary>
    private void DrawOffscreenArrows(Control c)
    {
        var stage = Stage;
        var shown = new List<string>();
        OffscreenArrows = shown;
        if (stage.State != StageState.Playing || stage.Player.IsDead) return;
        var targets = new List<(Vector3 At, string Name, Color Color)>();
        foreach (var p in stage.Players)
        {
            if (p == stage.Player || !IsInstanceValid(p) || p.IsDead) continue;
            var name = p.DisplayLabel.Length > 0 ? p.DisplayLabel : Loc.Text(p.Definition.DisplayName);
            targets.Add((p.GlobalPosition + Vector3.Up, name, p.IsDowned ? UiTheme.Danger : UiTheme.Heal));
        }
        if (stage.ModeRules is SurvivorRules { Boss: { IsDead: false } boss } && IsInstanceValid(boss))
            targets.Add((boss.GlobalPosition + Vector3.Up * 2, Loc.Text(boss.Monster.NameKey), UiTheme.Danger));
        const float inset = 52f;
        var size = c.Size;
        var center = size / 2;
        var font = c.GetThemeDefaultFont();
        foreach (var (at, name, color) in targets)
        {
            var screen = stage.Cam.UnprojectPosition(at);
            if (screen.X > inset && screen.X < size.X - inset && screen.Y > inset && screen.Y < size.Y - inset) continue;
            var dir = screen - center;
            if (dir.LengthSquared() < 1) continue;
            dir = dir.Normalized();
            float tx = Mathf.Abs(dir.X) > .001f ? ((dir.X > 0 ? size.X - inset : inset) - center.X) / dir.X : float.MaxValue;
            float ty = Mathf.Abs(dir.Y) > .001f ? ((dir.Y > 0 ? size.Y - inset : inset) - center.Y) / dir.Y : float.MaxValue;
            var edge = center + dir * Mathf.Min(tx, ty);
            var side = new Vector2(-dir.Y, dir.X);
            var tip = edge + dir * 16;
            var arrow = new[] { tip, edge - dir * 8 + side * 12, edge - dir * 8 - side * 12 };
            c.DrawColoredPolygon(arrow, new Color(color, .72f));
            c.DrawPolyline(new[] { arrow[0], arrow[1], arrow[2], arrow[0] }, new Color(0, 0, 0, .5f), 2, true);
            var label = edge - dir * 30;
            c.DrawString(font, label + new Vector2(-70, 6), name, HorizontalAlignment.Center, 140, 14, new Color(Hud.HudWhite, .82f));
            shown.Add(name);
        }
    }

    private void DrawModeBar(Control c)
    {
        if (Stage.ModeRules is not { } rules || rules.BarLabel.Length == 0) return;
        var size = c.Size;
        HealthTrack.Draw(c.GetCanvasItem(), new Rect2(Vector2.Zero, size));
        float t = Mathf.Clamp(rules.BarFraction, 0, 1);
        if (t > 0) c.DrawRect(new Rect2(3, 3, (size.X - 6) * t, size.Y - 6), new Color(rules.BarColor, .85f));
        var font = c.GetThemeDefaultFont();
        c.DrawString(font, new Vector2(1, 22), rules.BarLabel, HorizontalAlignment.Center, size.X, 16, new Color(0, 0, 0, .7f));
        c.DrawString(font, new Vector2(0, 21), rules.BarLabel, HorizontalAlignment.Center, size.X, 16, HudWhite);
    }

    private void DrawSubBar(Control c)
    {
        if (Stage.ModeRules is not { } rules || rules.SubBarLabel.Length == 0) return;
        var size = c.Size;
        c.DrawRect(new Rect2(Vector2.Zero, size), new Color(.03f, .05f, .09f, .75f));
        float t = Mathf.Clamp(rules.SubBarFraction, 0, 1);
        if (t > 0) c.DrawRect(new Rect2(2, 2, (size.X - 4) * t, size.Y - 4), new Color("7fd4ff"));
        var font = c.GetThemeDefaultFont();
        c.DrawString(font, new Vector2(-64, 12), rules.SubBarLabel, HorizontalAlignment.Right, 58, 13, HudWarm);
    }

    private void DrawHealth(Control c)
    {
        var player = Stage.Player;
        var size = c.Size;
        HealthTrack.Draw(c.GetCanvasItem(), new Rect2(Vector2.Zero, size));
        float t = player.MaxHealth > 0 ? Mathf.Clamp((float)player.Health / player.MaxHealth, 0, 1) : 0;
        if (t > 0)
            HealthFill.Draw(c.GetCanvasItem(), new Rect2(3, 3, (size.X - 6) * t, size.Y - 6));
        c.DrawString(c.GetThemeDefaultFont(), new Vector2(11, 20), "HP", HorizontalAlignment.Left, 30, 11, HudWhite);
        c.DrawString(c.GetThemeDefaultFont(), new Vector2(0, 21), $"{player.Health} / {player.MaxHealth}", HorizontalAlignment.Center, size.X, 15, HudWhite);
        if (player.IsHealing)
            c.DrawRect(new Rect2(2, size.Y - 6, (size.X - 4) * player.HealProgress, 4), UiTheme.Heal);
    }

    /// <summary>How long a hit-direction arc stays on screen (decisions.md C-23).</summary>
    public const float HitArcSeconds = 1.2f;
    private readonly List<(Vector3 Direction, float Left)> _hits = new();
    /// <summary>Screen angle (radians, 0 = right, clockwise) of each arc still showing; read by checks.</summary>
    public IReadOnlyList<float> HitArcAngles { get; private set; } = new List<float>();

    /// <summary>A red arc around your character on the side the hit came from, fading out.</summary>
    private void DrawHitArcs(Control c)
    {
        var stage = Stage;
        var player = stage.Player;
        float dt = (float)GetProcessDeltaTime();
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            var (direction, left) = _hits[i];
            left -= Paused ? 0 : dt;
            if (left <= 0) _hits.RemoveAt(i);
            else _hits[i] = (direction, left);
        }
        var angles = new List<float>();
        if (player.IsDead || stage.State != StageState.Playing || _hits.Count == 0) { HitArcAngles = angles; return; }
        var cam = stage.Cam;
        var body = player.GlobalPosition + Vector3.Up;
        var center = cam.UnprojectPosition(body);
        foreach (var (direction, left) in _hits)
        {
            // The direction points from the source to us; the arc sits on the source's side.
            var toward = cam.UnprojectPosition(body - direction * 2f) - center;
            if (toward.LengthSquared() < 1) continue;
            float angle = toward.Angle();
            angles.Add(angle);
            float alpha = Mathf.Clamp(left / HitArcSeconds, 0, 1);
            c.DrawArc(center, 78, angle - .5f, angle + .5f, 24, new Color(0, 0, 0, .45f * alpha), 9, true);
            c.DrawArc(center, 78, angle - .45f, angle + .45f, 24, new Color(1f, .22f, .18f, .9f * alpha), 5, true);
        }
        HitArcAngles = angles;
    }

    private static readonly Color ReticleFar = new("ffb347"), ReticleOut = new("ff5a4f"), ReticleHot = new("ff6b6b"),
        HitMarkHot = new("ff9a3c"), HitMarkCrit = new("ffd65c");
    /// <summary>Reticle sizes in 900-high UI pixels; family strokes and magazine layout live in HudReticle.</summary>
    public const float ReticleTickLength = 14, ReticleMinRadius = 12, ReticleDot = 2.5f;
    /// <summary>A hit's double slash lives this long; at most this many show at once (C-30).</summary>
    public const float HitMarkSeconds = .4f;
    public const int MaxHitMarks = 4;
    private readonly List<(double At, bool Crit)> _hitMarks = new();
    /// <summary>Hit marks on the reticle now (checks).</summary>
    public int HitMarks => System.Linq.Enumerable.Count(_hitMarks, m => Now - m.At < HitMarkSeconds);
    private static double Now => Time.GetTicksMsec() / 1000.0;

    /// <summary>The local player's shot landed: a double slash on each diagonal pops out and settles in (C-30).</summary>
    public void ShowHitMarker(bool crit)
    {
        _hitMarks.RemoveAll(m => Now - m.At >= HitMarkSeconds);
        _hitMarks.Add((Now, crit));
        if (_hitMarks.Count > MaxHitMarks) _hitMarks.RemoveAt(0);
    }

    /// <summary>A line with a thin dark edge, and a soft halo of <paramref name="glow"/> when it is hot.</summary>
    private static void ReticleLine(Control c, Vector2 a, Vector2 b, Color color, float width, Color glow, float heat)
    {
        if (heat > 0)
        {
            for (int i = 6; i >= 1; i--)
                c.DrawLine(a, b, new Color(glow, (.04f + .02f * (6 - i)) * heat * color.A), width + i * 1.5f, true);
        }
        c.DrawLine(a, b, new Color(0, 0, 0, .28f * color.A), width + 2, true);
        c.DrawLine(a, b, color, width, true);
    }

    private void DrawHitMarks(Control c, Vector2 center, float radius)
    {
        double now = Now;
        _hitMarks.RemoveAll(m => now - m.At >= HitMarkSeconds);
        foreach (var (at, crit) in _hitMarks)
        {
            float t = (float)((now - at) / HitMarkSeconds);
            // Out at once, then settling in toward the reticle; white turning orange (yellow on a crit), fading at the end.
            float reach = radius + 6 + 22 * Mathf.Pow(1 - t, 2);
            var tint = HudWhite.Lerp(crit ? HitMarkCrit : HitMarkHot, Mathf.Min(1, t * 2.5f));
            tint.A = t < .6f ? 1 : 1 - (t - .6f) / .4f;
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
            {
                var dir = d.Normalized();
                var side = new Vector2(-dir.Y, dir.X) * 2.5f;
                var mid = center + dir * (reach + 6);
                foreach (var s in new[] { side, -side })
                    ReticleLine(c, mid - dir * 6 + s, mid + dir * 6 + s, tint, 3, crit ? HitMarkCrit : HitMarkHot, Mathf.Min(1, t * 2.5f));
            }
        }
    }

    private void DrawReticle(Control c)
    {
        var stage = Stage;
        var player = stage.Player;
        if (player.IsDead || player.IsDowned || stage.State != StageState.Playing || Paused || stage.AllStopped || InventoryOpen || CardWindowOpen) return;
        var cam = stage.Cam;
        if (player.Weapons.AimingThrow && player.Weapons.Preview is { } preview)
        {
            // Throw aim: a small cross on the actual landing spot (the 3D ring shows the effect area).
            var spot = cam.UnprojectPosition(preview.Target);
            var tint = ThrowPreview.KindColor(player.Loadout.CurrentThrowable?.Kind ?? ThrowableKind.Frag);
            c.DrawLine(spot + new Vector2(-9, 0), spot + new Vector2(9, 0), tint, 2, true);
            c.DrawLine(spot + new Vector2(0, -9), spot + new Vector2(0, 9), tint, 2, true);
            return;
        }
        var aim = player.AimPoint;
        var center = cam.UnprojectPosition(aim);
        if (!player.CanAttack)
        {
            // Cannot attack (ladder, zipline, gondola, held; decisions.md M-36): a dim grey ring with a slash where the cursor is.
            var off = new Color(UiTheme.Muted, .7f);
            c.DrawArc(center, 11, 0, Mathf.Tau, 40, new Color(0, 0, 0, .35f), 5, true);
            c.DrawArc(center, 11, 0, Mathf.Tau, 40, off, 2.5f, true);
            c.DrawLine(center + new Vector2(-7.5f, 7.5f), center + new Vector2(7.5f, -7.5f), off, 2.5f, true);
            return;
        }
        var gun = player.Gun;
        float actionWeight = _weaponAction.Weight;
        if (player.AimBlend > 0 && actionWeight < 1)
        {
            // Aiming (decisions.md C-26): a faint sight line from the gun to the cursor.
            var from = cam.UnprojectPosition(player.GlobalPosition + Vector3.Up * Player.AimHeight);
            c.DrawLine(from, center, new Color(UiTheme.Accent, .35f * player.AimBlend * (1 - actionWeight)), 2, true);
        }

        float distance = Mathf.Max(1, (aim - player.GlobalPosition).Length());
        float Radius(float spread) =>
            Mathf.Max(ReticleMinRadius, (cam.UnprojectPosition(aim + cam.GlobalTransform.Basis.X * distance * Mathf.Tan(Mathf.DegToRad(spread * .5f))) - center).Length());
        // The same cone the shot leaves in: a shotgun's pellet cone, worn gear's narrowing (C-34).
        float cone = player.Weapons.ShotCone(gun);
        float aimedCone = player.Weapons.ShotCone(gun, fullyAimed: true);
        float radius = Radius(cone);
        float aimed = Mathf.Min(radius, Radius(aimedCone));
        // Past the effective range the reticle turns amber (damage falls off), past the maximum red (no hit; C-27, C-30).
        float reach = player.IsAiming ? gun.Def.AimRangeMultiplier : 1;
        var range = gun.IsMelee ? HudWhite : distance > gun.Def.MaxRange * reach ? ReticleOut : distance > gun.Def.EffectiveRange * reach ? ReticleFar : HudWhite;
        var color = player.CritReady ? HudWarm : new Color(range, .98f);
        color.A *= 1 - actionWeight;

        DrawGunSight(c, center, gun.Def, radius, aimed, color, gun.Reloading ? 0 : gun.Heat);
        DrawHitMarks(c, center, aimed);
        DrawMagazineAction(c, center, gun, radius);

        if (player.FocusTarget != null && actionWeight < .01f)
        {
            float focus = Mathf.Min(1, player.FocusTime / Player.CritFocusTime);
            c.DrawArc(center, radius + 26, 0, Mathf.Tau, 96, new Color(0, 0, 0, .25f), 5, true);
            if (focus > 0)
                DrawLightArc(c, center, radius + 26, -Mathf.Pi / 2 + Mathf.Tau * focus, UiTheme.Accent, player.CritReady ? .95f : .65f);
        }
    }

    /// <summary>Downs left in this life as dots after the name (M-40): ● still there, ○ used; none without revive.</summary>
    public static string DownPips(Stage stage, Player survivor)
    {
        if (!stage.ReviveEnabled || survivor.IsDead) return "";
        int total = stage.DownsPerLife, left = Mathf.Clamp(total - survivor.DownCount, 0, total);
        return "  " + new string('●', left) + new string('○', total - left);
    }

    /// <summary>The pause screen's first line: mode · campaign or enemy family · difficulty and what it allows (M-39 · M-40).</summary>
    private static string SetupLine(Stage stage)
    {
        string card = PlaySetup.CardKey(stage.Mode, stage.Mode == GameMode.Scenario ? GameSession.Campaign : stage.ThemeId);
        var rules = SquadDatabase.Current.Rules;
        return Loc.Format("fmt_pause_setup", Loc.Text("mode_" + stage.Mode.ToString().ToLowerInvariant()), Loc.Text(card),
            Loc.Text(PlaySetup.DifficultyKey(stage.Difficulty)), rules.Downs(stage.Difficulty), rules.Respawns(stage.Difficulty));
    }
}
