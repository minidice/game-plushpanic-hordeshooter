using Godot;

namespace HordeShooter;

public partial class RangedEnemy : Enemy
{
    public bool WindingUp { get; private set; }
    public int ProjectilesFired { get; private set; }
    private RangedTrait Shot => Monster.Ranged!;
    private float _timer, _cooldown = 2f;
    private Vector3 _target;
    private MeshInstance3D? _marker;

    public override void _Ready()
    {
        base._Ready();
        if (Monster.Behavior != MonsterBehavior.Shooter) return;
        var built = WeaponVisual.Build(ArmoryCatalog.Weapon("m4"), Definition.HandColor);
        var weapon = built.Root;
        weapon.Scale = Vector3.One / Definition.ModelScale;
        if (!Definition.ModelFacesPositiveZ) weapon.RotationDegrees = new Vector3(0, 180, 0);
        Rig.Weapon.AddChild(weapon);
        Rig.BindWeaponGrip(built);
    }

    protected override Vector3 Think(float dt)
    {
        _cooldown -= dt;
        var player = CurrentTarget(dt);
        if (player == null) { AbortShot(); return base.Think(dt); }
        var to = player.GlobalPosition - GlobalPosition;
        to.Y = 0;
        float distance = to.Length();
        if (WindingUp)
        {
            FaceOverride = _target - GlobalPosition;
            _timer -= dt;
            Kick = .25f;
            if (_timer <= 0)
            {
                // Check the locked aim point again: closing a door during windup stops the shot.
                var body = GlobalPosition + Vector3.Up * .8f;
                var from = Monster.Behavior == MonsterBehavior.Spitter ? Rig.Muzzle.GlobalPosition : body;
                // A protruding mouth must not spawn a projectile on the far side of cover.
                if ((from.IsEqualApprox(body) || Rays.Clear(this, body, from, Layers.World | Layers.Props)) &&
                    Rays.Clear(this, from, _target, Layers.World | Layers.Props))
                {
                    var projectile = new EnemyProjectile();
                    projectile.Setup(Stage, from, _target, Shot, AttackDamage);
                    Stage.AddChild(projectile);
                    Stage.Net?.OnEnemyProjectile(this, from, _target);
                    ProjectilesFired++;
                    Kick = 1;
                    Stage.Sfx.PlayAt(Monster.Behavior == MonsterBehavior.Shooter ? "shot_rifle" : "zombie_hit", from);
                }
                AbortShot();
            }
            return Vector3.Zero;
        }
        if (Awake && distance < Shot.Range && _cooldown <= 0 &&
            Rays.Clear(this, GlobalPosition + Vector3.Up * .8f, player.GlobalPosition + Vector3.Up * .8f, Layers.World | Layers.Props))
        { StartWindup(); return Vector3.Zero; }
        if (Awake && distance > 3 && distance < Shot.Range && _cooldown > 0 && HasLineOfSight(dt))
        { FaceOverride = to; return Vector3.Zero; }
        return base.Think(dt);
    }

    /// <param name="victim">Client puppet: the survivor the host's enemy is aiming at.</param>
    public void StartWindup(Player? victim = null)
    {
        if (IsDead || StunTime > 0) return;
        AbortShot();
        WindingUp = true;
        Awake = true;
        _timer = Shot.Windup;
        victim ??= Target ?? Stage.NearestAlivePlayer(GlobalPosition) ?? Stage.Player;
        _target = victim.GlobalPosition + Vector3.Up * .8f;
        // Shooting at you from the dark: you see who it is from the windup until a moment after the shot (C-23).
        RevealTo(victim, Shot.Windup + 1f + VisionSystem.RevealAfterHit);
        Stage.Net?.OnEnemyAttack(this, NetAttack.Ranged, victim, _target);
        var color = Shot.HazardSeconds > 0 ? new Color("b4df45") : new Color("ff6252");
        var flatTarget = new Vector3(_target.X, .055f, _target.Z);
        if (Shot.HazardSeconds > 0)
            _marker = Mat.Mesh(Stage.Effects, new CylinderMesh { TopRadius = Shot.HazardRadius, BottomRadius = Shot.HazardRadius, Height = .025f }, flatTarget, Mat.Fade(new Color(color, .35f)));
        else
        {
            var start = new Vector3(GlobalPosition.X, .055f, GlobalPosition.Z);
            _marker = Mat.Box(Stage.Effects, (start + flatTarget) * .5f, new Vector3(.12f, .03f, start.DistanceTo(flatTarget)), Mat.Glow(color));
            if (start.DistanceTo(flatTarget) > .01f) _marker.LookAt(flatTarget, Vector3.Up);
        }
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition, -8, 1.8f);
    }

    private void AbortShot()
    {
        if (WindingUp) Stage.Net?.OnEnemyAttackEnd(this);
        WindingUp = false;
        _cooldown = Shot.Cooldown;
        if (IsInstanceValid(_marker)) _marker!.QueueFree();
        _marker = null;
    }
    protected override void OnStunned() => AbortShot();
    public override void Die(Vector3 direction, float force) { AbortShot(); base.Die(direction, force); }
    public override void _ExitTree() { if (IsInstanceValid(_marker)) _marker!.QueueFree(); }
}
