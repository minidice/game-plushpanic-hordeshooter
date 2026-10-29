using Godot;

namespace HordeShooter;

/// <summary>
/// The hat and armor tiles at the end of the bottom row, in the same small squares as the weapons (decisions.md C-34,
/// M-42). Play shows each piece by icon (name in the tooltip); its option appears in developer mode alone (C-36 · M-41),
/// as a line over the two tiles with ● when its condition holds now.
/// </summary>
public partial class Hud
{
    /// <summary>Whether gear options are shown. One place, so a later rule (e.g. random pieces shown) changes only this.</summary>
    public static bool GearInfoVisible => GameSession.DeveloperMode;

    private Control _gearPanel = null!;
    private Control _gearInfo = null!;
    private readonly HudEquipmentSlot[] _gearSlots = new HudEquipmentSlot[2];
    private readonly Label[] _gearOptions = new Label[2];

    private Control BuildGearPanel()
    {
        var box = new HBoxContainer { Name = "GearPanel", SizeFlagsVertical = Control.SizeFlags.ShrinkEnd, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        // A little gap sets the gear apart from the weapons and items.
        box.AddChild(new Control { CustomMinimumSize = new Vector2(6, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        var info = new VBoxContainer { Name = "GearInfo", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        info.AddThemeConstantOverride("separation", 2);
        foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Armor })
        {
            string id = slot == EquipmentSlot.Head ? "Head" : "Armor";
            var tile = new HudEquipmentSlot(id + "Slot", Loc.Text(slot == EquipmentSlot.Head ? "hud_gear_head" : "hud_gear_armor"));
            box.AddChild(tile);
            _gearSlots[(int)slot] = tile;
            var line = HudLabel("", 10, HudWarm);
            line.Name = id + "GearOption";
            line.Visible = false;
            info.AddChild(line);
            _gearOptions[(int)slot] = line;
        }
        _root.AddChild(info);
        _gearInfo = info;
        _gearPanel = box;
        return box;
    }

    private void UpdateGear(Stage stage, Player player)
    {
        _gearPanel.Visible = stage.GearEnabled;
        bool anyInfo = false;
        foreach (var slot in new[] { EquipmentSlot.Head, EquipmentSlot.Armor })
        {
            var worn = stage.GearEnabled ? player.Gear.In(slot) : null;
            _gearSlots[(int)slot].UpdateSlot(worn != null ? Loc.Text(worn.Gear.NameKey) : Loc.Text("hud_gear_empty"), "", worn?.Gear.IconPath ?? "",
                slot == EquipmentSlot.Head ? "head" : "armor", false, worn != null);
            var line = _gearOptions[(int)slot];
            line.Visible = GearInfoVisible && worn != null;
            line.Text = line.Visible
                ? Loc.Text(slot == EquipmentSlot.Head ? "hud_gear_head" : "hud_gear_armor") + "  " + EquipmentText.Line(worn!.Option, EquipmentText.Active(worn.Option, player))
                : "";
            anyInfo |= line.Visible;
        }
        _gearInfo.Visible = anyInfo;
        if (!anyInfo) return;
        // Over the two gear tiles, right edges together, like the active weapon's pills.
        _gearInfo.ResetSize();
        var size = _gearInfo.GetCombinedMinimumSize();
        var right = _gearSlots[1].TileRect.End.X;
        var top = _gearSlots[0].TileRect.Position.Y;
        _gearInfo.Position = new Vector2(right - size.X, top - size.Y - 6) - _root.GetGlobalRect().Position;
    }
}
