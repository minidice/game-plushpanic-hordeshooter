using Godot;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Renders the Stage 1 supply spots with the default and close camera to output/supply-&lt;tag&gt;-*.png, so item
/// placement and pickup emphasis can be compared before/after a change (scenario-level-design.md 5.4, 7장).
/// Needs a real window. Run: Godot --path . res://tests/SupplyCapture.tscn -- --tag=after
/// </summary>
public partial class SupplyCapture : Node
{
    private static readonly (string name, Vector3 at, Vector3 aim)[] Spots =
    {
        ("start", new Vector3(5, 0, 0), new Vector3(9, 1, 0)),
        ("street", new Vector3(17, 0, 1), new Vector3(23, 1, 4)),
        ("warehouse", new Vector3(62, 0, 0), new Vector3(70, 1, 0)),
        ("yard", new Vector3(90, 0, 0), new Vector3(96, 1, -2)),
    };

    public override async void _Ready()
    {
        string tag = "now";
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--tag=")) tag = arg["--tag=".Length..];

        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        await Frames(5);
        var player = stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        stage.LeaveSafeRoom();
        foreach (var (name, at, aim) in Spots)
        {
            // Monsters would hide the items; the shot is about the floor.
            foreach (var e in stage.AllEnemies) e.QueueFree();
            player.TeleportTo(at);
            player.SetControls(Vector2.Zero, aim, false);
            stage.Cam.Close = false;
            stage.Cam.SnapTo(at);
            await Frames(40);
            await Shot($"supply-{tag}-{name}");
            stage.Cam.Close = true;
            await Frames(40);
            await Shot($"supply-{tag}-{name}-close");
        }
        GD.Print("SUPPLY_CAPTURE_DONE " + tag);
        QuitHygiene.Collect();
        GetTree().Quit();
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/{name}.png"));
    }
}
