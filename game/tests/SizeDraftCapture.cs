using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Renders the Stage 2 size draft (<see cref="LayoutMap"/>, L-29) from the default outdoor camera at one spot per zone,
/// to output/sizedraft-*.png. Needs a window (or a virtual display); with the compatibility renderer the light and
/// fog differ from the game's Forward+ look, so it shows size and layout, not the final picture.
/// </summary>
public partial class SizeDraftCapture : Node
{
    private static readonly (string name, Vector2 at, Vector2 look)[] Spots =
    {
        ("a-hatch", new(8, -2), new(30, -6)),
        ("b-forest", new(95, -22), new(130, -35)),
        ("c-bridge", new(178, -44), new(215, -46)),
        ("c-canyon", new(196, -20), new(196, -50)),
        ("d-safezone", new(236, -50), new(260, -50)),
        ("e-yard", new(330, -50), new(370, -60)),
        ("f-plateau", new(490, -100), new(520, -125)),
        ("g-arrival", new(605, -142), new(630, -145)),
    };

    public override async void _Ready()
    {
        var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = new LayoutMap(), DirectorEnabled = false, SquadOverride = 0 };
        AddChild(stage);
        for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        stage.Player.ManualControl = true;
        stage.Player.Invulnerable = true;
        foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        foreach (var (name, at, look) in Spots)
        {
            var ground = new Vector3(at.X, stage.Tiers.GroundY(new Vector3(at.X, 0, at.Y)) + .05f, at.Y);
            stage.Player.TeleportTo(ground);
            stage.Player.SetControls(Vector2.Zero, new Vector3(look.X, ground.Y + 1, look.Y), false);
            stage.Cam.SnapTo(ground);
            for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var path = ProjectSettings.GlobalizePath($"res://output/sizedraft-{name}.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print("SHOT: " + path);
        }
        stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(0);
    }
}
