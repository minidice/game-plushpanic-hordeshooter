using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// The survivor. Body/collision come from the chosen <see cref="CharacterDefinition"/>, visuals from
/// <see cref="CharacterRig"/>, weapons from <see cref="WeaponController"/>. Handles moving, aiming,
/// dodge roll, healing, being launched, and dying.
/// </summary>
public partial class Player : CharacterBody3D
{
    public const float AimHeight = 1.0f;
    public const float HealDuration = 2.6f;
    public const float CritFocusTime = .5f;
    public const float CritMultiplier = 2.5f;
    public const float RollCooldown = 5f;
    public const float RollTime = .42f;
    public const float RollSpeed = 10f;
    private const float Gravity = 20f;

    public Stage Stage { get; private set; } = null!;
    public CharacterDefinition Definition { get; private set; } = null!;
    public CharacterRig Rig { get; private set; } = null!;
    public WeaponController Weapons { get; private set; } = null!;
    public Loadout Loadout => Weapons.Loadout;
    /// <summary>The weapon in hand.</summary>
    public WeaponState Gun => Weapons.Current;
    public int ShotsFired => Weapons.ShotsFired;
    public int Health { get; private set; }
    public int MaxHealth => Definition.MaxHealth;
    /// <summary>Dead for good. A downed survivor has 0 health but is not dead (see PlayerDowned).</summary>
    public bool IsDead => Health <= 0 && !IsDowned;
    public int Medkits { get; set; }
    public bool IsHealing { get; private set; }
    public float HealProgress => IsHealing ? _healTime / HealDuration : 0;
    public float HeliumTime { get; private set; }
    public float BlindTime { get; private set; }
    public float StunTime { get; private set; }
    public bool IsStunned => StunTime > 0;
    public bool IsRolling => _rollTime > 0;
    public float RollCooldownLeft { get; private set; }
    public Vector3 AimPoint { get; private set; }
    public Vector3 AimDirection { get; private set; } = Vector3.Right;
    public Enemy? FocusTarget { get; private set; }
    public float FocusTime { get; private set; }
    public bool CritReady => !IsBot && FocusTarget != null && FocusTime >= CritFocusTime;
    public RigidBody3D? Corpse { get; private set; }
    public float DamageFlash { get; private set; }
    public bool ManualControl { get; set; }
    public bool Invulnerable { get; set; }
    /// <summary>An AI squadmate: driven through the same controls, no audio listener, no crits.</summary>
    public bool IsBot { get; set; }
    /// <summary>Scales this survivor's weapon damage (AI squadmates hit softer, decisions.md M-12).</summary>
    public float DamageMultiplier { get; set; } = 1f;
    /// <summary>Look to wear instead of the saved one for this species (AI squadmates use the default look).</summary>
    public AppearanceProfile? Appearance { get; set; }
    /// <summary>Name shown in the teammate list (empty = species name).</summary>
    public string DisplayLabel { get; set; } = "";

    public event Action? Died;

    private CollisionShape3D _shape = null!;
    private readonly RandomNumberGenerator _rng = new();
    private Vector2 _move;
    private bool _fire, _firePressed, _reloadPressed, _healPressed, _throwPressed, _rollPressed, _cancelPressed;
    private int _cycle;
    private int _slotPressed;
    private Vector3 _push, _tumbleDirection, _rollDirection;
    private float _hurt, _healTime, _tumble, _rollTime, _stepDistance, _honkCooldown;
    private bool _stepLeft;
    private readonly HashSet<Enemy> _rollBumped = new();

    public void Setup(Stage stage, CharacterDefinition def)
    {
        Stage = stage;
        Definition = def;
        Name = "Player";
    }

