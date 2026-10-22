using System;

namespace HordeShooter;

/// <summary>
/// Runtime state of one carried weapon (no nodes): magazine, reserve, bloom, bursts and reloading.
/// A dropped weapon keeps its state, so picking it back up returns the same ammo.
/// </summary>
public sealed class WeaponState
{
    public WeaponDefinition Def { get; }
    public int Mag { get; private set; }
    public int Reserve { get; private set; }
    public bool Reloading { get; private set; }
    /// <summary>Current full cone angle in degrees.</summary>
    public float Spread { get; private set; }
    public float ReloadElapsed { get; private set; }

    /// <summary>Onslaught: reserves never run out, whatever the weapon (decisions.md M-26).</summary>
    public bool UnlimitedReserve { get; set; }
    /// <summary>
    /// Onslaught's slot-3 kinds: after each shot the weapon recharges for this many seconds instead of reloading,
    /// also while put away (<see cref="TickCooldown"/>). 0 = normal magazines.
    /// </summary>
    public float ShotCooldown { get; set; }
    /// <summary>Seconds until a recharging weapon is ready again.</summary>
    public float CooldownLeft { get; private set; }
    /// <summary>Shots come this many times faster (the holder's cards; 1 = as the weapon says).</summary>
    public float RateScale { get; set; } = 1;

    /// <summary>Takes <paramref name="seconds"/> off a slot-3 recharge (honk charge); a finished one refills the magazine.</summary>
    public void ShortenCooldown(float seconds)
    {
        if (CooldownLeft <= 0 || seconds <= 0) return;
        TickCooldown(0);
        CooldownLeft = Math.Max(0, CooldownLeft - seconds);
        if (CooldownLeft <= 0) Mag = Def.MagSize;
    }
    public bool HasInfiniteReserve => Def.InfiniteReserve || UnlimitedReserve;

    private float _cooldown, _bloom, _burstTimer;
    private int _burstLeft;
    private bool _insertedShell;

    public WeaponState(WeaponDefinition def, int? mag = null, int? reserve = null)
    {
        Def = def;
        Mag = mag ?? def.MagSize;
        Reserve = def.InfiniteReserve ? 0 : reserve ?? def.ReserveStart;
        Spread = def.BaseSpread;
    }

    /// <summary>Client: the host's ammo count wins (multiplayer-sync-design.md 3장).</summary>
    internal void SetAmmo(int mag, int reserve)
    {
        Mag = Math.Clamp(mag, 0, Def.MagSize);
        if (!Def.InfiniteReserve) Reserve = Math.Max(0, reserve);
    }

    public bool IsMelee => Def.Mode == FireMode.Melee;
    public bool CanFire => !Reloading && _cooldown <= 0 && CooldownLeft <= 0 && (IsMelee || Mag > 0);
    public float ReloadTime => _insertedShell && Def.ShellReloadTime > 0 ? Def.ShellReloadTime : Def.ReloadTime;
    public float ReloadFraction => Reloading ? ReloadElapsed / Math.Max(.001f, ReloadTime) : 0;
    public bool IsEmpty => !IsMelee && Mag == 0 && CooldownLeft <= 0 && ShotCooldown <= 0 && (HasInfiniteReserve || Reserve == 0);
    public int TotalAmmo => Mag + Reserve;

    /// <summary>Share of the normal cone left while aiming (decisions.md C-26); moving adds nothing then.</summary>
    public const float AimSpreadShare = .35f;

    /// <summary>Recharging and the time between shots run in hand and put away alike.</summary>
    public void TickCooldown(float dt)
    {
        _cooldown = Math.Max(0, _cooldown - dt);
        if (CooldownLeft <= 0) return;
        CooldownLeft = Math.Max(0, CooldownLeft - dt);
        if (CooldownLeft <= 0) Mag = Def.MagSize;
    }

