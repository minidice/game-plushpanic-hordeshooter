using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Height tiers, first pass (decisions.md L-15 · L-16, doc/elevation-tiers.md): a test field with a tier-1 and a
/// tier-2 plateau, two ramps and one drop edge. Checks that flat maps stay flat, the flow field climbs ramps and
/// drops only at drop edges, walking keeps survivors on their tier while shoves do not, the cursor lands on the
/// tier under it, shots reach down but not up, melee reaches one tier across an edge, lobs and throws land on
/// the tier's floor, and blasts reach one tier.
/// </summary>
public partial class ElevationCheck : Node
{
    /// <summary>
    /// Ground (tier 0) x −20…40. Tier 1 plateau x 10…25, z −15…0, reached by a ramp at x 4…10. Tier 2 plateau x 25…40,
    /// z −15…0, reached from tier 1 by a ramp at x 19…25, with a drop edge on its camera side at x 30…36.
    /// </summary>
    private sealed class TierTestMap : StageMap
    {
        public override Rect2 Area => new(-22, -17, 64, 34);

        protected override void BuildMap()
        {
            PlayerStart = new Vector3(-5, 0, 5);
            Raised(-20, 40, -15, 15, 0, "3a4a36");
            Raised(10, 25, -15, 0, 1, "6b6450");
            Raised(25, 40, -15, 0, 2, "8a7f60");
            Ramp(4, 10, -6, -2, 0, Vector3.Right, "7a705a");
            Ramp(19, 25, -12, -8, 1, Vector3.Right, "7a705a");
            DropEdge(30, 36, -1, 0);
            Lamp(new Vector3(10, 6, 0), "ffe6b0", 4, 40, false);
        }
    }

    private sealed class FlatMap : StageMap
    {
        public override Rect2 Area => new(-12, -12, 24, 24);
        protected override void BuildMap()
        {
            PlayerStart = Vector3.Zero;
            Floor(-10, 10, -10, 10, "3a4a36");
        }
    }

    private const float H = TierMap.Height;
    private int _checks;
    private Stage _stage = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("ELEVATION_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("ELEVATION_CHECK_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(StageMap map)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false };
        AddChild(_stage);
        await Frames(3);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        foreach (var e in _stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
        return _stage;
    }

    private async Task Place(Player player, Vector3 at, Vector3 aim)
    {
        player.GlobalPosition = at + Vector3.Up * .05f;
        player.Velocity = Vector3.Zero;
        player.SetControls(Vector2.Zero, aim, false);
        await Frames(4);
    }

    /// <summary>Walks the flow field cell to cell from <paramref name="from"/>, as its directions say; returns the cell centres.</summary>
    private static List<Vector3> FollowFlow(FlowField flow, Vector3 from, Vector3 goal)
    {
        var path = new List<Vector3> { flow.Center(flow.Index(from)) };
        for (int i = 0; i < 600 && path[^1].DistanceTo(goal with { Y = 0 }) > 1.2f; i++)
        {
            var dir = flow.Direction(path[^1]);
            if (dir == Vector3.Zero) break;
            float step = Mathf.Abs(dir.X) > .1f && Mathf.Abs(dir.Z) > .1f ? Mathf.Sqrt2 * flow.Cell : flow.Cell;
            path.Add(flow.Center(flow.Index(path[^1] + dir * step)));
        }
        return path;
    }

    private static bool InRect(Vector3 p, float x0, float x1, float z0, float z1) => p.X >= x0 && p.X <= x1 && p.Z >= z0 && p.Z <= z1;

