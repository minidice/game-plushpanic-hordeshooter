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
    /// <summary>A lobbed weapon (mortar) is in hand: its landing ring follows the cursor and walking is slowed like a throw aim.</summary>
    public bool AimingLob => !AimingThrow && Current.Def.Mode == FireMode.Lob;
    /// <summary>Setting up or packing up a mortar: the survivor cannot walk (decisions.md C-25).</summary>
    public bool Planted => _plant != null || _recoverLeft > 0;
    /// <summary>A railgun shot is charging.</summary>
    public bool Charging => _charge != null;
    /// <summary>0–1 progress of the current mortar set-up or railgun charge.</summary>
    public float WindupProgress => _plant != null ? 1 - _plantLeft / Mathf.Max(.001f, _plant.Def.SetupTime)
        : _charge != null ? 1 - _chargeLeft / Mathf.Max(.001f, _charge.Def.ChargeTime) : 0;
    private WeaponState? _plant, _charge;
    private float _plantLeft, _recoverLeft, _chargeLeft;
    private MeshInstance3D? _chargeLine;
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
        _player.Died += OnDied;
        _player.Rig.HideBuiltInWeapon();
        _flash = new OmniLight3D { OmniRange = 5, LightEnergy = 0, LightColor = new Color("ffcc77") };
        _player.Rig.Weapon.AddChild(_flash);
        RefreshVisual();
    }

    public override void _ExitTree()
    {
        _player.Died -= OnDied;
        HideChargeLine();
    }

    private void OnDied()
    {
        CancelMeleeSwing();
        CancelWindup();
        EndThrowAim();
        _preview?.QueueFree();
        _preview = null;
    }

    public Vector3 MuzzlePosition => _muzzles.Count > 0 ? _muzzles[_muzzleIndex % _muzzles.Count].GlobalPosition : _player.Rig.Muzzle.GlobalPosition;

    /// <summary>
    /// Client: someone else fired on the host. Their tracers, flash and sound only; the host already decided the hits.
    /// </summary>
    public void ShowNetShot(WeaponDefinition def, Vector3 from, Vector3 aim, System.Collections.Generic.IReadOnlyList<Vector3> ends)
    {
        Stage.Sfx.PlayAt(def.ShotSound, from);
        Kick = def.Category == WeaponCategory.Heavy ? 1 : .6f;
        switch (def.Mode)
        {
            case FireMode.Melee:
                var flat = aim - _player.GlobalPosition;
                flat.Y = 0;
                if (flat.LengthSquared() > .01f)
                    Stage.Effects.Swoosh(_player.GlobalPosition + Vector3.Up * Player.AimHeight, flat.Normalized(), def.MeleeRange, def.MeleeArc);
                return;
            case FireMode.Projectile:
                if (ends.Count > 0) Rocket.Launch(Stage, from, (ends[0] - from).Normalized(), def, _player.GetRid());
                return;
            case FireMode.Lob:
                if (ends.Count > 0) MortarShell.Fire(Stage, from, ends[0], def);
                return;
        }
        if (def.ChargeTime > 0) foreach (var end in ends) Stage.Effects.Beam(from, end, RailColor);
        else foreach (var end in ends) Stage.Effects.Tracer(from, end, false);
        _flashTime = .045f;
        _muzzleIndex++;
    }

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
        CancelWindup();
        _preview?.QueueFree();
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
        CancelWindup();
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
        _recoverLeft = Mathf.Max(0, _recoverLeft - dt);
        if (blocked || AimingThrow) CancelWindup();
        else if (TickWindup(dt)) return;

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
        // Only your own landing ring: another player's mortar is theirs to aim.
        if (AimingLob && _player.IsLocal)
        {
            _preview ??= ThrowPreview.Create(Stage.Effects);
            _preview.ShowLob(state.Def, MuzzlePosition, LobTarget(state.Def), _player.GlobalPosition, dt);
        }
        else if (_preview != null)
        {
            _preview.QueueFree();
            _preview = null;
        }
        bool wasReloading = state.Reloading && _reloadWatch == state;
        state.Tick(dt, moving, _player.IsAiming);
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
        if (_swingState == null && !Planted && state.Trigger(pressed, held)) Fire(state);
    }

    // ---------- mortar set-up and railgun charge (decisions.md C-25) ----------

    private static readonly Color RailColor = new("6fd0ff");

    /// <summary>Runs a set-up or charge in progress; true while one is running (nothing else fires meanwhile).</summary>
    private bool TickWindup(float dt)
    {
        if (_plant != null)
        {
            if (_plant != Current) { CancelWindup(); return false; }
            _plantLeft -= dt;
            Kick = .25f;
            if (_plantLeft > 0) return true;
            var planted = _plant;
            _plant = null;
            Fire(planted, released: true);
            _recoverLeft = planted.Def.RecoverTime;
            return true;
        }
        if (_charge != null)
        {
            if (_charge != Current) { CancelWindup(); return false; }
            _chargeLeft -= dt;
            ShowChargeLine();
            if (_chargeLeft > 0) return true;
            var charged = _charge;
            _charge = null;
            HideChargeLine();
            Fire(charged, released: true);
            return true;
        }
        return false;
    }

    /// <summary>Stunned, switched or throwing before the shot left: no shot, the round comes back.</summary>
    public void CancelWindup()
    {
        _plant?.Refund();
        _charge?.Refund();
        _plant = _charge = null;
        HideChargeLine();
    }

    private void ShowChargeLine()
    {
        if (_charge == null) return;
        _chargeLine ??= Mat.Mesh(Stage.Effects, new BoxMesh { Size = Vector3.One }, MuzzlePosition, Mat.Fade(new Color(RailColor, .35f)));
        float range = _charge.Def.MaxRange * RangeScale(_charge.Def);
        var from = MuzzlePosition;
        var to = from + AimDirection() * range;
        _chargeLine.GlobalPosition = (from + to) * .5f;
        _chargeLine.LookAt(to, Vector3.Up);
        float t = WindupProgress;
        _chargeLine.Scale = new Vector3(.03f + .06f * t, .03f + .06f * t, range);
    }

    private void HideChargeLine()
    {
        _chargeLine?.QueueFree();
        _chargeLine = null;
    }

    /// <summary>Aiming stretches the range of the gun in hand (aim_range_mult).</summary>
    private float RangeScale(WeaponDefinition def) => _player.IsAiming ? def.AimRangeMultiplier : 1f;

    /// <summary>Where a lobbed shell would land now: the cursor, kept between the weapon's min and max range.</summary>
    public Vector3 LobTarget(WeaponDefinition def)
    {
        var flat = _player.AimPoint - _player.GlobalPosition;
        flat.Y = 0;
        float dist = Mathf.Clamp(flat.Length(), def.MinRange, def.MaxRange);
        var dir = flat.LengthSquared() > .01f ? flat.Normalized() : _player.AimDirection;
        var target = _player.GlobalPosition + dir * dist;
        return new Vector3(target.X, 0, target.Z);
    }

    // ---------- firing ----------

    private void Fire(WeaponState state, bool released = false)
    {
        // The mortar sets up first and the railgun charges first; the round is already taken (refunded on cancel).
        if (!released && state.Def.Mode == FireMode.Lob && state.Def.SetupTime > 0)
        {
            _plant = state;
            _plantLeft = state.Def.SetupTime;
            _player.PlayOwn("reload_start");
            return;
        }
        if (!released && state.Def.ChargeTime > 0)
        {
            _charge = state;
            _chargeLeft = state.Def.ChargeTime;
            _player.PlayOwn("balloon_inflate");
            return;
        }
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
                Stage.Net?.OnShot(_player, def, MuzzlePosition, System.Array.Empty<Vector3>(), _player.AimPoint);
                return;
            case FireMode.Projectile:
                var rocketDir = Spread(dir, state.Spread);
                Rocket.Launch(Stage, MuzzlePosition, rocketDir, def, _player.GetRid());
                Stage.Net?.OnShot(_player, def, MuzzlePosition, new[] { MuzzlePosition + rocketDir }, _player.AimPoint);
                break;
            case FireMode.Lob:
                var target = LobTarget(def);
                MortarShell.Fire(Stage, MuzzlePosition, target, def);
                Stage.Net?.OnShot(_player, def, MuzzlePosition, new[] { target }, _player.AimPoint);
                break;
            default:
                bool anyCrit = false;
                var origin = _player.GlobalPosition + Vector3.Up * Player.AimHeight;
                // Pellets split the shot's damage and push: all of them hitting = the full number.
                int pellets = Mathf.Max(1, def.Pellets);
                float cone = pellets > 1 ? def.PelletSpread : state.Spread;
                var ends = Stage.Net != null ? new System.Collections.Generic.List<Vector3>(pellets) : null;
                float rangeScale = RangeScale(def);
                for (int i = 0; i < pellets; i++)
                {
                    var end = Hitscan(def, origin, Spread(dir, cone), def.Damage * _player.DamageMultiplier / pellets, def.HitPower / pellets, rangeScale, out bool crit);
                    anyCrit |= crit;
                    var from = MuzzlePosition;
                    if (origin.DistanceTo(end) < origin.DistanceTo(from)) from = origin;
                    if (def.ChargeTime > 0) Stage.Effects.Beam(from, end, RailColor);
                    else Stage.Effects.Tracer(from, end, crit);
                    ends?.Add(end);
                }
                if (ends != null) Stage.Net!.OnShot(_player, def, MuzzlePosition, ends, _player.AimPoint);
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
        if (_player.IsLocal) Stage.Cam.AddShake(shake);
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
    /// One bullet (or pellet). Damage falls off past the effective range (both stretched by aiming, <paramref name="rangeScale"/>);
    /// it pierces up to <see cref="WeaponDefinition.Pierce"/> enemies, losing 20% each time; explosive rounds blow up where it stops.
    /// </summary>
    private Vector3 Hitscan(WeaponDefinition def, Vector3 origin, Vector3 dir, float damage, float power, float rangeScale, out bool crit)
    {
        crit = false;
        float range = def.MaxRange * rangeScale;
        var end = origin + dir * range;
        var exclude = new List<Rid> { _player.GetRid() };
        for (int pierced = 0; pierced < def.Pierce; pierced++)
        {
            if (Rays.Cast(_player, origin, origin + dir * range, Layers.BulletMask, exclude) is not { } hit) break;
            end = hit.Position;
            float falloff = def.FalloffAt(origin.DistanceTo(end), rangeScale);
            switch (hit.Collider)
            {
                case Enemy enemy when !enemy.IsDead:
                    bool isCrit = pierced == 0 && enemy == _player.FocusTarget && _player.CritReady;
                    crit |= isCrit;
                    enemy.TakeHit(new HitInfo(damage * falloff * (isCrit ? def.CritMultiplier : 1f), dir, end, isCrit, power));
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
            enemy.TakeHit(new HitInfo(def.Damage * _player.DamageMultiplier * (crit ? def.CritMultiplier : 1f), push, enemy.GlobalPosition + Vector3.Up, crit, def.HitPower));
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
            if (_player.IsLocal) Stage.Cam.AddShake(.12f);
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
        var item = ThrownItem.Throw(Stage, def, hand, _player.AimPoint, _player);
        Stage.Net?.OnThrow(_player, def, hand, _player.AimPoint);
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
