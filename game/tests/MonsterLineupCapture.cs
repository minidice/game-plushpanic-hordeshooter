using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Capture: every monster standing in two rows on a bare lit floor under the game camera, turned three ways
/// (toward the camera, side-on, away), for the model review (doc/model-briefs). Saves output/lineup-*.png.
/// </summary>
public partial class MonsterLineupCapture : Node
{
    private static readonly string[] Ids = { "plush", "wild_animal", "armed_animal", "humanoid", "crawler", "tether", "leaper", "spitter", "balloon", "bull", "boss" };

    /// <summary>A bare floor, nothing to hide behind.</summary>
    private sealed class BareFloor : StageMap
    {
        public override Rect2 Area => new(-22, -22, 44, 44);

        protected override void BuildMap()
        {
            PlayerStart = new Vector3(0, 0, 12);
            Floor(-20, 20, -20, 20, "4a5560");
        }
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Shot(string name)
    {
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/lineup-{name}.png"));
        GD.Print($"SHOT: output/lineup-{name}.png");
    }

    public override async void _Ready()
    {
        var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Survivor, MapOverride = new BareFloor(),
            RulesOverride = new CardLabRules { Name = "CardLabRules" }, DirectorEnabled = false };
        AddChild(stage);
        await Frames(3);
        stage.Player.ManualControl = true;
        stage.Player.Invulnerable = true;
        // Two rows, the bigger ones (named, boss) in the back row.
        var enemies = Ids.Select((id, i) =>
        {
            var at = i < 6 ? new Vector3(-6 + i * 2.4f, 0, 1.5f) : new Vector3(-6 + (i - 6) * 3.2f, 0, -2.5f);
            return stage.Spawn(MonsterDatabase.Get(id), at, awake: false);
        }).ToList();
        await Frames(10);
        // Fully lit and visible: no vision cone, a sun overhead.
        stage.Vision.SetPhysicsProcess(false);
        stage.Player.TeleportTo(new Vector3(0, 0, -.5f));
        await Frames(5);
        foreach (var e in enemies) { e.Awake = true; e.SetPhysicsProcess(false); e.Rig.ShowAs(VisionSystem.Visibility.Visible); }
        stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), LightEnergy = 1.4f });
        stage.Player.Visible = false;
        stage.Cam.Close = true;
        stage.Cam.Target = null;
        stage.Cam.SnapTo(new Vector3(1, .8f, -1.2f));
        foreach (var (facing, name) in new[] { (new Vector3(.3f, 0, 1), "front"), (Vector3.Right, "side"), (new Vector3(-.4f, 0, -1), "back") })
        {
            foreach (var e in enemies) e.Rig.Face(facing);
            // The fade from the dark into full colour takes a moment.
            ulong until = Time.GetTicksMsec() + 1500;
            while (Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shot(name);
        }

        // Behaviour: the bull winding up its charge, the balloon swelling and then popping (Head hidden, body left).
        foreach (var e in enemies.Where(e => e.Monster.Id is not ("bull" or "balloon"))) e.QueueFree();
        var bull = (BullEnemy)enemies.First(e => e.Monster.Id == "bull");
        var balloon = (BalloonEnemy)enemies.First(e => e.Monster.Id == "balloon");
        stage.Player.TeleportTo(new Vector3(0, 0, 3));
        foreach (var e in new Enemy[] { bull, balloon }) e.SetPhysicsProcess(true);
        bull.StartWindup(new Vector3(0, 0, 1));
        await Frames(25);
        await Shot("bull-windup");
        balloon.GlobalPosition = new Vector3(-2.5f, 0, 1.5f);
        ulong limit = Time.GetTicksMsec() + 6000;
        while (!balloon.Popped && Time.GetTicksMsec() < limit && IsInstanceValid(balloon) && balloon.Rig.Model.Scale.X < 1.3f) await Frames(1);
        balloon.Rig.ShowAs(VisionSystem.Visibility.Visible);
        await Shot("balloon-inflate");
        balloon.Pop();
        await Frames(30);
        await Shot("balloon-popped");

        // Onslaught's bosses side by side at their game size (bosses/<id>.json scale): the final one and the middle ones.
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        await Frames(5);
        var bossIds = new[] { "boss", "boss_bull_giant", "boss_balloon_king", "boss_spitter_mother" };
        var bosses = bossIds.Select((id, i) => stage.Spawn(MonsterDatabase.Get(id), new Vector3(-7.5f + i * 5, 0, -1), awake: false)).ToList();
        await Frames(10);
        foreach (var e in bosses) { e.Awake = true; e.SetPhysicsProcess(false); e.Rig.ShowAs(VisionSystem.Visibility.Visible); }
        stage.Player.TeleportTo(new Vector3(0, 0, 8));
        stage.Cam.Close = false;
        stage.Cam.SnapTo(new Vector3(0, 1.5f, -1));
        foreach (var (facing, name) in new[] { (new Vector3(.3f, 0, 1), "bosses-front"), (Vector3.Right, "bosses-side") })
        {
            foreach (var e in bosses) e.Rig.Face(facing);
            ulong until = Time.GetTicksMsec() + 1500;
            while (Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shot(name);
        }
        // Each middle boss up close, turned toward the camera.
        stage.Cam.Close = true;
        foreach (var e in bosses.Skip(1))
        {
            e.Rig.Face(new Vector3(.3f, 0, 1));
            stage.Cam.SnapTo(e.GlobalPosition + new Vector3(0, 1.2f, .5f));
            ulong until = Time.GetTicksMsec() + 800;
            while (Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shot(e.Monster.Id + "-close");
        }
        GetTree().Quit();
    }
}
