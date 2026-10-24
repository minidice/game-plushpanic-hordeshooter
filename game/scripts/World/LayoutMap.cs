using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// A graybox stage built from layout data (decisions.md T-24 option B, L-29): the size draft of the new Stage 2
/// (doc/map-expansion-plan.md 7.2 step 3). Regions become raised floors per height tier, merged into rectangles on the
/// 1 m grid; everything outside every region is blocked by tall invisible walls; links become ramps, ladders, drop
/// edges, ziplines and the gondola; climbable edges are painted for wall climbers. The size draft now has a first
/// environment pass and Stage 2 landmark GLBs. Not the default in <see cref="StageMap.Create"/> yet:
/// the current Stage 2 stays until this draft is accepted (L-29).
/// Placement (7.2 step 5) reads two more files: encounters (areas, waiting groups, wave entries, the safe zone, timed
/// raids, the mission's way back) and supply points. Markers build the beats: the raised bridge ("blocked") is the
/// crescendo's door, the pump ("mission") its panel, a hut stands on the safe zone and the safehouse on its marker.
/// </summary>
public sealed partial class LayoutMap : StageMap
{
    public const string Stage2Path = "res://content/data/stages/stage2_layout.json";
    public const string Stage2EncountersPath = "res://content/data/stages/stage2_draft_encounters.json";
    public const string Stage2SuppliesPath = "res://content/data/stages/stage2_draft_supplies.json";
    /// <summary>Safe-zone hut (`[임시]`, stage2-floorplan.md 4장): 12 × 10 m, a 3 m doorway at each end along X.</summary>
    public static readonly Vector2 HutSize = new(12, 10);
    public const float HutDoorway = 3f;
    /// <summary>Safehouse room (`[임시]`): 14 × 12 m, the door on the west wall.</summary>
    public static readonly Vector2 SafehouseSize = new(14, 12);
    private const float WallHeight = 2.6f, WallThick = .4f;
    /// <summary>Ramp length along its slope (`[임시]`): 6 m per tier, about 11°.</summary>
    public const float RampLength = 6f;
    private static readonly string[] TierColors = { "697350", "697350", "697350", "777765", "777765", "777765" };
    private ShaderMaterial? _groundMaterial;
    private ShaderMaterial GroundMaterial => _groundMaterial ??= ForestMaterial("res://shaders/stage2_ground.gdshader");

    public StageLayout Layout { get; }
    /// <summary>Floor and blocker rectangles the grid was merged into (checks).</summary>
    public int FloorRects { get; private set; }
    public int VoidRects { get; private set; }
    public Gondola? GondolaStation { get; private set; }
    private Vector3 _goal;
    private readonly string? _encountersPath, _suppliesPath;
    private Door? _gate, _safehouseDoor;
    private Interactable? _pump, _safehouseSwitch;
    private AlarmCar? _alarmTruck;
    private Rect2 _safehouseRoom;
    private readonly bool _hasDestructionExamples;

    public LayoutMap(string path = Stage2Path, string? encounters = Stage2EncountersPath, string? supplies = Stage2SuppliesPath)
    {
        Layout = StageLayout.Load(path);
        _hasDestructionExamples = path == Stage2Path;
        _encountersPath = encounters;
        _suppliesPath = supplies;
    }

