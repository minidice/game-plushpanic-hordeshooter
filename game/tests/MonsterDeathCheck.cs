using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual rendered vertices must settle on the floor, not just the empty corners of a bounding box.</summary>
public partial class MonsterDeathCheck : Node
{
    private int _checks, _failures;
    private void Check(bool value, string message) { _checks++; if (!value) _failures++; GD.Print((value ? "PASS: " : "FAIL: ") + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private static float Bottom(Node node)
    {
        float bottom = float.PositiveInfinity;
        if (node is MeshInstance3D mesh && mesh.IsVisibleInTree() && mesh.Mesh != null && !mesh.HasMeta("authored_grip"))
            for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
                foreach (var v in mesh.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    bottom = Mathf.Min(bottom, (mesh.GlobalTransform * v).Y);
        foreach (Node child in node.GetChildren()) bottom = Mathf.Min(bottom, Bottom(child));
        return bottom;
    }
    public override async void _Ready()
    {
        try
        {
            var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
            AddChild(stage); await Frames(3);
            stage.Player.ManualControl = true; stage.Player.Invulnerable = true;
            stage.Vision.SetPhysicsProcess(false);
            foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
            var at = stage.Player.GlobalPosition;
            // A death must start in the current attack pose, not snap back to standing.
            var attacking = stage.Spawn(MonsterDatabase.Get("boss"), at, awake: false);
            attacking.SetPhysicsProcess(false);
            attacking.Rig.ApplyBossPose("rear_roar", .5f);
            var raisedArm = (Node3D)attacking.Rig.Model.FindChild("boss_leftArm", true, false);
            var armBefore = raisedArm.GlobalTransform;
            attacking.Die(Vector3.Right, 6);
            Check(raisedArm.GlobalPosition.DistanceTo(armBefore.Origin) < .03f && raisedArm.GlobalBasis.IsEqualApprox(armBefore.Basis),
                "boss death enters from its current raised-arm attack pose");
            stage.Effects.NamedCorpses.Last().QueueFree(); await Frames(2);
            var ids = new[] { "bull", "balloon", "tether", "leaper", "spitter", "armed_veteran", "boss", "boss_bull_giant", "boss_balloon_king", "boss_spitter_mother" };
            foreach (var (yaw, spawnHeight, groundHeight) in new[] { (0f, 0f, 0f), (1.1f, 2f, 2f), (-2.3f, 4f, 0f) })
            {
                StaticBody3D? platform = null;
                if (groundHeight > 0)
                {
                    platform = new StaticBody3D { Position = at + Vector3.Up * (groundHeight - .1f), CollisionLayer = Layers.World };
                    platform.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10, .2f, 10) } });
                    AddChild(platform); await Frames(2);
                }
                stage.Player.TeleportTo(at + Vector3.Right * 12);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var bodies = ids.Select(id =>
                {
                    var e = stage.Spawn(MonsterDatabase.Get(id), at + Vector3.Up * spawnHeight, awake: false);
                    e.SetPhysicsProcess(false); e.Rig.Rotation = new Vector3(0, yaw, 0);
                    e.Rig.ShowAs(VisionSystem.Visibility.Visible);
                    e.Die(Vector3.Right, 6);
                    var c = stage.Effects.NamedCorpses.Last().Visual!;
                    return (id, body: c, start: c.GlobalTransform, anchor: c.NamedRecord!.GlobalPosition, rig: c.Rig!);
                }).ToArray();
                GD.Print($"DEATH_SETUP 10 actual spawns/deaths {timer.Elapsed.TotalMilliseconds:0.00}ms (includes loading/effects; not GPU frame time)");
                // Follow the whole collapse, including the initial and final frames.
                float lowest = bodies.Min(b => Bottom(b.rig) - b.anchor.Y);
                for (int i = 0; i < 75; i++)
                {
                    await Frames(1);
                    lowest = Mathf.Min(lowest, bodies.Min(b => Bottom(b.rig) - b.anchor.Y));
                }
                Check(lowest >= -.025f, $"yaw {yaw}: no visible geometry penetrates flat ground during collapse ({lowest:0.000})");
                foreach (var b in bodies)
                {
                    float gap = Bottom(b.rig) - b.anchor.Y;
                    Check(gap >= -.025f && gap <= .055f, $"{b.id} yaw {yaw}: actual surface rests at ground, gap={gap:0.000}m");
                    Check(b.body.Freeze && b.body.GlobalTransform.IsEqualApprox(b.start) && b.body.NamedRecord!.GlobalPosition.IsEqualApprox(b.anchor),
                        $"{b.id}: death record and collision body stay fixed while visuals collapse");
                    Check(Mathf.Abs(b.anchor.Y - at.Y - groundHeight) < .03f, $"{b.id}: death anchor finds ground at {groundHeight}m after spawn at {spawnHeight}m");
                    b.body.NamedRecord!.QueueFree();
                }
                var bull = bodies.First(b => b.id == "bull").rig.GlobalBasis;
                var heavy = bodies.First(b => b.id == "boss").rig.GlobalBasis;
                Check((bull.X - heavy.X).Length() > .15f, "bull and heavy boss finish in distinct body poses");
                platform?.QueueFree();
                await Frames(2);
            }
            stage.QueueFree(); await Frames(2); QuitHygiene.Collect();
            GD.Print($"MONSTER_DEATH_{(_failures == 0 ? "PASS" : "FAIL")} {_checks}");
            GetTree().Quit(_failures == 0 ? 0 : 1);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
