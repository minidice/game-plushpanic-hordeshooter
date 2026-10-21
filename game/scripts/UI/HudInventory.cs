using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Outpost's backpack screen (decisions.md M-25): I opens it, the game keeps running. Equipment slots 1 · 2 · 3 · G · Q
/// on top, the 4 × 5 backpack below. Drag a cell onto another (move, merge, swap), onto a slot (equip) or out of the
/// panel (drop on the floor). Hovering shows what an item is. Every drag goes through <see cref="Stage.InventoryDrag"/>.
/// </summary>
public partial class Hud
{
    public bool InventoryOpen { get; private set; }
    private PanelContainer? _inventory;
    private Control? _dropZone;
    private readonly List<InventoryCell> _inventoryCells = new();

    private void BuildInventory()
    {
        if (Stage.ModeRules is not SurvivalRules) return;
        _dropZone = new InventoryDropZone(this) { Name = "InventoryDropZone", Visible = false };
        _dropZone.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_dropZone);

        _inventory = Place(new PanelContainer { Name = "Inventory" }, 1, .5f, new Vector2(-24, 0), true, false);
        _inventory.GrowVertical = Control.GrowDirection.Both;
        _inventory.AddThemeStyleboxOverride("panel", HudPlate(pad: 14));
        _inventory.MouseFilter = Control.MouseFilterEnum.Stop;
        _inventory.Visible = false;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _inventory.AddChild(box);
        box.AddChild(HudLabel(Loc.Text("ui_inventory_title"), 16, HudWarm));
        var equip = new HBoxContainer();
        equip.AddThemeConstantOverride("separation", 6);
        box.AddChild(equip);
        string[] keys = { "1", "2", "3", "G", "Q" };
        for (int i = 0; i < InventoryOps.Slots.Length; i++) equip.AddChild(Cell(InventoryOps.Slots[i], keys[i]));
        var grid = new GridContainer { Columns = Backpack.Columns };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(grid);
        for (int i = 0; i < Backpack.Size; i++) grid.AddChild(Cell(i, ""));
        var hint = HudLabel(Loc.Text("ui_inventory_hint"), 11, HudMuted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(hint);
    }

    private InventoryCell Cell(int place, string key)
    {
        var cell = new InventoryCell(this, place, key) { Name = "InvCell" + place };
        _inventoryCells.Add(cell);
        return cell;
    }

    public void SetInventoryOpen(bool open)
    {
        if (_inventory == null || Stage.Player.Backpack == null) open = false;
        InventoryOpen = open;
        if (_inventory != null) _inventory.Visible = open;
        if (_dropZone != null) _dropZone.Visible = open;
    }

    internal void InventoryDrag(int from, int to) => Stage.InventoryDrag(from, to);

    /// <summary>What a place shows: a backpack cell's item, or what the equipment slot holds.</summary>
    internal InvItem? InventoryItem(int place)
    {
        var player = Stage.Player;
        if (InventoryOps.IsCell(place)) return player.Backpack?[place];
        int weaponSlot = InventoryOps.WeaponSlot(place);
        if (weaponSlot != 0) return player.Loadout.InSlot(weaponSlot) is { } state ? InvItem.Weapon(state) : null;
        if (place == InventoryOps.SlotThrow && player.Loadout.CurrentThrowable is { } t) return InvItem.Throwable(t.Id, player.Loadout.Count(t));
        if (place == InventoryOps.SlotMedkit && player.Medkits > 0) return InvItem.Medkit(player.Medkits);
        return null;
    }

    /// <summary>Hover text: weapon type, caliber, damage and magazine; or a stack's name and count.</summary>
    internal static string Tooltip(InvItem item)
    {
        if (item.Kind == ItemKind.Weapon && item.State is { } state)
        {
            var def = state.Def;
            var caliber = CaliberDatabase.Get(def.Caliber)?.DisplayName ?? "—";
            return Loc.Format("fmt_tip_weapon", def.DisplayName, Loc.Text("name_weapontype_" + def.TypeId), caliber, def.Damage, def.MagSize, state.Mag);
        }
        return Loc.Format("fmt_tip_stack", item.DisplayName, item.Count, item.StackLimit);
    }
}