    private async Task Run()
    {
        // ---------- flat maps keep the old paths ----------
        var stage = await NewStage(new FlatMap());
        Check(!stage.Tiers.Any && stage.Tiers.GroundY(new Vector3(3, 0, 3)) == 0, "a map without tiers stays flat (every tier rule is skipped)");

        stage = await NewStage(new TierTestMap());
        var tiers = stage.Tiers;
        var player = stage.Player;

        // ---------- painting ----------
        Check(tiers.Any && tiers.MaxTier == 2, "the test field has tiers 0 to 2");
        Check(tiers.TierAt(new Vector3(0, 0, 5)) == 0 && tiers.TierAt(new Vector3(15, 0, -8)) == 1 && tiers.TierAt(new Vector3(32, 0, -8)) == 2,
            "ground, tier-1 and tier-2 plateaus are painted where they were raised");
        Check(Mathf.IsEqualApprox(tiers.GroundY(new Vector3(15, 0, -8)), H) && Mathf.IsEqualApprox(tiers.GroundY(new Vector3(32, 0, -8)), 2 * H),
            "a tier's floor is tier × 1.2 m high");
        float rampMid = tiers.GroundY(new Vector3(7, 0, -4));
        Check(rampMid > .3f && rampMid < .9f, $"partway up a ramp the floor is between the tiers ({rampMid:0.00} m)");

        // ---------- flow field ----------
        stage.Flow.Update(new Vector3(32, 2 * H, -8));
        var up = FollowFlow(stage.Flow, new Vector3(-5, 0, 5), new Vector3(32, 0, -8));
        Check(up[^1].DistanceTo(new Vector3(32, 0, -8)) <= 1.2f, $"a monster on the ground finds the way up to tier 2 ({up.Count} steps)");
        Check(up.Any(p => InRect(p, 4, 10, -6, -2)) && up.Any(p => InRect(p, 19, 25, -12, -8)), "it climbs by both ramps, not up a cliff");
        Check(up.Zip(up.Skip(1)).All(s => tiers.CanWalk(s.First, s.Second)), "every step of that path is a step the tiers allow");
        var underDrop = FollowFlow(stage.Flow, new Vector3(33, 0, 2), new Vector3(32, 0, -8));
        Check(underDrop.Any(p => InRect(p, 4, 10, -6, -2)), "the drop edge is one way: from under it the path still goes round by the ramps");

        stage.Flow.Update(new Vector3(33, 0, 5));
        var down = FollowFlow(stage.Flow, new Vector3(33, 2 * H, -8), new Vector3(33, 0, 5));
        Check(down[^1].DistanceTo(new Vector3(33, 0, 5)) <= 1.2f && down.Count < 25, $"from tier 2 the way down is the drop edge ({down.Count} steps)");
        Check(down.Any(p => InRect(p, 30, 36, -1, 0)), "that path leaves tier 2 over the drop edge");

        stage.Flow.Update(new Vector3(15, 0, 6));
        var offTier1 = FollowFlow(stage.Flow, new Vector3(15, H, -5), new Vector3(15, 0, 6));
        Check(offTier1.Any(p => InRect(p, 4, 10, -6, -2)), "tier 1 has no drop edge: its way down is the ramp");

        // ---------- walking and shoves ----------
        await Place(player, new Vector3(15, H, -2), new Vector3(15, H + 1, 5));
        player.SetControls(new Vector2(0, 1), new Vector3(15, H + 1, 5), false);
        await Frames(60);
        Check(TierMap.TierOfHeight(player.GlobalPosition.Y) == 1 && player.GlobalPosition.Z < 0, "walking into a tier's edge stops at it");

        await Place(player, new Vector3(33, 2 * H, -2), new Vector3(33, 1, 6));
        player.SetControls(new Vector2(0, 1), new Vector3(33, 1, 6), false);
        await Frames(60);
        player.SetControls(Vector2.Zero, new Vector3(33, 1, 6), false);
        await Frames(30);
        Check(TierMap.TierOfHeight(player.GlobalPosition.Y) == 0 && player.GlobalPosition.Z > 0, "walking off a drop edge drops to the ground");

        await Place(player, new Vector3(2, 0, -4), new Vector3(20, 2, -4));
        player.SetControls(new Vector2(1, 0), new Vector3(20, 2, -4), false);
        await Frames(160);
        player.SetControls(Vector2.Zero, new Vector3(20, 2, -4), false);
        await Frames(10);
        Check(TierMap.TierOfHeight(player.GlobalPosition.Y) == 1 && player.GlobalPosition.X > 10, $"walking up the ramp reaches tier 1 (y {player.GlobalPosition.Y:0.00})");

        await Place(player, new Vector3(15, H, -1), new Vector3(15, 1, 5));
        player.Launch(new Vector3(0, 3, 8), .4f);
        await Frames(90);
        Check(TierMap.TierOfHeight(player.GlobalPosition.Y) == 0, "a shove can still throw a survivor off a tier edge (L-16)");

        // ---------- cursor ----------
        var eye = new Vector3(15, 20, 12);
        var onPlateau = tiers.AimPoint(eye, (new Vector3(15, H + Player.AimHeight, -8) - eye).Normalized(), Player.AimHeight);
        Check(onPlateau is { } a1 && a1.DistanceTo(new Vector3(15, H + Player.AimHeight, -8)) < .05f, "the cursor over tier 1 lands on tier 1's aim plane");
        var onGround = tiers.AimPoint(eye, (new Vector3(-5, Player.AimHeight, 5) - eye).Normalized(), Player.AimHeight);
        Check(onGround is { } a2 && a2.DistanceTo(new Vector3(-5, Player.AimHeight, 5)) < .05f, "the cursor over the ground passes the higher planes and lands on the ground");

        // ---------- shooting ----------
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        var common = stage.CommonDefinition;

        async Task<float> Shoot(Vector3 from, Vector3 target)
        {
            foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
            await Frames(2);
            var enemy = stage.SpawnEnemy<Enemy>(common, target, awake: false);
            await Frames(2);
            float before = enemy.Health;
            var aim = enemy.GlobalPosition + Vector3.Up * Player.AimHeight;
            await Place(player, from, aim);
            for (int i = 0; i < 40 && IsInstanceValid(enemy) && !enemy.IsDead; i++)
            {
                player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up * Player.AimHeight, i % 2 == 0);
                await Frames(1);
            }
            player.SetControls(Vector2.Zero, aim, false);
            await Frames(2);
            return IsInstanceValid(enemy) && !enemy.IsDead ? before - enemy.Health : before;
        }

