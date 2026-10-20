using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Turns the player's <see cref="Loadout"/> into action: weapon visuals in hand, firing for every
/// <see cref="FireMode"/>, and throwing. Lives as a child node of <see cref="Player"/>.
/// </summary>
public partial class WeaponController : Node
{
    public Loadout Loadout { get; } = ArmoryCatalog.NewLoadout();
    public WeaponState Current => Loadout.Current;
    public int ShotsFired { get; private set; }
    public float Kick { get; private set; }

    private Player _player = null!;
    private Stage Stage => _player.Stage;
    private readonly RandomNumberGenerator _rng = new();
    private WeaponDefinition? _shownDef;
    private Node3D? _visual;
    private Node3D? _heldThrowable;
    private ThrowPreview? _preview;
    private ThrowableDefinition? _shownThrowable;

    /// <summary>G was pressed: a throwable is in hand, the landing preview follows the cursor, click throws.</summary>
    public bool AimingThrow { get; private set; }
    public ThrowPreview? Preview => _preview;
    private List<Node3D> _muzzles = new();
    private int _muzzleIndex;
    private OmniLight3D _flash = null!;
    private float _flashTime, _swingElapsed;
    private WeaponState? _swingState;
    private bool _swingHit;
    private WeaponState? _reloadWatch;

    public void Setup(Player player)
    {
        _player = player;
        Name = "Weapons";
    }

    public override void _Ready()
    {
        _player.Died += CancelMeleeSwing;
        _player.Rig.HideBuiltInWeapon();
        _flash = new OmniLight3D { OmniRange = 5, LightEnergy = 0, LightColor = new Color("ffcc77") };
        _player.Rig.Weapon.AddChild(_flash);
        RefreshVisual();
    }

    public override void _ExitTree() => _player.Died -= CancelMeleeSwing;

    public Vector3 MuzzlePosition => _muzzles.Count > 0 ? _muzzles[_muzzleIndex % _muzzles.Count].GlobalPosition : _player.Rig.Muzzle.GlobalPosition;

    /// <summary>Rebuild the in-hand model if the active weapon changed.</summary>
    public void RefreshVisual()
    {
        if (_swingState != null && _swingState != Current) CancelMeleeSwing();
        if (_shownDef == Current.Def && _visual != null) return;
        CancelMeleeSwing();
        _visual?.QueueFree();
        _shownDef = Current.Def;
        var def = _player.Definition;
        var built = WeaponVisual.Build(Current.Def, _player.Rig.HandColor);
        _visual = built.Root;
        _muzzles = built.Muzzles;
        _muzzleIndex = 0;
        // Weapons are authored along +Z; turn and scale them into the model's own space.
        _visual.Scale = Vector3.One / def.ModelScale;
        if (!def.ModelFacesPositiveZ) _visual.RotationDegrees = new Vector3(0, 180, 0);
        _player.Rig.Weapon.AddChild(_visual);
        _player.Rig.BindWeaponGrip(built);
        _flash.Position = _player.Rig.Weapon.ToLocal(MuzzlePosition);
    }

    // ---------- throw aiming ----------

    /// <summary>Put the gun away and hold the current throwable. False if there is nothing to throw.</summary>
    public bool BeginThrowAim()
    {
        if (AimingThrow || Loadout.CurrentThrowable == null) return false;
        CancelMeleeSwing();
        AimingThrow = true;
        Current.Holster();
        _player.Rig.ResetReloadPose();
        if (_visual != null) _visual.Visible = false;
        _preview = ThrowPreview.Create(Stage.Effects);
        ShowHeldThrowable();
        return true;
    }

    /// <summary>Back to the weapon you had before (after throwing, or on cancel).</summary>
    public void EndThrowAim()
    {
        if (!AimingThrow) return;
        AimingThrow = false;
        _heldThrowable?.QueueFree();
        _heldThrowable = null;
        _shownThrowable = null;
        _preview?.QueueFree();
        _preview = null;
        if (_visual != null) _visual.Visible = true;
    }

    public void CycleThrowable(int direction)
    {
        if (AimingThrow && Loadout.CycleThrowable(direction)) ShowHeldThrowable();
    }

    private void ShowHeldThrowable()
    {
        var def = Loadout.CurrentThrowable;
        if (def == null || def == _shownThrowable) return;
        _heldThrowable?.QueueFree();
        _heldThrowable = new Node3D { Name = "HeldThrowable", Position = new Vector3(0, .05f, .1f) / _player.Definition.ModelScale };
        ThrownItem.BuildLook(_heldThrowable, def);
        _player.Rig.Weapon.AddChild(_heldThrowable);
        _shownThrowable = def;
    }

