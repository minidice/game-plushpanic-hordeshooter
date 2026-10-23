using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The Stage 2 size draft (decisions.md L-29, doc/map-expansion-plan.md 7.2 step 3): builds <see cref="LayoutMap"/> from
/// content/data/stages/stage2_layout.json, checks every part of the layout was built, follows the exit route and measures
/// it (walking length, L-1 share on the main route, rides), times the flow field rebuilds on the big grid, and walks a
/// survivor from the hatch to the safehouse with no monsters, using ramps, ladders, ziplines and the gondola as the
/// route says. Run with --fixed-fps 60 so the walk is simulated faster than real time. Prints MEASURE lines.
/// </summary>
public partial class SizeDraftCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
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
        var watch = Stopwatch.StartNew();
        var map = new LayoutMap();
        Check(map.Layout.Errors.Count == 0, "the layout data loads without errors" + string.Join("; ", map.Layout.Errors));
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(3);
        Measure($"build {watch.ElapsedMilliseconds} ms, {map.FloorRects} floor boxes, {map.VoidRects} blockers, grid {_stage.Tiers.Width}×{_stage.Tiers.Depth} = {_stage.Tiers.Width * _stage.Tiers.Depth} cells");
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        foreach (var e in _stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
        var tiers = _stage.Tiers;

        // ---------- everything in the layout was built ----------
        Check(tiers.Any && tiers.MaxTier == 4, "the draft has tiers 0 to 4");
        int ladders = map.Layout.Links.Count(l => l.Type == "ladder"), ziplines = map.Layout.Links.Count(l => l.Type == "zipline");
        Check(_stage.Ladders.Count == ladders && _stage.Ziplines.Count == ziplines && map.Gondola != null,
            $"every ladder ({ladders}), zipline ({ziplines}) and the gondola were built");
        Check(TierMap.TierOfHeight(player.GlobalPosition.Y) == 0 && player.GlobalPosition.DistanceTo(map.PlayerStart) < 1.5f, "the survivor starts at the bunker hatch on the ground");
        foreach (var region in map.Layout.Regions)
        {
            var c = region.Poly.Aggregate(Vector2.Zero, (s, p) => s + p) / region.Poly.Length;
            if (map.Layout.TierAt(c) != region.Tier) continue;
            Check(tiers.TierAt(new Vector3(c.X, 0, c.Y)) == region.Tier, $"region {region.Id} stands at tier {region.Tier}");
        }

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

        // ---------- walk it ----------
        int frames = 0, stuckFrames = 0, ladderUses = 0, ziplineUses = 0, gondolaRides = 0;
        var last = player.GlobalPosition;
        var lastMoved = 0;
        while (frames < 60 * 60 * 10 && player.GlobalPosition.DistanceTo(map.RouteGoal) > 2f)
        {
            await Frames(1);
            frames++;
            if (player.OnLadder || player.OnZipline || map.Gondola!.Riding) { player.SetControls(Vector2.Zero, player.AimPoint, false); lastMoved = frames; continue; }
            var here = player.GlobalPosition;
            int cell = tiers.Index(here);
            int next = exit.NextCell(here);
            if (map.Gondola.Aboard(player) && next >= 0 && exit.Center(next).DistanceTo(here with { Y = 0 }) > 3f)
            {
                if (map.Gondola.TryDepart()) gondolaRides++;
                continue;
            }
            var zip = _stage.Ziplines.FirstOrDefault(z => z.StartCell == cell && next == z.EndCell);
            if (zip != null && player.RideZipline(zip)) { ziplineUses++; continue; }
            var ladder = _stage.Ladders.FirstOrDefault(l => (l.BottomCell == cell && next == l.TopCell) || (l.TopCell == cell && next == l.BottomCell));
            if (ladder != null && player.ClimbLadder(ladder, up: ladder.BottomCell == cell)) { ladderUses++; continue; }
            var dir = exit.Direction(here);
            player.SetControls(new Vector2(dir.X, dir.Z), here + dir * 5 + Vector3.Up, false);
            if (here.DistanceTo(last) > .5f) { last = here; lastMoved = frames; }
            if (frames - lastMoved > 180)
            {
                stuckFrames = frames;
                break;
            }
        }
        float seconds = frames / 60f;
        Measure($"walk: {seconds / 60:0.0} min simulated, ladders {ladderUses}, ziplines {ziplineUses}, gondola {gondolaRides}" +
                (stuckFrames > 0 ? $", STUCK at {player.GlobalPosition} tier {tiers.TierOf(player)} cell tier {tiers.TierAt(player.GlobalPosition)}" : ""));
        Check(stuckFrames == 0, $"the survivor never gets stuck on the way ({player.GlobalPosition})");
        Check(player.GlobalPosition.DistanceTo(map.RouteGoal) <= 2f, $"a survivor walks from the hatch to the safehouse with no monsters in {seconds / 60:0.0} min");
    }
}
