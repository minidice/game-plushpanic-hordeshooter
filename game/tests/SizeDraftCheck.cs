using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The size draft, Stage 3 of the large campaign (decisions.md L-29, L-30, doc/map-expansion-plan.md 7.2 steps 3 and 5): builds <see cref="LayoutMap"/> from
/// content/data/stages/stage3_layout.json with its encounters and supplies, checks every part of the layout was built,
/// follows the exit route and measures it (walking length, L-1 share on the main route, rides), times the flow field
/// rebuilds on the big grid, checks the placement rules (supply budget on the tier floors, a monster way onto every raised
/// space, the safe zone's no-place / no-wave radii and raid holds), plays the bridge mission (drop into the canyon, start
/// the pump, the way back fills again, climb the ladder, cross the lowered bridge), then walks a survivor from the hatch to
/// the safehouse with no monsters and shuts the door. Run with --fixed-fps 60 so the walks are simulated faster than real
/// time. Prints MEASURE lines.
/// </summary>
public partial class SizeDraftCheck : Node
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
        try { await Run(); GD.Print("SIZE_DRAFT_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SIZE_DRAFT_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        // The launch option (--stage3-draft, formerly --stage2-draft) starts a large-campaign run on the draft, now Stage 3 (L-30).
        GameSession.Stage3Draft = true;
        GameSession.StartCampaign();
        Check(GameSession.ScenarioStage == 3 && StageMap.Create(GameMode.Scenario, 3) is LayoutMap { StageNumber: 3 } && StageMap.Create(GameMode.Survival) is not LayoutMap,
            "with --stage3-draft a new scenario run starts on the draft as Stage 3 (other modes unchanged)");
        GameSession.Stage3Draft = false;
        GameSession.StartCampaign();
        Check(GameSession.ScenarioStage == 1 && StageMap.Create(GameMode.Scenario, 2) is StageTwoMap, "without it, runs start at Stage 1 and Stage 2 is today's");

        var watch = Stopwatch.StartNew();
        var map = _map = new LayoutMap();
        Check(map.Layout.Errors.Count == 0, "the layout data loads without errors" + string.Join("; ", map.Layout.Errors));
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(3);
        Measure($"build {watch.ElapsedMilliseconds} ms, {map.FloorRects} floor boxes, {map.VoidRects} blockers, grid {_stage.Tiers.Width}×{_stage.Tiers.Depth} = {_stage.Tiers.Width * _stage.Tiers.Depth} cells");
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        await ClearEnemies();
        var tiers = _stage.Tiers;

        // ---------- everything in the layout was built ----------
        Check(tiers.Any && tiers.MaxTier == 4, "the draft has tiers 0 to 4");
        int ladders = map.Layout.Links.Count(l => l.Type == "ladder"), ziplines = map.Layout.Links.Count(l => l.Type == "zipline");
        Check(_stage.Ladders.Count == ladders && _stage.Ziplines.Count == ziplines && map.GondolaStation != null,
            $"every ladder ({ladders}), zipline ({ziplines}) and the gondola were built");
        Check(TierMap.TierOfHeight(player.GlobalPosition.Y) == 0 && player.GlobalPosition.DistanceTo(map.PlayerStart) < 1.5f, "the survivor starts at the bunker hatch on the ground");
        foreach (var region in map.Layout.Regions)
        {
            var c = Centroid(region);
            if (map.Layout.TierAt(c) != region.Tier) continue;
            Check(tiers.TierAt(new Vector3(c.X, 0, c.Y)) == region.Tier, $"region {region.Id} stands at tier {region.Tier}");
        }

        CheckPlacementData();

        // ---------- the exit route ----------
        var exit = _stage.ExitFlow;
        Check(exit.Reachable(map.PlayerStart), "the safehouse can be reached from the hatch (by every way survivors have)");
        var path = new List<Vector3> { exit.Center(exit.Index(map.PlayerStart)) };
        var goal = map.RouteGoal with { Y = 0 };
        for (int i = 0; i < 5000 && path[^1].DistanceTo(goal) > 1.5f; i++)
        {
            int next = exit.NextCell(path[^1]);
            if (next < 0) break;
            path.Add(exit.Center(next));
        }
        Check(path[^1].DistanceTo(goal) <= 1.5f, $"following the exit route reaches the safehouse ({path.Count} cells)");
        var cam = _stage.Cam;
        var right = (cam.GlobalBasis.X with { Y = 0 }).Normalized();
        var up = (-cam.GlobalBasis.Z with { Y = 0 }).Normalized();
        float walked = 0, upRight = 0, ridden = 0;
        int rides = 0;
        for (int i = 1; i < path.Count; i++)
        {
            var d = path[i] - path[i - 1];
            // A jump of more than a diagonal step is a ride (zipline or gondola): out of the L-1 measure (L-3).
            if (d.Length() > 1.5f) { ridden += d.Length(); rides++; continue; }
            walked += d.Length();
            float angle = Mathf.RadToDeg(Mathf.Atan2(d.Dot(up), d.Dot(right)));
            if (angle is >= 15 and <= 75) upRight += d.Length();
        }
        Measure($"exit route: walked {walked:0} m (pure walking {walked / player.Definition.MoveSpeed / 60:0.0} min at {player.Definition.MoveSpeed} m/s), {rides} rides over {ridden:0} m, L-1 share {upRight / walked:P0} (cell path, before body width)");
        Check(walked > 400 && walked < 900, $"the route is in the planned size ({walked:0} m, plan ~600 m)");

        // ---------- flow field cost on the big grid ----------
        foreach (var climb in new[] { MonsterClimb.Ladder, MonsterClimb.Wall, MonsterClimb.Jump, MonsterClimb.Fly })
        {
            var field = _stage.FlowFor(climb);
            var spots = new[] { new Vector3(100, 0, -20), new Vector3(330, 0, -50), new Vector3(470, 0, -90) };
            var times = new List<double>();
            foreach (var spot in spots)
            {
                var at = spot with { Y = tiers.GroundY(spot) };
                var t = Stopwatch.StartNew();
                field.Update(at);
                times.Add(t.Elapsed.TotalMilliseconds);
            }
            Measure($"flow rebuild ({climb}): {times.Average():0.0} ms average, {times.Max():0.0} ms worst over {spots.Length} targets");
        }
        Check(true, "flow rebuild times measured (no budget agreed yet, I-109)");

        await CheckSafeZone();
        await CheckBridgeMission();

        // ---------- walk it ----------
        _stage.Director.Stop();
        await ClearEnemies();
        player.TeleportTo(map.PlayerStart);
        await Frames(2);
        var (seconds, stuck, uses) = await Walk(exit, map.RouteGoal, 2f, 10);
        Measure($"walk: {seconds / 60:0.0} min simulated, {uses}" + (stuck ? $", STUCK at {player.GlobalPosition} tier {tiers.TierOf(player)} cell tier {tiers.TierAt(player.GlobalPosition)}" : ""));
        Check(!stuck, $"the survivor never gets stuck on the way ({player.GlobalPosition})");
        Check(player.GlobalPosition.DistanceTo(map.RouteGoal) <= 2f, $"a survivor walks from the hatch to the safehouse with no monsters in {seconds / 60:0.0} min");

        await Frames(3);
        var shut = map.SafehouseSwitch!;
        Check(_stage.Objective == Objective.CloseSafehouse && shut.Enabled, "inside the safehouse the door switch lights up");
        shut.Use(player);
        await Frames(2);
        Check(_stage.State == StageState.Cleared && !map.SafehouseDoor!.IsOpen, "shutting the safehouse door clears the stage");
    }

    // ---------- placement data (7.2 step 5) ----------

    private void CheckPlacementData()
    {
        var map = _map;
        var set = map.Encounters!;
        var tiers = _stage.Tiers;
        var spawnErrors = SpawnDatabase.Current.Errors.Where(e => e.Contains("stage3_encounters")).ToList();
        Check(spawnErrors.Count == 0, "the draft's encounters load without errors" + string.Join("; ", spawnErrors));
        Check(_stage.Director.EncounterErrors.Count == 0, "every wave entry and named monster stands on open floor" + string.Join("; ", _stage.Director.EncounterErrors));
        Check(set.SafeZones.Count == 1 && set.Raids != null && set.Groups.Any(g => g.After == "mission"),
            "the draft has a safe zone, timed raid rules and a way-back group for the mission");
        var supplies = StageSupplies.Load(LayoutMap.Stage3SuppliesPath);
        Check(supplies.Errors.Count == 0, "the draft's supply points load without errors" + string.Join("; ", supplies.Errors));

        // Budget (stage2-floorplan.md 5장, a 4-player team).
        int main = supplies.Points.Count(p => p.Kind == SupplyKind.Main), optional = supplies.Points.Count(p => p.Kind == SupplyKind.Optional);
        int events = supplies.Points.Count(p => p.Kind == SupplyKind.Event);
        Check(main == 3 && optional == 2 && events == 1, $"3 main, 2 optional and 1 event supply point ({main}, {optional}, {events})");
        int medkits = supplies.Points.SelectMany(p => p.Items).Count(i => i.Kind == SupplyItemKind.Medkit);
        int throwables = supplies.Points.SelectMany(p => p.Items).Count(i => i.Kind == SupplyItemKind.Throwable);
        int found = supplies.ItemsOf(SupplyKind.Main, SupplyKind.Optional).Count(i => i.Kind == SupplyItemKind.Weapon);
        Check(medkits is >= 3 and <= 5 && throwables is >= 3 and <= 5 && found is >= 2 and <= 3,
            $"medkits 3-5 ({medkits}), throwables 3-5 ({throwables}), 2-3 weapons found on the way ({found})");
        var piles = supplies.Points.Where(p => p.Kind is not (SupplyKind.Start or SupplyKind.Arrival))
            .SelectMany(p => p.Items.Where(i => i.Kind == SupplyItemKind.Ammo).Select(_ => p.Kind)).ToList();
        Check(piles.Count is >= 2 and <= 3 && piles.All(k => k == SupplyKind.Main), $"2-3 ammo piles, only in main points ({piles.Count})");
        var pump = map.ShutterPanel!.GlobalPosition;
        Check(supplies.Points.SelectMany(p => p.Items).Where(i => i.Kind == SupplyItemKind.Ammo).All(i => Flat(i.At - pump).Length() >= 10),
            "no ammo pile within 10 m of the pump (L-12)");

        // Every item lies on its tier's floor and every supply point can be walked to (with the bridge down).
        var pickups = _stage.Interactables.Where(i => i is WeaponPickup || i.Pickup is PickupKind.Medkit or PickupKind.Ammo).ToList();
        int items = supplies.Points.Sum(p => p.Items.Count);
        Check(pickups.Count == items, $"every supply item was placed ({pickups.Count} of {items})");
        var off = pickups.Where(i => Mathf.Abs(i.GlobalPosition.Y - tiers.GroundY(i.GlobalPosition)) > .3f || _stage.Flow.IsBlocked(i.GlobalPosition))
            .Select(i => $"{i.Name} at {i.GlobalPosition} (floor {tiers.GroundY(i.GlobalPosition):0.0}, blocked {_stage.Flow.IsBlocked(i.GlobalPosition)})").ToList();
        Check(off.Count == 0, "every pickup lies on the floor of its tier, off walls " + string.Join("; ", off));
        map.Shutter!.SetOpen(true, 0);
        // The safehouse door opens with a held E from outside (L-35): open it here, its arrival supply lies inside.
        map.SafehouseDoor?.SetOpen(true, 0);
        // Toward each point in turn: the gondola only goes one way, so "back to the hatch" is not the same question.
        var reach = _stage.Flow.CopyLayout();
        reach.Riders = true;
        var cut = new List<string>();
        foreach (var point in supplies.Points)
        {
            reach.Update(point.Center with { Y = tiers.GroundY(point.Center) });
            if (!reach.Reachable(map.PlayerStart)) cut.Add(point.Id);
        }
        Check(cut.Count == 0, "every supply point can be walked to from the hatch " + string.Join(", ", cut));

        // A monster way onto every raised space (map-expansion-plan.md 7.2 step 5: no untouchable high ground).
        foreach (var region in map.Layout.Regions.Where(r => r.Tier > 0))
        {
            var c = Centroid(region);
            if (map.Layout.TierAt(c) != region.Tier) continue;
            var at = new Vector3(c.X, tiers.GroundY(new Vector3(c.X, 0, c.Y)), c.Y);
            var ways = new List<string>();
            foreach (var climb in new[] { MonsterClimb.Ladder, MonsterClimb.Wall, MonsterClimb.Jump })
            {
                var field = _stage.Flow.CopyLayout();
                field.Mover = climb;
                field.Update(at);
                if (field.Reachable(map.PlayerStart)) ways.Add(climb.ToString());
            }
            if (ways.Count > 0)
            {
                Check(true, $"monsters can get onto {region.Id} (tier {region.Tier}) from the route by {string.Join("/", ways)}");
                continue;
            }
            // Past the one-way gondola nobody walks in from the route: such an island needs its own wave entries.
            int own = set.Entries.Count(e => Geometry2D.IsPointInPolygon(new Vector2(e.At.X, e.At.Z), region.Poly));
            Check(own > 0, $"{region.Id} (tier {region.Tier}) is cut off from the route by a ride and has its own wave entries ({own})");
        }
        map.Shutter.SetOpen(false, 0);
    }

    private async Task CheckSafeZone()
    {
        var director = _stage.Director;
        var zone = _map.Encounters!.SafeZones[0];
        var player = _stage.Player;
        var tiers = _stage.Tiers;
        Vector3 On(float x, float z) => new(x, tiers.GroundY(new Vector3(x, 0, z)) + .05f, z);

        // Fill every area as if the survivors had walked through, then look for anything waiting inside the zone.
        _stage.DirectorEnabled = true;
        foreach (var spot in new[] { On(60, -10), On(150, -30), On(175, -30), On(245, -70), On(330, -60), On(430, -60), On(480, -95) })
        {
            player.TeleportTo(spot);
            await Frames(2);
        }
        _stage.DirectorEnabled = false;
        int waiting = _stage.AllEnemies.Count(e => !e.IsDead);
        int inside = _stage.AllEnemies.Count(e => !e.IsDead && Flat(e.GlobalPosition - zone.At).Length() < zone.NoPlace);
        Measure($"placed before arrival: {waiting} monsters over {director.FilledThrough + 1} areas");
        Check(waiting > 40 && inside == 0, $"areas fill ahead, and nothing waits within {zone.NoPlace} m of the safe zone ({inside} of {waiting})");
        Check(!director.MissionRefilled, "the mission's way-back groups are not out before the mission");
        await ClearEnemies();

        // No wave within the no-wave radius while the leader stands in the zone.
        player.TeleportTo(zone.At with { Y = tiers.GroundY(zone.At) + .05f });
        await Frames(2);
        int picks = 0, near = 0;
        for (int i = 0; i < 60; i++)
            if (director.PickEntry(14, 36) is { } entry)
            {
                picks++;
                if (Flat(entry.Ground - zone.At).Length() < zone.NoWave) near++;
            }
        Check(picks > 0 && near == 0, $"waves near the safe zone come from {zone.NoWave} m or further ({near} of {picks} picks closer)");

        // Timed raids: held at the start, in the zone, and outside the travel areas.
        director.Begin();
        player.TeleportTo(On(90, -10));
        await Frames(2);
        Check(director.RaidsHeld, "timed raids wait at the start of the stage");
        director.EndRaidHoldForTest();
        await Frames(1);
        Check(!director.RaidsHeld, "after the start hold, raids run in the forest");
        player.TeleportTo(zone.At with { Y = tiers.GroundY(zone.At) + .05f });
        await Frames(2);
        Check(director.InSafeZone() && director.RaidsHeld, "raids hold while a survivor is inside the safe zone");
        player.TeleportTo(On(330, -50));
        await Frames(2);
        Check(!director.RaidsHeld, "raids run again in the yard once everyone is out");
        player.TeleportTo(On(20, 0));
        await Frames(2);
        Check(director.RaidsHeld, "no raids at the arrival clearing (not a travel area)");
        director.Stop();
    }

    private async Task CheckBridgeMission()
    {
        var map = _map;
        var player = _stage.Player;
        var tiers = _stage.Tiers;
        var gate = map.Shutter!;
        Check(_stage.Objective == Objective.ReachShutter && !gate.IsOpen, "the stage starts with the bridge raised: lower it at the pump");

        var survivors = _stage.Flow.CopyLayout();
        survivors.Riders = true;
        survivors.Update(map.RouteGoal);
        Check(!survivors.Reachable(map.PlayerStart), "with the bridge raised the safehouse cannot be reached");

        // Down into the canyon from the west rim, to the pump.
        player.TeleportTo(new Vector3(172, tiers.GroundY(new Vector3(172, 0, -30)) + .05f, -30));
        await Frames(2);
        var pump = map.ShutterPanel!.GlobalPosition;
        var toPump = _stage.Flow.CopyLayout();
        toPump.Riders = true;
        toPump.Update(pump);
        // The modeled valve is 1.1 m above the floor: stop within its actual 3D use radius.
        var (down, stuck, _) = await Walk(toPump, pump, map.ShutterPanel.Radius - .1f, 2);
        await Frames(2);
        Check(!stuck && player.GlobalPosition.DistanceTo(pump) < map.ShutterPanel.Radius && tiers.TierOf(player) == 0
            && _stage.FocusedInteractable == map.ShutterPanel,
            $"a survivor drops into the canyon and walks to the pump ({down:0} s; player {player.GlobalPosition}, valve {pump}, stuck {stuck})");

        // The pump starts the crescendo; when it ends the bridge comes down and the way back fills (L-27).
        map.ShutterPanel.Use(player);
        await Frames(2);
        Check(_stage.Objective == Objective.HoldOut && _stage.Director.CrescendoActive, "starting the pump begins the crescendo");
        await ClearEnemies();
        _stage.Director.EndCrescendoForTest();
        await Frames(3);
        Check(gate.IsOpen && _stage.Objective == Objective.ReachSafehouse, "the bridge comes down when the crescendo is held out");
        Check(_stage.Director.RaidsHeld, "timed raids rest after the crescendo");
        var backGroups = map.Encounters!.Groups.Where(g => g.After == "mission").ToList();
        int back = _stage.AllEnemies.Count(e => !e.IsDead && backGroups.Any(g => g.Rect.Grow(.5f).HasPoint(new Vector2(e.GlobalPosition.X, e.GlobalPosition.Z))));
        Check(_stage.Director.MissionRefilled && back >= backGroups.Sum(g => g.Count) / 2, $"monsters wait again on the way back from the pump ({back})");
        _stage.Director.Stop();
        await ClearEnemies();

        // Back up the ladder and across the bridge to the watch post.
        var zone = map.Encounters.SafeZones[0].At;
        var hut = zone with { Y = tiers.GroundY(zone) };
        var toHut = _stage.Flow.CopyLayout();
        toHut.Riders = true;
        toHut.Update(hut);
        var (up, stuckBack, uses) = await Walk(toHut, hut, 2f, 3);
        Measure($"mission loop back: {up:0} s from the pump to the watch post, {uses}");
        Check(!stuckBack && player.GlobalPosition.DistanceTo(hut) <= 2f && uses.Contains("ladders 1"),
            $"from the pump a survivor climbs the ladder and crosses the lowered bridge to the watch post ({uses})");
    }

    // ---------- helpers ----------

    /// <summary>
    /// Walks the local survivor along <paramref name="flow"/> to <paramref name="goal"/>, taking ladders, ziplines and the
    /// gondola where the next cell is one. Returns seconds, whether it got stuck (3 s without moving) and what it rode.
    /// </summary>
    private async Task<(float Seconds, bool Stuck, string Uses)> Walk(FlowField flow, Vector3 goal, float near, float minutes)
    {
        var player = _stage.Player;
        var tiers = _stage.Tiers;
        int frames = 0, ladderUses = 0, ziplineUses = 0, gondolaRides = 0, lastMoved = 0;
        bool stuck = false;
        var last = player.GlobalPosition;
        var gondola = _map.GondolaStation!;
        while (frames < 60 * 60 * minutes && player.GlobalPosition.DistanceTo(goal) > near)
        {
            await Frames(1);
            frames++;
            if (player.OnLadder || player.OnZipline || gondola.Riding) { player.SetControls(Vector2.Zero, player.AimPoint, false); lastMoved = frames; continue; }
            var here = player.GlobalPosition;
            int cell = tiers.Index(here);
            int next = flow.NextCell(here);
            if (gondola.Aboard(player) && next >= 0 && flow.Center(next).DistanceTo(here with { Y = 0 }) > 3f)
            {
                if (gondola.TryDepart()) gondolaRides++;
                continue;
            }
            var zip = _stage.Ziplines.FirstOrDefault(z => z.StartCell == cell && next == z.EndCell);
            if (zip != null && player.RideZipline(zip)) { ziplineUses++; continue; }
            var ladder = _stage.Ladders.FirstOrDefault(l => (l.BottomCell == cell && next == l.TopCell) || (l.TopCell == cell && next == l.BottomCell));
            if (ladder != null && player.ClimbLadder(ladder, up: ladder.BottomCell == cell)) { ladderUses++; continue; }
            var dir = flow.Direction(here);
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

    private static Vector2 Centroid(StageLayout.Region region) => region.Poly.Aggregate(Vector2.Zero, (s, p) => s + p) / region.Poly.Length;
    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
