using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Scenario Stage 2 "Forest Bunker Road" (decisions.md L-14), the outdoor stage after Stage 1's safehouse hatch. Built
/// from the outdoor kit (game/assets/outdoor-kit, prototypes/outdoor-kit/doc/integration-guide.md) and the transition
/// props, after the user's reference clip: the survivors climb out of a sandbagged bunker hatch into a fenced clearing
/// with rocks, pines, a leaning container, barrels and barriers, under a lower camera with depth and haze.
/// The route runs along +X like Stage 1 (L-1):
///   bunker clearing (x −2–20) → forest road (20–60.3) → stream bridge (60.3–67.7) → container yard (67.7–100) → safehouse (100–112).
/// Walls are invisible boxes along the zone edges; the kit models dress them. The far side (−Z) is rock and forest, the
/// camera side (+Z) a low edge of shrubs and wire fence, with trees kept back so they never cover the road.
/// </summary>
public sealed class StageTwoMap : StageMap
{
    public const float StreamX0 = 60.3f, StreamX1 = 67.7f;
    /// <summary>The bunker hatch the survivors climb out of; nothing spawns near it (L-11).</summary>
    public static readonly Vector3 ArrivalHatch = new(6, 0, -1.6f);

    private Door _safehouseDoor = null!;
    private Interactable _safehouseSwitch = null!;
    private static readonly Rect2 SafehouseRect = new(100.4f, -3.8f, 11.4f, 7.6f);

    public override Rect2 Area => new(-4, -12, 118, 24);
    public override string TitleKey => "ui_hud_stage2";
    public override bool HasRoute => true;
    public override Vector3 RouteGoal => new(SafehouseRect.GetCenter().X, 0, SafehouseRect.GetCenter().Y);
    public override EncounterSet? Encounters => SpawnDatabase.Current.Stage2;
    public override IReadOnlyList<Door> Doors => new[] { _safehouseDoor };
    public override int StageNumber => 2;
    /// <summary>Climbing out of the hatch puts the survivors on the route at once.</summary>
    public override Objective ScenarioStart => Objective.ReachSafehouse;
    public override Door SafehouseDoor => _safehouseDoor;
    public override Interactable SafehouseSwitch => _safehouseSwitch;
    public override bool InSafehouse(Vector3 p) => SafehouseRect.HasPoint(new Vector2(p.X, p.Z));
    public override bool NearSafehouse(Vector3 p) => SafehouseRect.Grow(.6f).HasPoint(new Vector2(p.X, p.Z));
    public override CameraView View => CameraView.Outdoor;

    /// <summary>Rainy dusk: grey-blue sky, warm low sun, and depth haze that only reaches the far ground.</summary>
    public override void Light(Godot.Environment env, DirectionalLight3D key)
    {
        env.BackgroundColor = new Color("5d6b78");
        env.AmbientLightColor = new Color("b9c6d2");
        env.AmbientLightEnergy = .42f;
        env.TonemapExposure = 1.05f;
        env.FogEnabled = true;
        env.FogMode = Godot.Environment.FogModeEnum.Depth;
        env.FogLightColor = new Color("7d8b97");
        env.FogDensity = .85f;
        env.FogDepthCurve = 1.4f;
        env.FogSkyAffect = 1;
        key.Name = "Sun";
        key.RotationDegrees = new Vector3(-36, -48, 0);
        key.LightColor = new Color("e5e8d5");
        key.LightEnergy = 1.2f;
    }

    protected override bool IndoorFloor(float x0) => x0 >= 100;

    protected override void BuildMap()
    {
        PlayerStart = ArrivalHatch + new Vector3(.6f, 0, 3.1f);
        BuildClearing();
        BuildForest();
        BuildStream();
        BuildYard();
        BuildSafehouse();
        LayGround();
        Backdrop();
        if (Mode == GameMode.Scenario) PlaceSupplies(StageSupplies.Stage2);
    }

    // ---------- zones ----------

