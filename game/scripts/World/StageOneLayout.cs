using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Stage 1 "Warehouse District". The route runs along +X:
///   start safe room (x 0–10) → street (10–52) → warehouse (52–84) → [shutter] → yard (84–116) → safehouse (116–126).
/// The camera looks toward -Z, so walls on the -Z side are tall facades and walls on the +Z side are low
/// fences that never hide the action. Who waits where and where waves come in is data
/// (content/data/stages/stage1_encounters.json, read by <see cref="HordeDirector"/>).
/// </summary>
public sealed class StageOneLayout
{
    public const float TallWall = 3.4f;
    public const float LowWall = 1.1f;

    public Vector3 PlayerStart { get; } = new(4, 0, 0);
    public float CrescendoSeconds { get; set; } = 50f;
    public Door StartDoor { get; private set; } = null!;
    public Door Shutter { get; private set; } = null!;
    public Door SafehouseDoor { get; private set; } = null!;
    public Interactable ShutterPanel { get; private set; } = null!;
    public Interactable StartDoorSwitch { get; private set; } = null!;
    public Interactable SafehouseSwitch { get; private set; } = null!;
    public AlarmCar AlarmCar { get; private set; } = null!;

    private static readonly Rect2 SafehouseRect = new(116.4f, -3.8f, 9.4f, 7.6f);
    public bool InSafehouse(Vector3 p) => SafehouseRect.HasPoint(new Vector2(p.X, p.Z));
    public Vector3 SafehouseCenter => new(SafehouseRect.GetCenter().X, 0, SafehouseRect.GetCenter().Y);
    /// <summary>Inside, or close enough to the doorway to be shoved in when the door slams.</summary>
    public bool NearSafehouse(Vector3 p) => SafehouseRect.Grow(.6f).HasPoint(new Vector2(p.X, p.Z));

    private Stage _stage = null!;
    private readonly RandomNumberGenerator _rng = new();

    public static StageOneLayout Build(Stage stage)
    {
        var layout = new StageOneLayout { _stage = stage };
        layout._rng.Seed = 1;
        layout.BuildSafeRoom();
        layout.BuildStreet();
        layout.BuildWarehouse();
        layout.BuildYard();
        layout.BuildSafehouse();
        layout.PlaceSupplies(StageSupplies.Stage1);
        layout.ApplyFlow();
        return layout;
    }

    private readonly System.Collections.Generic.List<(Vector3 center, Vector3 size)> _floors = new();
    private readonly System.Collections.Generic.List<(Vector3 center, Vector3 size)> _solids = new();

    /// <summary>Everything starts blocked; floors are carved out, then walls and doors are stamped back in.</summary>
    public void ApplyFlow() => ApplyFlow(_stage.Flow, doors: true);

    /// <summary>Stamps walls into <paramref name="flow"/>; without doors it is the stage's route, not what is walkable now.</summary>
    public void ApplyFlow(FlowField flow, bool doors)
    {
        flow.SetBlocked(new Vector3(63, 0, 0), new Vector3(140, 0, 30), 0, true);
        foreach (var (center, size) in _floors) flow.SetBlocked(center, size, -.05f, false);
        foreach (var (center, size) in _solids) flow.SetBlocked(center, size, Stage.WallPad, true);
        if (doors) foreach (var door in new[] { StartDoor, Shutter, SafehouseDoor }) door.StampFlow();
    }

    // ---------- zones ----------

