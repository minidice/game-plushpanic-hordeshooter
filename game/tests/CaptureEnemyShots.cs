using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Enemy shots frozen mid-flight in Stage 1 (I-66): gunshot tracers from an armed animal and a veteran, and a vomit blob,
/// with the default and close camera. Needs a real window. Run: Godot --path . res://tests/CaptureEnemyShots.tscn
/// </summary>
public partial class CaptureEnemyShots : Node
{
    public override async void _Ready()
    {
        var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "armed", DirectorEnabled = false };
        AddChild(stage);
        await Frames(5);
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        var player = stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        stage.LeaveSafeRoom();
        player.TeleportTo(new Vector3(22, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(34, 1, 0), false);
        stage.Cam.SnapTo(player.GlobalPosition);
        await Frames(30);

        var target = player.GlobalPosition + Vector3.Up * .9f;
        Freeze(stage, new Vector3(32, .9f, -2.5f), target, "armed_animal", .45f);
        Freeze(stage, new Vector3(33, .9f, 2), target, "armed_veteran", .6f);
        Freeze(stage, new Vector3(31, .9f, .5f), target, "spitter", .5f);
        await Frames(10);
        await Shot("enemy-shots");
        stage.Cam.Close = true;
        await Frames(40);
        await Shot("enemy-shots-close");
        GD.Print("ENEMY_SHOTS_CAPTURE_DONE");
        QuitHygiene.Collect();
        GetTree().Quit();
    }

    /// <summary>A shot of <paramref name="monster"/> placed part of the way to the target and stopped there.</summary>
    private static void Freeze(Stage stage, Vector3 from, Vector3 target, string monster, float along)
    {
        var shot = new EnemyProjectile();
        shot.Setup(stage, from.Lerp(target, along), target, MonsterDatabase.Get(monster).Ranged!, 0);
        stage.AddChild(shot);
        shot.SetPhysicsProcess(false);
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/{name}.png"));
    }
}
