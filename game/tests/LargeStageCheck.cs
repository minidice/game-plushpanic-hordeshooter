using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The large campaign (decisions.md L-30 · L-31 · L-32, doc/large-stages.md): for the urban Stage 1, the forest Stage 2 and
/// the size draft as Stage 3 it checks the launch wiring, the layout data (scale, alternating large/small zones, ladders
/// climbing north or east), that every ladder and button can be seen from the play camera (a ray from it toward the
/// camera meets no wall), supplies on their floors, a monster way onto every raised space, then plays the stage through
/// with no monsters: the start door (Stage 1), the mission panel and its crescendo, the walk to the safehouse, the
/// door, and the hatch to the next stage. Run with --fixed-fps 60. Prints MEASURE lines.
/// </summary>
public partial class LargeStageCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private LayoutMap _map = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private static void Measure(string what) => GD.Print("MEASURE: " + what);
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("LARGE_STAGE_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("LARGE_STAGE_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        GameSession.LargeStages = false;
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        // ---------- launch wiring ----------
        GameSession.LargeStages = true;
        GameSession.StartCampaign();
        Check(GameSession.ScenarioStage == 1 && Enumerable.Range(1, 3).All(n => StageMap.Create(GameMode.Scenario, n) is LayoutMap { StageNumber: var s } && s == n)
            && StageMap.Create(GameMode.Survival) is StageOneLayout && StageMap.Create(GameMode.Survivor) is OpenFieldMap,
            "with --large-stages the scenario runs the large Stage 1 → 2 → 3, other modes keep their maps");
        Check(LayoutMap.ForStage(1).Layout.NextStage == 2 && LayoutMap.ForStage(2).Layout.NextStage == 3 && LayoutMap.ForStage(3).Layout.NextStage == 0,
            "each large stage's safehouse hatch leads to the next, Stage 3 is the last for now");
        // Level test start (--zone=2:D): the option alone runs the large campaign from that stage.
        GameSession.LargeStages = false;
        GameSession.StartZone = "D";
        GameSession.StartZoneStage = 2;
        GameSession.StartCampaign();
        Check(GameSession.LargeCampaign && GameSession.ScenarioStage == 2 && StageMap.Create(GameMode.Scenario, 2) is LayoutMap { StageNumber: 2 },
            "--zone=2:D starts the large campaign on Stage 2");
        GameSession.StartZone = "";
        GameSession.StartZoneStage = 1;
        GameSession.StartCampaign();
        Check(StageMap.Create(GameMode.Scenario, 1) is StageOneLayout && StageMap.Create(GameMode.Scenario, 2) is StageTwoMap, "without the option today's Stage 1 and 2 stay");
        Check(PropLibrary.Errors.Count == 0 && new[] { "safehouse_b", "wall_button_panel", "shutter_leaf", "urban_rowhouse", "urban_shop", "urban_warehouse", "car_wreck", "bus_wreck", "dumpster" }
            .All(id => PropLibrary.Find(id) != null), "the large stage kit (Codex) is registered" + string.Join("; ", PropLibrary.Errors));

        for (int n = 1; n <= 3; n++)
        {
            await CheckStage(n);
            _stage.Free();
            _stage = null!;
            await Frames(2);
        }
        await CheckZoneStart();
    }

    /// <summary>A level test run started at Stage 1's D (--zone=D): the player, the open safe room and the running director.</summary>
    private async Task CheckZoneStart()
    {
        var map = LayoutMap.ForStage(1);
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0, StartZoneOverride = "d" };
        AddChild(_stage);
        await Frames(3);
        var entry = map.ZoneEntry("D");
        var at = _stage.Player.GlobalPosition;
        Check(entry is { } e && at.DistanceTo(e) < 1 && map.Layout.ZoneAt(new Vector2(at.X, at.Z))?.Id == "D" && _stage.StartedZone == "D",
            $"--zone=d starts the player at D's entry on the main route ({at}, entry {entry})");
        Check(_stage.Objective != Objective.LeaveSafeRoom && map.StartDoor is { IsOpen: true },
            $"a zone start is past the safe room: its door is open and the objective moved on ({_stage.Objective})");
        _stage.Free();
        _stage = null!;
        await Frames(2);
        var none = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = LayoutMap.ForStage(1), DirectorEnabled = false, SquadOverride = 0, StartZoneOverride = "Z" };
        _stage = none;
        AddChild(_stage);
        await Frames(3);
        Check(none.StartedZone == "" && none.Player.GlobalPosition.DistanceTo(none.Map.PlayerStart) < 1 && none.Objective == Objective.LeaveSafeRoom,
            "an unknown zone falls back to the safe-room start");
        _stage.Free();
        _stage = null!;
        await Frames(2);
    }

    private async Task CheckStage(int n)
    {
        var map = _map = LayoutMap.ForStage(n);
        var layout = map.Layout;
        string name = $"Stage {n}";
        Check(layout.Errors.Count == 0, $"{name}: the layout loads without errors (ladder directions included) " + string.Join("; ", layout.Errors));
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0, NextStageOverride = true };
        AddChild(_stage);
        await Frames(3);
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        await ClearEnemies();
        var tiers = _stage.Tiers;

        // ---------- scale and zones (L-30, L-31) ----------
        var exit = _stage.ExitFlow;
        if (map.Shutter is { } gate) gate.SetOpen(true, 0);
        // The exit route counts doors as open (Stage.RefreshRouteFlows), so it runs past the mission door already.
        var path = new List<Vector3> { exit.Center(exit.Index(map.PlayerStart)) };
        var goal = map.RouteGoal with { Y = 0 };
        for (int i = 0; i < 6000 && path[^1].DistanceTo(goal) > 1.5f; i++)
        {
            int next = exit.NextCell(path[^1]);
            if (next < 0) break;
            path.Add(exit.Center(next));
        }
        Check(path[^1].DistanceTo(goal) <= 1.5f, $"{name}: the exit route reaches the safehouse (mission door open, {path.Count} cells)");
        float walked = 0;
        var zones = new List<StageLayout.Zone>();
        for (int i = 1; i < path.Count; i++)
        {
            var d = path[i] - path[i - 1];
            if (d.Length() <= 1.5f) walked += d.Length();
            if (layout.ZoneAt(new Vector2(path[i].X, path[i].Z)) is { Branch: false } z && (zones.Count == 0 || zones[^1] != z)) zones.Add(z);
        }
        Measure($"{name}: exit route walked {walked:0} m (pure walking {walked / player.Definition.MoveSpeed / 60:0.0} min), zones {string.Join("-", zones.Select(z => z.Id))}");
        Check(walked is > 500 and < 800, $"{name}: the route is at the Stage 3 scale ({walked:0} m, L-30 ~550-650 m on the plan)");
        int large = layout.Zones.Count(z => z.Size == "large"), small = layout.Zones.Count(z => z.Size == "small" && !z.Branch);
        Check(large is >= 3 and <= 4 && small is >= 4 and <= 6, $"{name}: {large} large and {small} small zones on the route (L-31: 3-4 and 4-6)");
        var pairs = zones.Zip(zones.Skip(1)).Where(p => p.First.Size == "large" && p.Second.Size == "large").Select(p => $"{p.First.Id}-{p.Second.Id}").ToList();
        Check(pairs.Count == 0, $"{name}: the walked route never goes from a large zone straight into another (L-31) " + string.Join(", ", pairs));
        if (map.Shutter is { } shut) shut.SetOpen(false, 0);

        // ---------- visibility of ladders and buttons (L-32) ----------
        foreach (var ladder in _stage.Ladders)
        {
            var climb = ladder.Top - ladder.Bottom;
            var flat = new Vector2(climb.X, climb.Z);
            bool northOrEast = Mathf.Abs(flat.X) >= Mathf.Abs(flat.Y) ? flat.X > 0 : flat.Y < 0;
            Check(northOrEast, $"{name}: ladder at {ladder.Bottom} climbs north or east ({flat})");
            var face = ladder.Bottom.Lerp(ladder.Top, .5f) with { Y = (ladder.Bottom.Y + ladder.Top.Y) / 2 + .6f };
            Check(Seen(face - new Vector3(flat.X, 0, flat.Y).Normalized() * .35f), $"{name}: the camera sees the ladder at {ladder.Bottom}");
        }
        var buttons = new (string What, Interactable? Item, float Front)[] {
            ("start door switch", map.StartDoorSwitch, .05f), ("mission device", map.ShutterPanel, .05f), ("safehouse switch", map.SafehouseSwitch, .1f) };
        foreach (var (what, item, front) in buttons)
        {
            if (item == null) continue;
            Check(Seen(item.GlobalPosition + Vector3.Back * front), $"{name}: the camera sees the {what} at {item.GlobalPosition}");
        }
        var room = layout.FindMarker("safehouse")!.Pos;
        // Control: the old safehouse button spot on the west wall's inner face (screen lower-left) is hidden by that wall.
        var old = new Vector3(room.X, map.SafehouseSwitch!.GlobalPosition.Y, room.Y) + new Vector3(-6.5f + .1f, 0, 1.8f);
        Check(!Seen(old), $"{name}: the old west-wall button spot would be hidden from the camera (the ray check tells them apart)");
        var local = map.SafehouseSwitch!.GlobalPosition - new Vector3(room.X, map.SafehouseSwitch.GlobalPosition.Y - LayoutMap.SafehouseSwitchLocal.Y, room.Y);
        Check(local.Z < -LayoutMap.SafehouseSize.Y / 2 + 1, $"{name}: the safehouse switch hangs on the north wall, the screen's upper-left wall ({local})");

        // Level test starts (--zone): every zone a route crosses has an open entry inside it.
        var noEntry = new List<string>();
        foreach (var zone in layout.Zones)
        {
            var entry = map.ZoneEntry(zone.Id);
            if (entry is not { } at || layout.ZoneAt(new Vector2(at.X, at.Z))?.Id != zone.Id || _stage.Flow.IsBlocked(at)) noEntry.Add(zone.Id);
        }
        Check(noEntry.Count == 0, $"{name}: every zone has a level test start (a route entry, or the open middle of a branch no route crosses; none: {string.Join(", ", noEntry)})");

        // ---------- supplies and raised spaces ----------
        Check(_stage.Director.EncounterErrors.Count == 0, $"{name}: every wave entry and named monster stands on open floor " + string.Join("; ", _stage.Director.EncounterErrors));
        if (n == 1)
        {
            // Stage 1 guide plan (L-33): the alley car and the yard truck are two separate alarms, the first stays AlarmDevice.
            var alarms = map.AlarmDevices;
            Check(alarms.Count == 2 && alarms.All(a => _stage.AlarmCars.Contains(a)) && map.AlarmDevice == alarms[0]
                  && alarms[0].GlobalPosition.X < 200 && alarms[1].GlobalPosition.X > 450,
                $"{name}: the alley car and the yard truck are both alarms ({string.Join(", ", alarms.Select(a => a.GlobalPosition))})");
            // B wayfinding landmark (L-33): the warehouse sign north of the alley exit; its legs block, the road in front stays open.
            var sign = map.KitProps.FirstOrDefault(p => p.GetNodeOrNull("warehouse_sign") != null);
            var signAt = sign?.GlobalPosition ?? Vector3.Zero;
            Check(sign != null && _stage.Flow.IsBlocked(signAt + new Vector3(1.85f, 0, 0)) && _stage.Flow.IsBlocked(signAt - new Vector3(1.85f, 0, 0))
                  && !_stage.Flow.IsBlocked(signAt + new Vector3(0, 0, 4)),
                $"{name}: the warehouse sign stands at the alley exit, legs solid, the road in front open ({signAt})");
        }
        var pickups = _stage.Interactables.Where(i => i is WeaponPickup || i.Pickup is PickupKind.Medkit or PickupKind.Ammo).ToList();
        var off = pickups.Where(i => Mathf.Abs(i.GlobalPosition.Y - tiers.GroundY(i.GlobalPosition)) > .3f || _stage.Flow.IsBlocked(i.GlobalPosition))
            .Select(i => $"{i.Name} at {i.GlobalPosition}").ToList();
        Check(pickups.Count > 10 && off.Count == 0, $"{name}: {pickups.Count} pickups lie on their floors, off walls " + string.Join("; ", off));
        if (map.Shutter is { } open) open.SetOpen(true, 0);
        map.StartDoor?.SetOpen(true, 0);
        await Frames(2);
        var reach = _stage.Flow.CopyLayout();
        reach.Riders = true;
        var cut = new List<string>();
        foreach (var pickup in pickups)
        {
            reach.Update(pickup.GlobalPosition);
            if (!reach.Reachable(map.PlayerStart)) cut.Add($"{pickup.Name} {pickup.GlobalPosition}");
        }
        Check(cut.Count == 0, $"{name}: every pickup can be walked to from the start " + string.Join(", ", cut));
        foreach (var region in layout.Regions.Where(r => r.Tier > 0))
        {
            var c = region.Poly.Aggregate(Vector2.Zero, (s, p) => s + p) / region.Poly.Length;
            if (layout.TierAt(c) != region.Tier) continue;
            var at = new Vector3(c.X, tiers.GroundY(new Vector3(c.X, 0, c.Y)), c.Y);
            bool any = false;
            foreach (var climb in new[] { MonsterClimb.Ladder, MonsterClimb.Wall, MonsterClimb.Jump })
            {
                var field = _stage.Flow.CopyLayout();
                field.Mover = climb;
                field.Update(at);
                any |= field.Reachable(map.PlayerStart);
            }
            int own = map.Encounters!.Entries.Count(e => Geometry2D.IsPointInPolygon(new Vector2(e.At.X, e.At.Z), region.Poly));
            Check(any || own > 0, $"{name}: monsters can get onto {region.Id} (tier {region.Tier})");
        }
        if (map.Shutter is { } close) close.SetOpen(false, 0);
        map.StartDoor?.SetOpen(false, 0);
        await Frames(2);

        // ---------- play it through ----------
        player.TeleportTo(map.PlayerStart);
        await Frames(2);
        if (map.StartDoorSwitch is { } doorSwitch)
        {
            Check(_stage.Objective == Objective.LeaveSafeRoom && !map.StartDoor!.IsOpen, $"{name}: the run starts shut in the safe room");
            var stand = doorSwitch.GlobalPosition + new Vector3(0, -1.2f, .4f);
            var (s0, stuck0, _) = await Walk(Toward(stand), stand, .35f, 1);
            Check(!stuck0 && _stage.FocusedInteractable == doorSwitch, $"{name}: the door switch on the north wall is in reach ({s0:0} s)");
            doorSwitch.Use(player);
            await Frames(3);
            Check(map.StartDoor!.IsOpen && _stage.Objective == Objective.ReachShutter, $"{name}: the switch opens the safe room door");
        }
        var panel = map.ShutterPanel!;
        var (s1, stuck1, uses1) = await Walk(Toward(panel.GlobalPosition), panel.GlobalPosition, panel.Radius - .1f, 4);
        await Frames(2);
        Check(!stuck1 && _stage.FocusedInteractable == panel, $"{name}: a survivor walks to the mission device ({s1 / 60:0.0} min, {uses1}, at {player.GlobalPosition})");
        panel.Use(player);
        await Frames(2);
        Check(_stage.Objective == Objective.HoldOut, $"{name}: the device starts the crescendo");
        await ClearEnemies();
        _stage.Director.EndCrescendoForTest();
        await Frames(3);
        Check(map.Shutter!.IsOpen && _stage.Objective == Objective.ReachSafehouse, $"{name}: the way on opens when the crescendo is held out");
        _stage.Director.Stop();
        await ClearEnemies();
        var (s2, stuck2, uses2) = await Walk(exit, map.RouteGoal, 2f, 8);
        Measure($"{name}: mission device → safehouse {s2 / 60:0.0} min simulated, {uses2}");
        Check(!stuck2 && player.GlobalPosition.DistanceTo(map.RouteGoal) <= 2f, $"{name}: a survivor walks on to the safehouse ({player.GlobalPosition})");
        var shutSwitch = map.SafehouseSwitch!;
        // Stand on the floor inside, in front of the button (the button itself is in the wall's flow cell).
        var inFront = shutSwitch.GlobalPosition + new Vector3(0, -LayoutMap.SafehouseSwitchLocal.Y, .6f);
        var (_, stuck3, _) = await Walk(Toward(inFront), inFront, .35f, 1);
        await Frames(3);
        Check(!stuck3 && _stage.Objective == Objective.CloseSafehouse && shutSwitch.Enabled && _stage.FocusedInteractable == shutSwitch,
            $"{name}: the close switch on the north wall is in reach inside the safehouse (stuck {stuck3}, objective {_stage.Objective}, enabled {shutSwitch.Enabled}, "
            + $"focused {_stage.FocusedInteractable?.Name}, player {player.GlobalPosition}, switch {shutSwitch.GlobalPosition})");
        shutSwitch.Use(player);
        await Frames(3);
        if (layout.NextStage > 0)
            Check(_stage.Objective == Objective.TakeExit && map.StageExit is { Enabled: true } && !map.SafehouseDoor!.IsOpen,
                $"{name}: shutting the door opens the floor hatch to Stage {layout.NextStage}");
        else
            Check(_stage.State == StageState.Cleared && !map.SafehouseDoor!.IsOpen, $"{name}: shutting the door clears the last stage");
    }

    // ---------- helpers ----------

    /// <summary>
    /// A ray from <paramref name="point"/> back along the play camera's view meets no wall, floor or prop body within 3 m
    /// above it — the height of room walls and cliffs that hide a device. Higher up the only bodies are the invisible 8 m
    /// walls around the map, which hide nothing.
    /// </summary>
    private bool Seen(Vector3 point)
    {
        var back = GetViewport().GetCamera3D().GlobalBasis.Z.Normalized();
        var to = point + back * (3f / Mathf.Max(back.Y, .1f));
        var hit = _stage.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(point, to, Layers.World));
        if (hit.Count > 0) GD.Print($"  ray from {point} hits {((Node)hit["collider"]).Name} at {hit["position"]}");
        return hit.Count == 0;
    }

    private FlowField Toward(Vector3 at)
    {
        var flow = _stage.Flow.CopyLayout();
        flow.Riders = true;
        flow.Update(at);
        return flow;
    }

    /// <summary>Walks the survivor along <paramref name="flow"/>, taking ladders and ziplines (SizeDraftCheck's walker, no gondola here).</summary>
    private async Task<(float Seconds, bool Stuck, string Uses)> Walk(FlowField flow, Vector3 goal, float near, float minutes)
    {
        var player = _stage.Player;
        var tiers = _stage.Tiers;
        int frames = 0, ladderUses = 0, ziplineUses = 0, gondolaRides = 0, lastMoved = 0;
        bool stuck = false;
        var last = player.GlobalPosition;
        var gondola = _map.GondolaStation;
        while (frames < 60 * 60 * minutes && player.GlobalPosition.DistanceTo(goal) > near)
        {
            await Frames(1);
            frames++;
            if (player.OnLadder || player.OnZipline || (gondola?.Riding ?? false)) { player.SetControls(Vector2.Zero, player.AimPoint, false); lastMoved = frames; continue; }
            var here = player.GlobalPosition;
            int cell = tiers.Index(here);
            int next = flow.NextCell(here);
            if (gondola != null && gondola.Aboard(player) && next >= 0 && flow.Center(next).DistanceTo(here with { Y = 0 }) > 3f)
            {
                if (gondola.TryDepart()) gondolaRides++;
                continue;
            }
            var zip = _stage.Ziplines.FirstOrDefault(z => z.StartCell == cell && next == z.EndCell);
            if (zip != null && player.RideZipline(zip)) { ziplineUses++; continue; }
            var ladder = _stage.Ladders.FirstOrDefault(l => (l.BottomCell == cell && next == l.TopCell) || (l.TopCell == cell && next == l.BottomCell));
            if (ladder != null && player.ClimbLadder(ladder, up: ladder.BottomCell == cell)) { ladderUses++; continue; }
            var dir = next < 0 ? (goal - here) with { Y = 0 } : flow.Direction(here);
            if (dir.LengthSquared() > 1) dir = dir.Normalized();
            player.SetControls(new Vector2(dir.X, dir.Z), here + dir * 5 + Vector3.Up, false);
            if (here.DistanceTo(last) > .5f) { last = here; lastMoved = frames; }
            if (frames - lastMoved > 180) { stuck = true; break; }
        }
        player.SetControls(Vector2.Zero, player.AimPoint, false);
        return (frames / 60f, stuck, $"ladders {ladderUses}, ziplines {ziplineUses}, gondola {gondolaRides}");
    }

    private async Task ClearEnemies()
    {
        foreach (var e in _stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
    }
}
