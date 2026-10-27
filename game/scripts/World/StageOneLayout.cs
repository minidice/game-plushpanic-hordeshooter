using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Stage 1 "Warehouse District". The route runs along +X:
///   start safe room (x 0–10) → street (10–52) → warehouse (52–84) → [shutter] → yard (84–116) → safehouse (116–126).
/// The camera looks toward -Z, so walls on the -Z side are tall facades and walls on the +Z side are low
/// fences that never hide the action. Who waits where and where waves come in is data
/// (content/data/stages/stage1_encounters.json, read by <see cref="HordeDirector"/>).
/// Outpost and Onslaught reuse it from the warehouse hall (doc/menu-multiplayer-plan.md 3.1).
/// </summary>
public sealed partial class StageOneLayout : StageMap
{
    public const float TallWall = 3.4f;
    public const float LowWall = 1.1f;

    /// <summary>Outpost and Onslaught start in the middle of the warehouse, between its two doorways.</summary>
    public static readonly Vector3 ModeStart = new(66, 0, 1.6f);
    private Door _startDoor = null!, _shutter = null!, _safehouseDoor = null!;
    private Interactable _shutterPanel = null!, _startDoorSwitch = null!, _safehouseSwitch = null!, _exit = null!;
    public override Door StartDoor => _startDoor;
    public override Door Shutter => _shutter;
    public override Door SafehouseDoor => _safehouseDoor;
    public override Interactable ShutterPanel => _shutterPanel;
    public override Interactable StartDoorSwitch => _startDoorSwitch;
    public override Interactable SafehouseSwitch => _safehouseSwitch;
    /// <summary>The floor hatch in the safehouse: down the ladder to Stage 2 (decisions.md L-14).</summary>
    public override Interactable StageExit => _exit;
    public override int StageNumber => Mode == GameMode.Scenario ? 1 : 0;
    public override int NextStage => Mode == GameMode.Scenario ? 2 : 0;
    public AlarmCar AlarmCar { get; private set; } = null!;
    /// <summary>Where the exit hatch sits in the safehouse.</summary>
    public static readonly Vector3 ExitHatch = new(124.2f, 0, 1.4f);

    private static readonly Rect2 SafehouseRect = new(116.4f, -3.8f, 9.4f, 7.6f);
    public override bool InSafehouse(Vector3 p) => SafehouseRect.HasPoint(new Vector2(p.X, p.Z));
    public Vector3 SafehouseCenter => new(SafehouseRect.GetCenter().X, 0, SafehouseRect.GetCenter().Y);
    public override bool NearSafehouse(Vector3 p) => SafehouseRect.Grow(.6f).HasPoint(new Vector2(p.X, p.Z));

    public override Rect2 Area => new(-2, -12, 130, 24);
    public override string TitleKey => "ui_hud_stage";
    public override bool HasRoute => Mode == GameMode.Scenario;
    public override Vector3 RouteGoal => SafehouseCenter;
    public override EncounterSet? Encounters => SpawnDatabase.Current.Stage1;
    public override IReadOnlyList<Door> Doors => new[] { StartDoor, Shutter, SafehouseDoor };
    public override AlarmCar? AlarmDevice => AlarmCar;
    /// <summary>The generator stands in the open lane between the hall's shelves.</summary>
    public override Vector3 CoreSpot => new(68, 0, 1.6f);
    /// <summary>Far enough from the doorways that a group is off screen while the player guards the core.</summary>
    public override IReadOnlyList<Vector3[]> Gates { get; } = new[] { Spots(40, 49, -4.5f, 4.5f), Spots(89, 99, -5.5f, 5.5f) };

    protected override void BuildMap()
    {
        PlayerStart = new Vector3(4, 0, 0);
        BuildSafeRoom();
        BuildStreet();
        BuildWarehouse();
        BuildYard();
        BuildSafehouse();
        DressArchitecture();
        if (Mode == GameMode.Scenario) PlaceSupplies(StageSupplies.Stage1);
        else PrepareModeArena();
    }

    /// <summary>Indoor rooms (safe room, warehouse, safehouse) get tiles; the street and the yard do not.</summary>
    protected override bool IndoorFloor(float x0) => x0 < 10 || (x0 >= 52 && x0 < 84.4f) || x0 >= 116;

