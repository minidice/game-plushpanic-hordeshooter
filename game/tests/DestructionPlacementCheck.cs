using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Real-map breach placement regression: supported floors, blast opening, retained sides/art, and route access.</summary>
public partial class DestructionPlacementCheck : Node
{
    private Stage? _stage;
    private int _checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            Check(DestructionPlacements.Load("stage2").Count == 2 && DestructionPlacements.Load("stage3").Count == 3, "five trial placements across both Stage 2 maps");
            await MapCheck("stage2", new StageTwoMap());
            await MapCheck("stage3", new LayoutMap());
            GD.Print("DESTRUCTION_PLACEMENT_CHECK_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("DESTRUCTION_PLACEMENT_CHECK_FAIL " + e.Message); exit = 1; }
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(3); }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private async Task MapCheck(string key, StageMap map)
    {
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false,
            MapOverride = map, ModeOverride = GameMode.Scenario, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(5);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        Check(_stage.ExitFlow.Reachable(map.PlayerStart), key + ": intact walls preserve hatch-to-exit path");
        var entries = map.Encounters!.Entries;
        var beforeReachable = entries.Select(e => _stage.ExitFlow.Reachable(e.Ground)).ToArray();
        if (key == "stage2") Check(beforeReachable.All(x => x), "legacy Stage 2: every spawn entry retains its exit route");

        foreach (var p in DestructionPlacements.Load(key))
        {
            var wall = _stage.Breakables.Single(b => b.HasMeta("destruction_placement") && b.GetMeta("destruction_placement").AsString() == p.Id);
            var barrel = _stage.Props.OfType<ExplosiveBarrel>().Single(b => b.HasMeta("destruction_placement") && b.GetMeta("destruction_placement").AsString() == p.Id);
            var visual = wall.GetChildren().OfType<BreakableWallVisual>().Single();
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(p.Yaw));
            var normal = basis * Vector3.Back;
            var tangent = basis * Vector3.Right;
            Check(wall.GlobalPosition.DistanceTo(p.At) < .01f && barrel.GlobalPosition.DistanceTo(p.Barrel) < .15f,
                p.Id + ": paired wall and real barrel at configured floor positions");
            var shape = wall.GetChildren().OfType<CollisionShape3D>().Single();
            Check(shape.Shape is BoxShape3D box && Mathf.IsEqualApprox(box.Size.X, BreakableWallArt.OpeningWidth)
                && Mathf.IsEqualApprox(shape.Position.Y, BreakableWallArt.Height / 2), p.Id + ": central collision starts at floor, 3.2m wide");
            foreach (var at in new[] { p.At + normal * .8f, p.At - normal * .8f, p.Barrel })
            {
                var hit = Rays.Cast(_stage, at + Vector3.Up * .5f, at - Vector3.Up * .5f, Layers.MovementWorld);
                Check(hit is { } h && Mathf.Abs(h.Position.Y - p.At.Y) < .05f, p.Id + ": physical floor supports both sides and barrel " + at);
            }
            if (map is LayoutMap draft)
            {
                foreach (var endpoint in draft.Layout.Links.SelectMany(l => new[] { l.From, l.To }))
                {
                    var offset = basis.Inverse() * (new Vector3(endpoint.X, p.At.Y, endpoint.Y) - p.At);
                    float right = p.PlainPanel ? 7.2f : 2.4f;
                    Check(offset.X < -3.2f || offset.X > right + .8f || Mathf.Abs(offset.Z) > 1.025f,
                        p.Id + ": traversal endpoint outside wall and panel footprint " + endpoint);
                }
            }
            uint bullets = Layers.BulletMask & ~Layers.Props;
            Vector3 middle = p.At + Vector3.Up * Player.AimHeight;
            Check(Rays.Cast(_stage, middle - normal * .8f, middle + normal * .8f, bullets)?.Collider == wall,
                p.Id + ": intact middle blocks bullet ray");
            Check(Rays.Cast(_stage, middle - normal * .8f, middle + normal * .8f, Layers.MovementWorld)?.Collider == wall
                && _stage.Flow.IsBlocked(p.At), p.Id + ": intact middle blocks movement ray and navigation");
            using var collision = new KinematicCollision3D();
            var start = new Transform3D(Basis.Identity, p.At - normal * .7f + Vector3.Up * .05f);
            Check(_stage.Player.TestMove(start, normal * 1.4f, collision) && collision.GetCollider() == wall,
                p.Id + ": real survivor capsule cannot cross intact center");
            float health = wall.Health;
            wall.TakeDamage(1000, ObjectDamage.Gun, _stage.Player);
            Check(wall.Health == health && !wall.Broken, p.Id + ": gun damage cannot open explosive-only wall");
            int version = _stage.FlowVersion;
            barrel.TakeDamage(barrel.Health, ObjectDamage.Gun, _stage.Player);
            Check(barrel.Ignited && !barrel.Exploded && IsInstanceValid(wall), p.Id + ": shooting barrel starts actual timed fuse");
            await Frames(80);
            Check(!IsInstanceValid(barrel) && !IsInstanceValid(wall), p.Id + ": barrel timer blast removes both barrel and breach body");
            Check(_stage.FlowVersion > version && !_stage.Flow.IsBlocked(p.At), p.Id + ": blast rebuilds flow and opens middle cell");
            foreach (uint mask in new[] { bullets, Layers.MovementWorld })
                Check(Rays.Clear(_stage, middle - normal * 2, middle + normal * 2, mask), p.Id + ": no hidden collider behind opened center mask=" + mask);
            Check(!_stage.Player.TestMove(start, normal * 1.4f, collision), p.Id + ": survivor capsule can cross broken center");
            foreach (float sign in new[] { -1f, 1f })
            {
                var side = p.At + tangent * (sign * 2);
                Check(!Rays.Clear(_stage, side + Vector3.Up - normal * .8f, side + Vector3.Up + normal * .8f, Layers.MovementWorld)
                    && _stage.Flow.IsBlocked(side), p.Id + ": permanent shoulder survives physics and flow " + sign);
            }
            Check(IsInstanceValid(visual) && visual.GetParent() == _stage && visual.GlobalPosition.DistanceTo(p.At) < .01f
                && !visual.GetNode<Node3D>("IntactPanels").Visible && visual.GetNode<Node3D>("GroundGravel").Visible,
                p.Id + ": animated rubble remains on original floor after body removal");
            if (p.PlainPanel)
            {
                var panel = p.At + tangent * BreakableWallArt.Width;
                Check(!Rays.Clear(_stage, panel + Vector3.Up - normal * .8f, panel + Vector3.Up + normal * .8f, Layers.MovementWorld)
                    && _stage.Flow.IsBlocked(panel), p.Id + ": adjacent plain panel remains intact");
            }
        }
        Check(_stage.ExitFlow.Reachable(map.PlayerStart), key + ": exit remains reachable after every wall collapses");
        // Draft spawn anchors can have existing tier restrictions. A newly reachable anchor means an intact trial wall
        // severed a formerly usable spawn route; compare every anchor without redefining unrelated terrain contracts.
        var newlyReachable = entries.Where((e, i) => !beforeReachable[i] && _stage.ExitFlow.Reachable(e.Ground)).ToArray();
        Check(newlyReachable.Length == 0, key + ": no spawn route was gated by an intact trial wall " + string.Join(",", newlyReachable.Select(e => e.Ground)));
        _stage.QueueFree();
        await Frames(3);
        _stage = null;
    }
}
