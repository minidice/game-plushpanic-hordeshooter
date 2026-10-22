using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class CorpseReactionCheck : Node3D
{
    private Stage _stage = null!;
    private int _checks, _failures;
    private readonly Vector3 _origin = new(200, 0, 0);
    public override async void _Ready()
    {
        try
        {
            _stage = new Stage { CharacterOverride = "test_capsule", DirectorEnabled = false };
            AddChild(_stage);
            foreach (var enemy in _stage.AllEnemies.ToArray()) enemy.QueueFree();
            _stage.Player.ManualControl = true; _stage.Player.Invulnerable = true;
            _stage.Player.SetPhysicsProcess(false);
            _stage.Cam.SetProcess(false); _stage.Cam.Size = 22; _stage.Cam.SnapTo(_origin + Vector3.Forward * 2);
            var floor = new StaticBody3D { Position = _origin + Vector3.Down * .5f, CollisionLayer = Layers.World };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) } });
            Mat.Mesh(floor, new BoxMesh { Size = new Vector3(60, 1, 60) }, Vector3.Zero, Mat.Solid("6d7784"));
            AddChild(floor);
            await Frames(3);

            var walk = await Contact(false);
            var roll = await Contact(true);
            Check(walk > .15f, "walking actually pushes a sleeping corpse");
            Check(roll > walk + .15f, "rolling sends a corpse farther than walking");

            var late = Body(new Vector3(-3, 0, -3));
            late.GravityScale = 1; late.Sleeping = false;
            await Frames(420);
            Check(late.Sleeping && !late.Freeze, "common corpse rests in native sleep after seven seconds without permanent freeze");
            var restingAt = late.GlobalPosition;
            _stage.Player.Position = new Vector3(restingAt.X - .7f, 0, restingAt.Z);
            _stage.Player.Velocity = Vector3.Zero;
            _stage.Player.SetControls(Vector2.Right, restingAt + Vector3.Right, false);
            _stage.Player.SetPhysicsProcess(true);
            await Frames(12);
            _stage.Player.SetPhysicsProcess(false);
            Check(late.GlobalPosition.DistanceTo(restingAt) > .15f, "walking wakes a corpse after the old five-second freeze deadline");
            late.QueueFree();
            await Frames(2);

            var near = Body(new Vector3(1, 0, 0));
            var edge = Body(new Vector3(-5, 0, 0));
            var outside = Body(new Vector3(8, 0, 0));
            var covered = Body(new Vector3(0, 0, 4));
            var wall = new StaticBody3D { Position = _origin + new Vector3(0, 1.5f, 2.5f), CollisionLayer = Layers.World };
            wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, 3, .2f) } });
            AddChild(wall);
            var namedEnemy = _stage.Spawn(MonsterDatabase.Get("spitter"), _origin + new Vector3(3, 4, -2), true);
            namedEnemy.SetPhysicsProcess(false); namedEnemy.Rig.Face(Vector3.Left);
            namedEnemy.Rig.ShowAs(VisionSystem.Visibility.Silhouette);
            namedEnemy.Die(Vector3.Right, 20);
            var named = _stage.Effects.NamedCorpses.Last().Visual!;
            var namedAt = named.GlobalTransform;
            Check(named.GlobalPosition.Y < 1 && named.NamedRecord!.GlobalPosition.Y < .1f,
                "an airborne named death is anchored to the ground instead of freezing in midair");
            int column = 0;
            foreach (var id in new[] { "bull", "balloon", "tether", "leaper", "spitter", "armed_veteran" })
            {
                var enemy = _stage.Spawn(MonsterDatabase.Get(id), _origin + new Vector3(-6 + column++ * 2.5f, 4, -7), true);
                enemy.SetPhysicsProcess(false); enemy.Die(Vector3.Right, 20);
                var fixedBody = _stage.Effects.NamedCorpses.Last().Visual!;
                Check(MinimumMeshY(fixedBody.Rig!) >= -.02f, id + " fixed corpse geometry stays above ground");
            }
            // A named corpse topples into its fixed pose (I-83): it starts upright, never dips into the floor on the
            // way down, and ends lying exactly where its body was placed at once.
            var topple = _stage.Spawn(MonsterDatabase.Get("bull"), _origin + new Vector3(8, 0, -9), true);
            topple.SetPhysicsProcess(false); topple.Die(Vector3.Right, 20);
            var toppled = _stage.Effects.NamedCorpses.Last().Visual!;
            var lyingAt = toppled.GlobalTransform;
            Check(toppled.Rig!.Rotation.X > 1.4f, "a named corpse starts upright and falls over");
            float lowest = float.PositiveInfinity;
            ulong until = Time.GetTicksMsec() + (ulong)(Effects.NamedDeathMaxSeconds * 1000) + 300;
            while (Time.GetTicksMsec() < until)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                lowest = Mathf.Min(lowest, MinimumMeshY(toppled.Rig!));
            }
            Check(lowest >= -.02f, $"it never sinks into the floor while falling (lowest {lowest:0.000} m)");
            Check(Mathf.Abs(toppled.Rig!.GlobalBasis.Y.Dot(Vector3.Up)) < .35f && toppled.GlobalTransform.IsEqualApprox(lyingAt), "and ends lying on its side, its body never moved");
            await Frames(90);
            Check(named.Rig!.Visible && !named.Rig.IsSilhouette, "death restores full corpse materials from a living silhouette");
            var fresh = _stage.Spawn(MonsterDatabase.Get("humanoid"), _origin + new Vector3(2, 0, -1), true);
            fresh.SetPhysicsProcess(false);
            _stage.Player.Position = _origin + Vector3.Left * 15;
            await Frames(3);
            await Shot("before-blast");
            Blast.Explode(_stage, _origin, 6, 10000, Colors.Orange);
            await Frames(3);
            Check(near.LinearVelocity.X > 3 && near.LinearVelocity.Y > 2, "blast wakes and launches an existing corpse outward and upward");
            Check(near.LinearVelocity.Length() > edge.LinearVelocity.Length() + 1, "blast impulse falls off toward its radius");
            Check(outside.LinearVelocity.Length() < .1f, "out-of-range corpse is unaffected");
            Check(covered.LinearVelocity.Length() < .1f, "world cover blocks corpse blast impulse");
            Check(fresh.IsDead, "blast kills a living common monster");
            Check(_stage.Effects.GetChildren().OfType<Corpse>().Any(c => c.Rig == fresh.Rig && c.LinearVelocity.Y > 2),
                "corpse created by this blast launches in the same blast");
            Check(named.Freeze && named.GlobalTransform.IsEqualApprox(namedAt), "named corpse ignores death and explosion impulses");
            Check((named.CollisionMask & (Layers.Player | Layers.Enemy | Layers.Props)) == 0,
                "named corpse cannot obstruct gameplay bodies");
            await Frames(12);
            await Shot("after-blast");
            near.QueueFree(); edge.QueueFree(); outside.QueueFree(); covered.QueueFree(); wall.QueueFree();
            await Frames(2);
            var source = new StaticBody3D { Position = _origin + Vector3.Up, CollisionLayer = Layers.Props };
            source.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * 2 } });
            AddChild(source);
            var sourceProbe = Body(new Vector3(3, 0, 0));
            await Frames(2);
            Blast.Explode(_stage, source.GlobalPosition, 6, 0, Colors.Orange, source);
            await Frames(3);
            Check(sourceProbe.LinearVelocity.X > 3, "explosion source collider does not shield nearby corpses");
            source.QueueFree(); sourceProbe.QueueFree();
            await Frames(2);
            await ContactCover();
            _stage.QueueFree(); await Frames(2);
            GD.Print($"CORPSE_REACTION_{(_failures == 0 ? "PASS" : "FAIL")} {_checks}");
            QuitHygiene.Collect(); GetTree().Quit(_failures == 0 ? 0 : 1);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private Corpse Body(Vector3 offset)
    {
        var rig = CharacterRig.Create(CharacterCatalog.Enemy("humanoid"));
        AddChild(rig); rig.Position = _origin + offset;
        var body = (Corpse)_stage.Effects.SpawnCorpse(rig, .4f, 1.1f, Vector3.Zero, Vector3.Zero);
        body.GravityScale = 0; body.Sleeping = true;
        return body;
    }
    private async Task<float> Contact(bool roll)
    {
        var player = _stage.Player;
        player.Position = _origin; player.Velocity = Vector3.Zero;
        var body = Body(new Vector3(.85f, 0, .25f));
        await Frames(2);
        var start = body.GlobalPosition;
        player.SetControls(Vector2.Right, _origin + Vector3.Right * 10, false);
        if (roll) player.PressRoll();
        player.SetPhysicsProcess(true);
        await Frames(12);
        player.SetPhysicsProcess(false); player.SetControls(Vector2.Zero, player.Position + Vector3.Right, false);
        Check(player.Position.X > _origin.X + .5f, (roll ? "roll" : "walk") + " is not blocked by local corpse");
        float moved = body.Position.X - start.X;
        Check(body.AngularVelocity.Length() > .05f, (roll ? "roll" : "walk") + " glancing contact rotates corpse");
        body.QueueFree(); await Frames(2);
        return moved;
    }
    private async Task ContactCover()
    {
        var player = _stage.Player;
        player.Position = _origin; player.Velocity = Vector3.Zero;
        var body = Body(new Vector3(.9f, 0, 0));
        var wall = new StaticBody3D { Position = _origin + new Vector3(.5f, 1, 0), CollisionLayer = Layers.World };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.1f, 2, 4) } });
        AddChild(wall);
        await Frames(2);
        player.SetControls(Vector2.Right, _origin + Vector3.Right * 10, false);
        player.SetPhysicsProcess(true);
        await Frames(15);
        player.SetPhysicsProcess(false);
        Check(body.LinearVelocity.Length() < .1f, "walking against a wall does not push a corpse on the other side");
        body.QueueFree(); wall.QueueFree();
        await Frames(2);
    }
    private void Check(bool pass, string message)
    { _checks++; if (!pass) _failures++; GD.Print((pass ? "PASS: " : "FAIL: ") + message); }
    private async Task Frames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private static float MinimumMeshY(Node node)
    {
        float bottom = float.PositiveInfinity;
        if (node is MeshInstance3D mesh && mesh.IsVisibleInTree())
        {
            if (mesh.Mesh != null && !mesh.HasMeta("authored_grip"))
                for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
                    foreach (var vertex in mesh.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                        bottom = Mathf.Min(bottom, (mesh.GlobalTransform * vertex).Y);
        }
        foreach (Node child in node.GetChildren()) bottom = Mathf.Min(bottom, MinimumMeshY(child));
        return bottom;
    }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(ProjectSettings.GlobalizePath($"res://output/corpse-reaction-{name}.png")) != Error.Ok)
            throw new Exception("Screenshot failed");
    }
}
