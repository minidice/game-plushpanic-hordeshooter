using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Real physics regression checks for contact-free, interruptible melee.</summary>
public partial class EnemyAttackCheck : Node
{
    private Stage _stage = null!;
    private Player Player => _stage.Player;
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            PoseChecks();
            _stage = new Stage { CharacterOverride = "test_capsule", DirectorEnabled = false };
            AddChild(_stage);
            Player.ManualControl = true;
            Player.Invulnerable = false;
            foreach (var enemy in _stage.AllEnemies.ToArray()) enemy.QueueFree();
            await Frames(3);
            var attacker = await Attacker();
            int health = Player.Health;
            await BeginAttack(attacker);
            await Frames(10);
            Check(Player.Health == health, "body contact and early windup cause no damage");
            await Frames(12);
            Check(Player.Health == health - 5, "one swing applies exactly one impact");
            await Frames(20);
            Check(Player.Health == health - 5, "recovery does not repeat contact damage");

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            Player.GlobalPosition += Vector3.Right * 3;
            await Frames(25);
            Check(Player.Health == health, "moving out of reach during windup avoids damage");

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            Player.GlobalPosition = attacker.GlobalPosition + Vector3.Left * 1.05f;
            await Frames(25);
            Check(Player.Health == health, "moving behind locked attack direction avoids damage");

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            Player.SetControls(Vector2.Right, new Vector3(8, 1, 0), false);
            Player.PressRoll();
            await Frames(2);
            Check(Player.IsRolling, "dodge test actually starts a roll");
            await Frames(23);
            Check(Player.Health == health, "rolling during windup avoids the strike");
            await Frames(20);

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            var wall = new StaticBody3D { Position = new Vector3(3.525f, 1.5f, 0), CollisionLayer = Layers.World };
            wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.08f, 3, 3) } });
            _stage.AddChild(wall);
            await Frames(25);
            Check(Player.Health == health, "new cover during windup blocks impact using fresh line of sight");
            wall.QueueFree();

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            attacker.Stun(.7f);
            await Frames(45);
            Check(Player.Health == health, "stun cancels pending strike without delayed damage");
            await Frames(85);
            Check(Player.Health < health, "normal attacks resume after interruption and cooldown");

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            attacker.ApplyKnock(Vector3.Left, 2);
            await Frames(25);
            Check(Player.Health == health, "knockback interrupts pending swing");

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            attacker.Die(Vector3.Left, 0);
            await Frames(25);
            Check(Player.Health == health, "enemy death cancels pending strike");

            attacker = await Attacker(); health = Player.Health;
            await BeginAttack(attacker);
            var lure = ThrownItem.Throw(_stage, ArmoryCatalog.Throwable("lure"),
                new Vector3(2, .2f, 1), new Vector3(2, .2f, 1));
            lure.Freeze = true;
            _stage.SetLure(lure);
            await Frames(25);
            Check(Player.Health == health && !attacker.IsMeleeAttacking, "lure diverts an enemy without a delayed strike");
            _stage.ClearLure(); lure.QueueFree();

            foreach (string id in new[] { "bull", "tether", "armed_animal" })
            {
                attacker = await Attacker(id); health = Player.Health;
                await BeginAttack(attacker);
                switch (attacker)
                {
                    case BullEnemy bull: bull.StartWindup(Vector3.Right); break;
                    case CaptureEnemy captor: captor.StartWindup(); break;
                    case RangedEnemy ranged: ranged.StartWindup(); break;
                }
                await Frames(24);
                Check(!attacker.IsMeleeAttacking && Player.Health == health,
                    id + " special windup replaces melee without a delayed strike");
            }

            GD.Print("ENEMY_ATTACK_PASS " + _checks);
            _stage.QueueFree();
            await Frames(2);
            QuitHygiene.Collect(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("ENEMY_ATTACK_FAIL " + error); GetTree().Quit(1); }
    }

    private void PoseChecks()
    {
        var looks = new[] { "humanoid", "crawler", "bull", "tether", "leaper", "spitter" }
            .Select(CharacterCatalog.Enemy)
            .Concat(new[] { "goose", "duck", "raccoon", "redpanda" }.Select(CharacterCatalog.Player))
            .Concat(new[] { "goose", "duck", "raccoon", "redpanda" }.Select(species =>
                CharacterDefinition.MakeZombie(CharacterCatalog.Player(species), CharacterCatalog.Enemy("plush"))));
        foreach (var look in looks)
        {
            var rig = CharacterRig.Create(look); AddChild(rig);
            rig.InitializeEnemyAttackPose();
            if (look.IsZombie)
                Check(rig.Model.FindChild("*_leftArm", true, false) is Node3D { Visible: true } &&
                    rig.Model.FindChild("*_rightArm", true, false) is Node3D { Visible: true } &&
                    rig.Model.FindChildren("ZombieArm*", nameof(Node3D), true, false).Count == 0 &&
                    rig.FindChild("EnemyLeftArm", true, false) == null && rig.FindChild("EnemyRightArm", true, false) == null,
                    look.Id + " keeps its original two wings/paws without generated zombie arms");
            if (look.Id is "humanoid" or "crawler" or "tether" or "leaper" or "spitter")
                Check(rig.Model.FindChild(look.Id + "_leftArm", true, false) != null &&
                    rig.Model.FindChild(look.Id + "_rightArm", true, false) != null &&
                    rig.FindChild("EnemyLeftArm", true, false) == null && rig.FindChild("EnemyRightArm", true, false) == null,
                    look.Id + " animates its original two arms without duplicate fallback limbs");
            rig.Animate(.016f, 0, 0, 0, Vector3.Zero);
            var parts = rig.FindChildren("*", nameof(Node3D), true, false).Cast<Node3D>()
                .ToDictionary(part => part, part => part.Transform);
            rig.ApplyEnemyAttackPose(.28f, .28f, .32f);
            Check(parts.Any(part => part.Key != rig.Pose && part.Key.Transform != part.Value), look.Id + " hands move at impact");
            Check(rig.Pose.Rotation.X > .1f && rig.Pose.Position.Z > .1f, look.Id + " body leans into strike");
            rig.ResetEnemyAttackPose();
            Check(parts.All(part => part.Key.Transform == part.Value), look.Id + " interruption restores all pose homes");
            rig.Free();
        }
    }

    private async Task<Enemy> Attacker(string id = "humanoid")
    {
        foreach (var enemy in _stage.AllEnemies.ToArray()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        Player.GlobalPosition = new Vector3(4.05f, .05f, 0);
        Player.Velocity = Vector3.Zero;
        Player.SetControls(Vector2.Zero, new Vector3(3, 1, 0), false);
        await Frames(3);
        return _stage.Spawn(MonsterDatabase.Get(id), new Vector3(3, 0, 0), true);
    }

    private async Task BeginAttack(Enemy enemy)
    {
        for (int i = 0; i < 80 && !enemy.IsMeleeAttacking; i++) await Frames(1);
        Check(enemy.IsMeleeAttacking, "enemy enters visible windup");
    }

    private async Task Frames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); _checks++; GD.Print("PASS: " + label); }
}
