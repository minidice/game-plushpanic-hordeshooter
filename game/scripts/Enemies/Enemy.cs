using Godot;

namespace HordeShooter;

/// <summary>
/// Common infected: wanders while idle, wakes on noise or proximity, then follows the shared flow
/// field and swarms the player. Specials derive from this and override <see cref="Think"/>.
/// Stats come from <see cref="Monster"/> (monsters.csv); the look from <see cref="Definition"/> (.tres).
/// </summary>
public partial class Enemy : CharacterBody3D
{
    /// <summary>
    /// Bullets fly flat at <see cref="Player.AimHeight"/> and the top-down camera gives no way to aim lower,
    /// so every body reaches at least this high even when its model crouches or crawls below it.
    /// </summary>
    public const float MinHitHeight = Player.AimHeight + .4f;

    private const float Gravity = 20f;
    private const float MeleeWindup = .28f;
    private const float MeleeRecovery = .32f;

    public Stage Stage { get; private set; } = null!;
    /// <summary>Look (model, collider size, colors).</summary>
    public CharacterDefinition Definition { get; private set; } = null!;
    /// <summary>Stats and special behaviour from content/data/monsters.csv.</summary>
    public MonsterDefinition Monster { get; private set; } = null!;
    public CharacterRig Rig { get; private set; } = null!;
    public float Health { get; protected set; }
    public bool IsDead { get; protected set; }
    public bool Awake { get; set; }
    private Reaction _reaction;
    /// <summary>How this placement wakes up (decisions.md L-5). Hunters are always awake.</summary>
    public Reaction Reaction
    {
        get => _reaction;
        set { _reaction = value; if (value == Reaction.Hunter) Awake = true; }
    }
    /// <summary>Inside the player's vision cone this frame (maintained by <see cref="VisionSystem"/>).</summary>
    public bool IsSeen { get; set; } = true;
    public float PingTimer { get; set; }
    public bool IsSpecial => Monster.IsSpecial;
    /// <summary>Commons chase the lure; specials ignore it.</summary>
    public virtual bool Lurable => !IsSpecial;
    public float Radius => Definition.ColliderRadius;
    public float StunTime { get; private set; }
    /// <summary>Short stagger after a hit: own movement is slowed, so even small hits read as a flinch.</summary>
    public float FlinchTime { get; private set; }
    public float Speed => Monster.MoveSpeed;
    public bool IsMeleeAttacking => _meleeTime >= 0;

    /// <summary>Melee reach measured from body edge to body edge.</summary>
    protected float AttackReach { get; set; } = .4f;
    protected float AttackDamage { get; set; } = 4f;
    protected float AttackInterval { get; set; } = 1.0f;
    protected Vector3? FaceOverride { get; set; }
    protected CollisionShape3D Shape { get; private set; } = null!;
    protected readonly RandomNumberGenerator Rng = new();
    protected float Hurt, Kick, AttackCooldown;
    protected Vector3 Knock;

    private bool _los;
    private float _stepDistance;
    private float _losTimer, _wanderTimer;
    private Vector3 _wander;
    private float _meleeTime = -1;
    private Player? _meleeTarget;
    private float _targetTimer;

    /// <summary>The survivor this enemy is after (nearest living one, re-picked a few times a second).</summary>
    protected Player? Target { get; private set; }
    /// <summary>Who this enemy is currently after (read by AI squadmates to protect teammates).</summary>
    public Player? Victim => IsDead ? null : Target;

    protected Player? CurrentTarget(float dt)
    {
        _targetTimer -= dt;
        if (_targetTimer <= 0 || Target == null || !IsInstanceValid(Target) || Target.IsDead)
        {
            _targetTimer = .3f;
            Target = Stage.NearestAlivePlayer(GlobalPosition);
        }
        return Target;
    }
    private bool _meleeHit;
    private Vector3 _meleeDirection;

    // Specials can enter their own windups before base.Think runs, including scripted starts.
    private bool SpecialAttackActive => this switch
    {
        BullEnemy bull => bull.State != BullEnemy.Mode.Chase,
        CaptureEnemy captor => captor.State != CaptureEnemy.Mode.Chase,
        RangedEnemy ranged => ranged.WindingUp,
        _ => false,
    };

    public void Setup(Stage stage, MonsterDefinition monster, CharacterDefinition look, bool awake)
    {
        Stage = stage;
        Monster = monster;
        Definition = look;
        Reaction = awake ? Reaction.Hunter : Reaction.Dormant;
        Awake = awake;
        Health = monster.Hp;
        AttackDamage = monster.AttackDamage;
        AttackInterval = monster.AttackInterval;
        AttackReach = monster.AttackReach;
        Name = monster.Id;
    }

