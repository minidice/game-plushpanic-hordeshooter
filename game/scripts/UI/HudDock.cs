using Godot;

namespace HordeShooter;

public partial class Hud
{
    internal static readonly Color HudWhite = new("f3f4f1");
    internal static readonly Color HudMuted = new("a8b6c9");
    internal static readonly Color HudWarm = new("f5d6ad");
    private static readonly StyleBoxFlat HealthTrack = HudPlate(new Color("351e29"), new Color("713742"), 0, 16);
    private static readonly StyleBoxFlat HealthFill = HudPlate(new Color("ef575e"), new Color("ff8680"), 0, 13);

    internal static Label HudLabel(string text = "", int size = 16, Color? color = null)
    {
        var label = UiTheme.Label(text, size, color ?? HudWhite);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.04f, .08f, .65f));
        return label;
    }

    internal static StyleBoxFlat HudPlate(Color? fill = null, Color? border = null, int pad = 12, int radius = 12)
    {
        return new StyleBoxFlat
        {
            BgColor = fill ?? new Color(.025f, .045f, .09f, .83f),
            BorderColor = border ?? new Color(.45f, .57f, .72f, .26f),
            BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderWidthLeft = 1,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = pad, ContentMarginRight = pad,
            ContentMarginTop = pad, ContentMarginBottom = pad,
            ShadowColor = new Color(0, .01f, .04f, .3f), ShadowSize = 5,
        };
    }

    private void BuildCompactHud()
    {
        var objectivePanel = Place(new PanelContainer { Name = "ObjectivePanel" }, 0, 0, new Vector2(24, 24), false, false);
        objectivePanel.AddThemeStyleboxOverride("panel", HudPlate());
        var objectiveBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        objectiveBox.AddThemeConstantOverride("separation", 6);
        objectivePanel.AddChild(objectiveBox);
        // Survival / Survivor name the mode in front of the map so a recording shows which one it is.
        var stageTitle = Stage.ModeRules is { } rules
            ? Loc.Format("fmt_hud_mode_stage", Loc.Text("mode_" + rules.Mode.ToString().ToLowerInvariant()), Loc.Text("ui_hud_stage"))
            : Loc.Text("ui_hud_stage");
        objectiveBox.AddChild(HudLabel(stageTitle, 12, HudMuted));
        _objective = HudLabel("", 17);
        _objective.Name = "ObjectiveText";
        _objective.CustomMinimumSize = new Vector2(360, 0);
        _objective.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        objectiveBox.AddChild(_objective);
        _help = HudLabel(Loc.Text("hud_quick_help"), 12, HudMuted);
        _help.Name = "QuickHelp";
        objectiveBox.AddChild(_help);

        var stats = Place(new PanelContainer { Name = "RunStats" }, 1, 0, new Vector2(-24, 24), true, false);
        stats.AddThemeStyleboxOverride("panel", HudPlate());
        _timer = HudLabel("", 17);
        _timer.HorizontalAlignment = HorizontalAlignment.Right;
        stats.AddChild(_timer);
        _notice = Place(HudLabel("", 18, HudWarm), .5f, 0, new Vector2(0, 112), true, false, centerX: true);
        _notice.Name = "StageNotice";
        _notice.HorizontalAlignment = HorizontalAlignment.Center;

        var dock = Place(new HBoxContainer { Name = "EquipmentDock" }, .5f, 1, new Vector2(0, -22), true, true, centerX: true);
        dock.AddThemeConstantOverride("separation", 12);
        var vitals = new VBoxContainer { Name = "Vitals", CustomMinimumSize = new Vector2(220, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        vitals.AddThemeConstantOverride("separation", 7);
        _hp = HudLabel("", 14, HudMuted);
        _hp.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _hp.CustomMinimumSize = new Vector2(220, 0);
        vitals.AddChild(_hp);
        vitals.AddChild(new Paint { Name = "HealthBar", CustomMinimumSize = new Vector2(220, 30), Painter = DrawHealth });
        dock.AddChild(vitals);
        dock.AddChild(new Paint { Name = "LeapGauge", CustomMinimumSize = new Vector2(74, 84), Painter = DrawLeap });
        _slot1 = new HudEquipmentSlot("SecondarySlot", "1");
        _slot2 = new HudEquipmentSlot("PrimarySlot", "2");
        _slot3 = new HudEquipmentSlot("SpecialSlot", "3");
        _throwables = new HudEquipmentSlot("ThrowableSlot", "G");
        _medkit = new HudEquipmentSlot("MedkitSlot", "Q");
        foreach (var slot in new[] { _slot1, _slot2, _slot3, _throwables, _medkit }) dock.AddChild(slot);

        var ammoPanel = new PanelContainer { Name = "AmmoPanel", CustomMinimumSize = new Vector2(174, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        ammoPanel.AddThemeStyleboxOverride("panel", HudPlate(pad: 10));
        var ammoBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        ammoBox.AddThemeConstantOverride("separation", 0);
        ammoPanel.AddChild(ammoBox);
        _reserve = HudLabel("", 12, HudMuted);
        _reserve.Name = "ActiveWeaponName";
        _reserve.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _reserve.CustomMinimumSize = new Vector2(154, 0);
        _reserve.HorizontalAlignment = HorizontalAlignment.Right;
        ammoBox.AddChild(_reserve);
        _ammo = HudLabel("", 28);
        _ammo.Name = "ActiveAmmo";
        _ammo.HorizontalAlignment = HorizontalAlignment.Right;
        ammoBox.AddChild(_ammo);
        _ammoHint = HudLabel(Loc.Text("hud_ammo_hint"), 11, HudMuted);
        _ammoHint.HorizontalAlignment = HorizontalAlignment.Right;
        ammoBox.AddChild(_ammoHint);
        dock.AddChild(ammoPanel);
    }

    private void DrawLeap(Control c)
    {
        var left = Stage.Player.RollCooldownLeft;
        var center = new Vector2(c.Size.X / 2, 32);
        var ready = left <= 0;
        c.DrawCircle(center, 28, new Color(.025f, .045f, .09f, .9f));
        c.DrawArc(center, 26, 0, Mathf.Tau, 48, new Color(.28f, .36f, .48f, .6f), 3, true);
        var progress = 1 - Mathf.Clamp(left / Player.RollCooldown, 0, 1);
        if (progress > 0)
            c.DrawArc(center, 26, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * progress, 48, ready ? HudWarm : HudMuted, 3, true);
        var font = c.GetThemeDefaultFont();
        if (ready)
        {
            // A simple lightning mark reads as the leap ability without implying an extra stat.
            c.DrawColoredPolygon(new[] { center + new Vector2(3, -13), center + new Vector2(-9, 2), center + new Vector2(-1, 2), center + new Vector2(-4, 14), center + new Vector2(10, -3), center + new Vector2(2, -3) }, HudWarm);
        }
        else c.DrawString(font, new Vector2(0, 38), $"{left:0.0}", HorizontalAlignment.Center, c.Size.X, 17, HudWhite);
        c.DrawString(font, new Vector2(0, 78), "SPACE", HorizontalAlignment.Center, c.Size.X, 11, HudMuted);
    }
}
