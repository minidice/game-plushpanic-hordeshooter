using Godot;

namespace HordeShooter;

/// <summary>
/// A hat or an armor lying on the floor (decisions.md C-34 · C-36): one piece for the whole team, the first to press E
/// wears it and drops what they wore. Its option was chosen when it appeared and never changes. The prompt names the
/// piece only; developer mode (M-41) adds the option next to the worn one.
/// </summary>
public partial class EquipmentPickup : Interactable
{
    public static readonly Color HeadColor = new("8fd3ff"), ArmorColor = new("c7a6ff");

    public GearItem Item { get; private set; } = null!;
    private Node3D _display = null!;
    private Label3D _label = null!;
    private float _time;
    private bool _developer;

    /// <param name="netId">Client: the id the host gave this piece.</param>
    public static EquipmentPickup Spawn(Stage stage, Vector3 at, GearItem item, int netId = 0)
    {
        // The floor of the tier under it, like a dropped weapon (I-112).
        float floor = stage.Tiers.Any ? stage.Tiers.GroundY(at) : 0;
        var pickup = new EquipmentPickup { Name = "GearPickup", Position = new Vector3(at.X, floor, at.Z), Radius = 1.5f, NetId = netId, Item = item };
        pickup.OnUse = who => stage.TryEquipPickup(pickup, who ?? stage.Player);
        stage.AddChild(pickup);
        stage.AddInteractable(pickup);
        var color = item.Gear.Slot == EquipmentSlot.Head ? HeadColor : ArmorColor;
        GroundMarker.Circle(pickup, Vector3.Up * .035f, .5f, color, 0, .7f, .035f);
        pickup._display = new Node3D { Position = Vector3.Up * .55f };
        pickup.AddChild(pickup._display);
        if (item.Gear.IconPath != "" && ResourceLoader.Exists(item.Gear.IconPath))
            pickup._display.AddChild(new Sprite3D
            {
                Texture = GD.Load<Texture2D>(item.Gear.IconPath),
                PixelSize = .0028f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false,
            });
        pickup._label = new Label3D
        {
            Text = Loc.Text(item.Gear.NameKey),
            Font = UiTheme.Get().DefaultFont,
            FontSize = 40,
            OutlineSize = 10,
            PixelSize = .01f,
            Position = Vector3.Up * 1.25f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = color,
            NoDepthTest = true,
            Visible = false,
        };
        pickup.AddChild(pickup._label);
        pickup.UpdatePrompt(stage.Player);
        return pickup;
    }

    private void UpdatePrompt(Player viewer)
    {
        _developer = GameSession.DeveloperMode;
        var slot = Loc.Text(Item.Gear.Slot == EquipmentSlot.Head ? "hud_gear_head" : "hud_gear_armor");
        var prompt = Loc.Format("fmt_use_pickup_gear", Loc.Text(Item.Gear.NameKey), slot);
        if (_developer)
        {
            var worn = viewer.Gear.In(Item.Gear.Slot);
            prompt += "\n" + Loc.Format("fmt_gear_compare", EquipmentText.Describe(Item.Option),
                worn != null ? EquipmentText.Describe(worn.Option) : Loc.Text("hud_gear_empty"));
        }
        Prompt = prompt;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _display.Position = Vector3.Up * (.55f + Mathf.Sin(_time * 2.4f) * .06f);
        if (GetParent() is not Stage stage || stage.Player.IsDead) return;
        _label.Visible = stage.Player.GlobalPosition.DistanceTo(GlobalPosition) < 2.6f;
        // The compared piece is whatever this PC's player wears right now.
        if (_developer || GameSession.DeveloperMode) UpdatePrompt(stage.Player);
    }
}