    private void BuildClearing()
    {
        Floor(-2, 20, -9, 9, "", draw: false);
        Wall(-2.4f, -2, -9.4f, 9.4f, 3f, "", drawWall: false);
        Wall(-2.4f, 20, -9.4f, -9, 3.4f, "", drawWall: false);
        Wall(-2.4f, 20, 9, 9.4f, 1f, "", drawWall: false);
        // The step from the clearing's width to the road's.
        Wall(20, 20.4f, -9.4f, -7, 3.4f, "", drawWall: false);
        Wall(20, 20.4f, 7, 9.4f, 1f, "", drawWall: false);

        // The arrival: the hatch, a sign and barriers around it (reference clip 14–21 s).
        Prop("bunker_hatch", ArrivalHatch);
        Prop("warning_sign", new Vector3(2.6f, 0, -4.2f), 12, 1.35f);
        Prop("concrete_barrier", new Vector3(1.8f, 0, -6.6f), 8);
        Prop("concrete_barrier", new Vector3(9.6f, 0, -6.2f), -14);
        Prop("concrete_barrier", new Vector3(12.8f, 0, 4.4f), 78);
        Prop("sandbags", new Vector3(10.2f, 0, -2.9f), 64);
        Prop("container_closed", new Vector3(16.2f, 0, -6.4f), 6);
        Prop("container_closed", new Vector3(18.4f, 0, 5.6f), 74);
        Prop("wooden_crate", new Vector3(13.8f, 0, -3.9f), 18);
        Prop("pallet", new Vector3(12.4f, 0, -7.9f), 4, 1, solid: false);
        foreach (var at in new[] { new Vector3(4.6f, 0, -7.9f), new Vector3(5.4f, 0, -8.2f), new Vector3(5.1f, 0, -7.2f) })
            Prop("barrel", at, at.X * 37);
        Barrel(new Vector3(11.4f, 0, -7.2f));
        // Camera-side wire fence: see-through, it frames the clearing like the clip's foreground fence.
        for (float x = 0; x < 19; x += 3.1f) Prop("wire_fence", new Vector3(x + 1.5f, 0, 9.5f), 0, 1, solid: false);
        Prop("streetlamp", new Vector3(10.2f, 0, -8.4f));
        Lamp(new Vector3(10.2f, 3.7f, -7.7f), "ffcf8a", 3.4f, 12, false);
        Lamp(new Vector3(6.4f, 4.4f, 1.4f), "ffe2b0", 2.4f, 10, false);
    }

    private void BuildForest()
    {
        Floor(20, StreamX0, -7, 7, "", draw: false);
        Wall(20, StreamX0, -7.4f, -7, 3.4f, "", drawWall: false);
        Wall(20, StreamX0, 7, 7.4f, 1f, "", drawWall: false);

        // Cover along the road; the trees with crowns stand on the far side only (a crown over the road hides enemies, I-80).
        Prop("boulder", new Vector3(27.5f, 0, -3.6f), 28);
        Prop("rock_cluster", new Vector3(34.5f, 0, 3.9f), -20);
        Prop("fallen_log", new Vector3(40.5f, 0, -1.9f), 22);
        Prop("stump", new Vector3(45.5f, 0, 4.5f));
        Prop("boulder", new Vector3(52, 0, -4.4f), -40);
        Prop("broadleaf", new Vector3(31, 0, -5.8f), 40);
        Prop("round_oak", new Vector3(48.5f, 0, -5.9f), 10);
        Prop("pine_tall", new Vector3(23.5f, 0, -6.1f), 70);
        Prop("pine_small", new Vector3(57.4f, 0, -6f), 15);
        Barrel(new Vector3(38, 0, 5.8f));
        Barrel(new Vector3(54.5f, 0, -5.8f));
        Prop("streetlamp", new Vector3(36, 0, -6.6f));
        Lamp(new Vector3(36, 3.7f, -5.9f), "ffcf8a", 3.2f, 12, false);
        Prop("streetlamp", new Vector3(50.5f, 0, -6.6f));
        Lamp(new Vector3(50.5f, 3.7f, -5.9f), "ffcf8a", 3f, 11, true);

        // Rock wall on the far side and a low edge of shrubs and logs on the camera side.
        OutdoorEdgeArt.Bank(_stage,20,StreamX0,-7.6f,-1,seed:41);
        OutdoorEdgeArt.Grove(_stage,20,StreamX0,-7.6f,43);
        OutdoorEdgeArt.Bank(_stage,20,StreamX0,7.7f,1,low:true,seed:42);
        for (float x = 22; x < StreamX0 - 1; x += 4.6f)
        {
            Prop(((int)x % 3) switch { 0 => "shrub", 1 => "fallen_log", _ => "stump" }, new Vector3(x, 0, 8.2f), x * 23, 1, solid: false);
            Prop("grass_patch", new Vector3(x + 2.1f, 0, 7.9f), x * 41, 1, solid: false);
        }
    }