    private Vector3 HandPosition()
    {
        var chest = _player.GlobalPosition + Vector3.Up * 1.6f;
        var hand = chest + _player.AimDirection * .4f;
        if (Rays.Cast(_player, chest, hand + _player.AimDirection * .2f, Layers.World) is { } wall)
            hand = chest + _player.AimDirection * Mathf.Max(0, chest.DistanceTo(wall.Position) - .25f);
        return hand;
    }

    public bool SelectSlot(int slot)
    {
        EndThrowAim();
        if (!Loadout.SelectSlot(slot)) return false;
        RefreshVisual();
        return true;
    }

    public void Tick(float dt, bool moving, bool pressed, bool held, bool blocked)
    {
        TickActions(dt,moving,pressed,held,blocked);
        // Sample AFTER auto reload, shell insertion and trigger cancellation. Never advance a second timer.
        _player.Rig.SetReloadPose(dt,blocked||AimingThrow||_player.IsDead||_player.IsStunned ? null : Current);
    }

    private void TickActions(float dt, bool moving, bool pressed, bool held, bool blocked)
    {
        Kick = Mathf.MoveToward(Kick, 0, dt * 7);
        _flashTime -= dt;
        _flash.LightEnergy = _flashTime > 0 ? 4 : 0;
        blocked |= _player.IsDead || _player.IsStunned;
        if (blocked || AimingThrow || (_swingState != null && _swingState != Current)) CancelMeleeSwing();
        else AdvanceMeleeSwing(dt);

        if (AimingThrow)
        {
            if (blocked || Loadout.CurrentThrowable == null)
            {
                EndThrowAim();
                return;
            }
            ShowHeldThrowable();
            _preview?.Show(Loadout.CurrentThrowable, HandPosition(), _player.AimPoint, _player.GlobalPosition, dt);
            if (pressed) Throw();
            return;
        }

        var state = Current;
        bool wasReloading = state.Reloading && _reloadWatch == state;
        state.Tick(dt, moving);
        if (wasReloading && !state.Reloading) _player.PlayOwn("reload_end");
        _reloadWatch = state;
        if (blocked)
        {
            state.CancelBurst();
            return;
        }
        if (!state.IsMelee && state.Mag == 0 && !state.Reloading && (pressed || held))
        {
            if (state.StartReload()) _player.PlayOwn("reload_start");
            else if (pressed) _player.PlayOwn("dry_fire");
            if (!state.Reloading && pressed && state.IsEmpty && Loadout.ActiveSlot == 2)
            {
                // Out of everything: fall back to slot 1 like L4D does.
                SelectSlot(1);
                Stage.ShowNotice(Loc.Format("fmt_weapon_no_ammo", state.Def.DisplayName), 1.5f);
            }
            return;
        }
        if (_swingState == null && state.Trigger(pressed, held)) Fire(state);
    }

    // ---------- firing ----------

