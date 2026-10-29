using Godot;

namespace HordeShooter;

/// <summary>
/// The hat and armor rows next to the health bar (decisions.md C-34). Play shows each piece by icon and name only; the
/// option line under it appears in developer mode alone (C-36 · M-41), with ● when its condition holds now.
/// </summary>
public partial class Hud
{
    /// <summary>Whether gear options are shown. One place, so a later rule (e.g. random pieces shown) changes only this.</summary>
    public static bool GearInfoVisible => GameSession.DeveloperMode;

    private Control _gearPanel = null!;
    private readonly GearRow[] _gearRows = new GearRow[2];

    private sealed class GearRow
    {
        public Label Name = null!, Option = null!;
        public Texture2D? Icon;
        public string IconPath = "";
        public bool Worn;
    }

    private Control BuildGearPanel()
    {
        var box = new VBoxContainer { Name = "GearPanel", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 6);
        foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Armor })
        {
            string id = slot == EquipmentSlot.Head ? "Head" : "Armor";
            var row = new GearRow();
            var plate = new PanelContainer { Name = id + "Gear", CustomMinimumSize = new Vector2(176, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
            plate.AddThemeStyleboxOverride("panel", HudPlate(pad: 6, radius: 10));
            var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            line.AddThemeConstantOverride("separation", 6);
            line.AddChild(new Paint { CustomMinimumSize = new Vector2(34, 34), Painter = c => DrawGearIcon(c, row, slot) });
            var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
            words.AddThemeConstantOverride("separation", 0);
            words.AddChild(HudLabel(Loc.Text(slot == EquipmentSlot.Head ? "hud_gear_head" : "hud_gear_armor"), 10, UiTheme.Selection));
            row.Name = HudLabel("", 12, HudMuted);
            row.Name.Name = id + "GearName";
            row.Name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            row.Name.CustomMinimumSize = new Vector2(124, 0);
            words.AddChild(row.Name);
            row.Option = HudLabel("", 10, HudWarm);
            row.Option.Name = id + "GearOption";
            // Developer mode only: the row widens to show the whole option rather than cut it.
            row.Option.Visible = false;
            words.AddChild(row.Option);
            line.AddChild(words);
            plate.AddChild(line);
            box.AddChild(plate);
            _gearRows[(int)slot] = row;
        }
        _gearPanel = box;
        return box;
    }

    private void UpdateGear(Stage stage, Player player)
    {
        _gearPanel.Visible = stage.GearEnabled;
        if (!stage.GearEnabled) return;
        foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Armor })
        {
            var row = _gearRows[(int)slot];
            var worn = player.Gear.In(slot);
            row.Worn = worn != null;
            row.Name.Text = worn != null ? Loc.Text(worn.Gear.NameKey) : Loc.Text("hud_gear_empty");
            row.Name.AddThemeColorOverride("font_color", worn != null ? HudWhite : HudMuted);
            row.Option.Visible = GearInfoVisible && worn != null;
            row.Option.Text = row.Option.Visible ? EquipmentText.Line(worn!.Option, EquipmentText.Active(worn.Option, player)) : "";
            string path = worn?.Gear.IconPath ?? "";
            if (path != row.IconPath)
            {
                row.IconPath = path;
                row.Icon = path != "" && ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
            }
        }
    }

    private static void DrawGearIcon(Control c, GearRow row, EquipmentSlot slot)
    {
        var center = c.Size / 2;
        if (row.Icon != null)
        {
            var size = row.Icon.GetSize();
            float scale = Mathf.Min(c.Size.X / size.X, c.Size.Y / size.Y);
            c.DrawTextureRect(row.Icon, new Rect2(center - size * scale / 2, size * scale), false, Colors.White);
            return;
        }
        // Empty slot (or no icon yet): a faint outline of a cap or a vest.
        var color = new Color(.55f, .62f, .72f, row.Worn ? .9f : .35f);
        if (slot == EquipmentSlot.Head)
        {
            c.DrawArc(center + new Vector2(0, 4), 11, Mathf.Pi, Mathf.Tau, 16, color, 3, true);
            c.DrawLine(center + new Vector2(-13, 4), center + new Vector2(15, 4), color, 3, true);
        }
        else
        {
            c.DrawPolyline(new[] { center + new Vector2(-6, -13), center + new Vector2(-12, -9), center + new Vector2(-12, 13),
                center + new Vector2(12, 13), center + new Vector2(12, -9), center + new Vector2(6, -13), center + new Vector2(0, -5),
                center + new Vector2(-6, -13) }, color, 2.5f, true);
        }
    }
}