    private void BuildStream()
    {
        // The stream runs across the route; its bank tops sit at ground level, so the water lies 0.38 m down.
        // It runs past both edges of the view, so its ends are never seen.
        for (float z = -24; z <= 24; z += 8) Prop("stream_straight", new Vector3((StreamX0 + StreamX1) / 2, -.62f, z), 0, 1, solid: false);
        // One crossing: the bridge, scaled so its deck takes a small group abreast (L-9 bottleneck 3–4 m).
        Prop("timber_bridge", new Vector3((StreamX0 + StreamX1) / 2, -.375f * 1.5f, 0), 90, 1.5f, solid: false);
        Floor(StreamX0, StreamX1, -1.5f, 1.5f, "", draw: false);
        Wall(StreamX0, StreamX1, -1.9f, -1.5f, 1f, "", drawWall: false);
        Wall(StreamX0, StreamX1, 1.5f, 1.9f, 1f, "", drawWall: false);
        // The banks: nobody walks or falls into the water.
        Wall(StreamX0 - .4f, StreamX0, -7, -1.5f, 1f, "", drawWall: false);
        Wall(StreamX0 - .4f, StreamX0, 1.5f, 7, 1f, "", drawWall: false);
        Wall(StreamX1, StreamX1 + .4f, -10, -1.5f, 1f, "", drawWall: false);
        Wall(StreamX1, StreamX1 + .4f, 1.5f, 10, 1f, "", drawWall: false);
        foreach (var (x, z) in new[] { (61.2f, -4.6f), (66.6f, 4.2f), (61f, 5.8f), (66.9f, -6.2f) }) Prop("reeds", new Vector3(x, 0, z), x * 13, 1, solid: false);
        Prop("boulder", new Vector3(60.6f, 0, -9.2f), 60, .8f, solid: false);
        OutdoorEdgeArt.Bank(_stage,55,StreamX0-.4f,-9,-1,seed:56);
        OutdoorEdgeArt.Bank(_stage,StreamX1+.4f,74,10,1,low:true,seed:57);
    }