    private void BuildSafeRoom()
    {
        Floor(0, 10, -4, 4, "4a4538");
        Wall(0, 10, -4.4f, -4, TallWall, "56604f");
        Wall(0, 10, 4, 4.4f, LowWall, "56604f");
        Wall(-.4f, 0, -4.4f, 4.4f, 2.4f, "56604f");
        Wall(10, 10.4f, -6, -1.3f, 2.6f, "56604f");
        Wall(10, 10.4f, 1.3f, 6, 2.6f, "56604f");
        StartDoor = Door.Create(_stage, new Vector3(10.2f, 0, 0), new Vector3(.3f, 2.4f, 2.6f), new Color("4f6b4a"), new Vector3(0, 0, 2.5f));
        StartDoorSwitch = Use(new Vector3(9.2f, 0, 0), "ui_use_open_door", _stage.LeaveSafeRoom);

        Solid(new Vector3(3, .45f, -3.2f), new Vector3(2.4f, .9f, 1f), "6b5843");
        Lamp(new Vector3(2.9f, 2.85f, -3.72f), "ffd6a0", 3.8f, 8.5f, false);
        var terminalLight = Lamp(new Vector3(.28f, 2.38f, -1.5f), "75d6e0", 1.8f, 3.5f, false);
        terminalLight.RotationDegrees = new Vector3(0, 90, 0);
        FloorText(new Vector3(3, 0, 1.4f), "ui_floor_saferoom", "e9dfb4");
        StageArt.SafeRoomDetails(_stage);
    }

    private void BuildStreet()
    {
        Floor(10, 52, -6, 6, "2b2f33");
        for (float x = 13; x < 52; x += 4) Decal(new Vector3(x, 0, 0), new Vector3(2, .01f, .18f), "b8a660");
        Wall(10, 52, -6.4f, -6, TallWall, "4d4a52");
        Wall(10, 52, 6, 6.4f, LowWall, "4b5246");
        Windows(12, 52, -6);

        Solid(new Vector3(17, .65f, -3.8f), new Vector3(4.2f, 1.3f, 2f), "6d5a45");
        // The one with the blinking dash light: shoot it and the whole street hears (spawn-design.md 4장).
        AlarmCar = AlarmCar.Create(_stage, new Vector3(29, .65f, 3.6f), new Vector3(4.2f, 1.3f, 2f), "54657a");
        _solids.Add((AlarmCar.Position, new Vector3(4.2f, 1.3f, 2f)));
        Solid(new Vector3(42, .65f, -3.1f), new Vector3(4.2f, 1.3f, 2f), "7a4e48");
        Solid(new Vector3(22, .55f, 1.5f), new Vector3(1.2f, 1.1f, 1.2f), "8a7755");
        Solid(new Vector3(35, .6f, -.8f), new Vector3(1.4f, 1.2f, 1.4f), "8a7755");
        Solid(new Vector3(47, .5f, 2.6f), new Vector3(1.2f, 1f, 1.2f), "736749");
        Solid(new Vector3(25.5f, .7f, -5.2f), new Vector3(2.4f, 1.4f, 1.1f), "3e5a4a");

        Barrel(new Vector3(24, 0, -3.6f));
        Barrel(new Vector3(36.5f, 0, 4.8f));
        Barrel(new Vector3(46, 0, -5f));
        Boxes(new Vector3(19.5f, 0, 4.4f), 3);
        Boxes(new Vector3(33, 0, -5f), 2);

        Lamp(new Vector3(20, 2.95f, -5.72f), "ffbe7a", 3.4f, 9, false);
        Lamp(new Vector3(33, 2.95f, -5.72f), "ffbe7a", 3.4f, 9, true);
        Lamp(new Vector3(46, 2.95f, -5.72f), "ffbe7a", 3f, 8, false);
        FloorText(new Vector3(48, 0, 4.4f), "ui_floor_warehouse", "c9b27a");
    }