    /// <summary>
    /// Survival / Survivor: start in the warehouse with both doorways open (street side and the raised shutter),
    /// so the horde comes from two sides. Survivor gets an open hall (no shelves); Survival keeps the shelves as cover
    /// around the generator. Each gets its own small supply set instead of the scenario's route supplies.
    /// </summary>
    private void PrepareModeArena()
    {
        PlayerStart = ModeStart;
        Shutter.SetOpen(true, 0);
        ShutterPanel.Enabled = false;
        StartDoorSwitch.Enabled = false;
        SafehouseSwitch.Enabled = false;
        StageExit.Enabled = false;
        if (Mode == GameMode.Survival)
        {
            Weapon(new Vector3(66.5f, 0, -1.2f), "m4");
            Weapon(new Vector3(69.5f, 0, -1.2f), "r870");
            AmmoPile(new Vector3(64.2f, 0, 3.6f));
            AmmoPile(new Vector3(71.8f, 0, .2f));
            Medkit(new Vector3(68f, 0, 3.8f));
            Throwable(new Vector3(65.4f, 0, 0), "frag");
            Throwable(new Vector3(70.6f, 0, 2.6f), "molotov");
        }
        else
        {
            Weapon(new Vector3(67.6f, 0, -.6f), "m249");
            Weapon(new Vector3(64.4f, 0, -.6f), "benelli_m4");
            AmmoPile(new Vector3(56f, 0, -7.6f));
            AmmoPile(new Vector3(80.5f, 0, 7.4f));
            AmmoPile(new Vector3(68f, 0, 8f));
            Medkit(new Vector3(57f, 0, 7.6f));
            Medkit(new Vector3(80f, 0, -7.8f));
            Throwable(new Vector3(66f, 0, 3.6f), "frag");
            Throwable(new Vector3(70f, 0, -3.8f), "frag");
            Throwable(new Vector3(62f, 0, -4f), "molotov");
        }
    }

    // ---------- zones ----------

    private void BuildSafeRoom()
    {
        Floor(0, 10, -4, 4, "4a4538");
        Wall(0, 10, -4.4f, -4, TallWall, "56604f");
        Wall(0, 10, 4, 4.4f, LowWall, "56604f");
        Wall(-.4f, 0, -4.4f, 4.4f, TallWall, "56604f");
        Wall(10, 10.4f, -6, -1.3f, TallWall, "56604f");
        Wall(10, 10.4f, 1.3f, 6, TallWall, "56604f");
        _startDoor = Door.Create(_stage, new Vector3(10.2f, 0, 0), new Vector3(.3f, 2.4f, 2.6f), new Color("4f6b4a"), new Vector3(0, 0, 2.5f));
        _startDoorSwitch = Use(new Vector3(9.2f, 0, 0), "ui_use_open_door", _stage.LeaveSafeRoom);

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
        BlockFlow(AlarmCar.Position, new Vector3(4.2f, 1.3f, 2f));
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
        Wall(52, 52.4f, -10, -2.5f, TallWall, "5e5a50");
        Wall(52, 52.4f, 2.5f, 10, TallWall, "5e5a50");
        Wall(52, 84, -10.4f, -10, TallWall, "5e5a50");
        Wall(52, 84, 10, 10.4f, LowWall, "5e5a50");
        Wall(84, 84.4f, -10.4f, -3, TallWall, "5e5a50");
        Wall(84, 84.4f, 3, 10.4f, TallWall, "5e5a50");
        _shutter = Door.Create(_stage, new Vector3(84.2f, 0, 0), new Vector3(.4f, 3.2f, 6f), new Color("6f7468"), new Vector3(0, 3.1f, 0));
        _shutter.OpenSeconds = 1.2f;

        // Survivor wants an open hall to kite the swarm in; the other modes keep the shelves.
        if (Mode != GameMode.Survivor)
        {
            Shelf(56, 66, -6.4f);
            Shelf(56, 66, 5.4f);
            Shelf(70, 78, -3.6f);
            Shelf(70, 78, 4.2f);
            Solid(new Vector3(79, .6f, -1.3f), new Vector3(1.3f, 1.2f, 1.3f), "8a7755");
            Solid(new Vector3(79.4f, .6f, 2.2f), new Vector3(1.3f, 1.2f, 1.3f), "8a7755");
        }

        Barrel(new Vector3(60, 0, -8.6f));
        Barrel(new Vector3(74, 0, 8.4f));
        Barrel(new Vector3(81.5f, 0, 6.2f));
        // The scenario's carton stack stands where Survival's generator goes.
        if (Mode == GameMode.Scenario) Boxes(new Vector3(67.5f, 0, -1), 3);
        if (Mode == GameMode.Survivor)
        {
            Barrel(new Vector3(62, 0, 5.5f));
            Barrel(new Vector3(75, 0, -5f));
        }

        // The crescendo trigger.
        var panel = Solid(new Vector3(83.4f, 1.1f, -6.2f), new Vector3(.6f, 1.4f, 1.2f), "3a4148", surfaceObject: "wall_button_panel");
        Mat.Box(panel, new Vector3(-.31f, .2f, 0), new Vector3(.02f, .3f, .3f), Mat.Glow(new Color("ff3b30"), 3));
        _shutterPanel = Use(new Vector3(82.4f, 0, -6.2f), "ui_use_shutter", _stage.StartCrescendo);
        var alarm = Lamp(new Vector3(83.72f, 3.15f, -6.2f), "ff5040", 1.8f, 8, false);
        alarm.RotationDegrees = new Vector3(0, -90, 0);
        alarm.SetMeta("alarm", true);

        Lamp(new Vector3(60, 3.15f, -9.72f), "a8d8ed", 3.8f, 11.5f, false);
        Lamp(new Vector3(74, 3.15f, -9.72f), "a8d8ed", 3.8f, 11.5f, true);
    }

