using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Flow field cost and results (issues.md I-109): builds Stage 1, today's Stage 2 and the Stage 2 size draft, times the
/// wall stamping (a door opening re-stamps everything) and the rebuilds toward fixed targets for every way of climbing,
/// and prints a fingerprint of every cell's cost. The fingerprints must not change when the field is only made faster:
/// compare the FLOW_HASH lines before and after. Prints BENCH lines; always passes unless a map fails to build.
/// </summary>
public partial class FlowBench : Node
{
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            await Bench("stage1", new StageOneLayout(), new[] { new Vector3(10, 0, 0), new Vector3(60, 0, 2), new Vector3(110, 0, -2) });
            await Bench("stage2", new StageTwoMap(), new[] { new Vector3(8, 0, 0), new Vector3(50, 0, 0), new Vector3(90, 0, 0) });
            await Bench("draft", new LayoutMap(), new[] { new Vector3(100, 0, -20), new Vector3(330, 0, -50), new Vector3(470, 0, -90), new Vector3(200, 0, -12) });
            GD.Print("FLOW_BENCH_PASS");
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("FLOW_BENCH_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Bench(string name, StageMap map, Vector3[] targets)
    {
        var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0 };
        AddChild(stage);
        await Frames(3);
        foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(1);

        var watch = Stopwatch.StartNew();
        const int stamps = 3;
        for (int i = 0; i < stamps; i++) map.ApplyFlow();
        double stamp = watch.Elapsed.TotalMilliseconds / stamps;
        // A door opening mid-fight: every field in use is re-stamped, then rebuilt toward the survivors on the next frame.
        var fields = new List<FlowField> { stage.Flow };
        var survivor = stage.Player.GlobalPosition;
        if (stage.Tiers.Any)
            foreach (var climb in new[] { MonsterClimb.Wall, MonsterClimb.Jump, MonsterClimb.Fly })
            {
                // What the game pays when the first monster of a climb type shows up (step costs come from the load).
                var t = Stopwatch.StartNew();
                var chase = stage.FlowFor(climb);
                chase.Update(survivor);
                GD.Print($"BENCH {name}: first {climb} chase field in play {t.Elapsed.TotalMilliseconds:0.0} ms");
                fields.Add(chase);
            }
        foreach (var field in fields) field.Update(survivor);
        watch.Restart();
        stage.RebuildFlow();
        double restamp = watch.Elapsed.TotalMilliseconds;
        var parts = new List<string>();
        foreach (var field in fields)
        {
            var t = Stopwatch.StartNew();
            field.Update(survivor);
            parts.Add($"{field.Mover} {t.Elapsed.TotalMilliseconds:0.0}");
        }
        double rebuild = watch.Elapsed.TotalMilliseconds;
        GD.Print($"BENCH {name}: door parts: RebuildFlow (stamp + route fields) {restamp:0.0} ms, then {string.Join(", ", parts)} ms");
        GD.Print($"BENCH {name}: grid {stage.Flow.Width}×{stage.Flow.Depth}, stamp walls {stamp:0.0} ms, door opens (re-stamp + rebuild {fields.Count} chase fields + route fields) {rebuild:0.0} ms");

        var climbs = stage.Tiers.Any ? new[] { MonsterClimb.Ladder, MonsterClimb.Wall, MonsterClimb.Jump, MonsterClimb.Fly } : new[] { MonsterClimb.Ladder };
        foreach (bool riders in new[] { false, true })
            foreach (var climb in climbs)
            {
                var field = stage.Flow.CopyLayout();
                field.Mover = climb;
                field.Riders = riders;
                var times = new List<double>();
                ulong hash = 1469598103934665603;
                foreach (var target in targets)
                {
                    var at = target with { Y = stage.Tiers.GroundY(target) };
                    // First build (layout work included) and a second one toward a neighbour cell (the usual case).
                    var t = Stopwatch.StartNew();
                    field.Update(at);
                    times.Add(t.Elapsed.TotalMilliseconds);
                    hash = Fold(hash, field);
                    t.Restart();
                    field.Update(at + new Vector3(1, 0, 0));
                    times.Add(t.Elapsed.TotalMilliseconds);
                    hash = Fold(hash, field);
                }
                // The first build of a fresh copy also works out its step costs; the rest are what a moving target costs.
                var steady = times.Skip(1).ToList();
                GD.Print($"BENCH {name} {climb}{(riders ? " riders" : "")}: first {times[0]:0.00} ms, then {steady.Average():0.00} ms average, {steady.Max():0.00} ms worst over {steady.Count} builds");
                GD.Print($"FLOW_HASH {name} {climb}{(riders ? " riders" : "")} {hash:x16}");
                if (!stage.Tiers.Any) break;
            }
        stage.Free();
        await Frames(1);
    }

    /// <summary>FNV-1a over every cell's distance and its direction (what walkers actually read).</summary>
    private static ulong Fold(ulong hash, FlowField field)
    {
        for (int i = 0; i < field.Width * field.Depth; i++)
        {
            var c = field.Center(i);
            int d = field.Distance(c);
            var dir = field.Direction(c);
            foreach (int v in new[] { d, Mathf.RoundToInt(dir.X * 1000), Mathf.RoundToInt(dir.Z * 1000) })
            {
                hash ^= (uint)v;
                hash *= 1099511628211;
            }
        }
        return hash;
    }
}
