using Godot;

namespace HordeShooter;

public partial class RangedEnemy : Enemy
{
    public bool WindingUp { get; private set; }
    public int ProjectilesFired { get; private set; }
    public const float HunterBowRecovery = .36f;
    private bool HunterBow => Monster.CommonIdentity?.Weapon == "woodbow";
    private float _bowTime = -1;
    private bool _bowReleased;
    private AudioStreamPlayer3D? _bowDrawSound;
    private RangedTrait Shot => Monster.Ranged!;
    private float _timer, _cooldown = 2f;
    private Vector3 _target;
    private MeshInstance3D? _marker;
    private BaseCore? _targetCore;
    public string ShotSound => Monster.CommonIdentity?.Weapon == "woodbow" ? "hunter_bow_release" : Monster.Behavior == MonsterBehavior.Shooter ? "shot_rifle" : "zombie_hit";

    public override void _Ready()
    {
        base._Ready();
        if (Monster.Behavior != MonsterBehavior.Shooter || Monster.CommonIdentity != null) return;
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
                var weaponMuzzle = Monster.CommonIdentity is { } common
                    ? Rig.Weapon.FindChild("Weapon_" + common.Weapon, true, false)?.FindChild("Muzzle", true, false) as Node3D : null;
                var from = weaponMuzzle?.GlobalPosition ?? (Monster.Behavior == MonsterBehavior.Spitter ? Rig.Muzzle.GlobalPosition : body);
                // A protruding mouth must not spawn a projectile on the far side of cover.
                if ((from.IsEqualApprox(body) || Rays.Clear(this, body, from, Layers.World | Layers.Props)) &&
                    ShotClear(from, _target, _targetCore))
                {
                    var projectile = new EnemyProjectile();
                    projectile.Setup(Stage, from, _target, Shot, AttackDamage, Radius, Monster.CommonIdentity?.Weapon ?? "");
                    Stage.AddChild(projectile);
                    Stage.Net?.OnEnemyProjectile(this, from, _target);
                    ProjectilesFired++;
                    ShowShotRelease();
                    Kick = 1;
                    Stage.Sfx.PlayAt(ShotSound, from);
                }
                AbortShot();
            }
            return Vector3.Zero;
        }
        // Ordinary ranged loadouts defend the same objective as ordinary melee units.
        // Keep named shooters' legacy targeting unchanged.
        if (Monster.CommonIdentity != null && Stage.ModeRules?.Core is { Alive: true } core &&
            GlobalPosition.DistanceTo(core.GlobalPosition) < distance)
        {
            var coreAim = core.GlobalPosition + Vector3.Up * .8f;
            var coreDirection = core.GlobalPosition - GlobalPosition; coreDirection.Y = 0;
            if (Awake && coreDirection.Length() < Shot.Range &&
                (!Stage.Tiers.Any || Stage.Tiers.TierOf(core) <= Stage.Tiers.TierOf(this)) &&
                ShotClear(GlobalPosition + Vector3.Up * .8f, coreAim, core))
            {
                FaceOverride = coreDirection;
                if (_cooldown <= 0) StartWindup(null, coreAim, core);
                return Vector3.Zero;
            }
            return base.Think(dt);
        }
        if (Awake && distance < Shot.Range && _cooldown <= 0 && CanShootAt(player) &&
            ShotClear(GlobalPosition + Vector3.Up * .8f, player.GlobalPosition + Vector3.Up * .8f))
        { StartWindup(); return Vector3.Zero; }
        if (Awake && distance > 3 && distance < Shot.Range && _cooldown > 0 && HasLineOfSight(dt))
        { FaceOverride = to; return Vector3.Zero; }
        return base.Think(dt);
    }

    /// <summary>Height tiers (L-15): a gun does not shoot up a tier; vomit is lobbed and may (the lob exception).</summary>
    private bool CanShootAt(Player player) =>
        !Stage.Tiers.Any || Shot.HazardSeconds > 0 || Stage.Tiers.TierOf(player) <= Stage.Tiers.TierOf(this);

    /// <summary>
    /// Nothing in the way of the shot. Across tiers a lob clears the edge, and a gun firing down a tier checks the line at
    /// its own height (the shot flies level over the edge, see <see cref="EnemyProjectile"/>).
    /// </summary>
    private bool ShotClear(Vector3 from, Vector3 to, BaseCore? core = null)
    {
        if (Stage.Tiers.Any)
        {
            int fromTier = TierMap.FloorTier(from.Y - .8f), toTier = TierMap.FloorTier(to.Y - .8f);
            if (Shot.HazardSeconds > 0 && fromTier != toTier) return true;
            if (toTier < fromTier) to = new Vector3(to.X, from.Y, to.Z);
        }
        // Low cover right in front of it is fired over; cover the target hides behind stops the shot (C-29).
        if (core != null)
        {
            var hit = Rays.Cast(this, from, to, Layers.EnemyShotMask);
            return hit == null || hit.Value.Collider == core;
        }
        return CoverRule.Clear(this, GlobalPosition, Radius, from, to, Layers.EnemyShotMask);
    }

    /// <param name="victim">Client puppet: the survivor the host's enemy is aiming at.</param>
    public void StartWindup(Player? victim = null, Vector3? aim = null, BaseCore? core = null)
    {
        if (IsDead || StunTime > 0) return;
        AbortShot();
        WindingUp = true;
        Awake = true;
        _timer = Shot.Windup;
        if (HunterBow)
        {
            _bowReleased = false;
            _bowTime = 0;
            _bowDrawSound = Stage.Sfx.Attach("hunter_bow_draw", this);
            if (_bowDrawSound != null)
                _bowDrawSound.PitchScale = (float)_bowDrawSound.Stream.GetLength() / Shot.Windup;
        }
        if (aim == null) victim ??= Target ?? Stage.NearestAlivePlayer(GlobalPosition) ?? Stage.Player;
        _target = aim ?? victim!.GlobalPosition + Vector3.Up * .8f;
        _targetCore = core;
        // Shooting at you from the dark: you see who it is from the windup until a moment after the shot (C-23).
        RevealTo(victim, Shot.Windup + 1f + VisionSystem.RevealAfterHit);
        Stage.Net?.OnEnemyAttack(this, NetAttack.Ranged, victim, _target);
        var color = Shot.HazardSeconds > 0 ? new Color("b4df45") : new Color("ff6252");
        // The locked target is torso-height. Telegraphs belong on the actors' floor,
        // including upper tiers; a world-zero marker disappears inside elevated terrain.
        var flatTarget = new Vector3(_target.X, _target.Y - .8f + .055f, _target.Z);
        if (Shot.HazardSeconds > 0)
            _marker = Mat.Mesh(Stage.Effects, new CylinderMesh { TopRadius = Shot.HazardRadius, BottomRadius = Shot.HazardRadius, Height = .025f }, flatTarget, Mat.Fade(new Color(color, .35f)));
        else
        {
            var start = GlobalPosition + Vector3.Up * .055f;
            _marker = Mat.Box(Stage.Effects, (start + flatTarget) * .5f, new Vector3(.12f, .03f, start.DistanceTo(flatTarget)), Mat.Glow(color));
            if (start.DistanceTo(flatTarget) > .01f) _marker.LookAt(flatTarget, Vector3.Up);
        }
        if (!HunterBow) Stage.Sfx.PlayAt("bull_snort", GlobalPosition, -8, 1.8f);
    }

    private void AbortShot()
    {
        if (WindingUp) Stage.Net?.OnEnemyAttackEnd(this);
        WindingUp = false;
        StopBowDraw();
        if (HunterBow && !_bowReleased) { _bowTime = -1; Rig?.ResetHunterAttackPose(); }
        _targetCore = null;
        _cooldown = Shot.Cooldown;
        if (IsInstanceValid(_marker)) _marker!.QueueFree();
        _marker = null;
    }
    /// <summary>Actual projectile event, on host and puppet. Never release merely because a local timer expired.</summary>
    public void ShowShotRelease()
    {
        if (!HunterBow) return;
        _bowReleased = true;
        _bowTime = Shot.Windup;
        StopBowDraw();
    }

    internal void AnimateHunterBow(float dt)
    {
        if (!HunterBow) return;
        if (_bowTime >= 0)
        {
            _bowTime += dt;
            if (!_bowReleased) _bowTime = Mathf.Min(_bowTime, Shot.Windup - .001f);
            else if (_bowTime >= Shot.Windup + HunterBowRecovery) { _bowTime = -1; _bowReleased = false; }
        }
        Rig.SetHunterAttackPose("woodbow", _bowTime, Shot.Windup, HunterBowRecovery);
    }

    private void StopBowDraw()
    {
        if (IsInstanceValid(_bowDrawSound)) { _bowDrawSound!.Stop(); _bowDrawSound.QueueFree(); }
        _bowDrawSound = null;
    }

    // End follows Projectile in the reliable stream; retain the short release recovery.
    public void EndPuppetShot() => AbortShot();
    protected override void OnStunned()
    {
        AbortShot(); _bowTime = -1; _bowReleased = false;
        if (HunterBow) Rig?.ResetHunterAttackPose();
    }
    public override void Die(Vector3 direction, float force) { OnStunned(); base.Die(direction, force); }
    public override void _ExitTree() { if (IsInstanceValid(_marker)) _marker!.QueueFree(); }
}