        float downShot = await Shoot(new Vector3(32, 2 * H, -3), new Vector3(32, 0, 7));
        Check(downShot > 0, $"from tier 2 a gun hits a monster on the ground below ({downShot:0} damage)");
        float upShot = await Shoot(new Vector3(2, 0, -10), new Vector3(16, H, -10));
        Check(upShot == 0, "from the ground a gun cannot hit a monster up on tier 1: the cliff takes the bullets");
        float levelShot = await Shoot(new Vector3(12, H, -10), new Vector3(20, H, -10));
        Check(levelShot > 0, $"on the same tier shooting works as before ({levelShot:0} damage)");

        // ---------- melee ----------
        Check(tiers.MeleeReach(new Vector3(15, H, -.4f), new Vector3(15, 0, .5f)), "one tier apart at the edge, melee reaches");
        Check(!tiers.MeleeReach(new Vector3(15, H, -4), new Vector3(15, 0, .5f)), "one tier apart away from the edge, it does not");
        Check(!tiers.MeleeReach(new Vector3(33, 2 * H, -.4f), new Vector3(33, 0, .5f)), "two tiers apart, melee never reaches");

        foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
        await Place(player, new Vector3(15, H, -.45f), new Vector3(15, 1, 5));
        player.Invulnerable = false;
        int health = player.Health;
        var biter = stage.SpawnEnemy<Enemy>(common, new Vector3(15, 0, .55f), awake: true);
        for (int i = 0; i < 180 && player.Health == health; i++) await Frames(1);
        Check(player.Health < health, "a monster at the cliff foot bites a survivor standing on the edge one tier up");
        if (IsInstanceValid(biter)) biter.QueueFree();
        player.Invulnerable = true;
        await Frames(2);

        // ---------- lobs, throws and blasts ----------
        var shell = MortarShell.Fire(stage, player.GlobalPosition + Vector3.Up * 2, new Vector3(15, 9, -8), ArmoryCatalog.Weapon("mortar"), player);
        Check(Mathf.IsEqualApprox(shell.Target.Y, H), "a mortar shell aimed at tier 1 lands on tier 1's floor");
        shell.QueueFree();
        var hand = new Vector3(12, H + 1.5f, -8);
        var frag = ThrownItem.Throw(stage, ArmoryCatalog.Throwable("frag"), hand, new Vector3(18, 0, -8));
        var expected = ThrownItem.LaunchVelocity(hand, new Vector3(18, H + .2f, -8));
        Check(frag.LinearVelocity.DistanceTo(expected) < .01f, "a throw onto tier 1 is aimed at tier 1's floor, not the ground under it");
        frag.QueueFree();

        foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
        var twoBelow = stage.SpawnEnemy<Enemy>(common, new Vector3(33, 0, 1.2f), awake: false);
        var oneBelow = stage.SpawnEnemy<Enemy>(common, new Vector3(23, H, -6), awake: false);
        await Frames(2);
        float twoBefore = twoBelow.Health, oneBefore = oneBelow.Health;
        await Place(player, new Vector3(-10, 0, 8), new Vector3(0, 1, 8));
        Blast.Explode(stage, new Vector3(33, 2 * H, -1), 5, 80, Colors.Orange);
        Blast.Explode(stage, new Vector3(27, 2 * H, -6), 5, 80, Colors.Orange);
        await Frames(2);
        Check(IsInstanceValid(twoBelow) && !twoBelow.IsDead && twoBelow.Health == twoBefore, "a blast does not reach two tiers down");
        Check(!IsInstanceValid(oneBelow) || oneBelow.IsDead || oneBelow.Health < oneBefore, "a blast reaches one tier down");

        // A rocket bursting at chest height on the ground is still a ground blast: it does not reach tier 2.
        var chestHigh = stage.SpawnEnemy<Enemy>(common, new Vector3(33, 2 * H, -1.5f), awake: false);
        await Frames(2);
        float chestBefore = chestHigh.Health;
        Blast.Explode(stage, new Vector3(33, Player.AimHeight, 1.5f), 5, 80, Colors.Orange);
        await Frames(2);
        Check(IsInstanceValid(chestHigh) && !chestHigh.IsDead && chestHigh.Health == chestBefore, "a blast at chest height on the ground counts as a ground blast");

        // ---------- spawning ----------
        var lifted = stage.SpawnEnemy<Enemy>(common, new Vector3(15, 0, -10), awake: false);
        Check(lifted.Position.Y >= H, "a monster spawned over a plateau starts on its floor, not inside it");
    }
}
