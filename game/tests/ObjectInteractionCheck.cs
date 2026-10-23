using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

namespace HordeShooter.Tests;

public partial class ObjectInteractionCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-6, -7, 20, 14);
        protected override void BuildMap()
        {
            PlayerStart = new Vector3(0, 0, -3);
            Floor(-5, 13, -6, 6, "546852");
            Prop("wire_fence", Vector3.Zero);
            Prop("corrugated_fence", new Vector3(6, 0, 0));
            Prop("wooden_crate", new Vector3(10, 0, 0), solid: false);
        }
    }
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool yes, string label) { if (!yes) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    public override async void _Ready()
    {
        int exit = 0;
        try { await Run(); GD.Print("OBJECT_INTERACTION_PASS " + _checks); }
        catch (Exception error) { GD.PushError(error.ToString()); exit = 1; }
        if (_stage != null) { _stage.QueueFree(); await Frames(3); }
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }
    private async Task Run()
    {
        string json = FileAccess.GetFileAsString(ObjectInteractions.DataPath);
        var changed = JsonNode.Parse(json)!;
        changed["profiles"]!["wire_fence"]!["blocks_projectiles"] = true;
        Check(ObjectInteractions.Parse(changed.ToJsonString()).ForProp(PropLibrary.Find("wire_fence")!)!.CollisionLayer == Layers.World,
            "changing profile data changes collision policy without per-object code");
        changed["objects"]!["wire_fence"] = "missing_profile";
        bool rejected = false;
        try { ObjectInteractions.Parse(changed.ToJsonString()); } catch (FormatException) { rejected = true; }
        Check(rejected, "invalid profile reference is rejected");
        changed = JsonNode.Parse(json)!;
        changed["profiles"]!["structure"]!["blocks_movement"] = false;
        rejected = false;
        try { ObjectInteractions.Parse(changed.ToJsonString()); } catch (FormatException) { rejected = true; }
        Check(rejected, "unsupported projectile-only profile is rejected");
        Check(ObjectInteractions.Current.ForProp(PropLibrary.Find("container_open")!) == null, "compound model has no invented blocking policy");
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false,
            MapOverride = new TestMap(), ModeOverride = GameMode.Scenario, SquadOverride = 0 };
        AddChild(_stage); await Frames(5);
        _stage.Player.ManualControl = true;
        var fence = (StaticBody3D)_stage.Map.KitProps.Single(p => p.Name == "wire_fence");
        var solid = (StaticBody3D)_stage.Map.KitProps.Single(p => p.Name == "corrugated_fence");
        Check((fence.CollisionLayer & Layers.BulletMask) == 0, "wire fence is absent from bullet collisions");
        Check((solid.CollisionLayer & Layers.BulletMask) != 0, "ordinary structure blocks bullets");
        Check(_stage.Flow.IsBlocked(Vector3.Zero) && _stage.Flow.IsBlocked(new Vector3(6, 0, 0)), "both props block navigation");
        Check(_stage.Map.KitProps.Single(p => p.Name == "wooden_crate") is not StaticBody3D && !_stage.Flow.IsBlocked(new Vector3(10, 0, 0)),
            "explicit decorative placement remains nonblocking");
        foreach (var x in new[] { 0f, 6f })
        {
            var from = new Vector3(x, Player.AimHeight, -2); var to = new Vector3(x, Player.AimHeight, 2);
            Check((Rays.Cast(_stage, from, to, Layers.BulletMask) != null) == (x == 6), $"actual bullet ray at x={x}");
            Check((Rays.Cast(_stage, from, to, Layers.World | Layers.Props | Layers.Player) != null) == (x == 6), $"enemy projectile ray at x={x}");
            _stage.Player.GlobalPosition = new Vector3(x, 0, -2);
            _stage.Player.Velocity = Vector3.Zero;
            _stage.Player.SetControls(new Vector2(0, 1), to, false); await Frames(70);
            _stage.Player.SetControls(Vector2.Zero, to, false);
            Check(_stage.Player.GlobalPosition.Z < -.3f && _stage.Player.GlobalPosition.Z > -1.2f, $"player movement stops at x={x}: {_stage.Player.GlobalPosition}, mask={_stage.Player.CollisionMask}");
            var enemy = _stage.SpawnEnemy<Enemy>(_stage.CommonDefinition, new Vector3(x, 0, 3), awake: false);
            enemy.SetPhysicsProcess(false); enemy.ScaleHealth(100);
            await Frames(2);
            using var collision = new KinematicCollision3D();
            Check(enemy.TestMove(new Transform3D(Basis.Identity, new Vector3(x, .05f, 2)), new Vector3(0, 0, -4), collision)
                && collision.GetCollider() == (x == 0 ? fence : solid), $"enemy body cannot cross x={x}");
            _stage.Player.GlobalPosition = new Vector3(x, 0, -3);
            _stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
            float health = enemy.Health;
            int shots = _stage.Player.ShotsFired;
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up * Player.AimHeight, true);
            await Frames(35);
            _stage.Player.SetControls(Vector2.Zero, to, false);
            Check(_stage.Player.ShotsFired > shots, $"M4 actually fired at x={x}");
            Check((enemy.Health < health) == (x == 0), $"M4 damage behind {(x == 0 ? "wire fence" : "structure")}: {health} -> {enemy.Health}");
            _stage.Player.GlobalPosition = new Vector3(x, 0, -.65f);
            enemy.GlobalPosition = new Vector3(x, 0, .65f);
            _stage.Player.PressSlot(1);
            health = enemy.Health;
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, false); await Frames(30);
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, true); await Frames(24);
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, false);
            Check(enemy.Health == health, $"melee cannot hit through x={x}");
            enemy.GlobalPosition = new Vector3(x + 1.1f, 0, -.65f);
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, false); await Frames(60);
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, true); await Frames(24);
            _stage.Player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, false);
            Check(enemy.Health < health, $"same-side melee still deals damage at x={x}");
            _stage.Enemies.Remove(enemy); enemy.QueueFree(); await Frames(2);
        }
        _stage.Player.GlobalPosition = new Vector3(0, 0, -2);
        _stage.Player.Velocity = Vector3.Zero;
        _stage.Player.SetControls(new Vector2(0, 1), new Vector3(0, 1, 4), false);
        _stage.Player.PressRoll(); await Frames(35);
        Check(_stage.Player.GlobalPosition.Z < -.3f && (_stage.Player.CollisionMask & Layers.MovementOnly) != 0, "roll and restored player mask still stop at wire fence");
        _stage.Player.SetControls(Vector2.Zero, Vector3.Zero, false);
        _stage.QueueFree(); await Frames(3);
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false,
            MapOverride = new StageTwoMap(), ModeOverride = GameMode.Scenario, SquadOverride = 0 };
        AddChild(_stage); await Frames(5);
        foreach (var from in new[] { new Vector3(6, Player.AimHeight, 8.5f), new Vector3(85, Player.AimHeight, 9.5f) })
        {
            var to = from + Vector3.Back * 2.5f;
            Check(Rays.Cast(_stage, from, to, Layers.BulletMask) == null, $"Stage 2 fence and invisible boundary pass bullets: {from}");
            Check(Rays.Cast(_stage, from, to, Layers.MovementWorld) != null, $"Stage 2 boundary still blocks movement: {from}");
        }
        Check(Rays.Cast(_stage, new Vector3(85, Player.AimHeight, -9.5f), new Vector3(85, Player.AimHeight, -12), Layers.BulletMask) != null,
            "Stage 2 corrugated boundary still blocks bullets");
    }
}