    public override void _Ready()
    {
        CollisionLayer = Layers.Player;
        CollisionMask = Layers.World | Layers.Enemy | Layers.Props;
        FloorSnapLength = .3f;
        Health = Definition.MaxHealth;

        _shape = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Definition.ColliderRadius, Height = Definition.ColliderHeight },
            Position = Vector3.Up * Definition.ColliderHeight * .5f,
        };
        AddChild(_shape);

        Rig = CharacterRig.Create(Definition);
        Rig.ApplyAppearance(Appearance ?? AppearanceStore.ReadProfile(Definition.Id));
        AddChild(Rig);

        // Flashlight sits just in front of the body so the body never shadows its own beam.
        Rig.AddChild(new SpotLight3D
        {
            Name = "Flashlight",
            Position = new Vector3(0, 1.55f, Definition.ColliderRadius + .3f),
            RotationDegrees = new Vector3(-14, 180, 0),
            SpotAngle = 52,
            SpotRange = 26,
            SpotAttenuation = .7f,
            LightColor = new Color("fff0cf"),
            // Squadmates' beams stay unshadowed: four shadowed spotlights cost more than they add.
            ShadowEnabled = !IsBot,
            LightEnergy = IsBot ? 3f : 5.5f,
        });
        Rig.AddChild(new OmniLight3D
        {
            Name = "Aura",
            Position = new Vector3(0, 2.4f, 0),
            OmniRange = 4.2f,
            LightEnergy = .55f,
            LightColor = new Color("c9d8ff"),
        });

        // Ears on the goose, not on the camera 34 m up: zombies behind you sound behind you.
        if (!IsBot)
        {
            var ears = new AudioListener3D { Name = "Ears", Position = Vector3.Up * 1.5f };
            AddChild(ears);
            ears.MakeCurrent();
        }

        Weapons = new WeaponController();
        Weapons.Setup(this);
        AddChild(Weapons);
        AimPoint = GlobalPosition + AimDirection * 5;
    }

    // ---------- control surface (also used by tests) ----------

    /// <summary>Scripted input. <paramref name="fire"/> is "held"; a press is detected on its rising edge.</summary>
    public void SetControls(Vector2 move, Vector3 aimPoint, bool fire)
    {
        _move = move;
        _firePressed |= fire && !_fire;
        _fire = fire;
        SetAim(aimPoint);
    }

    public void PressReload() => _reloadPressed = true;
    public void PressHeal() => _healPressed = true;
    /// <summary>G: start aiming the current throwable, or stop aiming.</summary>
    public void PressThrow() => _throwPressed = true;
    /// <summary>Right click: cancel aiming.</summary>
    public void PressCancel() => _cancelPressed = true;
    /// <summary>Mouse wheel while aiming: change throwable kind.</summary>
    public void PressCycle(int direction) => _cycle = direction;
    public void PressRoll() => _rollPressed = true;
    public void PressSlot(int slot) => _slotPressed = slot;

    private void SetAim(Vector3 point)
    {
        AimPoint = point;
        var dir = point - GlobalPosition;
        dir.Y = 0;
        if (dir.LengthSquared() > .04f) AimDirection = dir.Normalized();
    }

    private void ReadInput()
    {
        _move = Input.GetVector(GameInput.MoveLeft, GameInput.MoveRight, GameInput.MoveUp, GameInput.MoveDown);
        var groundMove=Stage.Cam.GroundMovement(_move);
        _move=new Vector2(groundMove.X,groundMove.Z);
        _fire = Input.IsActionPressed(GameInput.Fire);
        _firePressed |= Input.IsActionJustPressed(GameInput.Fire);
        _reloadPressed |= Input.IsActionJustPressed(GameInput.Reload);
        _healPressed |= Input.IsActionJustPressed(GameInput.Heal);
        _throwPressed |= Input.IsActionJustPressed(GameInput.Throw);
        _cancelPressed |= Input.IsActionJustPressed(GameInput.AltFire);
        if (Input.IsActionJustPressed(GameInput.NextThrowable)) _cycle = 1;
        if (Input.IsActionJustPressed(GameInput.PrevThrowable)) _cycle = -1;
        _rollPressed |= Input.IsActionJustPressed(GameInput.Dodge);
        _reviveHeld = Input.IsActionPressed(GameInput.Interact);
        if (Input.IsActionJustPressed(GameInput.DebugEscape)) DebugEscapeRestraint();
        if (Input.IsActionJustPressed(GameInput.Slot1)) _slotPressed = 1;
        if (Input.IsActionJustPressed(GameInput.Slot2)) _slotPressed = 2;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;
        var mouse = GetViewport().GetMousePosition();
        var plane = new Plane(Vector3.Up, GlobalPosition.Y + AimHeight);
        var hit = plane.IntersectsRay(camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse));
        if (hit.HasValue) SetAim(hit.Value);
    }

    // ---------- simulation ----------

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _hurt = Mathf.MoveToward(_hurt, 0, dt * 3);
        DamageFlash = Mathf.MoveToward(DamageFlash, 0, dt * 2.5f);
        BlindTime = Mathf.Max(0, BlindTime - dt);
        _honkCooldown -= dt;
        UpdateRestraint(dt);
        if (IsDead) return;
        if (IsDowned) { UpdateDowned(dt); return; }

        if (!ManualControl) ReadInput();
        HeliumTime = Mathf.Max(0, HeliumTime - dt);
        RollCooldownLeft = Mathf.Max(0, RollCooldownLeft - dt);
        bool stunned = StunTime > 0;
        if (stunned) StunTime = Mathf.Max(0, StunTime - dt);
        UpdateFocus(dt);
        UpdateHealing(dt, stunned);

        if (_rollPressed) TryStartRoll(stunned);
        UpdateReviving(dt, _move.LengthSquared() > .01f);
        bool busy = stunned || IsRolling || IsHealing || IsRestrained || Reviving != null;

        if (_slotPressed != 0 && !stunned && !IsRestrained) Weapons.SelectSlot(_slotPressed);
        if (_throwPressed && !busy)
        {
            if (Weapons.AimingThrow) Weapons.EndThrowAim();
            else Weapons.BeginThrowAim();
        }
        if (_cancelPressed) Weapons.EndThrowAim();
        if (_cycle != 0 && !IsRestrained) Weapons.CycleThrowable(_cycle);
        if (_reloadPressed && !busy && !Weapons.AimingThrow && Gun.StartReload()) PlayOwn("reload_start");

        var move = stunned || IsRestrained || Reviving != null ? Vector3.Zero : new Vector3(_move.X, 0, _move.Y).LimitLength(1);
        float speed = Definition.MoveSpeed * (IsHealing ? .35f : 1f) * (Gun.Reloading ? .85f : 1f);
        bool grounded = IsOnFloor() && Velocity.Y <= 0;
        _push = _push.MoveToward(Vector3.Zero, dt * (grounded ? 10f : 1.2f));
        var horizontal = IsRestrained ? RestraintVelocity() : IsRolling ? _rollDirection * RollSpeed : move * speed + _push;
        Velocity = new Vector3(horizontal.X, grounded ? -.1f : Velocity.Y - Gravity * dt, horizontal.Z);
        MoveAndSlide();
        PushProps();
        if (IsRolling) UpdateRoll(dt);
        else if (grounded && !stunned) Footsteps(dt);

        if (!stunned && !IsRolling && !IsRestrained) Rig.Face(AimDirection);
        Rig.SetLocomotionPose(dt,move.Length(),_fire||_firePressed);
        Weapons.Tick(dt, move.LengthSquared() > .01f, _firePressed, _fire, busy);

        UpdateTumble(dt, stunned);
        float amount = Mathf.Min(1, new Vector2(Velocity.X, Velocity.Z).Length() / Definition.MoveSpeed);
        Rig.Animate(dt, stunned || IsRolling ? 0 : amount, Weapons.Kick, _hurt, Velocity);
        if (IsRolling) Rig.DodgePose(1 - _rollTime / RollTime);
        ApplyRestraintPose();

        _firePressed = _reloadPressed = _healPressed = _throwPressed = _rollPressed = _cancelPressed = false;
        _cycle = 0;
        _slotPressed = 0;
        if (GlobalPosition.Y < -10) TakeDamage(9999, Vector3.Up);
    }

    private void UpdateFocus(float dt)
    {
        Enemy? candidate = null;
        float best = float.MaxValue;
        foreach (var enemy in Stage.Enemies)
        {
            if (enemy.IsDead || !enemy.IsSeen) continue;
            var d = enemy.GlobalPosition - AimPoint;
            d.Y = 0;
            float reach = enemy.Definition.ColliderRadius + .4f;
            if (d.LengthSquared() < reach * reach && d.LengthSquared() < best)
            {
                best = d.LengthSquared();
                candidate = enemy;
            }
        }
        if (candidate != null && candidate == FocusTarget) FocusTime += dt;
        else
        {
            FocusTarget = candidate;
            FocusTime = 0;
        }
    }

    private void UpdateHealing(float dt, bool stunned)
    {
        if (_healPressed && !IsHealing && !IsRolling && !IsRestrained && Medkits > 0 && Health < MaxHealth && !stunned)
        {
            IsHealing = true;
            _healTime = 0;
            Gun.CancelReload();
        }
        if (!IsHealing) return;
        if (stunned)
        {
            IsHealing = false;
            return;
        }
        _healTime += dt;
        if (_healTime < HealDuration) return;
        // Like L4D's first aid kit: restores 80% of missing health.
        int gain = Mathf.CeilToInt((MaxHealth - Health) * .8f);
        Health = Math.Min(MaxHealth, Health + gain);
        Medkits--;
        IsHealing = false;
        ResetDowns();
        PlayOwn("heal");
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 2.6f, $"+{gain}", new Color("7dff9a"));
    }

    /// <summary>One soft pat per stride; alternating pitch so it waddles.</summary>
    private void Footsteps(float dt)
    {
        float speed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (speed < .5f) return;
        _stepDistance += speed * dt;
        if (_stepDistance < .75f) return;
        _stepDistance = 0;
        _stepLeft = !_stepLeft;
        Stage.Sfx.PlayAt("step", GlobalPosition, 0, _stepLeft ? 1f : 1.1f);
    }

    // ---------- dodge roll ----------

    private void TryStartRoll(bool stunned)
    {
        if (stunned || IsRolling || IsHealing || IsRestrained || RollCooldownLeft > 0) return;
        var input = new Vector3(_move.X, 0, _move.Y);
        _rollDirection = input.LengthSquared() > .01f ? input.Normalized() : AimDirection;
        _rollTime = RollTime;
        RollCooldownLeft = RollCooldown;
        _rollBumped.Clear();
        Stage.Sfx.PlayAt("roll", GlobalPosition + Vector3.Up * .5f);
        // Roll through the crowd instead of into it.
        CollisionMask = Layers.World | Layers.Props;
        Rig.Face(_rollDirection);
    }

    private void UpdateRoll(float dt)
    {
        _rollTime -= dt;
        Rig.Face(_rollDirection);
        // Visual pose is applied after Rig.Animate so foot/weapon motion is not overwritten.
        foreach (var enemy in Stage.Enemies)
        {
            if (enemy.IsDead || _rollBumped.Contains(enemy)) continue;
            var to = enemy.GlobalPosition - GlobalPosition;
            to.Y = 0;
            if (to.Length() > Definition.ColliderRadius + enemy.Radius + .2f) continue;
            _rollBumped.Add(enemy);
            // Shove them aside, not ahead: the roll opens a path through the crowd.
            var lateral = to - _rollDirection * to.Dot(_rollDirection);
            if (lateral.LengthSquared() < .01f) lateral = _rollDirection.Cross(Vector3.Up) * (_rng.Randf() < .5f ? 1 : -1);
            enemy.Shove(lateral, 50f, 0);
        }
        if (_rollTime > 0) return;
        _rollTime = 0;
        Rig.Pose.Transform = Transform3D.Identity;
        CollisionMask = Layers.World | Layers.Enemy | Layers.Props;
    }

    private void PushProps()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is RigidBody3D body && body is not ExplosiveBarrel { Exploded: true })
                body.ApplyCentralImpulse(-collision.GetNormal() * (IsRolling ? 1.2f : .35f));
        }
    }

    // ---------- being hurt ----------

    public void TakeDamage(float amount, Vector3 direction, float push = 2.5f)
    {
        if (IsDead || Invulnerable || IsRolling || amount <= 0) return;
        if (IsDowned) { BleedFromHit(); return; }
        StopReviving();
        int damage = Mathf.CeilToInt(amount);
        Health = Math.Max(0, Health - damage);
        Rig.FlashHit();
        _hurt = 1;
        DamageFlash = Mathf.Min(1, DamageFlash + .5f + damage / 40f);
        direction.Y = 0;
        if (direction.LengthSquared() > .0001f)
        {
            _push += direction.Normalized() * push;
            Rig.Jolt(direction.Normalized() * 3);
        }
        Stage.Director.ReportDamage(damage);
        if (Health > 0 && _honkCooldown <= 0)
        {
            _honkCooldown = .4f;
            Stage.Sfx.PlayAt("honk", GlobalPosition + Vector3.Up * 1.5f);
        }
        if (Health <= 0)
        {
            if (CanGoDown()) GoDown();
            else Die(direction);
        }
    }

    /// <summary>Slapstick launch (charger hits, explosions). Stuns and tumbles the character.</summary>
    public void Launch(Vector3 impulse, float stun)
    {
        if (IsDead || IsDowned || IsRolling) return;
        _push = new Vector3(impulse.X, 0, impulse.Z);
        Velocity = new Vector3(Velocity.X, impulse.Y, Velocity.Z);
        StunTime = Mathf.Max(StunTime, stun);
        IsHealing = false;
        var flat = new Vector3(impulse.X, 0, impulse.Z);
        _tumbleDirection = flat.LengthSquared() > .01f ? flat.Normalized() : -AimDirection;
    }

    /// <summary>The local player hears their own sounds in the ear; a squadmate's come from where they stand.</summary>
    public void PlayOwn(string id)
    {
        if (IsBot) Stage.Sfx.PlayAt(id, GlobalPosition + Vector3.Up);
        else Stage.Sfx.Play(id);
    }

    /// <summary>Moves the survivor instantly (AI catch-up); clears momentum so it does not slide on arrival.</summary>
    public void TeleportTo(Vector3 at)
    {
        GlobalPosition = at;
        Velocity = Vector3.Zero;
        _push = Vector3.Zero;
    }

    /// <summary>Recoil push from heavy weapons (no stun).</summary>
    public void Shove(Vector3 impulse) => _push += new Vector3(impulse.X, 0, impulse.Z);

    public void ApplyHelium(float seconds) => HeliumTime = Mathf.Max(HeliumTime, seconds);

    public void Blind(float seconds)
    {
        if (seconds <= 0) return;
        if (BlindTime <= 0) Stage.Sfx.Play("ear_ring");
        BlindTime = Mathf.Max(BlindTime, seconds);
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 2.6f, Loc.Text("msg_flashbang_blind"), Colors.White);
    }

    public int Heal(int amount)
    {
        int before = Health;
        Health = Math.Min(MaxHealth, Health + amount);
        return Health - before;
    }

    private void UpdateTumble(float dt, bool stunned)
    {
        if (IsRolling) return;
        if (!stunned && _tumble == 0) return;
        if (stunned)
        {
            Rig.Face(-_tumbleDirection);
            bool airborne = !IsOnFloor() || Velocity.Y > 0;
            if (airborne) _tumble += dt * 11f;
            else
            {
                // Settle on the back, then stand up during the last moments of the stun.
                float lying = Mathf.Round((_tumble - Mathf.Pi / 2) / Mathf.Tau) * Mathf.Tau + Mathf.Pi / 2;
                float upright = Mathf.Round(_tumble / Mathf.Tau) * Mathf.Tau;
                _tumble = Mathf.Lerp(_tumble, StunTime < .35f ? upright : lying, 1 - Mathf.Exp(-dt * 12));
            }
        }
        else _tumble = 0;
        Rig.Pose.Rotation = new Vector3(-_tumble, 0, 0);
    }

    private void Die(Vector3 direction)
    {
        IsDowned = false;
        Health = 0;
        StopReviving();
        ReleaseRestraint();
        IsHealing = false;
        StunTime = 0;
        _tumble = 0;
        _rollTime = 0;
        Gun.CancelReload();
        CollisionLayer = 0;
        CollisionMask = 0;
        _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        Velocity = Vector3.Zero;
        Rig.ResetPose();

        float h = Definition.ColliderHeight;
        var body = new RigidBody3D
        {
            Name = "PlayerCorpse",
            Mass = 1,
            CollisionLayer = Layers.Corpse,
            CollisionMask = HordeShooter.Corpse.SceneryCollisionMask,
            ContinuousCd = true,
            LinearDamp = .6f,
            AngularDamp = 1f,
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = .85f, Bounce = .15f },
        };
        body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = Definition.ColliderRadius, Height = h } });
        Stage.AddChild(body);
        body.GlobalPosition = GlobalPosition + Vector3.Up * h * .5f;
        Rig.Reparent(body);
        Rig.Position = Vector3.Down * h * .5f;
        var flat = direction.LengthSquared() > .0001f ? direction.Normalized() : -AimDirection;
        body.ApplyCentralImpulse(flat * 5f + Vector3.Up * 6f);
        body.ApplyTorqueImpulse(new Vector3(_rng.RandfRange(-3, 3), _rng.RandfRange(-1, 1), _rng.RandfRange(-3, 3)));
        Corpse = body;
        Stage.Sfx.PlayAt("honk_long", GlobalPosition + Vector3.Up * 1.5f);
        if (!IsBot) Stage.Cam.AddShake(.5f);
        Died?.Invoke();
    }
}
