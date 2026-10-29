using Godot;

namespace HordeShooter;

public partial class Hud
{
    internal static readonly Color HudWhite = UiTheme.Paper;
    internal static readonly Color HudMuted = UiTheme.Muted;
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
        => UiTheme.Plate(fill, border, pad, radius);

    private void BuildCompactHud()
    {
        var objectivePanel = Place(new PanelContainer { Name = "ObjectivePanel" }, 0, 0, new Vector2(24, 24), false, false);
        objectivePanel.AddThemeStyleboxOverride("panel", HudPlate());
        var objectiveBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        objectiveBox.AddThemeConstantOverride("separation", 6);
        objectivePanel.AddChild(objectiveBox);
        // Outpost / Onslaught name the mode in front of the map so a recording shows which one it is.
        var mapName = Stage.Map.TitleKey is { Length: > 0 } key ? Loc.Text(key) : "";
        var modeName = Stage.ModeRules is { } rules ? Loc.Text("mode_" + rules.Mode.ToString().ToLowerInvariant()) : "";
        var stageTitle = modeName.Length > 0 && mapName.Length > 0 ? Loc.Format("fmt_hud_mode_stage", modeName, mapName) : modeName + mapName;
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

        // One low row along the bottom (decisions.md M-42): health, the roll gauge, small square tiles with their keys under
        // them, then the hat and armor tiles. The active weapon's name and ammo float above its own tile.
        var dock = Place(new HBoxContainer { Name = "EquipmentDock" }, .5f, 1, new Vector2(0, -14), true, true, centerX: true);
        dock.AddThemeConstantOverride("separation", 8);
        var vitals = new VBoxContainer { Name = "Vitals", CustomMinimumSize = new Vector2(196, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd, MouseFilter = Control.MouseFilterEnum.Ignore };
        vitals.AddThemeConstantOverride("separation", 4);
        _hp = HudLabel("", 12, HudMuted);
        _hp.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _hp.CustomMinimumSize = new Vector2(196, 0);
        vitals.AddChild(_hp);
        vitals.AddChild(new Paint { Name = "HealthBar", CustomMinimumSize = new Vector2(196, 22), Painter = DrawHealth });
        // Lines the bar up with the tiles, above their key caps.
        vitals.AddChild(new Control { CustomMinimumSize = new Vector2(0, KeyCapHeight + 2), MouseFilter = Control.MouseFilterEnum.Ignore });
        dock.AddChild(vitals);
        dock.AddChild(new Paint { Name = "LeapGauge", CustomMinimumSize = new Vector2(56, 80), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            Painter = DrawLeap, TooltipText = Loc.Text("hud_roll_help") });
        _slot1 = new HudEquipmentSlot("SecondarySlot", "1");
        _slot2 = new HudEquipmentSlot("PrimarySlot", "2");
        _slot3 = new HudEquipmentSlot("SpecialSlot", "3");
        _throwables = new HudEquipmentSlot("ThrowableSlot", "G");
        _medkit = new HudEquipmentSlot("MedkitSlot", "Q");
        foreach (var slot in new[] { _slot1, _slot2, _slot3, _throwables, _medkit }) dock.AddChild(slot);
        // Onslaught's racks fill slots 1 and 2 only (decisions.md M-26).
        _slot3.Visible = true;
        dock.AddChild(BuildGearPanel());
        BuildActiveInfo();
    }

    internal const float KeyCapHeight = 16;
    private static readonly StyleBoxFlat KeyCapPlate = HudPlate(new Color(.02f, .03f, .05f, .7f), new Color(.6f, .7f, .82f, .22f), 0, 4);
    private static readonly StyleBoxFlat PillLight = HudPlate(new Color(.82f, .84f, .87f, .92f), new Color(1, 1, 1, .2f), 0, 6);
    private static readonly StyleBoxFlat PillDark = HudPlate(new Color(.015f, .02f, .035f, .88f), new Color(.6f, .7f, .82f, .2f), 0, 6);
    private static readonly StyleBoxFlat PillDanger = HudPlate(new Color(.82f, .22f, .2f, .95f), new Color(1, .55f, .5f, .5f), 0, 6);
    private static readonly Color PillInk = new("1b2230");

    /// <summary>A key in a small cap under a tile (or next to the active weapon's name).</summary>
    internal static Control KeyCap(string key)
    {
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var cap = new PanelContainer { CustomMinimumSize = new Vector2(KeyCapHeight, KeyCapHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        var plate = (StyleBoxFlat)KeyCapPlate.Duplicate();
        plate.ContentMarginLeft = plate.ContentMarginRight = 4;
        cap.AddThemeStyleboxOverride("panel", plate);
        var label = HudLabel(key, 9, HudMuted);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        cap.AddChild(label);
        center.AddChild(cap);
        return center;
    }

    private Control _activeInfo = null!;
    private PanelContainer _namePill = null!, _ammoPill = null!;
    private Control _reloadKey = null!;

    private static PanelContainer Pill(Label label, StyleBoxFlat style)
    {
        var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var plate = (StyleBoxFlat)style.Duplicate();
        plate.ContentMarginLeft = plate.ContentMarginRight = 7;
        plate.ContentMarginTop = plate.ContentMarginBottom = 1;
        pill.AddThemeStyleboxOverride("panel", plate);
        pill.AddChild(label);
        return pill;
    }

    /// <summary>The active weapon's name (with the reload key) and its ammo, in two small pills over its tile.</summary>
    private void BuildActiveInfo()
    {
        var box = new VBoxContainer { Name = "ActiveInfo", MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", 4);
        _reserve = HudLabel("", 11, PillInk);
        _reserve.Name = "ActiveWeaponName";
        _reserve.AddThemeConstantOverride("outline_size", 0);
        _namePill = Pill(_reserve, PillLight);
        top.AddChild(_namePill);
        _reloadKey = KeyCap("R");
        top.AddChild(_reloadKey);
        box.AddChild(top);
        _ammo = HudLabel("", 13);
        _ammo.Name = "ActiveAmmo";
        _ammo.HorizontalAlignment = HorizontalAlignment.Center;
        _ammoPill = Pill(_ammo, PillDark);
        _ammoPill.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        box.AddChild(_ammoPill);
        _root.AddChild(box);
        _activeInfo = box;
    }

    /// <summary>Fills the pills and puts them over the tile in use. An empty magazine turns both red (M-42).</summary>
    private void UpdateActiveInfo(Player player, HudEquipmentSlot slot)
    {
        var gun = player.Gun;
        bool throwing = player.Weapons.AimingThrow && player.Loadout.CurrentThrowable != null;
        bool busy = !gun.IsMelee && (gun.Reloading || gun.CooldownLeft > 0);
        bool empty = !throwing && !gun.IsMelee && !busy && gun.Mag == 0;
        bool dry = empty && gun.Reserve == 0 && !gun.HasInfiniteReserve;
        bool low = !gun.IsMelee && gun.Mag <= Mathf.Max(1, gun.Def.MagSize / 6);
        if (throwing)
        {
            var held = player.Loadout.CurrentThrowable!;
            _reserve.Text = held.DisplayName;
            _ammo.Text = $"×{player.Loadout.Count(held)}";
        }
        else
        {
            _reserve.Text = dry ? Loc.Text("hud_ammo_out") : empty ? Loc.Text("hud_ammo_unloaded") : gun.Def.DisplayName;
            _ammo.Text = gun.IsMelee ? Loc.Text("hud_melee")
                : gun.CooldownLeft > 0 ? Loc.Format("fmt_hud_cooldown", Mathf.CeilToInt(gun.CooldownLeft))
                : gun.Reloading ? Loc.Text("ui_hud_reload")
                : $"{gun.Mag:00} / {(gun.HasInfiniteReserve ? "∞" : gun.Reserve.ToString("00"))}";
        }
        Restyle(_namePill, empty ? PillDanger : PillLight);
        _reserve.AddThemeColorOverride("font_color", empty ? HudWhite : PillInk);
        Restyle(_ammoPill, empty ? PillDanger : PillDark);
        _ammo.AddThemeColorOverride("font_color", throwing || empty ? HudWhite : busy ? HudMuted : low ? UiTheme.Danger : HudWhite);
        _reloadKey.Visible = !throwing && !gun.IsMelee;
        // A melee weapon has no ammo: its name is enough.
        _ammoPill.Visible = throwing || !gun.IsMelee;
        _activeInfo.Visible = slot.IsVisibleInTree();
        if (!_activeInfo.Visible) return;
        _activeInfo.ResetSize();
        var rect = slot.TileRect;
        var size = _activeInfo.GetCombinedMinimumSize();
        _activeInfo.Position = new Vector2(rect.GetCenter().X - size.X / 2, rect.Position.Y - size.Y - 6) - _root.GetGlobalRect().Position;
    }

    private static void Restyle(PanelContainer pill, StyleBoxFlat look)
    {
        var plate = (StyleBoxFlat)pill.GetThemeStylebox("panel");
        plate.BgColor = look.BgColor;
        plate.BorderColor = look.BorderColor;
    }

    private void DrawLeap(Control c)
    {
        var player = Stage.Player;
        var left = player.RollRechargeLeft;
        var center = new Vector2(c.Size.X / 2, c.Size.Y - KeyCapHeight - 4 - 26);
        var ready = player.RollCharges > 0;
        c.DrawCircle(center, 24, new Color(.008f, .016f, .025f, .70f), true, -1, true);
        c.DrawArc(center, 22, 0, Mathf.Tau, 96, new Color(.28f, .36f, .48f, .25f), 3, true);
        var progress = player.RollCharges == Player.MaxRollCharges ? 1 : 1 - Mathf.Clamp(left / Player.RollCooldown, 0, 1);
        if (progress > 0)
            DrawLightArc(c, center, 22, -Mathf.Pi / 2 + Mathf.Tau * progress, UiTheme.ActionGlow, ready ? 1 : .55f);
        var font = c.GetThemeDefaultFont();
        c.DrawString(font, center + new Vector2(-c.Size.X / 2, 4), left > 0 && !ready ? $"{left:0.0}" : $"{player.RollCharges}/{Player.MaxRollCharges}",
            HorizontalAlignment.Center, c.Size.X, 14, ready ? HudWhite : HudMuted);
        for (int i = 0; i < Player.MaxRollCharges; i++)
            c.DrawCircle(center + new Vector2((i - .5f) * 10, 12), 2.5f, i < player.RollCharges ? UiTheme.ActionGlow : HudMuted.Darkened(.55f), true, -1, true);
        // The key, in the same cap the tiles use.
        var cap = new Rect2(c.Size.X / 2 - 22, c.Size.Y - KeyCapHeight, 44, KeyCapHeight);
        c.DrawStyleBox(KeyCapPlate, cap);
        c.DrawString(font, new Vector2(cap.Position.X, cap.End.Y - 4), "SPACE", HorizontalAlignment.Center, cap.Size.X, 9, HudMuted);
    }
}
