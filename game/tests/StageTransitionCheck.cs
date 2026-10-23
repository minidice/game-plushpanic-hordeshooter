using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Scenario Stage 1 → Stage 2 (decisions.md L-14) and the outdoor kit (prototypes/outdoor-kit/doc/integration-guide.md):
/// shutting the Stage 1 safehouse opens its floor hatch instead of ending the run, holding E on it closes a circle on the
/// survivor, shows the next stage's card and builds Stage 2 with the carried kit, where the circle opens at the bunker
/// hatch while the camera eases from Stage 1's steep orthographic view into the lower outdoor perspective with haze.
/// Stage 2 itself: kit models and colliders, the route to its safehouse (and L-1's 70 % toward the screen's upper
/// right), protected arrival, supplies and encounters, and its clear. With a real window it saves output/transition-*.png.
/// Run: Godot --path . res://tests/StageTransitionCheck.tscn
/// </summary>
public partial class StageTransitionCheck : Node
{
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
        try { await Run(); GD.Print("STAGE_TRANSITION_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("STAGE_TRANSITION_CHECK_FAIL " + e.Message); failed = true; }
        StageTransition.LoadOverride = null;
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        KitLibrary();
        Check(StageMap.Create(GameMode.Scenario, 2) is StageTwoMap && StageMap.Create(GameMode.Scenario) is StageOneLayout
              && StageMap.Create(GameMode.Survival, 2) is StageOneLayout, "the scenario picks Stage 2 only once it has reached it");
        await StageOneExit();
        await StageTwo();
    }

    /// <summary>Every kit and transition model loads with its contract parts (guide 2장).</summary>
    private void KitLibrary()
    {
        Check(PropLibrary.Errors.Count == 0, "all prop manifests read without errors: " + string.Join("; ", PropLibrary.Errors));
        Check(PropLibrary.All.Count == 52, $"32 outdoor kit + 4 transition + 6 edge-kit + 5 ground bits + 5 traversal props are listed ({PropLibrary.All.Count})");
        var edge = PropLibrary.All.Values.Where(p => p.Id.StartsWith("edge_")).ToList();
        Check(edge.Count == 6 && edge.All(p => p.Collision.Kind == "none" && p.OcclusionParts.Length == 0 && p.File.Contains("/edge-kit/")),
            "the edge kit has no colliders and no fading parts (A-23)");
        foreach (var (id, info) in PropLibrary.All)
        {
            var model = PropLibrary.Instance(id);
            // Godot wraps the GLB's own root: <file>/<id>/Keep.
            Check(model?.FindChild("Keep", true, false) != null, $"{id} loads with its Keep part");
            foreach (var part in info.OcclusionParts)
                Check(model!.FindChild(part, true, false) is Node3D { } p && p.GetChildCount() > 0, $"{id} has the occlusion part {part} its manifest lists");
            model!.Free();
        }
        foreach (var id in new[] { "floor_hatch", "bunker_hatch" })
            Check(PropLibrary.Find(id)!.OcclusionParts.Contains("Occlude_Lid"), $"{id}'s tall lid is its own part");
    }

    private async Task StageOneExit()
    {
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, DirectorEnabled = false, NextStageOverride = true };
        AddChild(_stage);
        await Frames(4);
        var map = _stage.Map;
        var player = _stage.Player;
        player.Invulnerable = true;
        Check(map.View == CameraView.Indoor && _stage.Cam.Projection == Camera3D.ProjectionType.Orthogonal
              && Mathf.IsEqualApprox(_stage.Cam.Size, CameraRig.WideSize, .1f), "Stage 1 keeps its 55° orthographic view (L-2)");
        Check(map.StageExit is { Enabled: false, HoldSeconds: > 0 } && map.NextStage == 2, "the safehouse hatch to Stage 2 waits, closed, for the clear");
        Check(map.KitProps.Any(p => p.Name == "floor_hatch" && p.GlobalPosition.DistanceTo(map.StageExit!.GlobalPosition) < .1f), "the floor hatch model marks the exit");
        if (Windowed)
        {
            player.TeleportTo(new Vector3(28, 0, 0));
            _stage.Cam.SnapTo(new Vector3(28, 0, 0));
            await Frames(40);
            await Shot("transition-0-stage1-street");
        }

        // Through the stage quickly: door, crescendo, safehouse.
        _stage.LeaveSafeRoom();
        map.CrescendoSeconds = 1;
        player.TeleportTo(map.ShutterPanel!.GlobalPosition + Vector3.Left * .8f);
        await Frames(3);
        map.ShutterPanel.Use();
        await Until(() => _stage.Objective == Objective.ReachSafehouse, "the crescendo ends");
        foreach (var e in _stage.AllEnemies) e.QueueFree();
        player.Weapons.PlaceInSlot(2, ArmoryCatalog.Weapon("m4"));
        player.Weapons.SelectSlot(2);
        var carried = player.Loadout.Primary!;
        int mag = carried.Mag, reserve = carried.Reserve;
        player.TeleportTo(new Vector3(119, 0, 0));
        await Until(() => _stage.Objective == Objective.CloseSafehouse, "inside the safehouse");
        Check(_stage.TryCloseSafehouse(), "the safehouse door closes");
        Check(_stage.Objective == Objective.TakeExit && _stage.State == StageState.Playing && map.StageExit!.Enabled && !map.SafehouseDoor!.IsOpen,
            "shutting the safehouse opens the hatch instead of ending the run");
        Check(!_stage.Director.Active && player.Invulnerable, "the director stops and the survivors are safe while they leave");

        // Hold E: letting go early starts over (reference clip 7–8.5 s).
        player.TeleportTo(StageOneLayout.ExitHatch + new Vector3(-.6f, 0, .4f));
        await Frames(4);
        Check(_stage.FocusedInteractable == map.StageExit, "the hatch prompt shows next to it");
        if (Windowed)
        {
            _stage.Cam.SnapTo(player.GlobalPosition);
            await Frames(40);
            await Shot("transition-1-safehouse-hatch");
        }
        StageTransition.LoadOverride = LoadStageTwo;
        player.ManualControl = false;
        Input.ActionPress(GameInput.Interact);
        await Frames(Engine.PhysicsTicksPerSecond / 2);
        Check(_stage.HoldProgress is > .25f and < .7f && !StageTransition.Busy, $"half a second of holding fills the bar part way ({_stage.HoldProgress:0.00})");
        Input.ActionRelease(GameInput.Interact);
        await Frames(2);
        Check(_stage.HoldProgress == 0 && !StageTransition.Busy, "letting go early resets the hold");
        Input.ActionPress(GameInput.Interact);
        await Until(() => StageTransition.Busy, "holding long enough takes the hatch", 3);
        Input.ActionRelease(GameInput.Interact);
        Check(!map.StageExit!.Enabled && GameSession.Carry != null && GameSession.Carry.Primary == carried, "the survivor's kit is packed for the next stage");
        await Until(() => StageTransition.Closed > .5f, "the circle closes on the survivor");
        if (Windowed) await Shot("transition-2-closing");
        await Until(() => StageTransition.TitleShown, "black screen with the next stage's card", 4);
        if (Windowed) await Shot("transition-3-title");
        _carriedMag = mag;
        _carriedReserve = reserve;
        _carried = carried;
        await Until(() => _stage.Map is StageTwoMap, "the next stage loads", 6);
    }

