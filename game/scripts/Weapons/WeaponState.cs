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

    public bool IsMelee => Def.Mode == FireMode.Melee;
    public bool CanFire => !Reloading && _cooldown <= 0 && (IsMelee || Mag > 0);
    public float ReloadTime => _insertedShell && Def.ShellReloadTime > 0 ? Def.ShellReloadTime : Def.ReloadTime;
    public float ReloadFraction => Reloading ? ReloadElapsed / Math.Max(.001f, ReloadTime) : 0;
    public bool IsEmpty => !IsMelee && Mag == 0 && (Def.InfiniteReserve || Reserve == 0);
    public int TotalAmmo => Mag + Reserve;

    public void Tick(float dt, bool moving)
    {
        _cooldown = Math.Max(0, _cooldown - dt);
        _burstTimer = Math.Max(0, _burstTimer - dt);
        _bloom = Math.Max(0, _bloom - Def.SpreadRecovery * dt);
        Spread = Math.Min(Def.MaxSpread, Def.BaseSpread + _bloom + (moving ? Def.MoveSpread : 0));
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
            if (!Def.InfiniteReserve) Reserve--;
            _insertedShell = true;
            if (Mag >= Def.MagSize || (!Def.InfiniteReserve && Reserve == 0)) Reloading = false;
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
        _cooldown = Def.FireInterval;
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
        _bloom = Math.Min(Def.MaxSpread, _bloom + Def.SpreadPerShot);
    }

    public bool StartReload()
    {
        if (IsMelee || Reloading || Mag >= Def.MagSize) return false;
        if (!Def.InfiniteReserve && Reserve <= 0) return false;
        Reloading = true;
        ReloadElapsed = 0;
        _insertedShell = false;
        _burstLeft = 0;
        return true;
    }

    public void CancelReload() => Reloading = false;

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
        int take = Def.InfiniteReserve ? need : Math.Min(need, Reserve);
        Mag += take;
        if (!Def.InfiniteReserve) Reserve -= take;
        Reloading = false;
    }
}
