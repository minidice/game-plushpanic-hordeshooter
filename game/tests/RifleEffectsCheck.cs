using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>M4 shot presentation must travel, stop at the hit, expire, and never delay or replay damage.</summary>
public partial class RifleEffectsCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private WeaponShotEffect[] Shots() => _stage.Effects.GetChildren().OfType<WeaponShotEffect>()
        .Where(n => !n.IsQueuedForDeletion()).ToArray();

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("RIFLE_EFFECTS_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("RIFLE_EFFECTS_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false };
        AddChild(_stage);
        await Frames(3);
        foreach (var enemy in _stage.AllEnemies.ToList()) enemy.QueueFree();
        foreach (var bot in _stage.GetChildren().OfType<Player>().Where(p => p.IsBot).ToList()) bot.QueueFree();
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        player.TeleportTo(new Vector3(14, 0, 0));
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        var aim = new Vector3(20, Player.AimHeight, 0);
        player.SetControls(Vector2.Zero, aim, false);
        var target = _stage.SpawnCommon(new Vector3(20, 0, 0), awake: false);
        await Frames(3);
        target.Stun(30);
        float hp = target.Health;
        int ammo = player.Gun.Mag;
        player.Weapons.Tick(.001f, false, true, true, false);
        Check(target.Health < hp && player.Gun.Mag == ammo - 1, "the M4 hits and spends one round immediately, before its visible streak travels");
        _stage.ProcessMode = ProcessModeEnum.Disabled;
        Check(Shots().Length == 1, "a local M4 shot starts the new presentation");
        var local = Shots()[0];
        local._Process(.8);
        await Frames(2);
        Check(Shots().Length == 0, "local shot visuals expire");

        float damagedHp = target.Health;
        int remainingAmmo = player.Gun.Mag;
        var from = player.Weapons.MuzzlePosition;
        var end = from + Vector3.Right * 12;
        player.Weapons.ShowNetShot(ArmoryCatalog.Weapon("m4"), from, end, new[] { end });
        Check(Shots().Length == 1, "a received M4 shot uses the same presentation");
        Check(target.Health == damagedHp && player.Gun.Mag == remainingAmmo, "received shot presentation does not repeat damage or spend ammunition");
        target.QueueFree();
        foreach (var effect in _stage.Effects.GetChildren().Where(n => n is CpuParticles3D or RigidBody3D)) effect.QueueFree();
        await Frames(1);
        var shot = Shots()[0];
        var tracer = shot.GetNode<MeshInstance3D>("Tracer");
        var flash = shot.GetNode<MeshInstance3D>("MuzzleFlash");
        var initial = tracer.GlobalPosition;
        Check(tracer.Scale.X < 4 && tracer.Scale.X < from.DistanceTo(end), "the visible tracer is a short streak, not a full-range beam");
        Check(flash.Visible && flash.GlobalPosition.DistanceTo(from) < 1, "the flash appears at the firing muzzle");
        _stage.Cam.SnapTo(player.GlobalPosition + Vector3.Right * 3 + Vector3.Up * .8f);
        await Shot("muzzle");
        shot._Process(.035);
        Check(tracer.GlobalPosition.X > initial.X + 1, "the streak travels forward after firing");
        await Shot("travel");
        shot._Process(.04);
        var front = tracer.GlobalTransform * new Vector3(.5f, 0, 0);
        Check(front.DistanceTo(end) < .01f, "the visible streak is clipped at the received hit position");
        Check(!flash.Visible, "the muzzle flash ends while the distant streak is still traveling");
        await Shot("end");
        shot._Process(.8);
        await Frames(2);
        Check(Shots().Length == 0, "received shot visuals expire without leaving meshes");

        foreach (float distance in new[] { 0f, .02f, .3f, 60f })
        {
            var stop = from + Vector3.Right * distance;
            player.Weapons.ShowNetShot(ArmoryCatalog.Weapon("m4"), from, stop, new[] { stop });
            var effect = Shots().Single();
            var line = effect.GetNode<MeshInstance3D>("Tracer");
            Check(!line.Visible || (line.GlobalTransform * new Vector3(.5f, 0, 0)).X <= stop.X + .001f,
                $"a {distance:0.00} m shot never draws through its endpoint");
            effect._Process(1);
            await Frames(1);
        }
        player.Weapons.ShowNetShot(ArmoryCatalog.Weapon("m1911"), from, end, new[] { end });
        Check(Shots().Length == 1, "the shared presentation also supports pistols");
        Shots()[0]._Process(1);
        await Frames(1);

        _stage.ProcessMode = ProcessModeEnum.Inherit;
        player.SetControls(Vector2.Zero, end, true);
        await Frames(45);
        player.SetControls(Vector2.Zero, end, false);
        await Frames(60);
        Check(Shots().Length == 0, "sustained M4 fire leaves no shot effects after release");
        await CaptureBurst();
    }

    private async Task CaptureBurst()
    {
        if (!Windowed) return;
        // Fixed presentation clock for a reproducible 60 fps preview; gameplay is checked above.
        _stage.ProcessMode = ProcessModeEnum.Disabled;
        var player = _stage.Player;
        var from = player.Weapons.MuzzlePosition;
        var end = from + Vector3.Right * 12;
        _stage.Cam.SnapTo(player.GlobalPosition + Vector3.Right * 3 + Vector3.Up * .8f);
        var directory = ProjectSettings.GlobalizePath("res://output/rifle-fx-frames");
        DirAccess.MakeDirRecursiveAbsolute(directory);
        for (int i = 0; i < 72; i++)
        {
            foreach (var effect in Shots()) effect._Process(1.0 / 60);
            if (i is 6 or 14 or 22 or 30 or 38)
                player.Weapons.ShowNetShot(ArmoryCatalog.Weapon("m4"), from, end, new[] { end });
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{directory}/frame-{i:000}.png");
        }
    }

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/rifle-fx-{name}.png"));
    }
}
