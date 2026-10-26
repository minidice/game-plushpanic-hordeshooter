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
    /// <summary>Max health after cards (glass cannon halves it, M-29); never below 1.</summary>
    public int MaxHealth => Mathf.Max(1, Mathf.RoundToInt(Definition.MaxHealth * MaxHealthScale * MadHealthScale));

    /// <summary>Cards' share of the definition's max health (1 = none). Lowering it trims health down to the new top.</summary>
    public float MaxHealthScale
    {
        get => _maxHealthScale;
        set
        {
            _maxHealthScale = value;
            if (Health > MaxHealth) Health = MaxHealth;
        }
    }
    private float _maxHealthScale = 1f;
    /// <summary>Dead for good. A downed survivor has 0 health but is not dead (see PlayerDowned).</summary>
    public bool IsDead => Health <= 0 && !IsDowned;
    public int Medkits { get; set; }
    /// <summary>Outpost's 4 × 5 backpack (decisions.md M-25); null in the other modes.</summary>
    public Backpack? Backpack { get; set; }
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
    // Focus needs this PC's own view of the enemies, so only the local player can line up a crit.
    /// <summary>Client: a hit the host reported (the arc on the HUD; health arrives with the snapshot).</summary>
    public void ShowNetHit(Vector3 direction)
    {
        direction.Y = 0;
        if (direction.LengthSquared() > .0001f) HitFrom?.Invoke(direction.Normalized());
    }

    public bool CritReady => IsLocal && FocusTarget != null && FocusTime >= CritFocusTime;

    /// <summary>
    /// One crit per focus (decisions.md C-32): every shot (a swing once it lands) spends the focus built so far, so the
    /// next crit needs another 0.5 s on the target.
    /// </summary>
    public void SpendFocus() => FocusTime = 0;

    /// <summary>Movement while the aim of a throw or a lobbed weapon is up (decisions.md C-26).</summary>
    public const float LobAimMoveScale = .5f;
    /// <summary>
    /// Holding right click or Ctrl with a gun that can aim (decisions.md C-26): range ×aim_range_mult, tighter spread,
    /// slower walk, faster focus, the camera looks further toward the cursor. Not while busy or throwing.
    /// </summary>
    public bool IsAiming { get; private set; }
    /// <summary>0–1 easing of the aim, for the low stance and the camera.</summary>
    public float AimBlend { get; private set; }
    /// <summary>Where shots have kicked the aim point (decisions.md C-30): added to the mouse, so the hand pulls it back.</summary>
    public WeaponRecoil Recoil { get; } = new();
    public RigidBody3D? Corpse { get; private set; }
    public float DamageFlash { get; private set; }
    public bool ManualControl { get; set; }
    public bool Invulnerable { get; set; }
    /// <summary>An AI squadmate: driven through the same controls, no audio listener, no crits.</summary>
    public bool IsBot { get; set; }
    /// <summary>Another person's survivor in a network match (set before it enters the tree).</summary>
    public bool IsRemote { get; set; }
    /// <summary>This PC's own human player: owns the camera, the ears, the HUD and screen shake.</summary>
    public bool IsLocal => !IsBot && !IsRemote;
    /// <summary>Network peer of the person playing this survivor (0 offline or for squadmates).</summary>
    public int PeerId { get; set; }
    /// <summary>True while the network layer applies the host's decisions (health, downs) on a client.</summary>
    internal bool NetApplying { get; set; }
    /// <summary>Scales this survivor's weapon damage (AI squadmates hit softer, decisions.md M-12).</summary>
    public float DamageMultiplier { get; set; } = 1f;
    /// <summary>What this survivor's weapons multiply their damage by: cards' multiplier, gather!'s bonus, the mad scientist's size.</summary>
    public float OutgoingDamage => DamageMultiplier * (1 + GatherDamage) * MadDamageScale * (WeakTime > 0 ? 1 - HonkBeamWeakDamage : 1);

    /// <summary>Caught in a teammate's honk beam (M-29 16.6.2): dazed (no moving, no shooting, stars), then weak (slower, softer).</summary>
    public float DazeTime { get; set; }
    public float WeakTime { get; set; }
    public const float HonkBeamWeakSpeed = .4f, HonkBeamWeakDamage = .5f;
    /// <summary>Static fluff's pole (M-29 16.6.3): 1 plus, −1 minus, 0 none. Sent to clients in the snapshot.</summary>
    public int Pole { get; set; }
    /// <summary>Static fluff: next to someone of the same pole, this survivor's rounds jump on to nearby monsters (host).</summary>
    public bool Charged { get; set; }
    private float _drainDebt;

    /// <summary>Static fluff's spark between opposite poles: loses health, never below <paramref name="floor"/> of the max (never downs).</summary>
    public void Drain(float amount, float floor)
    {
        if (IsDead || IsDowned || Invulnerable) return;
        _drainDebt += amount;
        int whole = Mathf.FloorToInt(_drainDebt);
        if (whole <= 0) return;
        _drainDebt -= whole;
        int lowest = Mathf.CeilToInt(MaxHealth * floor);
        if (Health > lowest) Health = Mathf.Max(lowest, Health - whole);
    }
    /// <summary>Gather! (M-29 16.6.1): this survivor's bonus while in the ring — damage, crit damage or fire rounds (host-set, 0 = none).</summary>
    public float GatherDamage { get; set; }
    public float GatherCrit { get; set; }
    public float GatherFire { get; set; }

    /// <summary>Mad scientist goose (M-29): 1 big, −1 small, 0 neither; its card level scales how much. Sent to clients in the snapshot.</summary>
    public int MadSize
    {
        get => _madSize;
        set
        {
            if (_madSize == value) return;
            _madSize = value;
            ApplyMadBody();
        }
    }
    public int MadLevel
    {
        get => _madLevel;
        set
        {
            if (_madLevel == value) return;
            _madLevel = value;
            ApplyMadBody();
        }
    }
    private int _madSize, _madLevel = 1;
    /// <summary>How strong the size change is: 1, 1.25, 1.5 by level.</summary>
    public float MadPower => 1 + .25f * (Mathf.Clamp(_madLevel, 1, 3) - 1);
    public float MadBodyScale => _madSize > 0 ? 1 + .35f * MadPower : _madSize < 0 ? 1 - .28f * MadPower : 1;
    public float MadHealthScale => _madSize > 0 ? 1 + .5f * MadPower : _madSize < 0 ? 1 - .3f * MadPower : 1;
    public float MadDamageScale => _madSize > 0 ? 1 + .3f * MadPower : 1;
    public float MadSpeedScale => _madSize > 0 ? 1 - .1f * MadPower : _madSize < 0 ? 1 + .25f * MadPower : 1;
    public float MadRollScale => _madSize < 0 ? 1 + .4f * MadPower : 1;

    /// <summary>Bigger or smaller body and collider; health trims to the new top.</summary>
    private void ApplyMadBody()
    {
        float s = MadBodyScale;
        if (Rig != null) Rig.Scale = Vector3.One * s;
        if (_shape?.Shape is CapsuleShape3D capsule && Definition != null)
        {
            capsule.Radius = Definition.ColliderRadius * s;
            capsule.Height = Definition.ColliderHeight * s;
            _shape.Position = new Vector3(_shape.Position.X, Definition.ColliderHeight * s * .5f, _shape.Position.Z);
        }
        if (Health > MaxHealth) Health = MaxHealth;
    }
    /// <summary>Tap dance steps (M-29): each one walks and fires a little faster. Set by the host's cards, sent to clients.</summary>
    public int TapStacks { get; set; }
    /// <summary>Fire rate from cards (1 = as the weapon says).</summary>
    public float FireRateScale => 1 + TapStacks * LabCards.TapFirePerStack;
    /// <summary>Look to wear instead of the saved one for this species (AI squadmates use the default look).</summary>
    public AppearanceProfile? Appearance { get; set; }
    /// <summary>Name shown in the teammate list (empty = species name).</summary>
    public string DisplayLabel { get; set; } = "";

    public event Action? Died;
    /// <summary>Accepted damage with a direction (from the source toward this survivor). Drives the HUD hit arc.</summary>
    public event Action<Vector3>? HitFrom;
    /// <summary>Which way the last hit pushed (from the attacker toward this survivor); the toy train runs back up it.</summary>
    public Vector3 LastHitDirection { get; private set; } = Vector3.Back;

    private CollisionShape3D _shape = null!;
    private readonly RandomNumberGenerator _rng = new();
    private Vector2 _move;
    private bool _fire, _firePressed, _reloadPressed, _healPressed, _throwPressed, _rollPressed, _cancelPressed, _aimHeld;
    /// <summary>Set while the card window is open, cleared once the fire button is let go.</summary>
    private bool _fireAfterCards;
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
        CollisionMask = Layers.MovementWorld | Layers.Enemy | Layers.Props;
        FloorSnapLength = .3f;
        Health = MaxHealth;

        _shape = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Definition.ColliderRadius, Height = Definition.ColliderHeight },
            Position = Vector3.Up * Definition.ColliderHeight * .5f,
        };
        AddChild(_shape);

        Rig = CharacterRig.Create(Definition);
        Rig.ApplyAppearance(Appearance ?? AppearanceStore.ReadProfile(Definition.Id));
        AddChild(Rig);
        StatusVisualBinding.Attach(this);

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
        if (IsLocal)
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
    public void PressRoll() { if (!OnGondola) _rollPressed = true; }
    public void PressSlot(int slot) => _slotPressed = slot;
    /// <summary>Scripted aim (right click / Ctrl held).</summary>
    public void HoldAim(bool held) => _aimHeld = held;

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
        if (Input.IsActionJustPressed(GameInput.Slot3)) _slotPressed = 3;
        _aimHeld = Input.IsActionPressed(GameInput.AltFire) || Input.IsActionPressed(GameInput.Aim);
        // With the backpack or the card window open the mouse is for them: no shooting, aiming or throwing.
        if (Stage.Hud is { } hud && (hud.InventoryOpen || hud.CardWindowOpen)) _fire = _firePressed = _aimHeld = _throwPressed = _cancelPressed = false;
        // 1 · 2 · 3 pick a card while the card window is open; the click that picked one does not start firing.
        if (Stage.Hud?.CardWindowOpen == true) { _slotPressed = 0; _fireAfterCards = true; }
        if (_fireAfterCards && !Input.IsActionPressed(GameInput.Fire)) _fireAfterCards = false;
        if (_fireAfterCards) _fire = _firePressed = false;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;
        var mouse = GetViewport().GetMousePosition() + Recoil.Offset * (GetViewport().GetVisibleRect().Size.Y / WeaponRecoil.ReferenceHeight);
        // With height tiers the cursor lands on the tier under it, not on a plane at the survivor's own height (L-15).
        var hit = Stage.Tiers.Any
            ? Stage.Tiers.AimPoint(camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse), AimHeight)
            : new Plane(Vector3.Up, GlobalPosition.Y + AimHeight).IntersectsRay(camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse));
        if (hit.HasValue) SetAim(hit.Value);
    }

    // ---------- simulation ----------

    public override void _PhysicsProcess(double delta)
    {
        if (OnGondola) { HoldGondolaPosition(); return; }
        float dt = (float)delta;
        _hurt = Mathf.MoveToward(_hurt, 0, dt * 3);
        DamageFlash = Mathf.MoveToward(DamageFlash, 0, dt * 2.5f);
        BlindTime = Mathf.Max(0, BlindTime - dt);
        _honkCooldown -= dt;
        UpdateRestraint(dt);
        // Downed, dead or grabbed on a ladder: let go, and gravity does the rest.
        if (OnLadder && (IsDead || IsDowned || IsRestrained)) EndLadder(arrived: false);
        if (OnZipline && (IsDead || IsDowned)) EndZipline();
        if (IsDead) return;
        if (IsDowned) { UpdateDowned(dt); return; }

        if (!ManualControl) ReadInput();
        // A client's own presses are kept until its next input message to the host.
        if (IsLocal && Stage.Role == NetRole.Client) RecordNetEdges();
        if (OnLadder || OnZipline)
        {
            // On a ladder or a zipline: no shooting, using or rolling (L-16 · L-18); presses made now are dropped.
            if (OnLadder) LadderStep(dt);
            else ZiplineStep(dt);
            IsAiming = false;
            _firePressed = _reloadPressed = _healPressed = _throwPressed = _rollPressed = _cancelPressed = false;
            _cycle = 0;
            _slotPressed = 0;
            return;
        }
        HeliumTime = Mathf.Max(0, HeliumTime - dt);
        RollCooldownLeft = Mathf.Max(0, RollCooldownLeft - dt);
        // A daze (honk beam) holds like a stun but keeps them on their feet.
        bool knocked = StunTime > 0;
        bool stunned = knocked || DazeTime > 0;
        if (knocked) StunTime = Mathf.Max(0, StunTime - dt);
        DazeTime = Mathf.Max(0, DazeTime - dt);
        WeakTime = Mathf.Max(0, WeakTime - dt);
        UpdateHealing(dt, stunned);

        if (_rollPressed) TryStartRoll(stunned);
        UpdateReviving(dt, _move.LengthSquared() > .01f);
        bool busy = stunned || IsRolling || IsHealing || IsRestrained || Reviving != null;
        IsAiming = _aimHeld && !busy && Gun.Def.CanAim && !Weapons.AimingThrow && !Gun.Reloading;
        AimBlend = Mathf.MoveToward(AimBlend, IsAiming ? 1 : 0, dt * 6);
        Recoil.Tick(dt);
        UpdateFocus(dt);

        if (_slotPressed != 0 && !stunned && !IsRestrained) Weapons.SelectSlot(_slotPressed);
        if (_throwPressed && !busy)
        {
            if (Weapons.AimingThrow) Weapons.EndThrowAim();
            else Weapons.BeginThrowAim();
        }
        if (_cancelPressed) Weapons.EndThrowAim();
        if (_cycle != 0 && !IsRestrained) Weapons.CycleThrowable(_cycle);
        if (_reloadPressed && !busy && !Weapons.AimingThrow && Gun.StartReload()) PlayOwn("reload_start");

        var move = stunned || IsRestrained || Reviving != null || Weapons.Planted ? Vector3.Zero : new Vector3(_move.X, 0, _move.Y).LimitLength(1);
        float speed = Definition.MoveSpeed * (IsHealing ? .35f : 1f) * (Gun.Reloading ? .85f : 1f) * HandlingSpeedScale * (1 + TapStacks * LabCards.TapSpeedPerStack) *
                      MadSpeedScale * (WeakTime > 0 ? 1 - HonkBeamWeakSpeed : 1);
        bool grounded = IsOnFloor() && Velocity.Y <= 0;
        _push = _push.MoveToward(Vector3.Zero, dt * (grounded ? 10f : 1.2f));
        var horizontal = IsRestrained ? RestraintVelocity() : IsRolling ? KeepOnTier(_rollDirection * RollSpeed * MadRollScale) : KeepOnTier(move * speed) + _push;
        // On the host a remote survivor's reported spot loses to the host's own pull (grab, launch, down).
        if (NetMotion is { } follow && !(Stage.Authoritative && HostMovesMe)) horizontal = FollowVelocity(follow, dt);
        Velocity = new Vector3(horizontal.X, grounded ? -.1f : Velocity.Y - Gravity * dt, horizontal.Z);
        var beforeMove = GlobalPosition;
        MoveAndSlide();
        PushProps();
        if (dt > 0)
            Stage.Effects.PushCorpses(_shape, (GlobalPosition - beforeMove) / dt, IsRolling);
        if (IsRolling) UpdateRoll(dt);
        else if (grounded && !stunned) Footsteps(dt);

        if (!stunned && !IsRolling && !IsRestrained) Rig.Face(AimDirection);
        Rig.SetLocomotionPose(dt,move.Length(),_fire||_firePressed);
        Weapons.Tick(dt, move.LengthSquared() > .01f, _firePressed, _fire, busy);

        UpdateTumble(dt, knocked);
        float amount = Mathf.Min(1, new Vector2(Velocity.X, Velocity.Z).Length() / Definition.MoveSpeed);
        Rig.Animate(dt, stunned || IsRolling ? 0 : amount, Weapons.Kick, _hurt, Velocity);
        if (IsRolling) Rig.DodgePose(1 - _rollTime / RollTime);
        else Rig.AimStance(AimBlend);
        ApplyRestraintPose();

        _firePressed = _reloadPressed = _healPressed = _throwPressed = _rollPressed = _cancelPressed = false;
        _cycle = 0;
        _slotPressed = 0;
        if (GlobalPosition.Y < -10) TakeDamage(9999, Vector3.Up);
    }

    /// <summary>
    /// Walking or rolling never takes a survivor off a tier edge except at a drop edge or down a ramp (decisions.md L-16);
    /// shoves and launches still can, so they are added after this. Slides along the edge when only one axis is blocked.
    /// </summary>
    private Vector3 KeepOnTier(Vector3 velocity)
    {
        var tiers = Stage.Tiers;
        if (!tiers.Any || velocity.LengthSquared() < .0001f) return velocity;
        var here = GlobalPosition;
        float reach = Definition.ColliderRadius + .15f;
        // Both sides of a diagonal count too, so a corner over a lower tier is not cut.
        bool Clear(Vector3 dir) => tiers.CanWalk(here, here + dir * reach);
        bool Free(Vector3 v)
        {
            if (v.LengthSquared() < .0001f) return true;
            var dir = v.Normalized();
            if (!Clear(dir)) return false;
            if (Mathf.Abs(dir.X) > .3f && !Clear(new Vector3(Mathf.Sign(dir.X), 0, 0))) return false;
            if (Mathf.Abs(dir.Z) > .3f && !Clear(new Vector3(0, 0, Mathf.Sign(dir.Z)))) return false;
            return true;
        }
        if (Free(velocity)) return velocity;
        var alongX = new Vector3(velocity.X, 0, 0);
        var alongZ = new Vector3(0, 0, velocity.Z);
        if (Free(alongX) && Mathf.Abs(velocity.X) > .01f) return alongX;
        if (Free(alongZ) && Mathf.Abs(velocity.Z) > .01f) return alongZ;
        return Vector3.Zero;
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
        if (candidate != null && candidate == FocusTarget) FocusTime += dt * (IsAiming ? Gun.Def.AimFocusMultiplier : 1f);
        else
        {
            FocusTarget = candidate;
            FocusTime = 0;
        }
    }

    /// <summary>
    /// What the weapon in hand does to walking speed: its own move_speed_pct, then aiming (aim_move_pct) or a throw /
    /// lobbed weapon's aim (−50%).
    /// </summary>
    public float HandlingSpeedScale
    {
        get
        {
            float scale = Weapons.AimingThrow ? 1f : Gun.Def.MoveSpeedScale;
            if (Weapons.AimingThrow || Weapons.AimingLob) scale *= LobAimMoveScale;
            else if (IsAiming) scale *= Gun.Def.AimMoveScale;
            return scale;
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
        // Outpost: the next medkit comes out of the backpack into the Q slot by itself.
        if (Medkits == 0 && Backpack is { } bag && Stage.Authoritative && bag.Take(ItemKind.Medkit, InvItem.MedkitId, 1) == 1) Medkits = 1;
        IsHealing = false;
        ResetDowns();
        PlayOwn("heal");
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 2.6f, $"+{gain}", new Color("7dff9a"));
    }

    /// <summary>Squadmates walk right beside you; their steps sit under your own so the group does not clatter (I-63).</summary>
    public const float BotStepVolumeDb = -5f;

    /// <summary>One soft pat per stride; alternating pitch so it waddles.</summary>
    private void Footsteps(float dt)
    {
        float speed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (speed < .5f) return;
        _stepDistance += speed * dt;
        if (_stepDistance < .75f) return;
        _stepDistance = 0;
        _stepLeft = !_stepLeft;
        Stage.Sfx.PlayAt("step", GlobalPosition, IsBot ? BotStepVolumeDb : 0, _stepLeft ? 1f : 1.1f);
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
        CollisionMask = Layers.MovementWorld | Layers.Props;
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
            enemy.Shove(lateral, 50f, 0, this);
        }
        if (_rollTime > 0) return;
        _rollTime = 0;
        Rig.Pose.Transform = Transform3D.Identity;
        CollisionMask = Layers.MovementWorld | Layers.Enemy | Layers.Props;
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
        // On a client only the host decides damage; local blasts and hazards are just pictures (M-20).
        if (!Stage.Authoritative && !NetApplying) return;
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
            HitFrom?.Invoke(direction.Normalized());
            LastHitDirection = direction.Normalized();
        }
        Stage.Director.ReportDamage(damage);
        if (direction.LengthSquared() > .0001f) Stage.Net?.OnPlayerHit(this, direction.Normalized());
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
        if (!Stage.Authoritative && !NetApplying) return;
        // Nothing knocks a survivor off a ladder (L-16).
        if (IsDead || IsDowned || IsRolling || OnLadder || OnZipline) return;
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
        if (!IsLocal) Stage.Sfx.PlayAt(id, GlobalPosition + Vector3.Up);
        else Stage.Sfx.Play(id);
    }

    /// <summary>Moves the survivor instantly (AI catch-up); clears momentum so it does not slide on arrival.</summary>
    public void TeleportTo(Vector3 at)
    {
        // A teleport (squad catch-up) takes them off any ladder first, or the climb would pull them back.
        EndLadder(arrived: false);
        EndZipline();
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
        Stage.Effects.DeathBurst(Rig);
        Rig.Reparent(body);
        Rig.Position = Vector3.Down * h * .5f;
        var flat = direction.LengthSquared() > .0001f ? direction.Normalized() : -AimDirection;
        body.ApplyCentralImpulse(flat * 5f + Vector3.Up * 6f);
        body.ApplyTorqueImpulse(new Vector3(_rng.RandfRange(-3, 3), _rng.RandfRange(-1, 1), _rng.RandfRange(-3, 3)));
        Corpse = body;
        Stage.Sfx.PlayAt("honk_long", GlobalPosition + Vector3.Up * 1.5f);
        if (IsLocal) Stage.Cam.AddShake(.5f);
        Died?.Invoke();
    }
}