    public override Rect2 Area => Layout.Bounds;
    public override string TitleKey => "ui_hud_stage2";
    public override bool HasRoute => true;
    public override Vector3 RouteGoal => _goal;
    public override int StageNumber => 2;
    public override Objective ScenarioStart => _gate != null ? Objective.ReachShutter : Objective.ReachSafehouse;
    public override EncounterSet? Encounters => _encountersPath == null ? null : SpawnDatabase.Current.Load(_encountersPath);
    public override IReadOnlyList<Door> Doors => new[] { _gate, _safehouseDoor }.OfType<Door>().ToArray();
    public override Door? Shutter => _gate;
    public override AlarmCar? AlarmDevice => _alarmTruck;
    public override Interactable? ShutterPanel => _pump;
    public override string CrescendoObjectiveKey => "ui_objective_lower_bridge";
    public override string CrescendoHoldOutKey => "fmt_objective_hold_bridge";
    public override string CrescendoAlarmKey => "msg_stage_bridge_alarm";
    public override string CrescendoDoneKey => "msg_stage_bridge_down";
    public override string RouteStartKey => "msg_stage_draft_start";
    public override Door? SafehouseDoor => _safehouseDoor;
    public override Interactable? SafehouseSwitch => _safehouseSwitch;
    public override bool InSafehouse(Vector3 p) => _safehouseDoor != null && _safehouseRoom.HasPoint(new Vector2(p.X, p.Z));
    public override bool NearSafehouse(Vector3 p) => _safehouseDoor != null && _safehouseRoom.Grow(.6f).HasPoint(new Vector2(p.X, p.Z));
    public override CameraView View => CameraView.Outdoor;
    public override Rect2? CameraBounds => Layout.Bounds;