    private int _carriedMag, _carriedReserve;
    private WeaponState _carried = null!;

    private void LoadStageTwo(SceneTree tree)
    {
        _stage.QueueFree();
        _stage = new Stage
        {
            CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, DirectorEnabled = false,
            NextStageOverride = true, ArrivalOverride = true, MapOverride = StageMap.Create(GameMode.Scenario, GameSession.ScenarioStage),
        };
        AddChild(_stage);
    }

    private async Task StageTwo()
    {
        var map = (StageTwoMap)_stage.Map;
        var player = _stage.Player;
        var cam = _stage.Cam;
        player.Invulnerable = true;
        Check(GameSession.ScenarioStage == 2 && GameSession.Carry == null, "the run is on Stage 2 and the carried kit was handed over");
        Check(player.Loadout.Primary == _carried && _carried.Mag == _carriedMag && _carried.Reserve == _carriedReserve && player.Loadout.ActiveSlot == 2,
            "the M4 comes along with its rounds and stays in hand");
        Check(cam.View == CameraView.Outdoor && cam.Projection == Camera3D.ProjectionType.Perspective, "Stage 2 is seen through the outdoor perspective view");
        Check(cam.Arriving && cam.CurrentPitch > CameraView.Outdoor.Pitch + 8, $"the view starts near Stage 1's steep angle ({cam.CurrentPitch:0.0}°)");
        Check(_stage.Objective == Objective.ReachSafehouse && map.StageExit == null && map.NextStage == 0, "Stage 2 starts on the route; its safehouse ends the run");
        Check(map.KitProps.Any(p => p.Name == "bunker_hatch" && p.GlobalPosition.DistanceTo(map.PlayerStart) < 4), "the survivors arrive next to the bunker hatch");
        var env = _stage.GetChildren().OfType<WorldEnvironment>().First().Environment;
        Check(env.FogEnabled && env.FogMode == Godot.Environment.FogModeEnum.Depth && cam.Haze == env, "outdoor haze follows the camera's distance");

        await Until(() => StageTransition.Closed is > .2f and < .8f, "the circle opens on the survivor", 4);
        if (Windowed) await Shot("transition-4-opening");
        await Until(() => !StageTransition.Busy && !StageTransition.Arriving, "the stage change finishes", 4);
        await Until(() => !cam.Arriving, "the camera settles", StageTransition.ArriveSeconds);
        Check(Mathf.IsEqualApprox(cam.CurrentPitch, CameraView.Outdoor.Pitch, .1f) && Mathf.IsEqualApprox(cam.Fov, CameraView.Outdoor.Fov, .1f),
            "the view ends at the outdoor angle and field of view");
        if (Windowed)
        {
            await Frames(20);
            await Shot("transition-5-arrival");
            cam.Close = true;
            await Frames(50);
            await Shot("transition-6-arrival-close");
            cam.Close = false;
        }

        // The ground: tiles, kit pieces, colliders.
        // Painted ground (A-24): dirt and asphalt fray into the grass instead of 4 m tiles cut square.
        var ground = map.Ground!;
        var hatch = new Vector2(StageTwoMap.ArrivalHatch.X, StageTwoMap.ArrivalHatch.Z);
        Check(ground != null && _stage.GetChildren().Any(n => n.Name.ToString().StartsWith("PaintedGround")) && !map.Scattered.ContainsKey("ground_grass"),
            "the stage lays painted ground, not kit tiles");
        Check(ground!.Weight(GroundKind.Dirt, hatch) > .95f && ground.Weight(GroundKind.Asphalt, new Vector2(85, 0)) > .95f &&
              ground.Weight(GroundKind.Gravel, new Vector2(85, 2.4f)) > .5f && ground.Weight(GroundKind.Dirt, new Vector2(40, -6)) < .05f &&
              ground.Lane.W > 0, "dirt round the hatch, asphalt with a gravel shoulder and a centre line in the yard, grass elsewhere");
        var edgeWeights = Enumerable.Range(0, 40).Select(i => ground.Weight(GroundKind.Dirt, new Vector2(24 + i, 2.1f))).ToList();
        Check(edgeWeights.Max() - edgeWeights.Min() > .3f, "the dirt road's edge wanders instead of running straight");
        var bits = map.GroundBits;
        int grass = bits.GetValueOrDefault("bit_grass_tuft") + bits.GetValueOrDefault("bit_grass_clump");
        int stones = bits.GetValueOrDefault("bit_stone") + bits.GetValueOrDefault("bit_pebbles");
        Check(grass > 150 && stones > 20 && grass > stones * 3 && bits.GetValueOrDefault("bit_gravel") > 10,
            "grass tufts lead, loose stones stay few, gravel on the shoulders: " + string.Join(", ", bits.Select(kv => $"{kv.Key} {kv.Value}")));
        var bitSpots = map.GroundBitSpots;
        Check(bitSpots.Count == bits.Values.Sum() && bitSpots.All(p => ground.Artificial(p) <= .3f), "no grass or stone lies on asphalt (artificial ground)");
        Check(bitSpots.Count(p => ground.Weight(GroundKind.Dirt, p) < .01f && ground.Weight(GroundKind.Gravel, p) < .01f && Mathf.Abs(p.Y) > 4) > 100,
            $"bits are scattered over the open grass, not only along the paths ({bitSpots.Count(p => ground.Weight(GroundKind.Dirt, p) < .01f && ground.Weight(GroundKind.Gravel, p) < .01f && Mathf.Abs(p.Y) > 4)} of {bitSpots.Count})");
        Check(_stage.GetChildren().OfType<MultiMeshInstance3D>().Where(m => m.Name.ToString().StartsWith("bit_"))
            .All(m => m.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off), "ground bits cast no shadows");
        var placed = map.KitProps.Select(p => p.Name.ToString()).Distinct().ToList();
        foreach (var id in new[] { "bunker_hatch", "warning_sign", "concrete_barrier", "container_closed", "wire_fence", "boulder",
                     "stream_straight", "timber_bridge", "corrugated_fence", "streetlamp", "utility_shed", "sandbags", "barrel" })
            Check(placed.Contains(id), $"Stage 2 places {id}");
        Check(_stage.GetChildren().OfType<MultiMeshInstance3D>().Any(m => m.Name.ToString().StartsWith("CloseRockBank")),
            "close rock outcrops replace the repeated cliff-segment wall");
        // Ground nobody can reach is packed with the edge kit (A-23).
        var edges = map.EdgeCounts;
        Check(edges.GetValueOrDefault("Pine")>80 && edges.GetValueOrDefault("Broadleaf")>10 && edges.GetValueOrDefault("Shrub")>100,
            "coherent trees and independent shrubs pack the unreachable ground: " + string.Join(", ", edges.Select(kv => $"{kv.Key} {kv.Value}")));
        var dressing = _stage.GetChildren().OfType<MultiMeshInstance3D>().Where(m => m.Name.ToString().StartsWith("Woodland")).ToList();
        Check(dressing.Count > 0 && !_stage.FindChildren("Woodland*", nameof(StaticBody3D), true, false).Any(), "the dressing is instanced and has no physics");
        Check(dressing.Any(m => m.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off) &&
              dressing.Any(m => m.CastShadow == GeometryInstance3D.ShadowCastingSetting.On),
            "distant and camera-side foliage omits shadows while the nearby far-side canopy casts them");
        Check(_stage.FindChild("FarGround", true, false) is MeshInstance3D && _stage.Cam.Bounds == map.CameraBounds && map.CameraBounds != null,
            "a far ground plane lies under everything and the camera keeps to the route");
        var flow = _stage.Flow;
        Check(!flow.IsBlocked(map.PlayerStart) && _stage.Squad.Count == 0, "the arrival spot is open");
        var bridge = new Vector3((StageTwoMap.StreamX0 + StageTwoMap.StreamX1) / 2, 0, 0);
        Check(!flow.IsBlocked(bridge) && flow.IsBlocked(bridge + Vector3.Back * 5), "the bridge is the one way over the stream");

        // The route: reachable, and mostly toward the screen's upper right (L-1, measured by floor distance, L-3).
        Check(_stage.ExitFlow.Reachable(map.PlayerStart), "the safehouse can be reached from the hatch");
        var path = new List<Vector3> { map.PlayerStart };
        for (int i = 0; i < 1200 && path[^1].DistanceTo(map.RouteGoal) > 1.5f; i++)
            path.Add(path[^1] + _stage.ExitFlow.Direction(path[^1]) * .5f);
        Check(path[^1].DistanceTo(map.RouteGoal) <= 1.5f, $"following the route reaches the safehouse ({path.Count} steps)");
        var right = cam.GlobalBasis.X with { Y = 0 };
        var up = -cam.GlobalBasis.Z with { Y = 0 };
        right = right.Normalized();
        up = up.Normalized();
        float total = 0, upRight = 0;
        for (int i = 1; i < path.Count; i++)
        {
            var d = path[i] - path[i - 1];
            float angle = Mathf.RadToDeg(Mathf.Atan2(d.Dot(up), d.Dot(right)));
            total += d.Length();
            if (angle is >= 15 and <= 75) upRight += d.Length();
        }
        Check(upRight / total >= .7f, $"{upRight / total:P0} of the route runs toward the screen's upper right (L-1)");

        // Arrival stays quiet (L-11) and the data is clean.
        var enc = map.Encounters!;
        Check(SpawnDatabase.Current.Errors.Count == 0 && StageSupplies.Stage2.Errors.Count == 0, "Stage 2 encounters and supplies read without errors: "
            + string.Join("; ", SpawnDatabase.Current.Errors.Concat(StageSupplies.Stage2.Errors)));
        float protect = SpawnDatabase.Current.Rules.ProtectRadius;
        Check(enc.Protected.Any(p => p.DistanceTo(StageTwoMap.ArrivalHatch) < .1f), "the arrival hatch is a protected landing");
        Check(enc.Entries.All(e => Flat(e.At).DistanceTo(Flat(StageTwoMap.ArrivalHatch)) > protect)
              && enc.Groups.Where(g => g.Special >= 0).All(g => g.At.DistanceTo(StageTwoMap.ArrivalHatch) > protect)
              && enc.Groups.Where(g => g.Special < 0).All(g => g.Rect.Position.X > StageTwoMap.ArrivalHatch.X + protect),
            "nothing waits or comes in near the arrival hatch");
        var stranded = enc.Entries.Where(e => !_stage.ExitFlow.Reachable(e.Ground)).Select(e => $"({e.At.X}, {e.At.Z})");
        Check(!stranded.Any(), "every entry can walk onto the route " + string.Join(" ", stranded));
        var supplies = StageSupplies.Stage2;
        Check(supplies.Points.Count(p => p.Kind == SupplyKind.Start) == 1 && supplies.Points.Count(p => p.Kind == SupplyKind.Main) == 1, "a start kit by the hatch and one main supply point");
        Check(supplies.Points.SelectMany(p => p.Items).All(item => _stage.ExitFlow.Reachable(item.At)), "every Stage 2 item can be walked to");

        if (Windowed)
        {
            foreach (var (name, at) in new[] { ("forest", new Vector3(38, 0, 0)), ("bridge", new Vector3(62, 0, 0)), ("yard", new Vector3(82, 0, 0)), ("safehouse", new Vector3(98, 0, 0)) })
            {
                player.TeleportTo(at);
                cam.SnapTo(at);
                await Frames(45);
                await Shot("transition-7-" + name);
            }
        }

        // Stage 2's safehouse ends the run (there is no Stage 3 yet).
        foreach (var e in _stage.AllEnemies) e.QueueFree();
        player.TeleportTo(new Vector3(106, 0, 0));
        await Until(() => _stage.Objective == Objective.CloseSafehouse, "inside the Stage 2 safehouse");
        Check(_stage.TryCloseSafehouse() && _stage.State == StageState.Cleared && !map.SafehouseDoor.IsOpen, "closing the Stage 2 safehouse clears the run");
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/{name}.png"));
    }
}
