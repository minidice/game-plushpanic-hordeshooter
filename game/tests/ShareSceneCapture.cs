using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Feedback pack scene shots (studio/share/README.md, decisions.md T-27): the models placed in the real game, from the game camera, without the HUD.
/// S1 Stage 1 safe room with the four survivors, S2 Stage 2 forest, S3 a fight with the common monsters, S4 every
/// survivor and common monster side by side for scale. Windowed only (GPU); writes studio/share/.build/scenes/S*.png.
/// </summary>
public partial class ShareSceneCapture : Node
{
    private static readonly string[] CommonPacks = { "zombie_street", "hunter_eagle", "hunter_parrot", "hunter_lion", "hunter_tiger", "hunter_reindeer", "hunter_rhino" };
    private string _out = "";

    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("scene shots need a window (GPU)");
            _out = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "studio", "share", ".build", "scenes"));
            Directory.CreateDirectory(_out);
            GetWindow().Size = new Vector2I(1600, 900);
            await SafeRoom();
            await Forest();
            await Fight();
            await Lineup();
            GD.Print("SHARE_SCENES_PASS 4");
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SHARE_SCENES_FAIL " + e.Message); exit = 1; }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private async Task<Stage> Open(Stage stage)
    {
        foreach (var old in GetChildren().OfType<Stage>().ToList()) { old.QueueFree(); }
        await Frames(3);
        AddChild(stage);
        await Frames(5);
        stage.Player.ManualControl = true;
        stage.Player.Invulnerable = true;
        stage.Hud.Visible = false;
        // Squadmates stand still: their brains would walk them back into formation and shoot the posed monsters.
        foreach (var brain in stage.Squad) brain.ProcessMode = ProcessModeEnum.Disabled;
        // Posed monsters are shown in full; the vision cone (C-23) would leave the ones off the leader's aim as silhouettes.
        stage.Vision.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        return stage;
    }

    /// <summary>The camera stops following the leader (and its aim lead) and holds <paramref name="focus"/>.</summary>
    private static void Frame(Stage stage, Vector3 focus)
    {
        stage.Cam.Target = null;
        stage.Cam.SnapTo(focus);
    }

    /// <summary>A posed common monster: spawned, given a few frames to settle, then frozen (no stun stars, no attack).
    /// Only its rig keeps running, to finish fading in from the silhouette.</summary>
    private async Task<Enemy> Pose(Stage stage, string pack, Vector3 at, Vector3 look)
    {
        var roster = CommonRoster.Current;
        var enemy = stage.SpawnEnemy<Enemy>(roster.Compose(roster.CreatePack(pack, 1)[0]), at, awake: false);
        enemy.LookAt(new Vector3(look.X, enemy.GlobalPosition.Y, look.Z), Vector3.Up, true);
        await Frames(4);
        enemy.ProcessMode = ProcessModeEnum.Disabled;
        enemy.Rig.ProcessMode = ProcessModeEnum.Always;
        enemy.Rig.ShowAs(VisionSystem.Visibility.Visible);
        return enemy;
    }

    private static void Team(Stage stage, Vector3 at, Vector3 aim)
    {
        var people = stage.Players.Where(IsInstanceValid).ToList();
        var offsets = new[] { Vector3.Zero, new Vector3(-1.3f, 0, 1.2f), new Vector3(-1.3f, 0, -1.2f), new Vector3(-2.6f, 0, 0) };
        for (int i = 0; i < people.Count; i++)
        {
            people[i].ManualControl = true;
            people[i].Invulnerable = true;
            people[i].GlobalPosition = at + offsets[i % offsets.Length];
            people[i].SetControls(Vector2.Zero, aim, false);
            people[i].Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        }
    }

    private async Task SafeRoom()
    {
        var stage = await Open(new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 3 });
        Team(stage, new Vector3(5.5f, 0, .4f), new Vector3(10, 1, 2));
        stage.Cam.Close = true;
        Frame(stage, new Vector3(4.6f, 0, .2f));
        await Frames(50);
        await Shot("S1");
    }

    private async Task Forest()
    {
        var stage = await Open(new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 3,
            ModeOverride = GameMode.Scenario, MapOverride = StageMap.Create(GameMode.Scenario, 2) });
        var at = stage.Flow.OpenCellsNear(stage.Map.PlayerStart + new Vector3(24, 0, 0), 0, 4).FirstOrDefault(stage.Map.PlayerStart);
        Team(stage, at, at + new Vector3(8, 1, -2));
        Frame(stage, at + new Vector3(2, 0, 0));
        await Frames(60);
        await Shot("S2");
    }

    private async Task Fight()
    {
        var stage = await Open(new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 3 });
        stage.LeaveSafeRoom();
        var at = new Vector3(24, 0, 0);
        Team(stage, at, at + new Vector3(8, 1, 0));
        for (int i = 0; i < CommonPacks.Length; i++)
        {
            var spot = at + new Vector3(5f + (i % 3) * 1.6f, 0, -2.4f + (i / 3) * 2.2f + (i % 2) * .5f);
            await Pose(stage, CommonPacks[i], spot, at);
        }
        Frame(stage, at + new Vector3(3.5f, 0, 0));
        await Frames(50);
        await Shot("S3");
    }

    private async Task Lineup()
    {
        var stage = await Open(new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 3 });
        stage.LeaveSafeRoom();
        var origin = new Vector3(18, 0, 2.2f);
        var people = stage.Players.Where(IsInstanceValid).ToList();
        for (int i = 0; i < people.Count; i++)
        {
            people[i].ManualControl = true;
            people[i].Invulnerable = true;
            people[i].GlobalPosition = origin + new Vector3(i * 1.25f, 0, 0);
            people[i].SetControls(Vector2.Zero, people[i].GlobalPosition + new Vector3(0, 1, 6), false);
        }
        for (int i = 0; i < CommonPacks.Length; i++)
        {
            var spot = origin + new Vector3(-.4f + i * 1.45f, 0, -2.6f);
            await Pose(stage, CommonPacks[i], spot, spot + new Vector3(0, 0, 6));
        }
        stage.Cam.Close = true;
        Frame(stage, origin + new Vector3(4.4f, 0, -1.1f));
        await Frames(60);
        await Shot("S4");
    }

    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_out, name + ".png"));
        if (error != Error.Ok) throw new IOException($"{name}: {error}");
        GD.Print("saved " + name);
    }
}
