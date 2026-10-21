using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Capture: close-up frames of monsters dying (a heavy hit, then flying, landing and resting) for the death-look review (I-83).</summary>
public partial class DeathCapture : Node
{
    private Stage _stage = null!;
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Shot(string name)
    {
        for (int i = 0; i < 2; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/death-{name}.png"));
        GD.Print($"SHOT: output/death-{name}.png");
    }

    public override async void _Ready()
    {
        var rules = new CardLabRules { Name = "CardLabRules" };
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Survivor, RulesOverride = rules, DirectorEnabled = false };
        AddChild(_stage);
        await Frames(3);
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        _stage.Cam.Close = true;
        var center = player.GlobalPosition;
        string[] ids = { "plush", "wild_animal", "armed_animal", "humanoid", "bull" };
        var row = ids.Select((id, i) => (id, at: center + new Vector3(-4 + i * 2f, 0, -3))).ToList();
        var enemies = row.Select(r => _stage.Spawn(MonsterDatabase.Get(r.id), r.at, awake: false)).ToList();
        await Frames(20);
        await Shot("0-alive");
        foreach (var e in enemies) e.TakeHit(new HitInfo(9999, new Vector3(.3f, 0, -1).Normalized(), e.GlobalPosition + Vector3.Up, false, 90));
        foreach (var (t, name) in new[] { (4, "1-hit"), (12, "2-flying"), (30, "3-landing"), (90, "4-rest") })
        {
            await Frames(t);
            await Shot(name);
        }
        GetTree().Quit();
    }
}
