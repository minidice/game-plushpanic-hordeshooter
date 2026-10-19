using Godot;

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

    private Control _root = null!;
    private Label _objective = null!, _timer = null!, _notice = null!, _hp = null!;
    private Label _ammo = null!, _reserve = null!, _ammoHint = null!, _status = null!, _prompt = null!, _help = null!;
    private HudEquipmentSlot _slot1 = null!, _slot2 = null!, _throwables = null!, _medkit = null!;
    private ColorRect _blind = null!;
    private Label _endTitle = null!, _endBody = null!;
    private PanelContainer _endPanel = null!, _pausePanel = null!;
    private ShaderMaterial _darkVignette = null!, _hurtVignette = null!, _heliumVignette = null!;

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

        var reticle = new Paint { Name = "Reticle", Painter = DrawReticle };
        reticle.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(reticle);

        BuildCompactHud();

        // Flashbang: you looked at it.
        _blind = new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _blind.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_blind);

        _status = Place(HudLabel("", 17, UiTheme.Helium), .5f, 1, new Vector2(0, -166), true, true, centerX: true);
        _status.Name = "Status";
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt = Place(HudLabel("", 18, HudWhite), .5f, 1, new Vector2(0, -132), true, true, centerX: true);
        _prompt.Name = "InteractionPrompt";
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;

        _endPanel = CenterPanel(out var endBox);
        _endTitle = UiTheme.Label("", 48, UiTheme.Accent);
        _endTitle.HorizontalAlignment = HorizontalAlignment.Center;
        endBox.AddChild(_endTitle);
        _endBody = UiTheme.Label("", 20);
        _endBody.HorizontalAlignment = HorizontalAlignment.Center;
        endBox.AddChild(_endBody);

        _pausePanel = CenterPanel(out var pauseBox);
        var pauseTitle = UiTheme.Label(Loc.Text("ui_pause_title"), 40, UiTheme.Accent);
        pauseTitle.HorizontalAlignment = HorizontalAlignment.Center;
        pauseBox.AddChild(pauseTitle);
        var pauseBody = UiTheme.Label(Loc.Text("ui_pause_keys") + "\n\n" + ControlsText, 18);
        pauseBody.HorizontalAlignment = HorizontalAlignment.Center;
        pauseBox.AddChild(pauseBody);
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
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(.04f, .05f, .06f, .92f), 36, UiTheme.Accent));
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
        int t = (int)stage.Elapsed;
        _timer.Text = Loc.Format("fmt_hud_timer", t / 60, t % 60, stage.Kills);
        _notice.Text = stage.NoticeTime > 0 ? stage.Notice : "";
        _notice.Modulate = new Color(1, 1, 1, Mathf.Clamp(stage.NoticeTime, 0, 1));

        _hp.Text = player.IsHealing ? Loc.Format("fmt_hud_healing", (int)(player.HealProgress * 100)) : Loc.Text(player.Definition.DisplayName);
        _medkit.UpdateSlot(Loc.Text("hud_medkit"), player.IsHealing ? $"{(int)(player.HealProgress * 100)}%" : $"×{player.Medkits}", "", "medkit", player.IsHealing, player.Medkits > 0);
        _ammo.Text = gun.IsMelee ? Loc.Text("hud_melee")
            : gun.Reloading ? Loc.Text("ui_hud_reload")
            : $"{gun.Mag:00} / {(gun.Def.InfiniteReserve ? "∞" : gun.Reserve.ToString("00"))}";
        bool low = !gun.IsMelee && gun.Mag <= Mathf.Max(1, gun.Def.MagSize / 6);
        _ammo.AddThemeColorOverride("font_color", low ? UiTheme.Danger : HudWhite);
        _reserve.Text = gun.Def.DisplayName;
        _ammoHint.Text = Loc.Text(gun.IsMelee ? "hud_melee_hint" : !gun.Def.InfiniteReserve && !gun.Def.Refillable ? "hud_no_refill_hint" : "hud_ammo_hint");

        var loadout = player.Loadout;
        var secondary = loadout.Secondary;
        _slot1.UpdateSlot(secondary.Def.DisplayName, secondary.IsMelee ? "" : $"{secondary.Mag} / ∞", secondary.Def.IconPath,
            secondary.IsMelee ? "melee" : "pistol", loadout.ActiveSlot == 1, true);
        var primary = loadout.Primary;
        _slot2.UpdateSlot(primary?.Def.DisplayName ?? Loc.Text("hud_empty"), primary == null ? "" : $"{primary.Mag} / {primary.Reserve}",
            primary?.Def.IconPath ?? "", "rifle", loadout.ActiveSlot == 2, primary != null);
        var throwable = loadout.CurrentThrowable;
        _throwables.UpdateSlot(throwable?.DisplayName ?? Loc.Text("hud_throwable"), throwable == null ? "—" : $"×{loadout.Count(throwable)}",
            throwable?.IconPath ?? "", "throwable", player.Weapons.AimingThrow, throwable != null);
        _blind.Color = new Color(1, 1, 1, Mathf.Clamp(player.BlindTime / 1.2f, 0, 1) * .95f);

        _status.Text = player.IsRestrained ? Loc.Text("restrained_status")
            : player.Weapons.AimingThrow && player.Loadout.CurrentThrowable is { } aiming
            ? Loc.Format("fmt_hud_throw_aim", aiming.DisplayName, player.Loadout.Count(aiming))
            : player.HeliumTime > 0 ? Loc.Format("fmt_hud_helium", Mathf.CeilToInt(player.HeliumTime))
            : player.IsStunned ? Loc.Text("ui_hud_stunned") : "";
        _prompt.Text = player.IsRestrained ? Loc.Text(GameSession.DebugEscapeEnabled ? "restrained_cheat" : "restrained_rescue")
            : playing && stage.FocusedInteractable is { } use ? $"[E]  {use.Prompt}" : "";
        _help.Visible = stage.Objective == Objective.LeaveSafeRoom || stage.Elapsed < 20;

        _darkVignette.SetShaderParameter("strength", player.HeliumTime > 0 ? 1f : .25f);
        _hurtVignette.SetShaderParameter("strength", Mathf.Clamp(player.DamageFlash, 0, 1) * .8f + (player.Health < 30 && !player.IsDead ? .25f : 0));
        float helium = player.HeliumTime > 0 ? .35f + .15f * Mathf.Sin(stage.Elapsed * 6) : 0;
        _heliumVignette.SetShaderParameter("strength", helium);

        UpdateEndPanel(stage, player);
        _pausePanel.Visible = Paused;
        bool captured = playing && !Paused && GetWindow().HasFocus();
        var mode = captured ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != mode) Input.MouseMode = mode;
    }

    private void UpdateEndPanel(Stage stage, Player player)
    {
        bool ended = stage.State != StageState.Playing;
        _endPanel.Visible = ended && !Paused;
        if (!ended) return;
        int t = (int)stage.Elapsed;
        _endTitle.Text = Loc.Text(stage.State == StageState.Cleared ? "ui_end_clear" : "ui_end_over");
        _endTitle.AddThemeColorOverride("font_color", stage.State == StageState.Cleared ? UiTheme.Heal : UiTheme.Danger);
        _endBody.Text =
            $"{Loc.Text(player.Definition.DisplayName)}\n" +
            Loc.Format("fmt_end_stats", t / 60, t % 60, stage.Kills, stage.SpecialKills, player.ShotsFired) + "\n\n" +
            Loc.Text("ui_end_keys");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        var stage = Stage;
        if (e.IsActionPressed(GameInput.Zoom)) stage.Cam.Close = !stage.Cam.Close;

        if (e.IsActionPressed(GameInput.Pause))
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

    private void GoMenu()
    {
        SetPaused(false);
        Stage.BackToMenu();
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
        GetTree().Paused = paused;
    }

    // ---------- painters ----------

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

    private void DrawReticle(Control c)
    {
        var stage = Stage;
        var player = stage.Player;
        if (player.IsDead || stage.State != StageState.Playing || Paused) return;
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
        var gun = player.Gun;

        float distance = Mathf.Max(1, (aim - player.GlobalPosition).Length());
        float spreadOffset = distance * Mathf.Tan(Mathf.DegToRad(gun.Spread * .5f));
        float radius = Mathf.Max(7, (cam.UnprojectPosition(aim + cam.GlobalTransform.Basis.X * spreadOffset) - center).Length());
        var color = player.CritReady ? HudWarm : gun.Reloading ? HudMuted : new Color(HudWhite, .95f);

        foreach (var dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
        {
            c.DrawLine(center + dir * (radius + 2), center + dir * (radius + 8), new Color(0, 0, 0, .6f), 4, true);
            c.DrawLine(center + dir * (radius + 2), center + dir * (radius + 8), color, 2, true);
        }

        if (player.FocusTarget != null)
        {
            float focus = Mathf.Min(1, player.FocusTime / Player.CritFocusTime);
            c.DrawArc(center, radius + 7, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * focus, 40, UiTheme.Accent, 3, true);
        }

        var font = c.GetThemeDefaultFont();
        if (gun.Reloading)
        {
            var bar = new Rect2(center + new Vector2(-60, radius + 18), new Vector2(120, 10));
            c.DrawRect(bar, new Color(0, 0, 0, .7f));
            float progress = Mathf.Clamp(gun.ReloadElapsed / gun.ReloadTime, 0, 1);
            c.DrawRect(new Rect2(bar.Position.X + bar.Size.X * progress - 1.5f, bar.Position.Y - 3, 3, bar.Size.Y + 6), UiTheme.Paper);
        }
        else if (!gun.IsMelee && gun.Mag <= Mathf.Max(1, gun.Def.MagSize / 6))
        {
            c.DrawString(font, center + new Vector2(-60, radius + 32), Loc.Text(gun.Mag == 0 ? "ui_hud_mag_empty" : "ui_hud_ammo_low"),
                HorizontalAlignment.Center, 120, 16, UiTheme.Accent);
        }
    }
}