    private void Fire(WeaponState state)
    {
        _player.Rig.ResetReloadPose();
        // A queued burst can fire after input is released; lower its muzzle before using its transform.
        _player.Rig.SetLocomotionPose(0,0,true);
        var def = state.Def;
        ShotsFired++;
        Kick = state.IsMelee ? 0 : def.Category == WeaponCategory.Heavy ? 1 : def.Category == WeaponCategory.Pistol ? .6f : 1;
        var dir = AimDirection();
        Stage.Sfx.PlayAt(def.ShotSound, MuzzlePosition);
        switch (def.Mode)
        {
            case FireMode.Melee:
                _swingState = state;
                _swingElapsed = 0;
                _swingHit = false;
                _player.Rig.SetMeleePose(0);
                return;
            case FireMode.Projectile:
                Rocket.Launch(Stage, MuzzlePosition, Spread(dir, state.Spread), def, _player.GetRid());
                break;
            case FireMode.Lob:
                var flat = _player.AimPoint - _player.GlobalPosition;
                flat.Y = 0;
                float dist = Mathf.Clamp(flat.Length(), def.MinRange, def.MaxRange);
                var target = _player.GlobalPosition + (flat.LengthSquared() > .01f ? flat.Normalized() : dir) * dist;
                MortarShell.Fire(Stage, MuzzlePosition, target, def);
                break;
            default:
                bool anyCrit = false;
                var origin = _player.GlobalPosition + Vector3.Up * Player.AimHeight;
                // Pellets split the shot's damage and push: all of them hitting = the full number.
                int pellets = Mathf.Max(1, def.Pellets);
                float cone = pellets > 1 ? def.PelletSpread : state.Spread;
                for (int i = 0; i < pellets; i++)
                {
                    var end = Hitscan(def, origin, Spread(dir, cone), def.Damage * _player.DamageMultiplier / pellets, def.HitPower / pellets, out bool crit);
                    anyCrit |= crit;
                    var from = MuzzlePosition;
                    if (origin.DistanceTo(end) < origin.DistanceTo(from)) from = origin;
                    Stage.Effects.Tracer(from, end, crit);
                }
                Stage.Effects.Shell(MuzzlePosition, _player.Rig.GlobalTransform.Basis.X);
                if (anyCrit) Stage.Effects.FloatingText(_player.AimPoint + Vector3.Up * .6f, "CRIT!", new Color("ffd65c"));
                break;
        }

        _flashTime = .045f;
        _muzzleIndex++;
        // Every gun pushes its shooter back; power comes from the data, scale from the player's option.
        // Squadmates take a third of the push, so a shotgun does not leave them behind the leader.
        if (def.RecoilPower > 0) _player.Shove(-dir * Knockback.Recoil(def.RecoilPower) * (_player.IsBot ? .3f : 1f));
        float shake = def.Category switch
        {
            WeaponCategory.Heavy => .3f,
            WeaponCategory.Sniper => .2f,
            WeaponCategory.Shotgun => .15f,
            _ => .06f,
        };
        if (!_player.IsBot) Stage.Cam.AddShake(shake);
        Stage.Director.MakeNoise(_player.GlobalPosition, def.Noise);
    }

    /// <summary>
    /// Aim assist: when the cursor has locked onto an enemy, shots go to its center rather than the
    /// floor point next to it. Without height in a quarter view, this is what makes aiming feel precise.
    /// </summary>
    private Vector3 AimDirection()
    {
        if (_player.FocusTarget is { IsDead: false } focus && IsInstanceValid(focus))
        {
            var toFocus = focus.GlobalPosition - _player.GlobalPosition;
            toFocus.Y = 0;
            if (toFocus.LengthSquared() > .04f) return toFocus.Normalized();
        }
        return _player.AimDirection;
    }

    private Vector3 Spread(Vector3 dir, float spreadDegrees)
    {
        float half = spreadDegrees * .5f;
        return dir.Rotated(Vector3.Up, Mathf.DegToRad(_rng.RandfRange(-half, half)));
    }

    /// <summary>
    /// One bullet (or pellet). Damage falls off past the effective range; it pierces up to
    /// <see cref="WeaponDefinition.Pierce"/> enemies, losing 20% each time; explosive rounds blow up where it stops.
    /// </summary>
    private Vector3 Hitscan(WeaponDefinition def, Vector3 origin, Vector3 dir, float damage, float power, out bool crit)
    {
        crit = false;
        var end = origin + dir * def.MaxRange;
        var exclude = new List<Rid> { _player.GetRid() };
        for (int pierced = 0; pierced < def.Pierce; pierced++)
        {
            if (Rays.Cast(_player, origin, origin + dir * def.MaxRange, Layers.BulletMask, exclude) is not { } hit) break;
            end = hit.Position;
            float falloff = def.FalloffAt(origin.DistanceTo(end));
            switch (hit.Collider)
            {
                case Enemy enemy when !enemy.IsDead:
                    bool isCrit = pierced == 0 && enemy == _player.FocusTarget && _player.CritReady;
                    crit |= isCrit;
                    enemy.TakeHit(new HitInfo(damage * falloff * (isCrit ? Player.CritMultiplier : 1f), dir, end, isCrit, power));
                    exclude.Add(hit.Rid);
                    damage *= .8f;
                    continue;
                case AlarmCar car:
                    // Squadmates are careful with it; only a person sets it off by shooting.
                    if (!_player.IsBot) car.Trigger();
                    Stage.Effects.Impact(end, -dir);
                    break;
                case ExplosiveBarrel barrel:
                    barrel.Ignite();
                    break;
                case RigidBody3D body:
                    body.ApplyImpulse(dir * Knockback.Hit(power) * .3f, end - body.GlobalPosition);
                    Stage.Effects.Impact(end, -dir);
                    break;
                default:
                    Stage.Effects.Impact(end, -dir);
                    break;
            }
            break;
        }
        if (def.Explodes) Blast.Explode(Stage, end, def.ExplosionRadius, def.ExplosionDamage, new Color("ff8a3c"), selfShare: def.ExplosionSelfShare, knockbackPower: def.ExplosionKnockback);
        return end;
    }

