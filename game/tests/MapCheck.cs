using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The map interface (decisions.md L-13): Stage 1 still builds and syncs as before, and a different map plugs into a
/// Stage without any Stage 1 pieces — a flat test field runs Onslaught and Outpost on its own start, area and spots.
/// With a real window it also saves output/map-*.png.
/// </summary>
public partial class MapCheck : Node
{
    /// <summary>A bare 40 × 40 m field with a few crates: no doors, no route, no scenario pieces.</summary>
    private sealed class FlatTestMap : StageMap
    {
        public override Rect2 Area => new(-22, -22, 44, 44);

        protected override void BuildMap()
        {
            PlayerStart = Vector3.Zero;
            Floor(-20, 20, -20, 20, "3a4a36");
            Solid(new Vector3(6, .6f, 4), new Vector3(1.2f, 1.2f, 1.2f), "8a7755");
            Solid(new Vector3(-7, .6f, -5), new Vector3(1.2f, 1.2f, 1.2f), "8a7755");
            Lamp(new Vector3(0, 4, 0), "ffe6b0", 4, 30, false);
        }
    }

    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Until(Func<bool> done, string what, float seconds = 10)
    {
        for (int i = 0; i < seconds * Engine.PhysicsTicksPerSecond; i++)
        {
            if (done()) { Check(true, what); return; }
            await Frames(1);
        }
        Check(done(), what + " (timed out)");
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("MAP_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("MAP_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(GameMode mode, StageMap? map = null)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = mode, MapOverride = map };
        AddChild(_stage);
        await Frames(3);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        return _stage;
    }

    private static string Labels(Node node) =>
        string.Join("|", node.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/map-{name}.png"));
        GD.Print($"SHOT: output/map-{name}.png");
    }

    private async Task Run()
    {
        // ---------- Stage 1 behind the interface ----------
        foreach (var mode in Enum.GetValues<GameMode>())
            Check(StageMap.Create(mode) is StageOneLayout, $"{mode} still plays Stage 1 (Onslaught's own field comes next)");
        var stage = await NewStage(GameMode.Scenario);
        var map = stage.Map;
        Check(map is StageOneLayout && map.HasRoute && map.Doors.Count == 3 && map.AlarmDevice != null && map.Encounters != null,
            "Stage 1 offers its route, three shared doors, the car alarm and its encounters");
        Check(stage.Flow.Width == Mathf.CeilToInt(map.Area.Size.X) && stage.Flow.Depth == Mathf.CeilToInt(map.Area.Size.Y),
            $"the flow field is sized from the map's area ({stage.Flow.Width}×{stage.Flow.Depth})");
        Check(stage.Player.GlobalPosition.DistanceTo(map.PlayerStart) < .5f && stage.Progress(map.PlayerStart) > stage.Progress(map.RouteGoal),
            "survivors start at the map's start, and the route still runs to the safehouse");
        Check(map.Doors[1] == stage.Layout.Shutter && Mathf.IsEqualApprox(stage.Layout.Shutter.OpenSeconds, 1.2f), "door order and the shutter's slower opening are kept for the network");
        stage = await NewStage(GameMode.Survival);
        Check(!stage.Map.HasRoute && stage.Progress(new Vector3(100, 0, 0)) == 0 && stage.Map.Gates.Count == 2,
            "Outpost on Stage 1 has no route to stay behind and two doorways to come in from");

        // ---------- a different map plugs in ----------
        stage = await NewStage(GameMode.Survivor, new FlatTestMap());
        Check(stage.Map is FlatTestMap && stage.Player.GlobalPosition.DistanceTo(Vector3.Zero) < .5f, "a flat test map builds and the player starts on it");
        Check(stage.Flow.Width == 44 && !stage.Flow.IsBlocked(new Vector3(15, 0, -15)) && stage.Flow.IsBlocked(new Vector3(6, 0, 4)) && stage.Flow.IsBlocked(new Vector3(21.5f, 0, 0)),
            "its floor is open, its crates and the edge are blocked");
        Check(!stage.Map.HasRoute && stage.RouteForward(Vector3.Zero) == stage.Player.AimDirection && stage.Map.Doors.Count == 0 && stage.Map.AlarmDevice == null,
            "no route, doors or alarm, and nothing asks for Stage 1's pieces");
        Check(stage.Hud.FindChild("ObjectivePanel", true, false) is Control panel && !Labels(panel).Contains(Loc.Text("ui_hud_stage")),
            "the HUD title does not call another map Stage 1");
        var survivor = (SurvivorRules)stage.ModeRules!;
        await Until(() => survivor.SpawnedThisMatch > 0, "Onslaught's swarm comes in on the flat map");
        Check(stage.AllEnemies.All(e => Mathf.Abs(e.GlobalPosition.X) < 21 && Mathf.Abs(e.GlobalPosition.Z) < 21), "the swarm stays on the map");
        await Frames(60);
        await Shot("flat-onslaught");

        stage = await NewStage(GameMode.Survival, new FlatTestMap());
        var outpost = (SurvivalRules)stage.ModeRules!;
        Check(outpost.Core!.GlobalPosition.DistanceTo(stage.Map.CoreSpot) < .01f, "the generator stands on the map's core spot");
        stage.AdvanceClockForTest(outpost.Rules.Prep + .1f);
        await Until(() => outpost.SpawnedThisMatch > 0, "Outpost waves come in without doorways (a ring around the generator)");
        Check(stage.AllEnemies.All(e => e.GlobalPosition.DistanceTo(outpost.Core.GlobalPosition) > 10), "they arrive away from the generator");
    }
}
