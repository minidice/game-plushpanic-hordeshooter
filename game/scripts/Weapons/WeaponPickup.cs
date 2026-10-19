using Godot;

namespace HordeShooter;

/// <summary>
/// A weapon or throwable lying on the floor. E picks it up into the right slot; a replaced primary
/// (or secondary) drops back on the floor with its ammo, so you can swap back.
/// </summary>
public partial class WeaponPickup : Interactable
{
    public WeaponDefinition? Weapon { get; private set; }
    public ThrowableDefinition? Throwable { get; private set; }
    /// <summary>Ammo state of a weapon someone dropped; null = fresh from the level.</summary>
    public WeaponState? State { get; private set; }

    private Node3D _display = null!;
    private Label3D _label = null!;
    private float _time;

    public static WeaponPickup ForWeapon(Stage stage, Vector3 at, WeaponDefinition def, WeaponState? state = null)
    {
        var pickup = Create(stage, at, def.DisplayName, def.Slot == WeaponSlot.Secondary ? new Color("6fb6ff") : new Color("ffb347"));
        pickup.Weapon = def;
        pickup.State = state;
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

    private static WeaponPickup Create(Stage stage, Vector3 at, string name, Color color)
    {
        var pickup = new WeaponPickup { Name = "Pickup", Position = new Vector3(at.X, 0, at.Z), Radius = 1.5f };
        pickup.OnUse = () => stage.PickUp(pickup);
        stage.AddChild(pickup);
        stage.Interactables.Add(pickup);
        var ringMat = Mat.Fade(new Color(color, .55f));
        Mat.Mesh(pickup, new TorusMesh { InnerRadius = .42f, OuterRadius = .5f, Rings = 24, RingSegments = 4 }, Vector3.Up * .03f, ringMat);
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

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _display.Position = Vector3.Up * (.35f + Mathf.Sin(_time * 2.4f) * .06f);
        _display.RotateY((float)delta * .8f);
        if (GetParent() is Stage stage && !stage.Player.IsDead)
            _label.Visible = stage.Player.GlobalPosition.DistanceTo(GlobalPosition) < 2.6f;
    }
}
