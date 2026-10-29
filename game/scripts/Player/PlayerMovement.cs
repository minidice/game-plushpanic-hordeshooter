using Godot;

namespace HordeShooter;

/// <summary>C-1/C-35: quiet movement runs automatically; rolls consume sequentially recovering charges.</summary>
public partial class Player
{
    public const float WalkSpeed = 3.6f;
    public const float AutoRunSpeed = 4.2f;
    public const float AutoRunDelay = 2f;
    public const int MaxRollCharges = 2;
    public int RollCharges { get; private set; } = MaxRollCharges;
    public float RollRechargeLeft { get; private set; }
    public float AutoRunDelayLeft { get; private set; } = AutoRunDelay;
    public bool IsAutoRunning { get; private set; }
    private bool _rollRechargeActive;

    /// <summary>Actual attacks and accepted hits restart the quiet period, even during a roll.</summary>
    internal void InterruptAutoRun()
    {
        AutoRunDelayLeft = AutoRunDelay;
        IsAutoRunning = false;
    }

    private void TickAutoRun(float dt, bool busy)
    {
        if (_aimHeld || busy || Gun.Reloading || Weapons.AimingThrow || Weapons.AimingLob ||
            Weapons.Charging || Weapons.Planted || _reviveHeld)
            InterruptAutoRun();
        else
        {
            AutoRunDelayLeft = Mathf.Max(0, AutoRunDelayLeft - dt);
            IsAutoRunning = AutoRunDelayLeft <= 0;
        }
    }

    private void TickRollRecharge(float dt)
    {
        if (!_rollRechargeActive) return;
        RollRechargeLeft -= dt;
        while (RollRechargeLeft <= 0 && RollCharges < MaxRollCharges)
        {
            RollCharges++;
            if (RollCharges < MaxRollCharges) RollRechargeLeft += RollCooldown;
        }
        if (RollCharges == MaxRollCharges)
        {
            RollRechargeLeft = 0;
            _rollRechargeActive = false;
        }
    }

    private void SpendRollCharge()
    {
        RollCharges--;
        // An already running timer is never reset or paused by another roll.
        if (!_rollRechargeActive) RollRechargeLeft = RollCooldown;
        InterruptAutoRun();
    }

    /// <summary>Natural completion and forced cancellation share collision/timer cleanup.</summary>
    private void FinishRoll()
    {
        _rollTime = 0;
        if (RollCharges < MaxRollCharges && !_rollRechargeActive)
        {
            RollRechargeLeft = RollCooldown;
            _rollRechargeActive = true;
        }
        CollisionMask = Layers.MovementWorld | Layers.Enemy | Layers.Props;
        InterruptAutoRun();
    }
}