    public void Tick(float dt, bool moving, bool aiming = false)
    {
        TickCooldown(dt);
        _burstTimer = Math.Max(0, _burstTimer - dt);
        _bloom = Math.Max(0, _bloom - Def.SpreadRecovery * dt);
        Spread = aiming
            ? Math.Min(Def.MaxSpread, (Def.BaseSpread + _bloom) * AimSpreadShare)
            : Math.Min(Def.MaxSpread, Def.BaseSpread + _bloom + (moving ? Def.MoveSpread : 0));
        if (!Reloading) return;
        ReloadElapsed += dt;
        if (Def.ShellReloadTime <= 0)
        {
            if (ReloadElapsed >= ReloadTime) FinishReload();
            return;
        }
        while (Reloading && ReloadElapsed >= ReloadTime)
        {
            ReloadElapsed -= ReloadTime;
            Mag++;
            if (!HasInfiniteReserve) Reserve--;
            _insertedShell = true;
            if (Mag >= Def.MagSize || (!HasInfiniteReserve && Reserve == 0)) Reloading = false;
        }
    }

    /// <summary>
    /// Call every frame with the trigger state. Returns true when one shot (or swing) should happen now.
    /// Automatic weapons fire while held, others once per press; bursts finish on their own.
    /// </summary>
    public bool Trigger(bool pressedThisFrame, bool held)
    {
        if (_burstLeft > 0)
        {
            if (Reloading || Mag <= 0)
            {
                _burstLeft = 0;
                return false;
            }
            if (_burstTimer > 0) return false;
            _burstLeft--;
            _burstTimer = Def.BurstInterval;
            Consume();
            return true;
        }
        bool wants = Def.Automatic ? held : pressedThisFrame;
        // Tube-fed shotguns may immediately use shells already inserted, without losing them.
        if (wants && Reloading && Def.ShellReloadTime > 0 && Mag > 0) CancelReload();
        if (!wants || !CanFire) return false;
        _cooldown = Def.FireInterval / Math.Max(.1f, RateScale);
        Consume();
        if (Def.BurstCount > 1)
        {
            _burstLeft = Def.BurstCount - 1;
            _burstTimer = Def.BurstInterval;
        }
        return true;
    }

    private void Consume()
    {
        if (IsMelee) return;
        Mag--;
        if (ShotCooldown > 0 && Mag <= 0) CooldownLeft = ShotCooldown;
        _bloom = Math.Min(Def.MaxSpread, _bloom + Def.SpreadPerShot);
    }

    public bool StartReload()
    {
        if (IsMelee || Reloading || Mag >= Def.MagSize || ShotCooldown > 0) return false;
        if (!HasInfiniteReserve && Reserve <= 0) return false;
        Reloading = true;
        ReloadElapsed = 0;
        _insertedShell = false;
        _burstLeft = 0;
        return true;
    }

    public void CancelReload() => Reloading = false;

    /// <summary>A shot that was taken but never left (mortar set-up or railgun charge cut short): the round comes back.</summary>
    internal void Refund()
    {
        if (!IsMelee) Mag = Math.Min(Def.MagSize, Mag + 1);
        CooldownLeft = 0;
    }

    /// <summary>Drop any unfinished burst (rolling, healing).</summary>
    public void CancelBurst() => _burstLeft = 0;

    /// <summary>Put away or drop: nothing left half-done that could resume on its own later.</summary>
    public void Holster()
    {
        CancelBurst();
        CancelReload();
    }

    public int AddReserve(int amount)
    {
        if (Def.InfiniteReserve) return 0;
        int before = Reserve;
        Reserve = Math.Min(Def.ReserveMax, Reserve + amount);
        return Reserve - before;
    }

    /// <summary>Ammo pile. Heavy weapons are not refillable.</summary>
    public int Refill() => Def.Refillable ? AddReserve(Def.ReserveMax) : 0;

    private void FinishReload()
    {
        int need = Def.MagSize - Mag;
        int take = HasInfiniteReserve ? need : Math.Min(need, Reserve);
        Mag += take;
        if (!HasInfiniteReserve) Reserve -= take;
        Reloading = false;
    }
}
