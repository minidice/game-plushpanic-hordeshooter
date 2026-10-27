using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Camera view comparison (L-2, L-14): the same spots in Stage 1 (street) and Stage 2 (forest) photographed through each
/// candidate <see cref="CameraView"/> (with <c>-- --candidates</c>) plus the map's own view; Stage 3 only its own view.
/// Windowed only; writes game/output/camera-view/*.png.
/// </summary>
public partial class CameraViewCapture : Node
{
    private static readonly (string Name, CameraView View)[] Candidates =
    {
        ("p55-s19-f38", new CameraView(55, CameraRig.Yaw, 19, 11.5f, true, 38)),
        ("p60-s19-f38", new CameraView(60, CameraRig.Yaw, 19, 11.5f, true, 38)),
        ("p60-s21-f34", new CameraView(60, CameraRig.Yaw, 21, 12.5f, true, 34)),
        ("p65-s21-f34", new CameraView(65, CameraRig.Yaw, 21, 12.5f, true, 34)),
    };

    private string _out = "";

    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("camera shots need a window (GPU)");
            _out = Path.Combine(ProjectSettings.GlobalizePath("res://"), "output", "camera-view");
            Directory.CreateDirectory(_out);
            GetWindow().Size = new Vector2I(1600, 900);
            foreach (var stage in new[] { 1, 2, 3 })
            {
                await Shoot(stage, "map", null);
                if (OS.GetCmdlineUserArgs().Contains("--candidates")) foreach (var (name, view) in Candidates) await Shoot(stage, name, view);
            }
            GD.Print("CAMERA_VIEW_CAPTURE_PASS");
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("CAMERA_VIEW_CAPTURE_FAIL " + e.Message); exit = 1; }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private async Task Shoot(int number, string name, CameraView? view)
    {
        foreach (var old in GetChildren().OfType<Stage>().ToList()) old.QueueFree();
        await Frames(3);
        var stage = number == 1
            ? new Stage { CharacterOverride = "duck", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0 }
            : new Stage { CharacterOverride = "duck", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0,
                ModeOverride = GameMode.Scenario, MapOverride = StageMap.Create(GameMode.Scenario, number) };
        AddChild(stage);
        await Frames(5);
        if (number == 1) stage.LeaveSafeRoom();
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        var player = stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        var at = number == 1 ? new Vector3(24, 0, 0)
            : stage.Flow.OpenCellsNear(stage.Map.PlayerStart + new Vector3(24, 0, 0), 0, 4).FirstOrDefault(stage.Map.PlayerStart);
        player.GlobalPosition = at;
        player.SetControls(Vector2.Zero, at + new Vector3(1, 1, 0), false);
        if (view is { } v)
        {
            stage.Cam.View = v;
            stage.Cam.Projection = v.Perspective ? Camera3D.ProjectionType.Perspective : Camera3D.ProjectionType.Orthogonal;
            stage.Cam.Arrive(v, .01f);
        }
        stage.Cam.SnapTo(at);
        await Frames(45);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var file = Path.Combine(_out, $"stage{number}-{name}.png");
        var error = GetViewport().GetTexture().GetImage().SavePng(file);
        if (error != Error.Ok) throw new IOException($"{file}: {error}");
        GD.Print($"saved stage{number}-{name} pitch {stage.Cam.CurrentPitch:0.0} distance {stage.Cam.FocusDistance:0.0}");
    }

    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
