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
///   3 = special (heavy finishers: rocket launcher, mortar, railgun; few rounds, never from ammo piles),
///   G = throwables (several kinds; G throws the most recently picked kind),
///   Q = medkit (lives on <see cref="Player"/>).
/// Pure logic so it can be tested without a scene.
/// </summary>
public sealed class Loadout
{
    public WeaponState Secondary { get; private set; }
    public WeaponState? Primary { get; private set; }
    public WeaponState? Special { get; private set; }
    public int ActiveSlot { get; private set; } = 1;
    public WeaponState Current => ActiveSlot switch
    {
        2 when Primary != null => Primary,
        3 when Special != null => Special,
        _ => Secondary,
    };

    /// <summary>What is in slot 1, 2 or 3 (null when empty).</summary>
    public WeaponState? InSlot(int slot) => slot switch { 1 => Secondary, 2 => Primary, 3 => Special, _ => null };

    private readonly Dictionary<string, int> _throwCounts = new();
    private readonly List<ThrowableDefinition> _throwOrder = new();

    public Loadout(WeaponDefinition startSecondary) => Secondary = new WeaponState(startSecondary);

    private bool _unlimited;
    private float _specialCooldown;

    /// <summary>
    /// Onslaught (decisions.md M-26): every weapon carried keeps unlimited reserves, and slot-3 kinds recharge for
    /// <paramref name="specialCooldown"/> seconds after a shot. Applied to what is carried now and to everything picked up later.
    /// </summary>
    public void UseOnslaughtAmmo(float specialCooldown)
    {
        _unlimited = true;
        _specialCooldown = specialCooldown;
        foreach (var state in new[] { Secondary, Primary, Special }) AdoptMaybe(state);
    }

    private WeaponState Adopt(WeaponState state)
    {
        AdoptMaybe(state);
        return state;
    }

    private WeaponState? AdoptMaybe(WeaponState? state)
    {
        if (state == null || !_unlimited) return state;
        state.UnlimitedReserve = true;
        state.ShotCooldown = state.Def.Slot == WeaponSlot.Special ? _specialCooldown : 0;
        return state;
    }

    /// <summary>Recharging weapons that are put away keep counting down (the one in hand ticks itself).</summary>
    public void TickIdle(float dt)
    {
        foreach (var state in new[] { Secondary, Primary, Special })
            if (state != null && state != Current) state.TickCooldown(dt);
    }

    /// <summary>
    /// Onslaught's racks: put <paramref name="def"/> in slot 1 or 2 whatever kind it is (a rocket launcher can be slot 1),
    /// replacing what was there, and take it in hand.
    /// </summary>
    public void SetSlot(int slot, WeaponDefinition def)
    {
        Current.Holster();
        var state = Adopt(new WeaponState(def));
        if (slot == 1) Secondary = state;
        else Primary = state;
        ActiveSlot = slot == 1 ? 1 : 2;
    }

    /// <summary>
    /// Client, own player: take the host's weapons, ammo and throwables. Returns true when a weapon changed
    /// (the caller rebuilds the model in hand). The slot follows the host only when a pickup swapped a weapon.
    /// </summary>
    internal bool ApplyNet(WeaponDefinition secondary, int secMag, int secReserve, WeaponDefinition? primary, int priMag, int priReserve,
        WeaponDefinition? special, int speMag, int speReserve, int slot, IReadOnlyList<(ThrowableDefinition def, int count)> throwables)
    {
        bool changed = false;
        if (Secondary.Def.Id != secondary.Id) { Secondary = Adopt(new WeaponState(secondary, secMag, secReserve)); changed = true; }
        else Secondary.SetAmmo(secMag, secReserve);
        Primary = AdoptMaybe(ApplySlot(Primary, primary, priMag, priReserve, ref changed));
        Special = AdoptMaybe(ApplySlot(Special, special, speMag, speReserve, ref changed));
        if (changed) ActiveSlot = InSlot(slot) != null ? slot : 1;
        if (InSlot(ActiveSlot) == null) ActiveSlot = 1;
        // Same kinds, counts and order (G throws the first): a cycle the host refused must not stick.
        var mine = Throwables.Select(t => (t.def.Id, t.count)).ToList();
        if (!mine.SequenceEqual(throwables.Select(t => (t.def.Id, t.count))))
        {
            _throwCounts.Clear();
            _throwOrder.Clear();
            foreach (var (def, count) in throwables.Reverse())
            {
                _throwCounts[def.Id] = count;
                _throwOrder.Add(def);
            }
        }
        return changed;
    }

    private static WeaponState? ApplySlot(WeaponState? mine, WeaponDefinition? host, int mag, int reserve, ref bool changed)
    {
        if (host == null)
        {
            if (mine != null) changed = true;
            return null;
        }
        if (mine == null || mine.Def.Id != host.Id) { changed = true; return new WeaponState(host, mag, reserve); }
        mine.SetAmmo(mag, reserve);
        return mine;
    }

    public bool SelectSlot(int slot)
    {
        if (slot == ActiveSlot) return false;
        if (InSlot(slot) == null) return false;
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
                Secondary = Adopt(new WeaponState(dual));
                Primary?.Holster();
            Special?.Holster();
                ActiveSlot = 1;
                return (PickupResult.Upgraded, null);
            }
            if (same) return (PickupResult.Full, null);
            var droppedSecondary = Secondary;
            droppedSecondary.Holster();
            state?.Holster();
            Secondary = Adopt(state ?? new WeaponState(def));
            Primary?.Holster();
            Special?.Holster();
            ActiveSlot = 1;
            return (PickupResult.Swapped, droppedSecondary);
        }

        bool special = def.Slot == WeaponSlot.Special;
        var held = special ? Special : Primary;
        if (held != null && held.Def.Id == def.Id)
        {
            // Take what fits; whatever is left stays on the floor as a lighter copy of the gun.
            int carried = state?.TotalAmmo ?? def.ReserveStart + def.MagSize;
            int added = held.AddReserve(carried);
            if (added <= 0) return (PickupResult.Full, null);
            int left = carried - added;
            if (left <= 0) return (PickupResult.Refilled, null);
            int mag = System.Math.Min(left, def.MagSize);
            return (PickupResult.Refilled, new WeaponState(def, mag, left - mag));
        }
        held?.Holster();
        state?.Holster();
        Current.Holster();
        var taken = Adopt(state ?? new WeaponState(def));
        if (special) Special = taken;
        else Primary = taken;
        ActiveSlot = (int)def.Slot;
        return (held == null ? PickupResult.Equipped : PickupResult.Swapped, held);
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
