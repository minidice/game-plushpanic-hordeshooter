using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Run windowed with --fixed-fps 60 for an actual 60 fps hit/death sequence, including corpse physics.</summary>
public partial class CaptureDamageEffects : Node
{
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    public override async void _Ready()
    {
        var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0, ReviveOverride = false };
        AddChild(stage); await Frames(3);
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        var player = stage.Player;
        player.ManualControl = true;
        player.TeleportTo(new Vector3(14, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(20, 1, 0), false);
        var enemy = stage.Spawn(MonsterDatabase.Get("humanoid"), new Vector3(17, 0, 0), false);
        enemy.SetPhysicsProcess(false);
        await Frames(3);
        stage.SetProcess(false); stage.SetPhysicsProcess(false); // Hold scenario/visibility, retain actors and corpse physics.
        stage.Hud.Hide();
        enemy.Rig.ShowAs(VisionSystem.Visibility.Visible);
        stage.Cam.SetProcess(false);
        stage.Cam.Size = 10;
        stage.Cam.Position = new Vector3(15.5f, 10, 12);
        stage.Cam.LookAt(new Vector3(15.5f, .7f, 0));
        await Frames(30); // Finish the silhouette-to-color reveal before the comparison.
        string dir = ProjectSettings.GlobalizePath("res://output/damage-fx-frames");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        for (int frame = 0; frame < 150; frame++)
        {
            if (frame == 18)
            {
                player.TakeDamage(5, Vector3.Right, 0);
                enemy.TakeHit(new HitInfo(1, Vector3.Right, enemy.GlobalPosition + Vector3.Up, true, 0));
            }
            if (frame == 66)
                enemy.TakeHit(new HitInfo(99999, Vector3.Right, enemy.GlobalPosition + Vector3.Up, true, 0));
            if (frame == 102) player.TakeDamage(99999, Vector3.Left, 0);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/frame-{frame:000}.png");
        }
        GD.Print("DAMAGE_EFFECTS_CAPTURE_PASS 150 frames");
        QuitHygiene.Collect(); GetTree().Quit();
    }
}