    private void BuildYard()
    {
        Floor(StreamX1, 100, -10, 10, "", draw: false);
        Wall(StreamX1, 100, -10.4f, -10, 3f, "", drawWall: false);
        Wall(StreamX1, 100, 10, 10.4f, 1f, "", drawWall: false);

        // A checkpoint: barriers make a chicane, containers and a shed line the edges.
        Prop("concrete_barrier", new Vector3(75.5f, 0, -3.6f), 0);
        Prop("concrete_barrier", new Vector3(77.6f, 0, -3.6f), 0);
        Prop("concrete_barrier", new Vector3(80.5f, 0, 3.2f), 0);
        Prop("concrete_barrier", new Vector3(82.6f, 0, 3.2f), 0);
        Prop("warning_sign", new Vector3(73.4f, 0, -5.4f), -8, 1.35f);
        Prop("sandbags", new Vector3(87.5f, 0, -1.8f), 90);
        Prop("container_closed", new Vector3(74.2f, 0, -7.6f), 0);
        Prop("container_closed", new Vector3(86.5f, 0, 7.4f), 0);
        Prop("container_closed", new Vector3(93.6f, 0, -6.8f), 90);
        Prop("container_open", new Vector3(96.4f, 0, 7.6f), 180, 1, solid: false);
        Solid(new Vector3(96.4f, 1.35f, 7.6f), new Vector3(6, 2.7f, 2.4f), "", drawCargo: false);
        Prop("utility_shed", new Vector3(83.6f, 0, -7.9f), 0, 1, solid: false);
        Solid(new Vector3(83.6f, 1.4f, -7.9f), new Vector3(3.5f, 2.8f, 3.1f), "", drawCargo: false);
        Prop("wooden_crate", new Vector3(89.6f, 0, 4.2f), 12);
        Prop("wooden_crate", new Vector3(90.5f, 0, 5.2f), -20);
        Prop("pallet", new Vector3(79.2f, 0, 8.4f), 30, 1, solid: false);
        Prop("bench", new Vector3(75.5f, 0, 9.1f), 180);
        Boxes(new Vector3(88.5f, 0, -5.4f), 2);
        Barrel(new Vector3(80, 0, -8.6f));
        Barrel(new Vector3(94, 0, 8.6f));
        foreach (var at in new[] { new Vector3(71.6f, 0, 8.8f), new Vector3(72.4f, 0, 9.1f) }) Prop("barrel", at, at.X * 29);

        for (float x = StreamX1 + 1.6f; x < 99; x += 3.1f)
        {
            Prop("corrugated_fence", new Vector3(x, 0, -10.6f), 0, 1, solid: false);
            Prop("wire_fence", new Vector3(x, 0, 10.6f), 0, 1, solid: false);
        }
        Prop("streetlamp", new Vector3(71.5f, 0, -9.4f));
        Lamp(new Vector3(71.5f, 3.7f, -8.7f), "ffcf8a", 3.4f, 12, false);
        Prop("streetlamp", new Vector3(91, 0, -9.4f));
        Lamp(new Vector3(91, 3.7f, -8.7f), "ffcf8a", 3.2f, 12, true);
        Lamp(new Vector3(98.4f, 2.7f, -3.2f), "9dffa8", 3.1f, 9, false);
        FloorText(new Vector3(96, 0, 0), "ui_floor_safehouse_arrow", "9dffa8");
    }

    private void BuildSafehouse()
    {
        Floor(100, 112, -4, 4, "3f4a3d");
        Wall(100, 100.4f, -10.4f, -1.3f, 2.6f, "4e5f4b");
        Wall(100, 100.4f, 1.3f, 10.4f, 2.6f, "4e5f4b");
        Wall(100, 112, -4.4f, -4, StageOneLayout.TallWall, "4e5f4b");
        Wall(100, 112, 4, 4.4f, StageOneLayout.LowWall, "4e5f4b");
        Wall(112, 112.4f, -4.4f, 4.4f, 2.6f, "4e5f4b");
        _safehouseDoor = Door.Create(_stage, new Vector3(100.2f, 0, 0), new Vector3(.3f, 2.4f, 2.6f), new Color("4f6b4a"), new Vector3(0, 0, 2.5f), open: true);
        _safehouseSwitch = Use(new Vector3(101.4f, 0, 0), "ui_use_close_safehouse", () => _stage.TryCloseSafehouse());
        _safehouseSwitch.Enabled = false;
        Lamp(new Vector3(106, 2.85f, -3.72f), "b5e6c3", 3.6f, 9, false);
        FloorText(new Vector3(106, 0, 2), "ui_floor_safehouse", "c8ffd0");
        ArchitectureArt.Portal(_stage,100.2f,2.6f,2.65f);
        ArchitectureArt.FloorApron(_stage,100.2f,2.6f,false);
        ArchitectureArt.Services(_stage,new Vector3(108,0,-3.96f));
    }

    protected override void DrawWall(Node3D body,Vector3 size) => ArchitectureArt.Wall(body,size,WallFinish.Shelter);

    // ---------- ground and backdrop (decoration only) ----------

