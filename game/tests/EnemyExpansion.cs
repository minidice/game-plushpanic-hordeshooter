using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual physics/lifecycle checks: Godot --headless --path . res://tests/EnemyExpansion.tscn</summary>
public partial class EnemyExpansion : Node
{
    private readonly List<string> _checks = new();
    private Stage _stage = null!;
    private Player Player => _stage.Player;

    public override async void _Ready()
    {
        try
        {
            Check(MonsterDatabase.Current.Errors.Count == 0, "expanded monster CSV and strict traits validate");
            foreach (var id in new[] { "humanoid", "crawler", "tether", "leaper", "spitter" })
                Check(CharacterCatalog.Enemy(id).Id == id, id + " resolves its own enemy model definition");
            foreach (var theme in new[] { "zombie", "animals", "armed" }) await ThemeChecks(theme);
            await CreateStage("zombie");
            await CaptureChecks();
            await RangedChecks();
            await DeathChecks();
            Finish(true, "");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAILED: " + description);
        _checks.Add(description);
        GD.Print("PASS: " + description);
    }
    private async Task Frames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task CreateStage(string theme)
    {
        if (IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "test_capsule", ThemeOverride = theme, DirectorEnabled = false };
        AddChild(_stage);
        Player.ManualControl = true;
        Player.Invulnerable = true;
        await Frames(2);
    }

    private async Task ThemeChecks(string theme)
    {
        await CreateStage(theme);
        var allowed = StageTheme.Commons(theme).Concat(StageTheme.Specials(theme)).ToHashSet();
        Check(_stage.AllEnemies.All(e => allowed.Contains(e.Monster.Id)), theme + " initial population stays in encounter pool");
        var common = _stage.SpawnCommon(new Vector3(7, 0, 1), false);
        Check(StageTheme.Commons(theme).Contains(common.Monster.Id), theme + " dynamic common spawn uses encounter pool");
        var specials = StageTheme.Specials(theme);
        // Area specials placed so far took the first pool slots (only the start areas are filled at load).
        int placed = _stage.AllEnemies.Count(e => e.IsSpecial);
        for (int i = 0; i < specials.Length; i++)
        {
            var id = specials[(i + placed) % specials.Length];
            var special = _stage.SpawnThemeSpecial(new Vector3(8, 0, 0), false);
            Check(special.Monster.Id == id && special.IsSpecial, theme + " special rotation: " + id);
            special.QueueFree();
        }
        if (theme == "armed")
            Check(_stage.AllEnemies.Any(e => e is RangedEnemy && e.Monster.Id == "armed_animal"), "armed theme creates functioning ranged enemies");
    }

    private async Task Clear()
    {
        Player.ReleaseRestraint();
        foreach (var enemy in _stage.AllEnemies.ToArray()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        foreach (var node in _stage.GetChildren()) if (node is EnemyProjectile or VomitHazard) node.QueueFree();
        Player.GlobalPosition = new Vector3(3, .05f, 0);
        Player.Velocity = Vector3.Zero;
        Player.SetControls(Vector2.Zero, new Vector3(8, 1, 0), false);
        await Frames(3);
    }

    private CaptureEnemy Captor(string id) => (CaptureEnemy)_stage.Spawn(MonsterDatabase.Get(id), new Vector3(8, 0, 0), true);

    private async Task CaptureChecks()
    {
        await Clear();
        var tether = Captor("tether");
        tether.StartWindup();
        await Frames(25);
        Check(tether.State == CaptureEnemy.Mode.Windup && !Player.IsRestrained, "tether visibly winds up before grabbing");
        await Frames(65);
        Check(Player.RestrainingEnemy == tether && tether.State == CaptureEnemy.Mode.Holding, "tether actually captures target after windup");
        float beforePull = Player.GlobalPosition.X;
        await Frames(30);
        Check(Player.GlobalPosition.X > beforePull + .3f, "tether pulls player through collision-based movement");
        var other = Captor("leaper");
        other.SetPhysicsProcess(false);
        Check(!Player.TryRestrain(other), "single captor ownership prevents double grabs");
        int shots = Player.ShotsFired;
        Player.SetControls(Vector2.Left, new Vector3(8, 1, 0), true);
        Player.PressRoll(); Player.PressHeal(); Player.PressReload(); Player.PressThrow();
        await Frames(330);
        Check(Player.IsRestrained && !Player.IsRolling && !Player.IsHealing && !Player.Gun.Reloading &&
            !Player.Weapons.AimingThrow && Player.ShotsFired == shots, "held input cannot escape or act and capture has no timeout");
        GameSession.DebugEscapeEnabled = false;
        Check(!Player.DebugEscapeRestraint() && Player.IsRestrained, "disabled debug escape cannot release a capture");
        Player.ManualControl = false;
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.F8,Pressed=true});
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.F8,Pressed=false});
        Check(Player.IsRestrained,"physical F8 cannot escape when cheat is disabled");
        await Frames(2);
        GameSession.DebugEscapeEnabled = true;
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.F8,Pressed=true});
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.F8,Pressed=false});
        Check(!Player.IsRestrained && tether.StunTime > 0, "physical F8 test cheat releases and interrupts captor");
        Player.ManualControl = true;
        Player.SetControls(Vector2.Zero, new Vector3(8, 1, 0), false);
        Check(Player.TryRestrain(other), "player can be captured again after release");
        other.Stun(1);
        Check(!Player.IsRestrained, "captor stun releases immediately");
        tether = Captor("tether");
        Check(Player.TryRestrain(tether), "capture owner can be assigned for lifecycle check");
        tether.TakeHit(new HitInfo(999, Vector3.Right, tether.GlobalPosition, Power: 0));
        Check(!Player.IsRestrained, "lethal existing TakeHit path releases player");
        other = Captor("leaper");
        other.SetPhysicsProcess(false);
        Check(Player.TryRestrain(other), "new lifecycle owner is accepted");
        other.QueueFree();
        await Frames(3);
        Check(!Player.IsRestrained, "queued captor cleanup releases player");

        await Clear();
        var leaper = Captor("leaper");
        leaper.StartWindup();
        await Frames(30);
        Check(leaper.State == CaptureEnemy.Mode.Windup, "pouncer telegraphs before leap");
        await Frames(75);
        Check(Player.RestrainingEnemy == leaper && leaper.State == CaptureEnemy.Mode.Holding, "pouncer traverses space and pins on contact");
        Check(Mathf.Abs(Player.Rig.Pose.Rotation.X + 1.15f) < .01f, "pin pose survives animation and tumble updates");
        leaper.Stun(1);
        Check(!Player.IsRestrained, "pouncer interruption releases pin");

        await Clear();
        tether = Captor("tether"); tether.StartWindup();
        await Frames(90);
        Check(Player.RestrainingEnemy == tether, "cover interruption test begins with an actual active tether");
        var closingWall = AddWall();
        await Frames(4);
        Check(!Player.IsRestrained && tether.State == CaptureEnemy.Mode.Chase, "new cover breaks an already connected tether");
        closingWall.QueueFree();
        await Clear();
        tether = Captor("tether"); tether.StartWindup();
        await Frames(90);
        Check(Player.RestrainingEnemy == tether, "range interruption test begins with an active tether");
        tether.GlobalPosition = new Vector3(30, 0, 0);
        await Frames(2);
        Check(!Player.IsRestrained, "displacing captor beyond tether range releases restraint");
        await Clear();
        leaper = Captor("leaper"); leaper.StartWindup();
        await Frames(105);
        Check(Player.RestrainingEnemy == leaper, "pin interruption test begins with an actual contact pin");
        leaper.GlobalPosition = new Vector3(30, 0, 0);
        await Frames(2);
        Check(!Player.IsRestrained, "displacing pouncer away from body contact interrupts pin");
        await Clear();
        var wall = AddWall();
        await Frames(3);
        tether = Captor("tether"); tether.StartWindup();
        await Frames(85);
        Check(!Player.IsRestrained, "tether does not grab through a wall");
        tether.QueueFree();
        leaper = Captor("leaper"); leaper.StartWindup();
        await Frames(110);
        Check(!Player.IsRestrained && leaper.GlobalPosition.X > 5.5f, "pounce stops at wall without pinning through it");
        wall.QueueFree();
        await Clear();
    }

    private StaticBody3D AddWall()
    {
        var wall = new StaticBody3D { Position = new Vector3(5.5f, 1.5f, 0), CollisionLayer = Layers.World };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.4f, 3, 6) } });
        _stage.AddChild(wall);
        return wall;
    }

    private async Task RangedChecks()
    {
        await Clear();
        Player.Invulnerable = false;
        int health = Player.Health;
        var shooter = (RangedEnemy)_stage.Spawn(MonsterDatabase.Get("armed_animal"), new Vector3(8, 0, 0), true);
        shooter.StartWindup();
        await Frames(25);
        Check(shooter.WindingUp && shooter.ProjectilesFired == 0 && Player.Health == health, "armed enemy telegraphs before firing");
        await Frames(65);
        Check(shooter.ProjectilesFired == 1 && Player.Health < health, "armed enemy projectile damages exposed player");
        await Clear();
        var wall = AddWall();
        await Frames(3);
        health = Player.Health;
        shooter = (RangedEnemy)_stage.Spawn(MonsterDatabase.Get("armed_animal"), new Vector3(8, 0, 0), true);
        shooter.StartWindup();
        await Frames(85);
        Check(shooter.ProjectilesFired == 0 && Player.Health == health, "ranged windup cannot fire through cover");
        var projectile = new EnemyProjectile();
        projectile.Setup(_stage, new Vector3(8, .8f, 0), Player.GlobalPosition + Vector3.Up * .8f,
            MonsterDatabase.Get("armed_animal").Ranged!, 20);
        _stage.AddChild(projectile);
        await Frames(30);
        Check(Player.Health == health && !IsInstanceValid(projectile), "swept projectile collides with wall before player");
        var blockedHazard = new VomitHazard();
        blockedHazard.Setup(_stage, new Vector3(6, .06f, 0), 1, 4, 10);
        _stage.AddChild(blockedHazard);
        await Frames(40);
        Check(Player.Health == health, "vomit hazard damage cannot cross wall");
        wall.QueueFree();
        await Clear();
        var spitter = (RangedEnemy)_stage.Spawn(MonsterDatabase.Get("spitter"), new Vector3(8, 0, 0), true);
        spitter.StartWindup();
        await Frames(45);
        Check(spitter.WindingUp && spitter.ProjectilesFired == 0, "spitter telegraphs impact zone before vomiting");
        await Frames(85);
        Check(spitter.ProjectilesFired == 1 && _stage.GetChildren().OfType<VomitHazard>().Any(), "vomit projectile creates persistent ground hazard");
        spitter.SetPhysicsProcess(false);
        health = Player.Health;
        await Frames(45);
        Check(Player.Health < health, "standing in vomit ground hazard deals periodic damage");
        Player.Invulnerable = true;
        await Frames(300);
        Check(!_stage.GetChildren().OfType<VomitHazard>().Any(), "ground hazard expires cleanly");
    }

    private async Task DeathChecks()
    {
        await Clear();
        var captor = Captor("tether");
        Check(Player.TryRestrain(captor), "death test establishes restraint");
        Player.Invulnerable = false;
        Player.TakeDamage(9999, Vector3.Zero, 0);
        Check(Player.IsDead && !Player.IsRestrained, "player death clears captor ownership");
        _stage.QueueFree();
        await Frames(3);
        Check(!IsInstanceValid(captor), "stage disposal frees capture enemy and its visual effects");
    }

    private void Finish(bool ok, string error)
    {
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output"));
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://output/enemy-expansion-report.json"),
            System.Text.Json.JsonSerializer.Serialize(new { ok, passed = _checks.Count, checks = _checks, error },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print((ok ? "ENEMY_EXPANSION_PASS " : "ENEMY_EXPANSION_FAIL ") + _checks.Count + " " + error);
        QuitHygiene.Collect();
        GetTree().Quit(ok ? 0 : 1);
    }
}
