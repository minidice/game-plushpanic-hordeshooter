using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Real physics probes at the thickened torso rims, plus authored mouth effect origins.</summary>
public partial class InfectedAlignmentCheck : Node
{
    private readonly List<string> _failures = new();
    private int _checks;
    private Stage _stage = null!;

    public override async void _Ready()
    {
        try
        {
            _stage = new Stage { CharacterOverride = "test_capsule", DirectorEnabled = false };
            AddChild(_stage);
            _stage.Player.ManualControl = true;
            _stage.Player.Invulnerable = true;
            _stage.Player.SetPhysicsProcess(false);
            _stage.Player.Position = new Vector3(3, 0, 0);
            _stage.Cam.SetProcess(false);
            _stage.Cam.Size = 7;
            _stage.Cam.SnapTo(new Vector3(6, 0, 0));
            foreach (var enemy in _stage.AllEnemies.ToArray()) enemy.QueueFree();
            await Frames(2);
            // Hand-checked points inside the revised cloth/belly surface but outside the old
            // narrow capsules. The crawler probe checks its intentional elevated bullet target.
            foreach (var (id, y, z) in new[] {
                ("humanoid", .77f, -.375f), ("tether", .77f, -.375f),
                ("leaper", .65f, -.455f), ("spitter", .75f, .455f), ("crawler", 1f, 0f) })
            {
                var enemy = Spawn(id);
                enemy.Rig.Face(Vector3.Back);
                await Frames(2);
                var point = enemy.Position + new Vector3(0, y, z);
                var hit = Rays.Cast(enemy, point + Vector3.Left * 2, point + Vector3.Right * 2, Layers.Enemy);
                Check(hit?.Collider == enemy, id + " thickened body rim receives bullets");
                var outside = enemy.Position + new Vector3(0, 1, enemy.Radius + .08f);
                Check(Rays.Cast(enemy, outside + Vector3.Left * 2, outside + Vector3.Right * 2, Layers.Enemy) == null,
                    id + " shot outside capsule still misses");
                var barrier = new StaticBody3D { Position = enemy.Position + new Vector3(0, 1, -1), CollisionLayer = Layers.World };
                barrier.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3, 3, .1f) } });
                _stage.AddChild(barrier);
                await Frames(2);
                using var contact = new KinematicCollision3D();
                Check(enemy.TestMove(enemy.GlobalTransform, Vector3.Forward * 2, contact) &&
                    Mathf.Abs(contact.GetTravel().Length() - (.95f - enemy.Radius)) < .01f,
                    id + " movement stops at the same body radius");
                barrier.QueueFree();
                var radius = enemy.Radius;
                enemy.Die(Vector3.Zero, 0);
                await Frames(2);
                var corpse = _stage.Effects.FindChildren("*", nameof(RigidBody3D), true, false).OfType<Corpse>().Last();
                var capsule = (CapsuleShape3D)corpse.GetChildren().OfType<CollisionShape3D>().Single().Shape;
                Check(Mathf.IsEqualApprox(capsule.Radius, radius), id + " corpse inherits the live body radius");
                corpse.QueueFree();
                await Frames(2);
            }

            if (DisplayServer.GetName() != "headless") await Frames(120);
            var spitter = (RangedEnemy)Spawn("spitter");
            Vector3? emitted = null;
            Vector3 expectedMouth = Vector3.Zero;
            _stage.ChildEnteredTree += child => {
                if (child is EnemyProjectile projectile)
                {
                    emitted = projectile.GlobalPosition;
                    expectedMouth = spitter.Rig.Head!.ToGlobal(new Vector3(.02f, -.207f, .39f));
                    projectile.SetPhysicsProcess(false);
                }
            };
            spitter.StartWindup();
            spitter._PhysicsProcess(.01);
            await Frames(2);
            Advance(spitter, spitter.Monster.Ranged!.Windup);
            Check(emitted.HasValue, "vomit fixture really fires");
            Check(emitted.HasValue && emitted.Value.DistanceTo(expectedMouth) < .01f,
                "vomit starts at the animated mouth instead of the torso");
            await Shot("spitter-mouth");
            foreach (var projectile in _stage.GetChildren().OfType<EnemyProjectile>()) projectile.QueueFree();
            spitter.QueueFree();
            await Frames(2);

            // A mouth extending through a thin wall must never become a way to shoot through it.
            spitter = (RangedEnemy)Spawn("spitter");
            var wall = new StaticBody3D { Position = new Vector3(7.75f, 1, 0), CollisionLayer = Layers.World };
            wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.08f, 3, 3) } });
            _stage.AddChild(wall);
            await Frames(2);
            spitter.StartWindup();
            spitter._PhysicsProcess(.01);
            Advance(spitter, spitter.Monster.Ranged!.Windup);
            Check(spitter.ProjectilesFired == 0, "cover between body and mouth blocks vomit");
            wall.QueueFree();
            spitter.QueueFree();
            await Frames(2);

            var tether = (CaptureEnemy)Spawn("tether");
            tether.StartWindup();
            tether._PhysicsProcess(.01);
            Advance(tether, tether.Monster.Capture!.Windup);
            tether._PhysicsProcess(.01);
            Check(tether.State == CaptureEnemy.Mode.Holding, "tether fixture really captures");
            var line = _stage.Effects.GetChildren().OfType<MeshInstance3D>()
                .SingleOrDefault(mesh => Mathf.IsEqualApprox(mesh.Scale.X, .06f));
            var mouth = tether.Rig.Head!.ToGlobal(new Vector3(.02f, -.207f, .39f));
            float endpointError = line == null ? float.PositiveInfinity : Mathf.Min(
                (line.GlobalPosition + line.GlobalBasis.Z * .5f).DistanceTo(mouth),
                (line.GlobalPosition - line.GlobalBasis.Z * .5f).DistanceTo(mouth));
            Check(endpointError < .03f, "held tether starts at the animated mouth");
            await Shot("tether-mouth");
            _stage.QueueFree();
            await Frames(2);
            GD.Print($"INFECTED_ALIGNMENT_{(_failures.Count == 0 ? "PASS" : "FAIL")} {_checks}");
            QuitHygiene.Collect();
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private Enemy Spawn(string id)
    {
        var enemy = _stage.Spawn(MonsterDatabase.Get(id), new Vector3(8, 0, 0), false);
        enemy.SetPhysicsProcess(false);
        enemy.Rig.Face(Vector3.Left);
        return enemy;
    }
    private void Check(bool pass, string message)
    {
        _checks++;
        GD.Print((pass ? "PASS: " : "FAIL: ") + message);
        if (!pass) _failures.Add(message);
    }
    private async Task Frames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private static void Advance(Enemy enemy, float seconds)
    {
        // Small steps preserve the same head spring behavior as normal 60 Hz gameplay.
        for (float elapsed = 0; elapsed < seconds; elapsed += 1f / 60) enemy._PhysicsProcess(1f / 60);
    }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(ProjectSettings.GlobalizePath($"res://output/infected-alignment-{name}.png"));
        if (error != Error.Ok) throw new Exception($"Saving {name} failed: {error}");
    }
}