    private void BuildWarehouse()
    {
        Floor(52, 84.4f, -10, 10, "393c3a");
        Wall(52, 52.4f, -10, -2.5f, 3.2f, "5e5a50");
        Wall(52, 52.4f, 2.5f, 10, 3.2f, "5e5a50");
        Wall(52, 84, -10.4f, -10, 3.8f, "5e5a50");
        Wall(52, 84, 10, 10.4f, LowWall, "5e5a50");
        Wall(84, 84.4f, -10.4f, -3, 3.8f, "5e5a50");
        Wall(84, 84.4f, 3, 10.4f, 3.8f, "5e5a50");
        Shutter = Door.Create(_stage, new Vector3(84.2f, 0, 0), new Vector3(.4f, 3.2f, 6f), new Color("6f7468"), new Vector3(0, 3.1f, 0));

        Shelf(56, 66, -6.4f);
        Shelf(56, 66, 5.4f);
        Shelf(70, 78, -3.6f);
        Shelf(70, 78, 4.2f);
        Solid(new Vector3(79, .6f, -1.3f), new Vector3(1.3f, 1.2f, 1.3f), "8a7755");
        Solid(new Vector3(79.4f, .6f, 2.2f), new Vector3(1.3f, 1.2f, 1.3f), "8a7755");

        Barrel(new Vector3(60, 0, -8.6f));
        Barrel(new Vector3(74, 0, 8.4f));
        Barrel(new Vector3(81.5f, 0, 6.2f));
        Boxes(new Vector3(67.5f, 0, -1), 3);

        // The crescendo trigger.
        var panel = Solid(new Vector3(83.4f, 1.1f, -6.2f), new Vector3(.6f, 1.4f, 1.2f), "3a4148");
        Mat.Box(panel, new Vector3(-.31f, .2f, 0), new Vector3(.02f, .3f, .3f), Mat.Glow(new Color("ff3b30"), 3));
        ShutterPanel = Use(new Vector3(82.4f, 0, -6.2f), "ui_use_shutter", _stage.StartCrescendo);
        var alarm = Lamp(new Vector3(83.72f, 3.15f, -6.2f), "ff5040", 1.8f, 8, false);
        alarm.RotationDegrees = new Vector3(0, -90, 0);
        alarm.SetMeta("alarm", true);

        Lamp(new Vector3(60, 3.15f, -9.72f), "a8d8ed", 3.8f, 11.5f, false);
        Lamp(new Vector3(74, 3.15f, -9.72f), "a8d8ed", 3.8f, 11.5f, true);
    }

    private void BuildYard()
    {
        Floor(84.4f, 116, -7, 7, "38372d");
        Wall(84.4f, 116, -7.4f, -7, 3f, "4a4a3c");
        Wall(84.4f, 116, 7, 7.4f, LowWall, "4a4a3c");
        Wall(84.4f, 84.8f, -10, -7, 3f, "4a4a3c");

        Solid(new Vector3(92, 1.3f, -4.4f), new Vector3(6, 2.6f, 2.4f), "7a4a35");
        Solid(new Vector3(100, 1.3f, 3.9f), new Vector3(6, 2.6f, 2.4f), "35587a");
        Solid(new Vector3(109, 1.3f, -4.2f), new Vector3(5, 2.6f, 2.2f), "5b6b3a");
        Solid(new Vector3(96, .6f, -.4f), new Vector3(1.3f, 1.2f, 1.3f), "8a7755");
        Solid(new Vector3(104.5f, .55f, .2f), new Vector3(1.2f, 1.1f, 1.2f), "736749");

        Barrel(new Vector3(94, 0, 5.4f));
        Barrel(new Vector3(105, 0, -6f));
        Barrel(new Vector3(111, 0, 5.6f));
        Boxes(new Vector3(88, 0, 5.4f), 2);
        Lamp(new Vector3(98, 2.65f, -6.72f), "ffbe7a", 3.3f, 9, false);
        Lamp(new Vector3(112, 2.65f, -6.72f), "9dffa8", 3.1f, 9, false);
        FloorText(new Vector3(112, 0, 0), "ui_floor_safehouse_arrow", "9dffa8");
    }

    private void BuildSafehouse()
    {
        Floor(116, 126, -4, 4, "3f4a3d");
        Wall(116, 116.4f, -7.4f, -1.3f, 2.6f, "4e5f4b");
        Wall(116, 116.4f, 1.3f, 7.4f, 2.6f, "4e5f4b");
        Wall(116, 126, -4.4f, -4, TallWall, "4e5f4b");
        Wall(116, 126, 4, 4.4f, LowWall, "4e5f4b");
        Wall(126, 126.4f, -4.4f, 4.4f, 2.6f, "4e5f4b");
        SafehouseDoor = Door.Create(_stage, new Vector3(116.2f, 0, 0), new Vector3(.3f, 2.4f, 2.6f), new Color("4f6b4a"), new Vector3(0, 0, 2.5f), open: true);
        SafehouseSwitch = Use(new Vector3(117.4f, 0, 0), "ui_use_close_safehouse", () => _stage.TryCloseSafehouse());
        SafehouseSwitch.Enabled = false;
        Lamp(new Vector3(121, 2.85f, -3.72f), "b5e6c3", 3.6f, 9, false);
        FloorText(new Vector3(121, 0, 2), "ui_floor_safehouse", "c8ffd0");
    }

