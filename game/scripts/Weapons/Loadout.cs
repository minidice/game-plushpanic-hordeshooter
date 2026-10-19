using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public enum PickupResult
{
    /// <summary>Slot was empty (or melee/pistol swap with nothing to drop).</summary>
    Equipped,
    /// <summary>Same weapon again turned it into its upgrade (pistol → dual pistols).</summary>
    Upgraded,
    /// <summary>Replaced the weapon in that slot; the old one is returned to drop on the floor.</summary>
    Swapped,
    /// <summary>Same primary again: its ammo was added to the reserve (any remainder is returned to stay on the floor).</summary>
    Refilled,
    /// <summary>Nothing to gain (dual pistols already, throwable stack full).</summary>
    Full,
}

/// <summary>
/// What the player carries, L4D style:
///   1 = secondary (melee → pistol → dual pistols, unlimited ammo),
///   2 = primary (limited ammo),
///   G = throwables (several kinds; G throws the most recently picked kind),
///   Q = medkit (lives on <see cref="Player"/>).
/// Pure logic so it can be tested without a scene.
/// </summary>
public sealed class Loadout
{
    public WeaponState Secondary { get; private set; }
    public WeaponState? Primary { get; private set; }
    public int ActiveSlot { get; private set; } = 1;
    public WeaponState Current => ActiveSlot == 2 && Primary != null ? Primary : Secondary;

    private readonly Dictionary<string, int> _throwCounts = new();
    private readonly List<ThrowableDefinition> _throwOrder = new();

    public Loadout(WeaponDefinition startSecondary) => Secondary = new WeaponState(startSecondary);

    public bool SelectSlot(int slot)
    {
        if (slot == ActiveSlot) return false;
        if (slot == 2 && Primary == null) return false;
        if (slot != 1 && slot != 2) return false;
        Current.Holster();
        ActiveSlot = slot;
        return true;
    }

    public (PickupResult result, WeaponState? dropped) Pickup(WeaponDefinition def, WeaponState? state = null)
    {
        if (def.Slot == WeaponSlot.Secondary)
        {
            // Same model again: becomes the dual version if its data has the dual_wield trait (M1911 → 쌍 M1911).
            bool same = Secondary.Def.BaseId == def.BaseId;
            if (same && !Secondary.Def.Dual && !def.Dual && def.DualVariant is { } dual)
            {
                Secondary = new WeaponState(dual);
                Primary?.Holster();
                ActiveSlot = 1;
                return (PickupResult.Upgraded, null);
            }
            if (same) return (PickupResult.Full, null);
            var droppedSecondary = Secondary;
            droppedSecondary.Holster();
            state?.Holster();
            Secondary = state ?? new WeaponState(def);
            Primary?.Holster();
            ActiveSlot = 1;
            return (PickupResult.Swapped, droppedSecondary);
        }

        if (Primary != null && Primary.Def.Id == def.Id)
        {
            // Take what fits; whatever is left stays on the floor as a lighter copy of the gun.
            int carried = state?.TotalAmmo ?? def.ReserveStart + def.MagSize;
            int added = Primary.AddReserve(carried);
            if (added <= 0) return (PickupResult.Full, null);
            int left = carried - added;
            if (left <= 0) return (PickupResult.Refilled, null);
            int mag = System.Math.Min(left, def.MagSize);
            return (PickupResult.Refilled, new WeaponState(def, mag, left - mag));
        }
        var dropped = Primary;
        dropped?.Holster();
        state?.Holster();
        Primary = state ?? new WeaponState(def);
        Secondary.Holster();
        ActiveSlot = 2;
        return (dropped == null ? PickupResult.Equipped : PickupResult.Swapped, dropped);
    }

    // ---------- throwables ----------

    public ThrowableDefinition? CurrentThrowable => _throwOrder.LastOrDefault(t => Count(t) > 0);

    public int Count(ThrowableDefinition def) => _throwCounts.GetValueOrDefault(def.Id);

    public IEnumerable<(ThrowableDefinition def, int count)> Throwables =>
        _throwOrder.AsEnumerable().Reverse().Where(t => Count(t) > 0).Select(t => (t, Count(t)));

    /// <summary>Adds one and makes this kind the one G throws.</summary>
    public bool AddThrowable(ThrowableDefinition def)
    {
        int count = Count(def);
        if (count >= def.MaxCarry) return false;
        _throwCounts[def.Id] = count + 1;
        _throwOrder.RemoveAll(t => t.Id == def.Id);
        _throwOrder.Add(def);
        return true;
    }

    /// <summary>Wheel while aiming: make the next (or previous) kind you carry the one G throws.</summary>
    public bool CycleThrowable(int direction)
    {
        var carried = _throwOrder.Where(t => Count(t) > 0).ToList();
        if (carried.Count < 2) return false;
        int index = carried.IndexOf(CurrentThrowable!);
        var next = carried[((index + direction) % carried.Count + carried.Count) % carried.Count];
        _throwOrder.Remove(next);
        _throwOrder.Add(next);
        return true;
    }

    public ThrowableDefinition? TakeThrowable()
    {
        var current = CurrentThrowable;
        if (current == null) return null;
        _throwCounts[current.Id]--;
        return current;
    }
}