    public override void _Ready()
    {
        CollisionLayer = Layers.Enemy;
        CollisionMask = Layers.World | Layers.Player | Layers.Props;
        FloorSnapLength = .3f;
        float bodyHeight = Mathf.Max(Mathf.Max(Definition.ColliderHeight, MinHitHeight), Radius * 2.05f);
        Shape = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Radius, Height = bodyHeight },
            Position = Vector3.Up * bodyHeight * .5f,
        };
        AddChild(Shape);
        Rig = CharacterRig.Create(Definition);
        AddChild(Rig);
        Rig.InitializeEnemyAttackPose();
        if (Monster.Id is "wild_animal" or "armed_animal" or "armed_veteran")
        {
            Rig.HideBuiltInWeapon();
            if (Monster.Id == "wild_animal")
                foreach (var gear in Definition.GearNodes) Rig.HidePart(gear);
        }
        Rig.Face(new Vector3(Rng.RandfRange(-1, 1), 0, Rng.RandfRange(-1, 1)));
        _losTimer = Rng.Randf() * .3f;
        AttackCooldown = Rng.RandfRange(.2f, .6f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;
        float dt = (float)delta;
        Hurt = Mathf.MoveToward(Hurt, 0, dt * 3);
        Kick = Mathf.MoveToward(Kick, 0, dt * 5);
        AttackCooldown -= dt;
        FaceOverride = null;

        Vector3 desired;
        if (StunTime > 0)
        {
            CancelMelee();
            StunTime -= dt;
            Hurt = Mathf.Max(Hurt, .35f);
            desired = Vector3.Zero;
        }
        else if (Lurable && Stage.Lure is { } lure && LureDirection(lure, dt) is { } toLure)
        {
            CancelMelee();
            desired = toLure;
        }
        else desired = Think(dt);
        if (SpecialAttackActive || (IsMeleeAttacking && _meleeTarget is not { IsDead: false })) CancelMelee();
        TickMelee(dt);
        if (FlinchTime > 0)
        {
            FlinchTime -= dt;
            desired *= .25f;
        }
        desired += Stage.Crowd.Separation(this) * 2.2f;
        Knock = Knock.MoveToward(Vector3.Zero, dt * 14f);
        var horizontal = desired + Knock;
        bool grounded = IsOnFloor() && Velocity.Y <= 0;
        Velocity = new Vector3(horizontal.X, grounded ? -.1f : Velocity.Y - Gravity * dt, horizontal.Z);
        MoveAndSlide();
        AfterMove(dt);

        var flat = new Vector3(Velocity.X, 0, Velocity.Z);
        Footsteps(flat.Length() * dt);
        if (FaceOverride is { } face) Rig.Face(face);
        else if (flat.LengthSquared() > .05f) Rig.Face(flat);
        Rig.ResetEnemyAttackPose();
        Rig.Animate(dt, Mathf.Min(1, flat.Length() / Speed), Kick, Hurt, Velocity);
        if (IsMeleeAttacking) Rig.ApplyEnemyAttackPose(_meleeTime, MeleeWindup, MeleeRecovery);
        if (this is BullEnemy bull && bull.State is BullEnemy.Mode.Windup or BullEnemy.Mode.Charge)
            Rig.ApplyBullChargePose(bull.State == BullEnemy.Mode.Charge ? 1f : .65f);

        if (GlobalPosition.Y < -10) Die(Vector3.Up, 0);
    }

    /// <summary>Returns the desired horizontal velocity for this frame.</summary>
    protected virtual Vector3 Think(float dt)
    {
        if (IsMeleeAttacking)
        {
            FaceOverride = _meleeDirection;
            return Vector3.Zero;
        }
        var player = CurrentTarget(dt);
        if (player == null) return Wander(dt);
        var toPlayer = player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0;
        float dist = toPlayer.Length();

        if (!Awake)
        {
            // Dormant ones ignore being looked at; watchers use their own eyes (spawn-design.md 2장).
            if (dist < Monster.WakeRange ||
                (Reaction == Reaction.Watcher && dist < Stage.Director.Rules.SightRange && HasLineOfSight(dt))) Wake();
            else return Wander(dt);
        }
        if (dist <= Radius + player.Definition.ColliderRadius + AttackReach)
        {
            FaceOverride = toPlayer;
            TryMelee(player, toPlayer);
            return Vector3.Zero;
        }
        return ChaseDirection(toPlayer, dist, dt) * Speed;
    }

    protected virtual void AfterMove(float dt) { }

    /// <summary>Shuffling feet for awake zombies close enough to matter; the voice limit keeps a horde from roaring.</summary>
    private void Footsteps(float moved)
    {
        if (!Awake || moved <= 0) return;
        _stepDistance += moved;
        if (_stepDistance < .9f) return;
        _stepDistance = Rng.RandfRange(-.2f, 0);
        if (GlobalPosition.DistanceSquaredTo(Stage.Player.GlobalPosition) < 16f * 16f)
            Stage.Sfx.PlayAt("zombie_step", GlobalPosition, 0, Rng.RandfRange(.85f, 1.1f));
    }

    /// <summary>Walk to the beeping lure (shared flow field toward it), crowd around it, ignore the player.</summary>
    private Vector3? LureDirection(ThrownItem lure, float dt)
    {
        var to = lure.GlobalPosition - GlobalPosition;
        to.Y = 0;
        float dist = to.Length();
        if (dist > lure.AttractRadius) return null;
        Awake = true;
        if (dist < 1.4f) return Vector3.Zero;
        var flow = Stage.LureFlow?.Direction(GlobalPosition) ?? Vector3.Zero;
        if (dist < 5f || flow == Vector3.Zero) return to / dist * Speed;
        return flow * Speed;
    }

    /// <summary>
    /// Wakes up. A direct cause (hit, too close, seen) also wakes still-idle neighbours within the alert
    /// radius, one hop only, so one shot never wakes the whole map (decisions.md L-5).
    /// </summary>
    public void Wake(bool alertOthers = true)
    {
        if (Awake || IsDead) return;
        Awake = true;
        if (alertOthers) Stage.Director.AlertAround(this);
    }

    /// <summary>Jump off a wall top toward <paramref name="direction"/> (ledge entry). Gravity does the rest.</summary>
    public void LeapDown(Vector3 direction)
    {
        Knock += new Vector3(direction.X, 0, direction.Z).Normalized() * 6f;
        Kick = 1;
    }

    /// <summary>Flashbang: stand there dazed.</summary>
    public void Stun(float seconds)
    {
        if (IsDead) return;
        CancelMelee();
        StunTime = Mathf.Max(StunTime, seconds);
        Wake();
        OnStunned();
        Rig.Jolt(new Vector3(Rng.RandfRange(-3, 3), 0, Rng.RandfRange(-3, 3)));
    }

    /// <summary>Hook for specials that must abort what they were doing (bull charge).</summary>
    protected virtual void OnStunned() { }

    /// <summary>Damage over time (fire). No knockback, no stuffing spam.</summary>
    public void Burn(float damage)
    {
        if (IsDead || damage <= 0) return;
        Health -= damage;
        Rig.FlashHit();
        Wake();
        Hurt = Mathf.Max(Hurt, .3f);
        if (Health <= 0) Die(Vector3.Up, 2f);
    }

    protected Vector3 ChaseDirection(Vector3 toPlayer, float dist, float dt)
    {
        bool los = HasLineOfSight(dt);
        if (los && dist < 8f) return toPlayer / Mathf.Max(dist, .001f);
        var flow = Stage.Flow.Direction(GlobalPosition);
        if (flow != Vector3.Zero) return flow;
        return los ? toPlayer / Mathf.Max(dist, .001f) : Vector3.Zero;
    }

    protected Vector3 Wander(float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0)
        {
            _wanderTimer = Rng.RandfRange(2, 5);
            _wander = Rng.Randf() < .45f
                ? Vector3.Zero
                : new Vector3(Rng.RandfRange(-1, 1), 0, Rng.RandfRange(-1, 1)).Normalized() * Speed * .18f;
        }
        if (_wander != Vector3.Zero && Stage.Flow.IsBlocked(GlobalPosition + _wander.Normalized() * .8f)) _wander = -_wander;
        return _wander;
    }

    protected bool HasLineOfSight(float dt)
    {
        _losTimer -= dt;
        if (_losTimer > 0) return _los;
        _losTimer = .25f;
        var target = Target ?? Stage.NearestAlivePlayer(GlobalPosition);
        if (target == null) { _los = false; return false; }
        var from = GlobalPosition + Vector3.Up;
        var to = target.GlobalPosition + Vector3.Up;
        _los = Rays.Clear(this, from, to, Layers.SightMask);
        return _los;
    }

    protected void TryMelee(Player player, Vector3 toPlayer)
    {
        if (AttackCooldown > 0 || IsMeleeAttacking || StunTime > 0 || FlinchTime > 0 || player.IsDead || SpecialAttackActive) return;
        if (!Rays.Clear(this, GlobalPosition + Vector3.Up * .7f, player.GlobalPosition + Vector3.Up * .7f, Layers.World | Layers.Props)) return;
        AttackCooldown = Mathf.Max(AttackInterval, MeleeWindup + MeleeRecovery);
        _meleeTarget = player;
        _meleeTime = 0;
        _meleeHit = false;
        toPlayer.Y = 0;
        _meleeDirection = toPlayer.LengthSquared() > .0001f ? toPlayer.Normalized() : Rig.Forward;
        FaceOverride = _meleeDirection;
    }

    private void TickMelee(float dt)
    {
        if (!IsMeleeAttacking) return;
        FaceOverride = _meleeDirection;
        _meleeTime += dt;
        if (!_meleeHit && _meleeTime >= MeleeWindup)
        {
            _meleeHit = true;
            Kick = 1;
            var player = _meleeTarget;
            if (player == null || !IsInstanceValid(player)) { CancelMelee(); return; }
            var to = player.GlobalPosition - GlobalPosition;
            float height = Mathf.Abs(to.Y);
            to.Y = 0;
            float distance = to.Length();
            // Re-evaluate the real target at impact: a dodge, sidestep or new wall makes the swing miss.
            if (!IsDead && StunTime <= 0 && !player.IsDead && !player.IsRolling &&
                height < 1.1f && distance <= Radius + player.Definition.ColliderRadius + AttackReach &&
                (distance < .001f || _meleeDirection.Dot(to / distance) >= .55f) &&
                Rays.Clear(this, GlobalPosition + Vector3.Up * .7f, player.GlobalPosition + Vector3.Up * .7f, Layers.World | Layers.Props))
                player.TakeDamage(AttackDamage, _meleeDirection, 1.2f);
        }
        if (_meleeTime >= MeleeWindup + MeleeRecovery) CancelMelee();
    }

    protected void CancelMelee()
    {
        _meleeTime = -1;
        _meleeHit = false;
        _meleeTarget = null;
        Rig?.ResetEnemyAttackPose();
    }

    public virtual void TakeHit(HitInfo hit)
    {
        if (IsDead) return;
        Health -= hit.Damage;
        if (hit.Damage > 0) Rig.FlashHit();
        Hurt = 1;
        Wake();
        var flat = new Vector3(hit.Direction.X, 0, hit.Direction.Z).Normalized();
        ApplyKnock(flat, hit.Power);
        Rig.Jolt(flat * 4f);
        Stage.Effects.Stuffing(hit.Point, -flat, Definition.StuffingColor, hit.Crit ? 16 : 5);
        if (Health > 0) Stage.Sfx.PlayAt("zombie_hit", hit.Point);
        if (Health <= 0) Die(flat, hit.Crit ? 7.5f : 5f);
    }

    /// <summary>
    /// Knockback power → push. Heavier monsters move less (speed ÷ mass) and the flinch is shorter.
    /// A rifle bullet (15) is a ~0.1 m stagger; a full shotgun blast (90) or explosion (150) sends them flying.
    /// </summary>
    public void ApplyKnock(Vector3 direction, float power)
    {
        if (power <= 0) return;
        CancelMelee();
        Knock += direction * HordeShooter.Knockback.Hit(power) / Monster.Mass;
        FlinchTime = Mathf.Max(FlinchTime, Mathf.Clamp(power / Monster.Mass * .004f, .05f, .35f));
    }

    /// <summary>Shove from explosions, charges, rolls and balloon pops. Lethal shoves turn into bowling-pin deaths.</summary>
    public virtual void Shove(Vector3 direction, float power, float damage)
    {
        if (IsDead) return;
        direction.Y = 0;
        direction = direction.LengthSquared() > .0001f ? direction.Normalized() : Vector3.Forward;
        Wake();
        Health -= damage;
        if (damage > 0) Rig.FlashHit();
        Hurt = 1;
        ApplyKnock(direction, power);
        if (Health <= 0) Die(direction, HordeShooter.Knockback.Hit(power) / Monster.Mass);
    }

    public virtual void Die(Vector3 direction, float force)
    {
        if (IsDead) return;
        CancelMelee();
        IsDead = true;
        Stage.OnEnemyDied(this);
        CollisionLayer = 0;
        CollisionMask = 0;
        Shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        var center = GlobalPosition + Vector3.Up * Definition.ColliderHeight * .5f;
        Stage.Effects.Stuffing(center, direction, Definition.StuffingColor, 22);
        Stage.Sfx.PlayAt("zombie_die", center);
        Stage.Effects.PaintSplat(GlobalPosition);
        SpawnCorpse(direction, force, Definition.ColliderRadius, Definition.ColliderHeight);
        QueueFree();
    }

    protected void SpawnCorpse(Vector3 direction, float force, float radius, float height)
    {
        direction.Y = 0;
        var impulse = direction.Normalized() * force + Vector3.Up * (force * .7f + 2f);
        var torque = new Vector3(Rng.RandfRange(-2, 2), Rng.RandfRange(-1, 1), Rng.RandfRange(-2, 2)) * (1 + force * .15f);
        Stage.Effects.SpawnCorpse(Rig, radius, height, impulse, torque, Monster);
    }
}
