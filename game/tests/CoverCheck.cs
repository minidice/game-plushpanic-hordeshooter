using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Low cover and breakable objects (decisions.md C-29, object_interactions.json): sandbags stop shots from further off but a
/// shooter hugging them fires over (one body width is the line), both sides hugging shoot each other, melee and sight go
/// over, walking does not; sandbags break from gunfire or a blast after their health, the concrete barrier never does;
/// monster shots follow the same rule. Also the data's validation.
/// </summary>
public partial class CoverCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-6, -8, 24, 16);
        protected override void BuildMap()
        {
            PlayerStart = new Vector3(0, 0, -5);
            Floor(-5, 17, -7, 7, "546852");
            Prop("sandbags", Vector3.Zero);
            Prop("sandbags", new Vector3(4, 0, 0));
            Prop("concrete_barrier", new Vector3(10, 0, 0));
        }
    }

    private Stage _stage = null!;
    private int _checks;
    private void Check(bool yes, string label) { if (!yes) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        int exit = 0;
        try { await Run(); GD.Print("COVER_CHECK_PASS " + _checks); }
        catch (Exception error) { GD.PushError(error.ToString()); GD.Print("COVER_CHECK_FAIL " + error.Message); exit = 1; }
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(3); }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private async Task Run()
    {
        // ---------- data ----------
        var data = ObjectInteractions.Current;
        var sandRule = data.ForProp(PropLibrary.Find("sandbags")!)!;
        var barrierRule = data.ForProp(PropLibrary.Find("concrete_barrier")!)!;
        Check(sandRule.Cover && sandRule.Destruction is { BreakableBy: BreakableBy.Gun, MaxHealth: 50 } && sandRule.CollisionLayer == Layers.Cover,
            "sandbags: low cover, broken by guns, 50 health (half a survivor's), on the cover layer");
        Check(barrierRule.Cover && barrierRule.Destruction == null, "concrete barrier: low cover that does not break");
        Check(data.ShootOverGapBodies == 1, "shoot over within one body width");
        Check(new ObjectDestruction(BreakableBy.Explosive, 80).Accepts(ObjectDamage.Explosion) && !new ObjectDestruction(BreakableBy.Explosive, 80).Accepts(ObjectDamage.Gun),
            "an explosive-only object ignores gunfire");
        string json = FileAccess.GetFileAsString(ObjectInteractions.DataPath);
        foreach (var (change, label) in new (Action<JsonNode>, string)[]
        {
            (j => j["profiles"]!["sandbag_cover"]!["blocks_projectiles"] = false, "cover that lets bullets through"),
            (j => j["profiles"]!["sandbag_cover"]!["destruction"]!["breakable_by"] = "melee", "unknown breakable_by"),
            (j => j["profiles"]!["sandbag_cover"]!["destruction"]!["max_health"] = 0, "zero health"),
        })
        {
            var node = JsonNode.Parse(json)!;
            change(node);
            bool rejected = false;
            try { ObjectInteractions.Parse(node.ToJsonString()); } catch (Exception e) when (e is FormatException or InvalidOperationException) { rejected = true; }
            Check(rejected, "data rejects " + label);
        }

        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false,
            MapOverride = new TestMap(), ModeOverride = GameMode.Scenario, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(5);
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        float r = player.Definition.ColliderRadius;
        var bodies = _stage.Map.KitProps.OfType<ObjectBody>().ToList();
        var sand = bodies.First(b => b.Interaction.Destruction != null && b.GlobalPosition.X < 1);
        var sand2 = bodies.First(b => b.Interaction.Destruction != null && b.GlobalPosition.X > 3);
        var barrier = bodies.Single(b => b.GlobalPosition.X > 9);
        Check(_stage.Breakables.Count == 2 && sand.Health == 50 && barrier.NetId == 0, "both sandbags can break, the barrier is not listed");
        float half = .65f / 2;
        float hug = -(half + r + .05f);

        // ---------- the rule on its own ----------
        var aim = new Vector3(0, Player.AimHeight, 5);
        Check(CoverRule.Clear(_stage, new Vector3(0, 0, hug), r, new Vector3(0, Player.AimHeight, hug), aim, Layers.BulletMask & ~Layers.Enemy),
            "hugging the sandbags, the shot goes over");
        float justIn = -(half + r + r * 2 * .9f), justOut = -(half + r + r * 2 * 1.1f);
        Check(CoverRule.Clear(_stage, new Vector3(0, 0, justIn), r, new Vector3(0, Player.AimHeight, justIn), aim, Layers.BulletMask & ~Layers.Enemy),
            "a gap of 0.9 body widths still fires over");
        Check(!CoverRule.Clear(_stage, new Vector3(0, 0, justOut), r, new Vector3(0, Player.AimHeight, justOut), aim, Layers.BulletMask & ~Layers.Enemy),
            "a gap of 1.1 body widths: the sandbags stop the shot");
        var other = new Vector3(0, 0, half + r + .05f);
        Check(CoverRule.Clear(_stage, other, r, other + Vector3.Up * Player.AimHeight, new Vector3(0, Player.AimHeight, hug), Layers.BulletMask & ~Layers.Enemy)
            && CoverRule.Clear(_stage, new Vector3(0, 0, hug), r, new Vector3(0, Player.AimHeight, hug), other + Vector3.Up * Player.AimHeight, Layers.BulletMask & ~Layers.Enemy),
            "both sides hugging the same sandbags shoot each other");
        Check(Rays.Clear(_stage, new Vector3(0, Player.AimHeight, -4), aim, Layers.SightMask), "low cover does not block sight");

        // ---------- walking stops at it ----------
        player.GlobalPosition = new Vector3(0, 0, -2.5f);
        player.Velocity = Vector3.Zero;
        player.SetControls(new Vector2(0, 1), aim, false);
        await Frames(70);
        player.SetControls(Vector2.Zero, aim, false);
        Check(player.GlobalPosition.Z < -half - r + .1f, $"a survivor cannot walk through sandbags ({player.GlobalPosition.Z:0.00})");
        Check(_stage.Flow.IsBlocked(Vector3.Zero), "sandbags block the flow field");

        // ---------- real gunfire: over when hugging, into it from further off ----------
        var enemy = _stage.SpawnEnemy<Enemy>(_stage.CommonDefinition, new Vector3(10, 0, 4), awake: false);
        enemy.SetPhysicsProcess(false);
        enemy.ScaleHealth(100);
        await Frames(2);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        player.GlobalPosition = new Vector3(10, 0, -(.25f + r + .05f));
        float before = enemy.Health;
        await Shoot(player, enemy.GlobalPosition + Vector3.Up * Player.AimHeight, 20);
        Check(enemy.Health < before, $"M4 from behind the barrier, hugging it, hits the monster beyond ({before} -> {enemy.Health})");
        player.GlobalPosition = new Vector3(10, 0, -4);
        before = enemy.Health;
        await Shoot(player, enemy.GlobalPosition + Vector3.Up * Player.AimHeight, 30);
        Check(enemy.Health == before && IsInstanceValid(barrier) && !barrier.Broken, "from 4 m back the barrier takes the bullets and never breaks");

        // ---------- melee over the sandbags ----------
        enemy.GlobalPosition = new Vector3(0, 0, half + enemy.Radius + .05f);
        player.GlobalPosition = new Vector3(0, 0, hug);
        player.PressSlot(1);
        await Frames(20);
        before = enemy.Health;
        player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, true);
        await Frames(24);
        player.SetControls(Vector2.Zero, enemy.GlobalPosition + Vector3.Up, false);
        Check(enemy.Health < before, $"melee reaches over the sandbags ({before} -> {enemy.Health})");

        // ---------- monster shots: protected from afar, not from a monster hugging the other side ----------
        var shot = new RangedTrait(20, 0, 0, 30, 0, 0);
        player.Invulnerable = false;
        player.GlobalPosition = new Vector3(10, 0, -(.25f + r + .05f));
        int health = player.Health;
        Fire(new Vector3(10, .8f, 5), player.GlobalPosition + Vector3.Up * .8f, shot, 10, .4f);
        await Frames(30);
        Check(player.Health == health, "a monster shot from afar stops at the barrier the survivor hides behind");
        Fire(new Vector3(10, .8f, .25f + .4f + .05f), player.GlobalPosition + Vector3.Up * .8f, shot, 10, .4f);
        await Frames(30);
        Check(player.Health < health, $"a monster hugging the other side fires over it ({health} -> {player.Health})");
        player.Invulnerable = true;
        Check(!CoverRule.Clear(_stage, new Vector3(10, 0, 5), .4f, new Vector3(10, .8f, 5), player.GlobalPosition + Vector3.Up * .8f, Layers.EnemyShotMask),
            "a monster's line-of-fire check sees the barrier in the way");

        // ---------- breaking ----------
        enemy.GlobalPosition = new Vector3(0, 0, 4);
        player.PressSlot(2);
        await Frames(20);
        player.GlobalPosition = new Vector3(0, 0, -4);
        before = enemy.Health;
        int hits = 0;
        for (int i = 0; i < 10 && IsInstanceValid(sand) && !sand.Broken; i++)
        {
            float was = sand.Health;
            await Shoot(player, new Vector3(0, Player.AimHeight, 0), 8);
            if (IsInstanceValid(sand) && sand.Health < was) hits++;
        }
        await Frames(3);
        Check(!IsInstanceValid(sand) && !_stage.Breakables.Contains(sand), $"gunfire wears the sandbags down and they give way ({hits} bursts landed)");
        Check(!_stage.Flow.IsBlocked(Vector3.Zero), "the broken sandbags free their cells in the flow field");
        before = enemy.Health;
        await Shoot(player, enemy.GlobalPosition + Vector3.Up * Player.AimHeight, 20);
        Check(enemy.Health < before, "with the sandbags gone the bullets reach the monster");
        sand2.TakeDamage(30, ObjectDamage.Gun);
        Check(Mathf.IsEqualApprox(sand2.Health, 20) && !sand2.Broken, "30 damage leaves the other sandbags at 20");
        Blast.Explode(_stage, new Vector3(4, .2f, 1.2f), 4, 60, Colors.Orange);
        await Frames(3);
        Check(!IsInstanceValid(sand2), "a grenade-sized blast finishes them");
        barrier.TakeDamage(1000, ObjectDamage.Explosion);
        Check(IsInstanceValid(barrier) && !barrier.Broken, "the concrete barrier shrugs off even a huge blast");
    }

    private async Task Shoot(Player player, Vector3 at, int frames)
    {
        player.SetControls(Vector2.Zero, at, true);
        await Frames(frames);
        player.SetControls(Vector2.Zero, at, false);
        await Frames(2);
    }

    private void Fire(Vector3 from, Vector3 target, RangedTrait shot, float damage, float shooterRadius)
    {
        var projectile = new EnemyProjectile();
        projectile.Setup(_stage, from, target, shot, damage, shooterRadius);
        _stage.AddChild(projectile);
    }
}