/// <summary>One inventory square: draws its item and takes part in drag and drop.</summary>
public partial class InventoryCell : Control
{
    private readonly Hud _hud;
    private readonly string _key;
    public int Place { get; }

    public InventoryCell(Hud hud, int place, string key)
    {
        _hud = hud;
        Place = place;
        _key = key;
        CustomMinimumSize = new Vector2(70, 70);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        var item = _hud.InventoryItem(Place);
        TooltipText = item != null ? Hud.Tooltip(item) : "";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        bool slot = InventoryOps.IsSlot(Place);
        DrawRect(new Rect2(Vector2.Zero, size), slot ? new Color(.16f, .14f, .1f, .92f) : new Color(.06f, .09f, .14f, .9f));
        DrawRect(new Rect2(Vector2.Zero, size), slot ? new Color(Hud.HudWarm, .55f) : new Color(.45f, .57f, .72f, .35f), false, 1.5f);
        var font = GetThemeDefaultFont();
        if (_key != "") DrawString(font, new Vector2(5, 15), _key, HorizontalAlignment.Left, 20, 12, Hud.HudWarm);
        if (_hud.InventoryItem(Place) is not { } item) return;
        var icon = new Rect2(8, 14, size.X - 16, size.Y - 32);
        switch (item.Kind)
        {
            case ItemKind.Weapon when item.State?.Def.LoadIcon() is { } tex:
                DrawTextureRect(tex, icon, false);
                break;
            case ItemKind.Throwable when ArmoryCatalog.Throwables.TryGetValue(item.Id, out var t) && t.IconPath != "" && ResourceLoader.Exists(t.IconPath):
                DrawTextureRect(ResourceLoader.Load<Texture2D>(t.IconPath), icon, false);
                break;
            case ItemKind.Throwable:
                // No icon yet: the dock's grenade outline in the throwable's own colour.
                var tint = ArmoryCatalog.Throwables.TryGetValue(item.Id, out var kind) ? kind.Color.Lightened(.25f) : Hud.HudMuted;
                var center = icon.GetCenter();
                DrawCircle(center + new Vector2(0, 3), 10, tint);
                DrawRect(new Rect2(center + new Vector2(-4, -11), new Vector2(8, 6)), tint);
                DrawArc(center + new Vector2(6, -10), 4, 0, Mathf.Tau, 16, tint, 2, true);
                break;
            case ItemKind.Ammo:
                var color = CaliberDatabase.Get(item.Id)?.Color ?? Colors.Gray;
                for (int i = 0; i < 3; i++) DrawRect(new Rect2(icon.Position.X + 8 + i * 13, icon.Position.Y + 6, 9, icon.Size.Y - 10), color);
                break;
            case ItemKind.Medkit:
                DrawRect(new Rect2(icon.GetCenter() - new Vector2(14, 10), new Vector2(28, 20)), new Color("d3d8cc"));
                DrawRect(new Rect2(icon.GetCenter() - new Vector2(3, 8), new Vector2(6, 16)), new Color("ff4040"));
                DrawRect(new Rect2(icon.GetCenter() - new Vector2(8, 3), new Vector2(16, 6)), new Color("ff4040"));
                break;
        }
        DrawString(font, new Vector2(0, size.Y - 4), item.DisplayName, HorizontalAlignment.Center, size.X, 10, Hud.HudMuted);
        if (item.Count > 1 || item.Kind == ItemKind.Ammo)
            DrawString(font, new Vector2(0, 15), $"×{item.Count}", HorizontalAlignment.Right, size.X - 5, 12, Hud.HudWhite);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_hud.InventoryItem(Place) is not { } item) return default;
        var preview = Hud.HudLabel(item.DisplayName, 14, Hud.HudWarm);
        SetDragPreview(preview);
        return Place;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int && data.AsInt32() != Place;

    public override void _DropData(Vector2 atPosition, Variant data) => _hud.InventoryDrag(data.AsInt32(), Place);
}

/// <summary>Everything outside the backpack panel while it is open: dropping here puts the item on the floor.</summary>
public partial class InventoryDropZone : Control
{
    private readonly Hud _hud;

    public InventoryDropZone(Hud hud)
    {
        _hud = hud;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int;

    public override void _DropData(Vector2 atPosition, Variant data) => _hud.InventoryDrag(data.AsInt32(), InventoryOps.Floor);
}
