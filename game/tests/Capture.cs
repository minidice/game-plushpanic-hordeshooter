using Godot;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Renders a few scripted moments to output/*.png for visual review (needs a real window, not --headless).
/// Run: Godot --path . res://tests/Capture.tscn
/// Other language: Godot --path . res://tests/Capture.tscn -- --lang=eng  (files get a -eng suffix)
/// </summary>
public partial class Capture : Node
{
    private string _suffix = "";

    public override async void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--lang="))
            {
                Loc.Language = arg["--lang=".Length..];
                _suffix = "-" + Loc.Language;
            }

        var menu = GD.Load<PackedScene>("res://scenes/Menu.tscn").Instantiate<MainMenu>();
        AddChild(menu);
        await Frames(20);
        await Shot("01-menu");
        menu.QueueFree();
        await Frames(2);

        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        await Frames(5);
        var player = stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        player.SetControls(Vector2.Zero, new Vector3(9, 1, 1), false);
        await Frames(40);
        await Shot("02-saferoom");

        stage.LeaveSafeRoom();
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        player.GlobalPosition = new Vector3(21, 0, .5f);
        stage.Cam.SnapTo(player.GlobalPosition);
        var plush = stage.CommonDefinition;
        for (int i = 0; i < 9; i++)
            stage.SpawnEnemy<Enemy>(plush, new Vector3(30 + i % 3 * 1.4f, 0, -1.5f + i / 3 * 1.3f), awake: true);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(50);
        await Shot("03-street-fight");
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);

        stage.Cam.Close = true;
        await Frames(40);
        await Shot("04-close-up");
        stage.Cam.Close = false;

        // Molotov on one group, a mortar shell in the air over another.
        foreach (var e in stage.AllEnemies) e.QueueFree();
        player.GlobalPosition = new Vector3(40, 0, .5f);
        stage.Cam.SnapTo(player.GlobalPosition);
        for (int i = 0; i < 5; i++) stage.SpawnEnemy<Enemy>(plush, new Vector3(47 + i % 2, 0, -2 + i), awake: false).Stun(9);
        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("molotov"));
        player.SetControls(Vector2.Zero, new Vector3(47.5f, 1, 0), false);
        player.PressThrow();
        await Frames(20);
        await Shot("05a-throw-aim");
        player.SetControls(Vector2.Zero, new Vector3(47.5f, 1, 0), true);
        await Frames(3);
        player.SetControls(Vector2.Zero, new Vector3(47.5f, 1, 0), false);
        await Frames(70);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("mortar"), null);
        player.SetControls(Vector2.Zero, new Vector3(33, 1, 3), true);
        await Frames(4);
        player.SetControls(Vector2.Zero, new Vector3(33, 1, 3), false);
        await Frames(25);
        await Shot("05-molotov-mortar");

        foreach (var e in stage.AllEnemies) e.QueueFree();
        player.Weapons.Pickup(ArmoryCatalog.Weapon("alabong"), null);
        player.GlobalPosition = new Vector3(81.5f, 0, -5.5f);
        stage.Cam.SnapTo(player.GlobalPosition);
        stage.StartCrescendo();
        player.SetControls(Vector2.Zero, new Vector3(66, 1, 0), true);
        await Frames(60 * 6);
        await Shot("06-crescendo");
        player.SetControls(Vector2.Zero, new Vector3(66, 1, 0), false);

        foreach (var e in stage.AllEnemies) e.QueueFree();
        stage.Director.Stop();
        await Frames(2);
        player.GlobalPosition = new Vector3(60, 0, 0);
        stage.Cam.SnapTo(player.GlobalPosition);
        player.SetControls(Vector2.Zero, new Vector3(70, 1, 0), false);
        var bull = stage.SpawnEnemy<BullEnemy>(MonsterDatabase.Get("bull"), new Vector3(69, 0, 0), awake: true);
        var balloon = stage.SpawnEnemy<BalloonEnemy>(MonsterDatabase.Get("balloon"), new Vector3(66, 0, 2.5f), awake: false);
        await Frames(3);
        bull.StartWindup(Vector3.Left);
        await Frames(30);
        await Shot("07-bull-windup");
        balloon.Pop();
        await Frames(60);
        await Shot("08-aftermath");

        player.Invulnerable = false;
        player.TakeDamage(999, Vector3.Right);
        await Frames(90);
        await Shot("09-game-over");

        GD.Print("CAPTURE_DONE");
        QuitHygiene.Collect();
        GetTree().Quit();
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Shot(string name)
    {
        name += _suffix;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/{name}.png"));
        GD.Print("saved " + name);
    }
}