    private void BuildYard()
    {
        Floor(84.4f, 116, -7, 7, "38372d");
        Wall(84.4f, 116, -7.4f, -7, TallWall, "4a4a3c");
        Wall(84.4f, 116, 7, 7.4f, LowWall, "4a4a3c");
        Wall(84.4f, 84.8f, -10, -7, TallWall, "4a4a3c");

        Solid(new Vector3(92, 1.3f, -4.4f), new Vector3(6, 2.6f, 2.4f), "7a4a35", surfaceObject: "container_closed");
        Solid(new Vector3(100, 1.3f, 3.9f), new Vector3(6, 2.6f, 2.4f), "35587a", surfaceObject: "container_closed");
        Solid(new Vector3(109, 1.3f, -4.2f), new Vector3(5, 2.6f, 2.2f), "5b6b3a", surfaceObject: "container_closed");
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
        Wall(116, 116.4f, -7.4f, -1.3f, TallWall, "4e5f4b");
        Wall(116, 116.4f, 1.3f, 7.4f, TallWall, "4e5f4b");
        Wall(116, 126, -4.4f, -4, TallWall, "4e5f4b");
        Wall(116, 126, 4, 4.4f, LowWall, "4e5f4b");
        Wall(126, 126.4f, -4.4f, 4.4f, TallWall, "4e5f4b");
        _safehouseDoor = Door.Create(_stage, new Vector3(116.2f, 0, 0), new Vector3(.3f, 2.4f, 2.6f), new Color("4f6b4a"), new Vector3(0, 0, 2.5f), open: true);
        _safehouseSwitch = Use(new Vector3(117.4f, 0, 0), "ui_use_close_safehouse", () => _stage.TryCloseSafehouse());
        _safehouseSwitch.Enabled = false;
        // Shut the safehouse and the hatch opens: hold to climb down to Stage 2 (reference clip: hold, then the iris).
        Prop("floor_hatch", ExitHatch, solid: false);
        _exit = Use(ExitHatch, "ui_use_next_stage", () => _stage.TakeStageExit());
        _exit.HoldSeconds = 1.2f;
        _exit.Radius = 1.6f;
        _exit.Enabled = false;
        Lamp(new Vector3(121, 2.85f, -3.72f), "b5e6c3", 3.6f, 9, false);
        FloorText(new Vector3(121, 0, 2), "ui_floor_safehouse", "c8ffd0");
    }

    // ---------- Stage 1 pieces ----------

    private void Shelf(float x0, float x1, float z)
    {
        var body = Wall(x0, x1, z - .6f, z + .6f, 2.2f, "5a4a3a", false);
        var colors = new List<string>();
        for (float x = x0 + .8f; x < x1 - .4f; x += 1.6f)
            colors.Add(_rng.Randf() < .5f ? "ae8c62" : "82917c");
        SurfaceAudio.Tag(body,"runtime:shelf");
        StageArt.Shelf(body, x1 - x0, colors.ToArray());
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

    private static Vector3[] Spots(float x0, float x1, float z0, float z1)
    {
        var spots = new List<Vector3>();
        for (float x = x0; x <= x1; x += 1.5f)
            for (float z = z0; z <= z1; z += 1.5f)
                spots.Add(new Vector3(x, 0, z));
        return spots.ToArray();
    }
}