    /// <summary>Weapons, throwables, ammo and medkits from the stage's supply points (decisions.md L-12).</summary>
    private void PlaceSupplies(StageSupplies supplies)
    {
        foreach (var item in supplies.Points.SelectMany(p => p.Items))
            switch (item.Kind)
            {
                case SupplyItemKind.Weapon: Weapon(item.At, item.Id); break;
                case SupplyItemKind.Throwable: Throwable(item.At, item.Id); break;
                case SupplyItemKind.Ammo: AmmoPile(item.At); break;
                case SupplyItemKind.Medkit: Medkit(item.At); break;
            }
    }

    // ---------- building blocks ----------

    private void Floor(float x0, float x1, float z0, float z1, string color)
    {
        var center = new Vector3((x0 + x1) / 2, -.2f, (z0 + z1) / 2);
        var size = new Vector3(x1 - x0, .4f, z1 - z0);
        var body = new StaticBody3D { Name = "Floor", Position = center, CollisionLayer = Layers.World, CollisionMask = 0 };
        _stage.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        StageArt.Floor(body, size, x0 < 10 || (x0 >= 52 && x0 < 84.4f) || x0 >= 116);
        _floors.Add((center, size));
    }

    private StaticBody3D Wall(float x0, float x1, float z0, float z1, float height, string color, bool drawWall = true)
    {
        var wallSize = new Vector3(x1 - x0, height, z1 - z0);
        var body = Solid(new Vector3((x0 + x1) / 2, height / 2, (z0 + z1) / 2), wallSize, color, false);
        if (drawWall) StageArt.Wall(body, wallSize);
        // Low camera-side fences get an invisible tall collider so nobody gets launched out of the map.
        if (height < 2f)
        {
            var size = new Vector3(x1 - x0, 4f, z1 - z0);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, 2f - height / 2, 0) });
        }
        return body;
    }

    private StaticBody3D Solid(Vector3 center, Vector3 size, string color, bool drawCargo = true)
    {
        var body = new StaticBody3D { Name = "Solid", Position = center, CollisionLayer = Layers.World, CollisionMask = 0 };
        _stage.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (drawCargo) StageArt.Cargo(body, size, color);
        _solids.Add((center, size));
        return body;
    }

    private void Shelf(float x0, float x1, float z)
    {
        var body = Wall(x0, x1, z - .6f, z + .6f, 2.2f, "5a4a3a", false);
        var colors = new System.Collections.Generic.List<string>();
        for (float x = x0 + .8f; x < x1 - .4f; x += 1.6f)
            colors.Add(_rng.Randf() < .5f ? "ae8c62" : "82917c");
        StageArt.Shelf(body, x1 - x0, colors.ToArray());
    }

    private void Barrel(Vector3 at) => ExplosiveBarrel.Create(_stage, at);

    private void Weapon(Vector3 at, string id) => WeaponPickup.ForWeapon(_stage, at, ArmoryCatalog.Weapon(id));

    private void Throwable(Vector3 at, string id) => WeaponPickup.ForThrowable(_stage, at, ArmoryCatalog.Throwable(id));

    private void Boxes(Vector3 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var box = new RigidBody3D
            {
                Name = "Box",
                Mass = .6f,
                Position = at + new Vector3(i * .75f, .3f + (i == 2 ? .62f : 0), i == 2 ? 0 : _rng.RandfRange(-.2f, .2f)),
                CollisionLayer = Layers.Props,
                CollisionMask = Layers.World | Layers.Props | Layers.Player | Layers.Enemy,
            };
            box.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .6f } });
            StageArt.Carton(box, Vector3.Zero, Vector3.One * .6f);
            _stage.AddChild(box);
            _stage.Props.Add(box);
        }
    }

    private OmniLight3D Lamp(Vector3 at, string color, float energy, float range, bool flicker)
    {
        var light = new OmniLight3D { Position = at, LightColor = new Color(color), LightEnergy = energy, OmniRange = range, ShadowEnabled = false };
        light.SetMeta("base_energy", energy);
        // Enemies in a lamp's glow show as silhouettes (decisions.md C-23).
        light.AddToGroup(VisionSystem.LightGroup);
        _stage.AddChild(light);
        StageArt.Fixture(light, color);
        if (flicker) _stage.FlickerLights.Add(light);
        return light;
    }

    private void Windows(float x0, float x1, float z)
    {
        for (float x = x0; x < x1; x += 3.2f)
        {
            bool lit = _rng.Randf() < .25f;
            StageArt.Box(_stage, new Vector3(x, 2.3f, z + .015f), new Vector3(1.4f, 1.05f, .08f), "273c49", true);
            Mat.Box(_stage, new Vector3(x, 2.3f, z + .065f), new Vector3(1.18f, .83f, .025f),
                lit ? Mat.Glow(new Color("c9ac76"), .55f) : Mat.Solid("263d4b", .3f));
            StageArt.Box(_stage, new Vector3(x, 2.3f, z + .09f), new Vector3(.045f, .86f, .04f), "6c8188");
            StageArt.Box(_stage, new Vector3(x, 2.3f, z + .09f), new Vector3(1.21f, .045f, .04f), "6c8188");
        }
    }

    private void Decal(Vector3 at, Vector3 size, string color) => Mat.Box(_stage, at + Vector3.Up * .01f, size, Mat.Solid(color, .9f));

    private void FloorText(Vector3 at, string textKey, string color)
    {
        _stage.AddChild(new Label3D
        {
            Text = Loc.Text(textKey),
            Position = at + Vector3.Up * .03f,
            RotationDegrees = new Vector3(-90, 0, 0),
            FontSize = 72,
            PixelSize = .012f,
            Modulate = new Color(color, .8f),
            OutlineSize = 0,
        });
    }

    private Interactable Use(Vector3 at, string promptKey, System.Action action)
    {
        var item = new Interactable { Name = "Use", Position = at, Prompt = promptKey, OnUse = action };
        _stage.AddChild(item);
        _stage.Interactables.Add(item);
        return item;
    }

    private void AmmoPile(Vector3 at)
    {
        var node = new Node3D { Name = "Ammo", Position = at + Vector3.Up * .2f };
        _stage.AddChild(node);
        StageArt.Ammo(node);
        var use = Use(at, "ui_use_ammo", _stage.RefillAmmo);
        use.Radius = 1.6f;
    }

    private void Medkit(Vector3 at)
    {
        var node = new Interactable { Name = "Medkit", Position = at, Prompt = "ui_use_medkit", Radius = 1.6f };
        _stage.AddChild(node);
        _stage.Interactables.Add(node);
        StageArt.Box(node, new Vector3(0, .15f, 0), new Vector3(.6f, .3f, .45f), "d3d8cc", true);
        StageArt.Box(node, new Vector3(0, .15f, .229f), new Vector3(.5f, .025f, .025f), "557a79");
        Mat.Box(node, new Vector3(0, .31f, 0), new Vector3(.36f, .02f, .1f), Mat.Glow(new Color("ff4040"), 1.5f));
        Mat.Box(node, new Vector3(0, .31f, 0), new Vector3(.1f, .02f, .3f), Mat.Glow(new Color("ff4040"), 1.5f));
        node.OnUse = () =>
        {
            if (_stage.Player.Medkits > 0)
            {
                _stage.ShowNotice(Loc.Text("msg_pickup_medkit_full"), 2);
                return;
            }
            _stage.Player.Medkits = 1;
            _stage.Sfx.Play("pickup");
            node.Enabled = false;
            node.Visible = false;
            _stage.ShowNotice(Loc.Text("msg_pickup_medkit"), 3);
        };
    }
}
