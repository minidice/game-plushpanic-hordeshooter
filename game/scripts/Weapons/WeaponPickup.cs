using Godot;

namespace HordeShooter;

/// <summary>
/// A weapon or throwable lying on the floor. E picks it up into the right slot; a replaced primary
/// (or secondary) drops back on the floor with its ammo, so you can swap back. A shared one (<see cref="Infinite"/>,
/// decisions.md C-33) lies on a base's table and stays: the same gun only refills its ammo, another gun swaps.
/// </summary>
public partial class WeaponPickup : Interactable
{
    public WeaponDefinition? Weapon { get; private set; }
    public ThrowableDefinition? Throwable { get; private set; }
    /// <summary>Ammo state of a weapon someone dropped; null = fresh from the level.</summary>
    public WeaponState? State { get; private set; }
    /// <summary>Shared and never empty (C-33): everyone takes or refills from it as often as they like.</summary>
    public bool Infinite { get; private set; }
    private GroundMarker _marker = null!;

    private Node3D _display = null!;
    private Label3D _label = null!;
    private float _time;

    /// <param name="netId">Client: the id the host gave this dropped weapon.</param>
    public static WeaponPickup ForWeapon(Stage stage, Vector3 at, WeaponDefinition def, WeaponState? state = null, int netId = 0)
    {
        var pickup = Create(stage, at, def.DisplayName, def.Slot == WeaponSlot.Secondary ? new Color("6fb6ff") : new Color("ffb347"), netId);
        pickup.Weapon = def;
        pickup.State = state;
        stage.Net?.OnPickupSpawn(pickup);
        pickup.Prompt = Loc.Format("fmt_use_pickup_weapon", def.DisplayName, (int)def.Slot);
        var look = WeaponVisual.Build(def, null).Root;
        look.Position = new Vector3(0, 0, -def.Length * .45f);
        pickup._display.AddChild(look);
        pickup._display.RotationDegrees = new Vector3(0, 0, 90);
        return pickup;
    }

    public static WeaponPickup ForThrowable(Stage stage, Vector3 at, ThrowableDefinition def)
    {
        var pickup = Create(stage, at, def.DisplayName, new Color("8dff9c"));
        pickup.Throwable = def;
        pickup.Prompt = Loc.Format("fmt_use_pickup_throwable", def.DisplayName);
        ThrownItem.BuildLook(pickup._display, def);
        pickup._display.Scale = Vector3.One * 1.4f;
        return pickup;
    }

    private static WeaponPickup Create(Stage stage, Vector3 at, string name, Color color, int netId = 0)
    {
        // On the floor: the ground, or on a map with height tiers the floor of the tier under it (a weapon dropped on a
        // raised tier stays up there, I-112).
        float floor = stage.Tiers.Any ? stage.Tiers.GroundY(at) : 0;
        var pickup = new WeaponPickup { Name = "Pickup", Position = new Vector3(at.X, floor, at.Z), Radius = 1.5f, NetId = netId };
        pickup.OnUse = who => stage.PickUp(pickup, who ?? stage.Player);
        stage.AddChild(pickup);
        stage.AddInteractable(pickup);
        pickup._marker = GroundMarker.Circle(pickup, Vector3.Up * .035f, .5f, color, 0, .7f, .035f);
        pickup._display = new Node3D { Position = Vector3.Up * .35f };
        pickup.AddChild(pickup._display);
        pickup._label = new Label3D
        {
            Text = name,
            Font = UiTheme.Get().DefaultFont,
            FontSize = 40,
            OutlineSize = 10,
            PixelSize = .01f,
            Position = Vector3.Up * 1.1f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = color,
            NoDepthTest = true,
            Visible = false,
        };
        pickup.AddChild(pickup._label);
        return pickup;
    }

    /// <summary>
    /// Turns a level weapon into a shared one lying <paramref name="height"/> above its floor (a table top): a rounded square
    /// instead of the circle, and an infinity sign by its name (C-33). Colours stay the slot's.
    /// </summary>
    public WeaponPickup MakeInfinite(float height)
    {
        if (Weapon is not { } weapon) return this;
        Infinite = true;
        Position += Vector3.Up * height;
        var tint = weapon.Slot == WeaponSlot.Secondary ? new Color("6fb6ff") : new Color("ffb347");
        // Reached from in front of the table, not only from on top of it.
        Radius = 1.9f;
        _marker.QueueFree();
        _marker = GroundMarker.RoundedSquare(this, Vector3.Up * .035f, 1.1f, .22f, tint, .05f, .85f, .04f);
        _label.Text = weapon.DisplayName + "  ∞";
        Prompt = Loc.Format("fmt_use_pickup_weapon_infinite", weapon.DisplayName, (int)weapon.Slot);
        return this;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _display.Position = Vector3.Up * (.35f + Mathf.Sin(_time * 2.4f) * .06f);
        _display.RotateY((float)delta * .8f);
        if (GetParent() is Stage stage && !stage.Player.IsDead)
            _label.Visible = stage.Player.GlobalPosition.DistanceTo(GlobalPosition) < 2.6f;
    }
}