    public void CancelMeleeSwing()
    {
        if (_swingState == null) return;
        _swingState = null;
        _swingElapsed = 0;
        _swingHit = false;
        _player.Rig.SetMeleePose(-1);
    }

    private void AdvanceMeleeSwing(float dt)
    {
        if (_swingState == null) return;
        _swingElapsed += Mathf.Max(0, dt);
        _player.Rig.SetMeleePose(_swingElapsed);
        if (!_swingHit && _swingElapsed >= CharacterRig.MeleeImpactTime)
        {
            _swingHit = true;
            // Resolve targets at the visible impact, using the same facing as the animated rig.
            Swing(_swingState.Def, Flat(_player.Rig.Forward).Normalized());
        }
        if (_swingElapsed >= CharacterRig.MeleeDuration) CancelMeleeSwing();
    }

    /// <summary>Impact marker: everything inside the arc in front, nearest first, up to the target limit.</summary>
    private void Swing(WeaponDefinition def, Vector3 dir)
    {
        var origin = _player.GlobalPosition;
        var targets = Stage.Enemies
            .Where(e => !e.IsDead)
            .Select(e => (e, to: Flat(e.GlobalPosition - origin)))
            .Where(t => t.to.Length() < def.MeleeRange + t.e.Radius && Mathf.RadToDeg(dir.AngleTo(t.to.Normalized())) <= def.MeleeArc * .5f)
            .Where(t => Mathf.Abs(t.e.GlobalPosition.Y - origin.Y) < 1.5f &&
                Rays.Clear(_player, origin + Vector3.Up * Player.AimHeight,
                    t.e.GlobalPosition + Vector3.Up * Player.AimHeight, Layers.World | Layers.Props))
            .OrderBy(t => t.to.Length())
            .Take(def.MeleeMaxTargets)
            .ToList();
        foreach (var (enemy, to) in targets)
        {
            var push = to.LengthSquared() > .001f ? to.Normalized() : dir;
            bool crit = enemy == _player.FocusTarget && _player.CritReady;
            enemy.TakeHit(new HitInfo(def.Damage * _player.DamageMultiplier * (crit ? Player.CritMultiplier : 1f), push, enemy.GlobalPosition + Vector3.Up, crit, def.HitPower));
        }
        foreach (var prop in Stage.Props)
        {
            if (!IsInstanceValid(prop) || prop is ExplosiveBarrel) continue;
            var to = Flat(prop.GlobalPosition - origin);
            if (to.Length() < def.MeleeRange + .4f && dir.AngleTo(to.Normalized()) < Mathf.DegToRad(def.MeleeArc * .5f))
                prop.ApplyCentralImpulse(to.Normalized() * 3f + Vector3.Up * 1.5f);
        }
        Stage.Effects.Swoosh(origin + Vector3.Up * Player.AimHeight, dir, def.MeleeRange, def.MeleeArc);
        if (targets.Count > 0)
        {
            Stage.Effects.FloatingText(targets[0].e.GlobalPosition + Vector3.Up * 2.2f, "BONG!", new Color("ffe07a"));
            Stage.Sfx.PlayAt("pan_bong", targets[0].e.GlobalPosition + Vector3.Up);
            if (!_player.IsBot) Stage.Cam.AddShake(.12f);
        }
        Stage.Director.MakeNoise(origin, def.Noise);
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    // ---------- throwing ----------

    /// <summary>Throw the held item at the cursor (clamped to its max range), then go back to the previous weapon.</summary>
    public ThrownItem? Throw()
    {
        var def = Loadout.TakeThrowable();
        if (def == null)
        {
            EndThrowAim();
            return null;
        }
        var hand = HandPosition();
        Kick = .6f;
        Stage.Sfx.PlayAt("throw", hand);
        var item = ThrownItem.Throw(Stage, def, hand, _player.AimPoint);
        EndThrowAim();
        return item;
    }

    /// <summary>Picks up a weapon; returns the weapon that fell out of that slot, if any.</summary>
    public (PickupResult result, WeaponState? dropped) Pickup(WeaponDefinition def, WeaponState? state)
    {
        var outcome = Loadout.Pickup(def, state);
        RefreshVisual();
        return outcome;
    }
}