    public override void Light(Godot.Environment env, DirectionalLight3D key)
    {
        env.BackgroundColor = new Color("5d6b78");
        env.AmbientLightColor = new Color("b9c6d2");
        env.AmbientLightEnergy = .55f;
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

    protected override void BuildMap()
    {
        foreach (var error in Layout.Errors) GD.PushWarning("LayoutMap: " + error);
        var tiers = _stage.Tiers;
        int width = tiers.Width, depth = tiers.Depth;
        var grid = new int[width * depth];
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
            grid[z * width + x] = Layout.TierAt(new Vector2(tiers.MinX + (x + .5f) * tiers.Cell, tiers.MinZ + (z + .5f) * tiers.Cell));

        foreach (var (x0, z0, x1, z1, value) in MergeRects(grid, width, depth))
        {
            float wx0 = tiers.MinX + x0 * tiers.Cell, wx1 = tiers.MinX + x1 * tiers.Cell;
            float wz0 = tiers.MinZ + z0 * tiers.Cell, wz1 = tiers.MinZ + z1 * tiers.Cell;
            if (value >= 0)
            {
                Raised(wx0, wx1, wz0, wz1, value, "", draw: false);
                float top = value * TierMap.Height;
                DrawFloor(new Rect2(wx0, wz0, wx1 - wx0, wz1 - wz0), top, value);
                FloorRects++;
            }
            else
            {
                // Off the map: a tall invisible wall, and a dark floor so the edge reads as "nowhere to go".
                Solid(new Vector3((wx0 + wx1) / 2, 4, (wz0 + wz1) / 2), new Vector3(wx1 - wx0, 8, wz1 - wz0), "", drawCargo: false);
                Mat.Box(_stage, new Vector3((wx0 + wx1) / 2, -.05f, (wz0 + wz1) / 2), new Vector3(wx1 - wx0, .1f, wz1 - wz0), GroundMaterial);
                VoidRects++;
            }
        }

        foreach (var edge in Layout.Edges)
        {
            if (!edge.Climbable) continue;
            for (int i = 0; i + 1 < edge.Line.Length; i++)
            {
                var a = edge.Line[i];
                var b = edge.Line[i + 1];
                ClimbableWall(Mathf.Min(a.X, b.X) - 1, Mathf.Max(a.X, b.X) + 1, Mathf.Min(a.Y, b.Y) - 1, Mathf.Max(a.Y, b.Y) + 1);
            }
        }

        foreach (var link in Layout.Links) BuildLink(link);

        var start = Layout.FindMarker("start")?.Pos ?? Layout.Bounds.GetCenter();
        PlayerStart = FloorAt(start) + Vector3.Up * .05f;
        var goal = Layout.FindMarker("safehouse")?.Pos ?? start;
        _goal = FloorAt(goal);
        Landmark("floor_hatch", FloorAt(start));
        Lamp(PlayerStart + Vector3.Up * 4, "ffe6b0", 3, 24, false);

        if (Layout.FindMarker("blocked") is { } blocked && Layout.FindMarker("mission") is { } mission) BuildBridgeMission(blocked.Pos, mission.Pos);
        if (Layout.FindMarker("safe_zone") is { } zone) BuildHut(zone.Pos);
        if (Layout.FindMarker("safehouse") is { } safehouse) BuildSafehouse(safehouse.Pos);
        BuildLandmarks();
        BuildEnvironment();
        if (_hasDestructionExamples) PlaceDestructionExamples("stage2_draft");
        if (Mode == GameMode.Scenario && _suppliesPath != null) PlaceSupplies(StageSupplies.Load(_suppliesPath));
    }

    /// <summary>
    /// The raised bridge (stage2-floorplan.md 2장 C): a leaf stands up across the deck until the pump's crescendo is held
    /// out, then sinks — the crescendo door of Stage 1, so the beat, its sync bit and the "hold out" timer are the same.
    /// </summary>
    private void BuildBridgeMission(Vector2 bridge, Vector2 pump)
    {
        var deck = FloorAt(bridge);
        if (BridgeFootprint() is { } span)
            for (float x = span.Position.X + 3; x < span.End.X; x += 6)
                Landmark("bridge_truss", new Vector3(x, deck.Y, span.GetCenter().Y));
        var leaf = PropLibrary.Instance("bridge_leaf");
        if (leaf != null) leaf.RotationDegrees = new Vector3(0, -90, 0);
        _gate = Door.Create(_stage, deck, new Vector3(1.2f, 3.6f, 4.6f), new Color("8a6d3b"), new Vector3(0, -3.9f, 0), visual: leaf);
        if (leaf != null) PropOcclusion.Attach(_stage, leaf);
        _gate.Name = "BridgeGate";
        _gate.OpenSeconds = 1.2f;
        var at = FloorAt(pump) + new Vector3(0, 0, -2.5f);
        Landmark("pump_house", at);
        // Visual-only GLB: explicit walls match its 6 × 5 m footprint and 3 m front opening.
        foreach (float x in new[] { -2.85f, 2.85f })
            Solid(at + new Vector3(x, 1.6f, 0), new Vector3(.3f, 3.2f, 5), "", drawCargo: false);
        Solid(at + new Vector3(0, 1.6f, -2.35f), new Vector3(5.4f, 3.2f, .3f), "", drawCargo: false);
        foreach (float x in new[] { -2.1f, 2.1f })
            Solid(at + new Vector3(x, 1.1f, 2.35f), new Vector3(1.2f, 2.2f, .3f), "", drawCargo: false);
        Solid(at + new Vector3(0, .72f, .55f), new Vector3(1.8f, 1.44f, 1.7f), "", drawCargo: false);
        _pump = Use(at + new Vector3(0, 1.1f, 1.6f), "ui_use_pump", _stage.StartCrescendo);
        Lamp(at + new Vector3(0, 2.8f, -1), "ffcf8a", 3, 12, true);
    }

    /// <summary>The safe zone's hut (L-24): modeled half-roof, unchanged wall collision and two always-open X doorways.</summary>
    private void BuildHut(Vector2 center)
    {
        float y = FloorAt(center).Y;
        float x0 = center.X - HutSize.X / 2, x1 = center.X + HutSize.X / 2, z0 = center.Y - HutSize.Y / 2, z1 = center.Y + HutSize.Y / 2;
        float g0 = center.Y - HutDoorway / 2, g1 = center.Y + HutDoorway / 2;
        RoomWall(x0, x1, z0, z0 + WallThick, y, draw: false);
        RoomWall(x0, x1, z1 - WallThick, z1, y, draw: false);
        foreach (float x in new[] { x0, x1 - WallThick })
        {
            RoomWall(x, x + WallThick, z0, g0, y, draw: false);
            RoomWall(x, x + WallThick, g1, z1, y, draw: false);
        }
        Landmark("watch_post", new Vector3(center.X, y, center.Y));
        Lamp(new Vector3(center.X, y + 2.6f, center.Y), "b5e6c3", 3, 10, false);
    }

    /// <summary>Modeled destination shelter; existing walls, sliding-door collision and clear rules stay in the game.</summary>
    private void BuildSafehouse(Vector2 center)
    {
        float y = FloorAt(center).Y;
        float x0 = center.X - SafehouseSize.X / 2, x1 = center.X + SafehouseSize.X / 2, z0 = center.Y - SafehouseSize.Y / 2, z1 = center.Y + SafehouseSize.Y / 2;
        const float door = 2.6f;
        RoomWall(x0, x1, z0, z0 + WallThick, y, draw: false);
        RoomWall(x0, x1, z1 - WallThick, z1, y, draw: false);
        RoomWall(x1 - WallThick, x1, z0, z1, y, draw: false);
        RoomWall(x0, x0 + WallThick, z0, center.Y - door / 2, y, draw: false);
        RoomWall(x0, x0 + WallThick, center.Y + door / 2, z1, y, draw: false);
        var at = new Vector3(center.X, y, center.Y);
        Landmark("safehouse", at);
        _safehouseRoom = new Rect2(x0 + WallThick, z0 + WallThick, SafehouseSize.X - 2 * WallThick, SafehouseSize.Y - 2 * WallThick);
        var leaf = PropLibrary.Instance("safehouse_door");
        _safehouseDoor = Door.Create(_stage, new Vector3(x0 + WallThick / 2, y, center.Y), new Vector3(.3f, 2.4f, door), new Color("4f6b4a"), new Vector3(0, 0, 2.5f), open: true, visual: leaf);
        if (leaf != null) PropOcclusion.Attach(_stage, leaf);
        _safehouseSwitch = Use(at + new Vector3(-6.5f, 1.4f, 1.8f), "ui_use_close_safehouse", () => _stage.TryCloseSafehouse());
        _safehouseSwitch.Enabled = false;
        Lamp(new Vector3(center.X, y + 2.85f, center.Y), "b5e6c3", 3.6f, 12, false);
    }

    /// <summary>A plain graybox wall standing on the floor at height <paramref name="y"/>.</summary>
    private void RoomWall(float x0, float x1, float z0, float z1, float y, bool draw = true)
    {
        var center = new Vector3((x0 + x1) / 2, y + WallHeight / 2, (z0 + z1) / 2);
        var size = new Vector3(x1 - x0, WallHeight, z1 - z0);
        Solid(center, size, "", drawCargo: false);
        if (draw) Mat.Box(_stage, center, size, Mat.Solid("6b7a68", .9f));
    }

    private void Landmark(string id, Vector3 at)
    {
        var prop = Prop(id, at, solid: false);
        if (prop != null) PropOcclusion.Attach(_stage, prop);
    }

    private Rect2? BridgeFootprint()
        => RegionFootprint("bridge");

    private Rect2? RegionFootprint(string id)
    {
        var region = Layout.Regions.Find(r => r.Id == id);
        if (region == null || region.Poly.Length == 0) return null;
        var rect = new Rect2(region.Poly[0], Vector2.Zero);
        foreach (var point in region.Poly) rect = rect.Expand(point);
        return rect;
    }

    private void BuildLandmarks()
    {
        if (RegionFootprint("watchtower") is { } tower)
            Landmark("watchtower", new Vector3(tower.GetCenter().X, 0, tower.GetCenter().Y));
        foreach (var marker in Layout.Markers)
        {
            var at = FloorAt(marker.Pos);
            if (marker.Label == "크레인")
            {
                Landmark("crane", at);
                // Only the base is a walking obstacle; the boom must not block the whole yard.
                Solid(at + Vector3.Up * 2, new Vector3(2.4f, 4, 2.4f), "", drawCargo: false);
            }
            else if (marker.Label == "벙커 정문")
            {
                var gate = Prop("bunker_gate", at);
                if (gate != null) PropOcclusion.Attach(_stage, gate);
            }
            else if (marker.Type == "noise")
            {
                var size = new Vector3(5.2f, 2, 2.2f);
                _alarmTruck = AlarmCar.Create(_stage, at + Vector3.Up, size, "c7a35c", PropLibrary.Instance("alarm_truck"));
                BlockFlow(_alarmTruck.Position, size);
            }
        }
    }

    // Only art is cut out; Raised() still owns the original deck height, solid volume and flow.
    private void DrawFloor(Rect2 rect, float top, int tier)
    {
        void Box(Rect2 r, bool deck = false)
        {
            if (r.Size.X <= 0 || r.Size.Y <= 0) return;
            bool container=deck && tier==3;
            float thick = container ? .04f : deck ? .3f : top + .4f;
            var center = r.GetCenter();
            var mesh = Mat.Box(_stage, new Vector3(center.X, top - thick / 2, center.Y),
                new Vector3(r.Size.X, thick, r.Size.Y), deck && !container ? Mat.Solid("8a6a46", .95f) : GroundMaterial);
            if (deck) mesh.Name = container ? "ContainerRoof" : RegionFootprint("watchtower") is { } tower && r.Intersects(tower) ? "WatchtowerDeck" : "BridgeDeck";
        }
        var span = new[] { BridgeFootprint(), RegionFootprint("watchtower"),RegionFootprint("stack_1"),RegionFootprint("stack_2"),RegionFootprint("stack_3") }
            .FirstOrDefault(r => r.HasValue && rect.Intersects(r.Value));
        if (span == null) { Box(rect); return; }
        var cut = rect.Intersection(span.Value);
        Box(new Rect2(rect.Position, new Vector2(rect.Size.X, cut.Position.Y - rect.Position.Y)));
        Box(new Rect2(rect.Position.X, cut.End.Y, rect.Size.X, rect.End.Y - cut.End.Y));
        Box(new Rect2(rect.Position.X, cut.Position.Y, cut.Position.X - rect.Position.X, cut.Size.Y));
        Box(new Rect2(cut.End.X, cut.Position.Y, rect.End.X - cut.End.X, cut.Size.Y));
        Box(cut, deck: true);
    }

    private Vector3 FloorAt(Vector2 p) => new(p.X, _stage.Tiers.GroundY(new Vector3(p.X, 0, p.Y)), p.Y);

    private void BuildLink(StageLayout.Link link)
    {
        switch (link.Type)
        {
            case "ramp":
            {
                if (Crossing(link.From, link.To) is not { } c) return;
                bool alongX = Mathf.Abs(c.Up.X) > .5f;
                // Whole cells across too: the cells painted as ramp and the wedge's body must be the same strip,
                // or a walker on the ramp's side cell is led into the cliff beside it.
                float side0 = Mathf.Round((alongX ? c.Edge.Y : c.Edge.X) - link.Width / 2), side1 = side0 + Mathf.Round(link.Width);
                if (alongX)
                {
                    float x0 = c.Up.X > 0 ? c.Edge.X - RampLength : c.Edge.X, x1 = c.Up.X > 0 ? c.Edge.X : c.Edge.X + RampLength;
                    Ramp(x0, x1, side0, side1, c.Low, c.Up, TierColors[Math.Clamp(c.Low, 0, TierColors.Length - 1)], smooth:true);
                }
                else
                {
                    float z0 = c.Up.Z > 0 ? c.Edge.Y - RampLength : c.Edge.Y, z1 = c.Up.Z > 0 ? c.Edge.Y : c.Edge.Y + RampLength;
                    Ramp(side0, side1, z0, z1, c.Low, c.Up, TierColors[Math.Clamp(c.Low, 0, TierColors.Length - 1)], smooth:true);
                }
                break;
            }
            case "ladder":
            {
                if (Crossing(link.From, link.To) is not { } c) return;
                var edge = new Vector3(c.Edge.X, 0, c.Edge.Y);
                Ladder(edge - c.Up * .5f, edge + c.Up * .5f);
                break;
            }
            case "drop":
            {
                if (Crossing(link.From, link.To) is not { } c) return;
                // The drop cells are the rim of the high side, facing down toward the low side.
                var edge = new Vector3(c.Edge.X, 0, c.Edge.Y);
                var rim = edge + c.Up * .9f;
                var across = c.Up.Cross(Vector3.Up).Abs() * 1.6f;
                DropEdge(Mathf.Min(rim.X - across.X, edge.X), Mathf.Max(rim.X + across.X, edge.X), Mathf.Min(rim.Z - across.Z, edge.Z), Mathf.Max(rim.Z + across.Z, edge.Z));
                break;
            }
            case "zipline":
                Zipline(FloorAt(link.From), FloorAt(link.To));
                break;
            case "gondola":
            {
                var from = FloorAt(link.From);
                var to = FloorAt(link.To);
                GondolaStation = Gondola(new Rect2(link.From.X - 3, link.From.Y - 2.5f, 6, 5), from.Y, to, to - from);
                // The route crosses the gorge by the gondola (survivors only, like a zipline).
                _stage.Tiers.AddRide(from, to, GondolaStation.RideSeconds);
                break;
            }
        }
    }

    /// <summary>
    /// Where the segment <paramref name="a"/> → <paramref name="b"/> crosses from one tier to another, snapped to the grid
    /// line along its main axis: the edge point, the lower tier, and the unit axis pointing from the low side to the high.
    /// </summary>
    private (Vector2 Edge, int Low, Vector3 Up)? Crossing(Vector2 a, Vector2 b)
    {
        int ta = Layout.TierAt(a), tb = Layout.TierAt(b);
        if (ta < 0 || tb < 0 || ta == tb)
        {
            GD.PushWarning($"LayoutMap: link {a} → {b} does not cross between two tiers ({ta}, {tb})");
            return null;
        }
        var d = b - a;
        bool alongX = Mathf.Abs(d.X) >= Mathf.Abs(d.Y);
        float steps = Mathf.Max(1, d.Length() / .05f);
        var edge = b;
        for (int i = 1; i <= steps; i++)
        {
            var p = a + d * (i / steps);
            if (Layout.TierAt(p) == ta) continue;
            edge = p;
            break;
        }
        edge = alongX ? new Vector2(Mathf.Round(edge.X), edge.Y) : new Vector2(edge.X, Mathf.Round(edge.Y));
        var toHigh = (tb > ta ? d : -d);
        var up = alongX ? new Vector3(Mathf.Sign(toHigh.X), 0, 0) : new Vector3(0, 0, Mathf.Sign(toHigh.Y));
        return (edge, Math.Min(ta, tb), up);
    }

    /// <summary>Greedy rectangles of equal values on the grid: (x0, z0, x1, z1) in cells, end exclusive.</summary>
    public static List<(int x0, int z0, int x1, int z1, int value)> MergeRects(int[] grid, int width, int depth)
    {
        var done = new bool[grid.Length];
        var rects = new List<(int, int, int, int, int)>();
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            int i = z * width + x;
            if (done[i]) continue;
            int v = grid[i];
            int x1 = x + 1;
            while (x1 < width && !done[z * width + x1] && grid[z * width + x1] == v) x1++;
            int z1 = z + 1;
            while (z1 < depth)
            {
                bool full = true;
                for (int k = x; k < x1 && full; k++) full = !done[z1 * width + k] && grid[z1 * width + k] == v;
                if (!full) break;
                z1++;
            }
            for (int zz = z; zz < z1; zz++)
            for (int xx = x; xx < x1; xx++) done[zz * width + xx] = true;
            rects.Add((x, z, x1, z1, v));
        }
        return rects;
    }
}
