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
    /// Ground (tier 0) x −20…40. Tier 1 plateau x 10…25, z −15…0, reached by a ramp at x 4…10 (and its west face x 10,
    /// z −14…−8 is climbable). Tier 2 plateau x 25…40, z −15…0, reached from tier 1 by a ramp at x 19…25, with a drop
    /// edge on its camera side at x 30…36 and a ladder at x 27.
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
            // Tier 1's west face is climbable rock; tier 2 has a ladder on its camera side.
            ClimbableWall(9, 11, -14, -8);
            Ladder(new Vector3(27, 0, .6f), new Vector3(27, 0, -.6f));
            // A zipline from tier 2 down to the ground, and a gondola station on the ground (L-18).
            Zipline(new Vector3(37, 0, -12), new Vector3(37, 0, 12));
            Gondola(new Rect2(-18, -12, 4, 4), 0, new Vector3(-16, 0, 10), Vector3.Back, 2f);
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

    private async Task<Stage> NewStage(StageMap map, int squad = 0)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false,
            SquadOverride = squad };
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
        Check(!underDrop.Any(p => InRect(p, 30, 36, -1, 0)) && underDrop[^1].DistanceTo(new Vector3(32, 0, -8)) <= 1.2f,
            "the drop edge is one way: from under it the path goes round (here by the ladder), never up the drop edge");

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

        await Vision();
        await Ranged();
        await Squad();
        await Climbing();
        await Rides();
    }

    /// <summary>L-18: the zipline is a one-way ride for survivors only; the gondola waits for everyone and then plays out.</summary>
    private async Task Rides()
    {
        var stage = await NewStage(new TierTestMap(), squad: 1);
        var player = stage.Player;
        var tiers = stage.Tiers;
        var bot = stage.Squad[0].Bot;
        var brain = stage.Squad[0];
        bot.Invulnerable = true;
        var line = stage.Ziplines.Single();
        var gondola = stage.Gondolas.Single();

        // ---------- zipline ----------
        var riders = stage.Flow.CopyLayout();
        riders.Riders = true;
        riders.Update(line.End);
        var walkers = stage.Flow.CopyLayout();
        walkers.Update(line.End);
        Check(riders.NextCell(line.Start) == line.EndCell && riders.Distance(line.Start) < walkers.Distance(line.Start) - 100,
            "a survivor's route from the top takes the zipline");
        Check(stage.Flow.Riders == false && walkers.NextCell(line.Start) != line.EndCell, "a monster's route never does");

        brain.ProcessMode = ProcessModeEnum.Disabled;
        await Place(bot, new Vector3(-15, 0, 10), new Vector3(-15, 1, 14));
        await Place(player, line.Start, line.End);
        Check(player.RideZipline(line), "a survivor hooks onto the zipline at its top");
        int mag = player.Gun.Mag;
        player.SetControls(Vector2.Zero, line.End, true);
        player.Launch(new Vector3(6, 3, 0), .4f);
        await Frames(20);
        Check(player.OnZipline && player.Gun.Mag == mag, "on the zipline: no shooting, and a hit does not knock you off");
        player.SetControls(Vector2.Zero, line.End, false);
        for (int i = 0; i < 400 && player.OnZipline; i++) await Frames(1);
        Check(!player.OnZipline && tiers.TierOf(player) == 0 && player.GlobalPosition.DistanceTo(line.End) < 1.5f,
            $"the ride ends at the foot ({line.Seconds:0.0} s for {line.Start.DistanceTo(line.End):0} m)");

        brain.ProcessMode = ProcessModeEnum.Inherit;
        await Place(player, line.End + Vector3.Right * 1.5f, line.End);
        await Place(bot, line.Start, line.End);
        bool rode = false;
        for (int i = 0; i < 420 && tiers.TierOf(bot) != 0; i++)
        {
            await Frames(1);
            rode |= bot.OnZipline;
        }
        Check(rode && tiers.TierOf(bot) == 0, "a squadmate rides the zipline down after the leader");
        for (int i = 0; i < 240 && bot.OnZipline; i++) await Frames(1);

        // ---------- gondola ----------
        brain.ProcessMode = ProcessModeEnum.Disabled;
        var platform = new Vector3(gondola.Platform.GetCenter().X, 0, gondola.Platform.GetCenter().Y);
        await Place(player, platform, platform + Vector3.Back * 3);
        await Place(bot, platform + Vector3.Back * 6, platform + Vector3.Back * 9);
        Check(!gondola.TryDepart() && !stage.AllStopped, "the gondola waits while a living survivor is not aboard");
        await Place(bot, platform + Vector3.Right * .8f, platform + Vector3.Back * 3);
        var watcher = stage.SpawnEnemy<Enemy>(stage.CommonDefinition, platform + Vector3.Back * 9, awake: true);
        await Frames(2);
        var watcherAt = watcher.GlobalPosition;
        Check(gondola.TryDepart() && stage.AllStopped && gondola.Riding, "with everyone aboard it leaves and the world holds");
        await Frames(40);
        Check(gondola.BackdropTravel > 0 && watcher.GlobalPosition.DistanceTo(watcherAt) < .01f && player.GlobalPosition.DistanceTo(platform) < 1f,
            "during the ride the riders stay put, monsters hold, and only the fake gorge scrolls");
        for (int i = 0; i < 300 && gondola.Riding; i++) await Frames(1);
        Check(!gondola.Riding && !stage.AllStopped && player.GlobalPosition.DistanceTo(gondola.Arrival) < 2.5f && bot.GlobalPosition.DistanceTo(gondola.Arrival) < 2.5f,
            "at the end everyone steps off at the far station and the world goes on");
        brain.ProcessMode = ProcessModeEnum.Inherit;
    }

    /// <summary>L-16: each way of climbing has its own route, monsters climb for real, survivors and squadmates take ladders.</summary>
    private async Task Climbing()
    {
        var stage = await NewStage(new TierTestMap(), squad: 1);
        var player = stage.Player;
        var tiers = stage.Tiers;
        var bot = stage.Squad[0].Bot;
        bot.Invulnerable = true;
        var ladder = stage.Ladders.Single();
        async Task Clear()
        {
            foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
            await Frames(2);
        }
        await Clear();

        // ---------- a route per way of climbing ----------
        await Place(player, new Vector3(20, H, -13), new Vector3(24, 1, -13));
        await Place(bot, new Vector3(20.6f, H, -13), new Vector3(24, 1, -13));
        var wall = FollowFlow(stage.FlowFor(MonsterClimb.Wall), new Vector3(5, 0, -13), new Vector3(20, 0, -13));
        Check(wall[^1].DistanceTo(new Vector3(20, 0, -13)) <= 1.2f && !wall.Any(p => InRect(p, 4, 10, -6, -2)) && wall.Count < 20,
            $"a wall climber's route goes straight up the climbable face ({wall.Count} steps)");
        var walker = FollowFlow(stage.Flow, new Vector3(5, 0, -13), new Vector3(20, 0, -13));
        Check(walker.Any(p => InRect(p, 4, 10, -6, -2)), "a ladder user's route from the same spot goes round by the ramp");
        var jump = FollowFlow(stage.FlowFor(MonsterClimb.Jump), new Vector3(15, 0, 5), new Vector3(20, 0, -13));
        Check(jump[^1].DistanceTo(new Vector3(20, 0, -13)) <= 1.2f && !jump.Any(p => InRect(p, 4, 10, -6, -2)), "a jumper's route jumps the south face");
        var fly = FollowFlow(stage.FlowFor(MonsterClimb.Fly), new Vector3(15, 0, 5), new Vector3(20, 0, -13));
        Check(fly[^1].DistanceTo(new Vector3(20, 0, -13)) <= 1.2f && fly.Count <= jump.Count, "a flyer's route goes straight over");
        var noClimb = FollowFlow(stage.FlowFor(MonsterClimb.Wall), new Vector3(15, 0, 5), new Vector3(20, 0, -13));
        Check(noClimb.Any(p => InRect(p, 4, 11, -14, -2)), "where the face is not climbable a wall climber goes round to the rock or the ramp");

        // ---------- monsters climb for real ----------
        // The squadmate would shoot them down (it shoots down a tier now): keep it out of this part.
        var brain = stage.Squad[0];
        brain.ProcessMode = ProcessModeEnum.Disabled;
        await Place(bot, new Vector3(-15, 0, 10), new Vector3(-15, 1, 14));
        async Task<Enemy> Climber(string id, Vector3 at, Vector3 target, int tier)
        {
            await Clear();
            await Place(player, target, target + Vector3.Back * 3);
            var e = stage.Spawn(MonsterDatabase.Get(id), at, awake: true);
            for (int i = 0; i < 600 && IsInstanceValid(e) && tiers.TierOf(e) != tier; i++)
            {
                await Frames(1);
            }
            return e;
        }
        var zombie = await Climber("humanoid", new Vector3(6, 0, -13), new Vector3(20, H, -13), 1);
        Check(zombie.ClimbsStarted > 0 && tiers.TierOf(zombie) == 1, "a zombie climbs the rock face up to tier 1");
        var beast = await Climber("wild_animal", new Vector3(30, 0, 4), new Vector3(30, 2 * H, -4), 2);
        Check(beast.ClimbsStarted > 0 && tiers.TierOf(beast) == 2, "a wild animal jumps two tiers straight up");
        var armed = await Climber("armed_animal", new Vector3(27, 0, 4), new Vector3(29, 2 * H, -6), 2);
        Check(armed.ClimbsStarted > 0 && tiers.TierOf(armed) == 2, "an armed animal takes the ladder up to tier 2");

        await Clear();
        await Place(player, new Vector3(20, H, -13), new Vector3(24, 1, -13));
        var hanging = stage.Spawn(MonsterDatabase.Get("humanoid"), new Vector3(8.6f, 0, -13), awake: true);
        for (int i = 0; i < 120 && !hanging.ClimbingWall; i++) await Frames(1);
        Check(hanging.ClimbingWall && Mathf.IsEqualApprox(hanging.DamageTakenScale, Enemy.ClimbDamageScale), "a zombie on the wall takes extra damage");
        hanging.Shove(Vector3.Left, 120, 0);
        await Frames(60);
        Check(!hanging.Climbing && tiers.TierOf(hanging) == 0, "a hard hit knocks a climbing zombie off the wall");
        await Clear();

        brain.ProcessMode = ProcessModeEnum.Inherit;

        // ---------- survivors on the ladder ----------
        await Place(player, ladder.Bottom, ladder.Top);
        await Place(bot, new Vector3(-15, 0, 10), new Vector3(-15, 1, 14));
        Check(player.ClimbLadder(ladder, up: true), "a survivor gets on the ladder at its foot");
        await Frames(40);
        int mag = player.Gun.Mag;
        player.SetControls(Vector2.Zero, ladder.Top + Vector3.Back * 5, true);
        player.Launch(new Vector3(6, 3, 0), .4f);
        await Frames(10);
        Check(player.OnLadder && player.Gun.Mag == mag && player.GlobalPosition.Y > .5f, "on the ladder: no shooting, and a hit does not knock you off");
        player.SetControls(Vector2.Zero, ladder.Top + Vector3.Back * 5, false);
        await Place(bot, ladder.Top, ladder.Top + Vector3.Forward);
        for (int i = 0; i < 240 && player.OnLadder; i++) await Frames(1);
        Check(!player.OnLadder && tiers.TierOf(player) == 2, $"climbing up takes about a second a tier and ends on tier 2");
        await Frames(10);
        Check(bot.GlobalPosition.DistanceTo(ladder.Top) > .9f, "arriving at the top shoves whoever stood at the exit");
        var started = Time.GetTicksMsec();
        player.ClimbLadder(ladder, up: false);
        for (int i = 0; i < 240 && player.OnLadder; i++) await Frames(1);
        Check(!player.OnLadder && tiers.TierOf(player) == 0, "climbing down ends on the ground");

        // Two on one ladder pass through each other (the squadmate's own mind paused so only the ladder moves it).
        brain.ProcessMode = ProcessModeEnum.Disabled;
        for (int i = 0; i < 240 && bot.OnLadder; i++) await Frames(1);
        await Place(player, ladder.Bottom, ladder.Top);
        await Place(bot, ladder.Bottom + Vector3.Back * .4f, ladder.Top);
        player.ClimbLadder(ladder, up: true);
        await Frames(10);
        Check(bot.ClimbLadder(ladder, up: true), "a second survivor gets on the ladder while the first is still climbing");
        for (int i = 0; i < 300 && (player.OnLadder || bot.OnLadder); i++) await Frames(1);
        Check(tiers.TierOf(player) == 2 && tiers.TierOf(bot) == 2, "two survivors climb the same ladder one behind the other");

        brain.ProcessMode = ProcessModeEnum.Inherit;

        // ---------- a squadmate takes the ladder on its own ----------
        await Place(player, new Vector3(27.5f, 2 * H, -3), new Vector3(27.5f, 1, -8));
        await Place(bot, new Vector3(27, 0, 5), new Vector3(27, 1, 0));
        bool climbed = false;
        for (int i = 0; i < 600 && tiers.TierOf(bot) != 2; i++)
        {
            await Frames(1);
            climbed |= bot.OnLadder;
        }
        Check(climbed && tiers.TierOf(bot) == 2, "a squadmate following the leader climbs the ladder by itself");
    }

    /// <summary>AI squadmates on tiers: follow up a ramp, shoot down a tier, never up.</summary>
    private async Task Squad()
    {
        var stage = await NewStage(new TierTestMap(), squad: 1);
        var player = stage.Player;
        var tiers = stage.Tiers;
        var bot = stage.Squad[0].Bot;
        bot.Invulnerable = true;
        var common = stage.CommonDefinition;

        await Place(player, new Vector3(15, H, -8), new Vector3(20, 1, -8));
        await Place(bot, new Vector3(-5, 0, 5), new Vector3(0, 1, 5));
        for (int i = 0; i < 900 && tiers.TierOf(bot) != 1; i++) await Frames(1);
        Check(tiers.TierOf(bot) == 1, "a squadmate follows the leader up the ramp onto tier 1");

        await Place(player, new Vector3(35, 2 * H, -8), new Vector3(35, 1, 6));
        await Place(bot, new Vector3(33, 2 * H, -4), new Vector3(33, 1, 6));
        var below = stage.SpawnEnemy<Enemy>(common, new Vector3(33, 0, 7), awake: true);
        float before = below.Health;
        bool hit = false;
        for (int i = 0; i < 300 && !hit; i++)
        {
            await Frames(1);
            hit = !IsInstanceValid(below) || below.IsDead || (below.Health < before && tiers.TierOf(bot) == 2);
        }
        Check(hit, "a squadmate up on tier 2 shoots a monster on the ground");
        foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);

        await Place(player, new Vector3(2, 0, 8), new Vector3(15, 1, -6));
        await Place(bot, new Vector3(4, 0, 9), new Vector3(15, 1, -6));
        var above = stage.SpawnEnemy<Enemy>(common, new Vector3(15, H, -6), awake: true);
        int mag = bot.Gun.Mag;
        bool firedUp = false;
        for (int i = 0; i < 120; i++)
        {
            await Frames(1);
            if (!IsInstanceValid(above) || above.IsDead) break;
            if (bot.Gun.Mag < mag && tiers.TierOf(above) > tiers.TierOf(bot)) firedUp = true;
            mag = bot.Gun.Mag;
        }
        Check(!firedUp, "a squadmate on the ground does not shoot at a monster up on tier 1");
    }

    /// <summary>Rockets, monster guns, vomit and grabs across tiers (L-15 · L-16).</summary>
    private async Task Ranged()
    {
        var stage = await NewStage(new TierTestMap());
        var player = stage.Player;
        var tiers = stage.Tiers;
        var common = stage.CommonDefinition;
        var rocket = ArmoryCatalog.Weapon("alabong");

        async Task Clear()
        {
            foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
            await Frames(2);
        }

        // Rockets fly level from the shooter's tier: down onto the ground, but into the cliff when fired up.
        await Clear();
        var below = stage.SpawnEnemy<Enemy>(common, new Vector3(32, 0, 7), awake: false);
        await Place(player, new Vector3(32, 2 * H, -3), new Vector3(32, 1, 7));
        float belowBefore = below.Health;
        Rocket.Launch(stage, new Vector3(32, 2 * H + 1.1f, -3), Vector3.Back, rocket, player.GetRid(), player);
        await Frames(40);
        Check(!IsInstanceValid(below) || below.IsDead || below.Health < belowBefore, "a rocket fired from tier 2 hits a monster on the ground below");
        await Clear();
        var above = stage.SpawnEnemy<Enemy>(common, new Vector3(19, H, -10), awake: false);
        await Place(player, new Vector3(1, 0, -10), new Vector3(19, 1, -10));
        float aboveBefore = above.Health;
        Rocket.Launch(stage, new Vector3(1, 1.1f, -10), Vector3.Right, rocket, player.GetRid(), player);
        await Frames(40);
        Check(IsInstanceValid(above) && !above.IsDead && above.Health == aboveBefore, "a rocket fired from the ground at tier 1 bursts on the cliff");

        // A monster gun shoots down a tier, never up.
        player.Invulnerable = false;
        await Clear();
        await Place(player, new Vector3(38, 0, 6), new Vector3(38, 1, -4));
        var gunner = (RangedEnemy)stage.Spawn(MonsterDatabase.Get("armed_animal"), new Vector3(38, 2 * H, -5), awake: true);
        int health = player.Health;
        bool hitFromAbove = false;
        for (int i = 0; i < 300 && !hitFromAbove; i++)
        {
            await Frames(1);
            if (player.Health < health && tiers.TierOf(gunner) == 2) hitFromAbove = true;
            health = player.Health;
        }
        Check(hitFromAbove, "an armed animal up on tier 2 shoots a survivor on the ground");
        await Clear();
        await Place(player, new Vector3(15, H, -2), new Vector3(15, 1, 8));
        var lowGunner = (RangedEnemy)stage.Spawn(MonsterDatabase.Get("armed_animal"), new Vector3(15, 0, 8), awake: true);
        bool shotUp = false;
        int fired = 0;
        for (int i = 0; i < 180; i++)
        {
            await Frames(1);
            if (lowGunner.ProjectilesFired > fired && tiers.TierOf(lowGunner) < tiers.TierOf(player)) shotUp = true;
            fired = lowGunner.ProjectilesFired;
        }
        Check(!shotUp, "an armed animal on the ground never shoots up at tier 1");

        // Vomit is lobbed (the lob exception): a spitter below still reaches tier 1.
        await Clear();
        await Place(player, new Vector3(15, H, -2), new Vector3(15, 1, 7));
        var spitter = (RangedEnemy)stage.Spawn(MonsterDatabase.Get("spitter"), new Vector3(15, 0, 7), awake: true);
        bool lobbed = false;
        for (int i = 0; i < 360 && !lobbed; i++)
        {
            await Frames(1);
            if (spitter.ProjectilesFired > 0 && tiers.TierOf(spitter) == 0) lobbed = true;
        }
        Check(lobbed, "a spitter on the ground lobs its vomit up onto tier 1");

        // Grabs reach across tiers (L-16): a tether below catches a survivor on the edge above.
        await Clear();
        await Place(player, new Vector3(15, H, -1.2f), new Vector3(15, 1, 5));
        var tether = stage.Spawn(MonsterDatabase.Get("tether"), new Vector3(15, 0, 5), awake: true);
        bool grabbed = false;
        for (int i = 0; i < 480 && !grabbed; i++)
        {
            await Frames(1);
            if (player.IsRestrained && tiers.TierOf(tether) == 0) grabbed = true;
        }
        Check(grabbed, "a tether on the ground grabs a survivor up on tier 1");
        player.Invulnerable = true;
        await Clear();
    }

    /// <summary>L-21: from below, enemies up a tier are hidden unless a survivor up there lights them or they attack you.</summary>
    private async Task Vision()
    {
        var stage = await NewStage(new TierTestMap(), squad: 1);
        var player = stage.Player;
        var bot = stage.Squad[0].Bot;
        var vision = stage.Vision;
        var common = stage.CommonDefinition;
        bot.Invulnerable = true;
        await Place(bot, new Vector3(-15, 0, 10), new Vector3(-15, 1, 14));

        var up = stage.SpawnEnemy<Enemy>(common, new Vector3(15, H, -3), awake: false);
        await Frames(2);
        await Place(player, new Vector3(15, 0, 2.5f), new Vector3(15, 1, -3));
        Check(vision.Classify(up) == VisionSystem.Visibility.Hidden, "from the ground a monster up on tier 1 is hidden, even close and in the cone");
        up.RevealTo(player, .1f);
        Check(vision.Classify(up) == VisionSystem.Visibility.Silhouette, "while it attacks you it shows as a silhouette");
        await Frames(20);
        bot.GlobalPosition = new Vector3(11, H + .05f, -3);
        bot.SetControls(Vector2.Zero, up.GlobalPosition, false);
        Check(vision.Classify(up) == VisionSystem.Visibility.Visible, "a squadmate up on tier 1 shining a light on it shows it to you below");
        bot.GlobalPosition = new Vector3(11, .05f, 2.5f);
        bot.SetControls(Vector2.Zero, up.GlobalPosition, false);
        Check(vision.Classify(up) == VisionSystem.Visibility.Hidden, "a light from the ground below does not");
        await Place(bot, new Vector3(-15, 0, 10), new Vector3(-15, 1, 14));

        var level = stage.SpawnEnemy<Enemy>(common, new Vector3(15, 0, 7), awake: false);
        await Frames(2);
        await Place(player, new Vector3(15, 0, 2.5f), new Vector3(15, 1, 7));
        Check(vision.Classify(level) == VisionSystem.Visibility.Visible, "on the same tier the cone works as before");
        await Place(player, new Vector3(15, H, -1), new Vector3(15, 1, 7));
        Check(vision.Classify(level) == VisionSystem.Visibility.Visible, "looking down from tier 1, a monster on the ground is in view");

        foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
        var watcher = stage.SpawnEnemy<Enemy>(common, new Vector3(15, 0, 9), awake: false);
        watcher.Reaction = Reaction.Watcher;
        await Place(player, new Vector3(15, H, -1.5f), new Vector3(15, 1, 9));
        await Frames(60);
        Check(!watcher.Awake, "a watcher below does not see a survivor up on tier 1");
        await Place(player, new Vector3(6, 0, 1), new Vector3(15, 1, 9));
        for (int i = 0; i < 90 && !watcher.Awake; i++) await Frames(1);
        Check(watcher.Awake, "the same watcher sees a survivor on its own tier at that distance");
    }
}