    /// <summary>
    /// Painted ground (A-24): a dirt patch round the arrival hatch and a dirt road through the forest, an asphalt road with a
    /// gravel shoulder through the yard, all fraying into the grass; tufts and pebbles break the edges. Holes are left for
    /// the stream bed and the safehouse floor.
    /// </summary>
    private void LayGround()
    {
        var painter = new GroundPainter(new Rect2(-14, -28, 140, 52), 22);
        var hatch = new Vector2(ArrivalHatch.X, ArrivalHatch.Z);
        painter.Disc(GroundKind.Dirt, hatch, 7.5f);
        painter.Strip(GroundKind.Dirt, hatch, new Vector2(20, 0), 2.4f);
        painter.Strip(GroundKind.Dirt, new Vector2(18, 0), new Vector2(StreamX0 + .4f, 0), 2f);
        painter.Road(new Vector2(StreamX1 - .3f, 0), new Vector2(100.4f, 0), 2f);
        var room = SafehouseRect.Grow(.6f);
        PaintGround(painter, new[]
        {
            new Rect2(-14, -28, StreamX0 + 14, 52),
            new Rect2(StreamX1, -28, room.Position.X - StreamX1, 52),
            new Rect2(room.Position.X, -28, room.Size.X, room.Position.Y + 28),
            new Rect2(room.Position.X, room.End.Y, room.Size.X, 24 - room.End.Y),
            new Rect2(room.End.X, -28, 126 - room.End.X, 52),
        });
        GroundDressing(painter, Area.Grow(1), p => p.X > StreamX0 - .4f && p.X < StreamX1 + .4f || room.HasPoint(p));
        // Soil just under the ground fills the thin seams at the stream banks and the safehouse.
        var soil = Mat.Solid("56693f", .95f);
        Mat.Box(_stage, new Vector3((-16 + StreamX0) / 2, -.13f, -2), new Vector3(StreamX0 + 16, .2f, 56), soil);
        Mat.Box(_stage, new Vector3((StreamX1 + 128) / 2, -.13f, -2), new Vector3(128 - StreamX1, .2f, 56), soil);
    }

    /// <summary>
    /// Everything outside the walkable ground (A-23): packed with the edge kit by <see cref="StageMap.DressEdge"/> — a rock
    /// band and forest into the haze on the far side, low shrubs and low clumps on the camera side, forest past both
    /// ends — over a far ground plane, so neither gaps between trees nor the end of the floor are ever in view.
    /// </summary>
    private void Backdrop()
    {
        // The old -0.22m plane covered the -0.38m water across the entire stream.
        FarGround("465c48",height:-1.1f);
        OutdoorEdgeArt.Bank(_stage,-1,19,-9.6f,-1,seed:60);
        OutdoorEdgeArt.Grove(_stage,-1,19,-9.6f,63);
        OutdoorEdgeArt.Bank(_stage,68,99,-11.5f,-1,seed:61);
        OutdoorEdgeArt.Grove(_stage,68,99,-11.5f,64);
        OutdoorEdgeArt.Bank(_stage,100,113,-5.1f,-1,seed:62);
        ArchitectureArt.FloorApron(_stage,ArrivalHatch.X,2.6f,false);
        const float far = -64, near = 30;
        // Far side (−Z), each zone from just past its own walkable edge.
        DressEdge(new Rect2(-2.4f, far, 22.4f, -9.6f - far), EdgeSide.Far, rocky: true);
        DressEdge(new Rect2(20, far, StreamX0 - 20, -9.6f - far), EdgeSide.Far);
        DressEdge(new Rect2(StreamX0, far, StreamX1 - StreamX0, -14 - far), EdgeSide.Far, ground: false);
        DressEdge(new Rect2(StreamX1, far, 100 - StreamX1, -10.6f - far), EdgeSide.Far, rocky: true);
        DressEdge(new Rect2(100, far, 14, -4.6f - far), EdgeSide.Far);
        // Camera side (+Z): low only.
        DressEdge(new Rect2(-2.4f, 9.6f, 22.4f, near - 9.6f), EdgeSide.Near);
        DressEdge(new Rect2(20, 9.2f, StreamX0 - 20, near - 9.2f), EdgeSide.Near);
        DressEdge(new Rect2(StreamX0, 12, StreamX1 - StreamX0, near - 12), EdgeSide.Near, ground: false);
        DressEdge(new Rect2(StreamX1, 10.6f, 100 - StreamX1, near - 10.6f), EdgeSide.Near);
        DressEdge(new Rect2(100, 4.6f, 14, near - 4.6f), EdgeSide.Near);
        // Past both ends of the route.
        DressEdge(new Rect2(-40, far, 37.6f, near - far), EdgeSide.West);
        DressEdge(new Rect2(114, far, 36, near - far), EdgeSide.East);
    }

    /// <summary>The camera never follows past the route's ends and sides (A-23).</summary>
    public override Rect2? CameraBounds => new Rect2(-3, -9, 116, 18);
}
