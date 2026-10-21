using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Outpost inventory actions (decisions.md M-25). Every change is "drag from one place to another": a backpack cell
/// (0–19), an equipment slot (1, 2, 3, G, Q) or the floor. The deciding PC applies them (<see cref="Drag"/>); a client
/// asks the host with the same two numbers and sees the result in the next snapshot.
/// </summary>
public static class InventoryOps
{
    public const int Slot1 = 20, Slot2 = 21, Slot3 = 22, SlotThrow = 23, SlotMedkit = 24, Floor = -1;
    public static readonly int[] Slots = { Slot1, Slot2, Slot3, SlotThrow, SlotMedkit };

    public static bool IsCell(int place) => place is >= 0 and < Backpack.Size;
    public static bool IsSlot(int place) => place is >= Slot1 and <= SlotMedkit;
    /// <summary>Weapon slot number (1–3) of a place, 0 for the others.</summary>
    public static int WeaponSlot(int place) => place is >= Slot1 and <= Slot3 ? place - Slot1 + 1 : 0;

    /// <summary>One drag. Returns true when something changed.</summary>
    public static bool Drag(Player who, int from, int to)
    {
        if (who.Backpack is not { } bag || from == to || who.IsDead) return false;
        if (to == Floor) return Drop(who, from);
        if (IsCell(from) && IsCell(to))
        {
            if (bag[from] == null) return false;
            bag.Move(from, to);
            return true;
        }
        if (IsCell(from) && IsSlot(to)) return Equip(who, from, to);
        if (IsSlot(from) && IsCell(to)) return Unequip(who, from, to);
        return false;
    }

    /// <summary>A backpack cell onto an equipment slot: a weapon goes to its own slot (and the old one comes back here).</summary>
    private static bool Equip(Player who, int cell, int slot)
    {
        var bag = who.Backpack!;
        if (bag[cell] is not { } item) return false;
        switch (item.Kind)
        {
            case ItemKind.Weapon when item.State is { } state && WeaponSlot(slot) != 0:
                int target = (int)state.Def.Slot;
                var old = who.Loadout.Equip(target, state);
                bag.Set(cell, old != null ? InvItem.Weapon(old) : null);
                who.Weapons.RefreshVisual();
                return true;
            case ItemKind.Throwable when slot == SlotThrow && ArmoryCatalog.Throwables.TryGetValue(item.Id, out var def):
                int added = 0;
                while (added < item.Count && who.Loadout.AddThrowable(def)) added++;
                item.Count -= added;
                bag.Set(cell, item);
                return added > 0;
            case ItemKind.Medkit when slot == SlotMedkit && who.Medkits == 0:
                who.Medkits = 1;
                item.Count--;
                bag.Set(cell, item);
                return true;
        }
        return false;
    }

    /// <summary>An equipment slot into a backpack cell (an empty one, or a weapon of the same slot to swap with).</summary>
    private static bool Unequip(Player who, int slot, int cell)
    {
        var bag = who.Backpack!;
        int weaponSlot = WeaponSlot(slot);
        if (weaponSlot != 0)
        {
            if (bag[cell] is { Kind: ItemKind.Weapon, State: { } other } && (int)other.Def.Slot == weaponSlot) return Equip(who, cell, slot);
            if (bag[cell] != null || weaponSlot == 1 || who.Loadout.Unequip(weaponSlot) is not { } state) return false;
            bag.Set(cell, InvItem.Weapon(state));
            who.Weapons.RefreshVisual();
            return true;
        }
        if (slot == SlotThrow && who.Loadout.CurrentThrowable is { } def)
        {
            int count = who.Loadout.Count(def);
            int left = bag.Add(InvItem.Throwable(def.Id, count));
            who.Loadout.RemoveThrowable(def, count - left);
            return left < count;
        }
        if (slot == SlotMedkit && who.Medkits > 0 && bag.Add(InvItem.Medkit()) == 0)
        {
            who.Medkits = 0;
            return true;
        }
        return false;
    }

    /// <summary>Anything onto the floor in front of the survivor: weapons as weapon pickups, the rest as loot.</summary>
    private static bool Drop(Player who, int from)
    {
        var bag = who.Backpack!;
        var stage = who.Stage;
        var spot = who.GlobalPosition + who.AimDirection * 1.1f;
        if (stage.Flow.IsBlocked(spot)) spot = who.GlobalPosition;
        if (IsCell(from))
        {
            if (bag[from] is not { } item) return false;
            bag.Set(from, null);
            Spawn(stage, spot, item);
            return true;
        }
        int weaponSlot = WeaponSlot(from);
        if (weaponSlot != 0)
        {
            if (who.Loadout.Unequip(weaponSlot) is not { } state) return false;
            who.Weapons.RefreshVisual();
            WeaponPickup.ForWeapon(stage, spot, state.Def, state);
            return true;
        }
        if (from == SlotThrow && who.Loadout.CurrentThrowable is { } def)
        {
            int count = who.Loadout.RemoveThrowable(def, who.Loadout.Count(def));
            LootPickup.Spawn(stage, spot, InvItem.Throwable(def.Id, count));
            return true;
        }
        if (from == SlotMedkit && who.Medkits > 0)
        {
            who.Medkits = 0;
            LootPickup.Spawn(stage, spot, InvItem.Medkit());
            return true;
        }
        return false;
    }

    /// <summary>Puts <paramref name="item"/> on the floor at <paramref name="at"/>.</summary>
    public static void Spawn(Stage stage, Vector3 at, InvItem item)
    {
        if (item.Kind == ItemKind.Weapon && item.State is { } state) WeaponPickup.ForWeapon(stage, at, state.Def, state);
        else LootPickup.Spawn(stage, at, item);
    }

    /// <summary>
    /// Outpost: a weapon from the floor. Its loose reserve becomes ammo in the backpack; it goes straight to its slot
    /// when that is empty, else into the backpack. Pistols and melee (slot 1) keep the usual swap. False = not handled.
    /// </summary>
    public static bool TakeWeapon(Player who, WeaponPickup pickup)
    {
        if (who.Backpack is not { } bag || pickup.Weapon is not { } def || def.Slot == HordeShooter.WeaponSlot.Secondary) return false;
        var state = pickup.State ?? new WeaponState(def);
        if (bag.FreeCells == 0 && who.Loadout.InSlot((int)def.Slot) != null)
        {
            if (who.IsLocal) who.Stage.ShowNotice(Loc.Text("msg_backpack_full"), 2);
            return true;
        }
        if (state.Reserve > 0 && def.Caliber != "")
        {
            int left = bag.Add(InvItem.Ammo(def.Caliber, state.Reserve));
            state.SetAmmo(state.Mag, left);
        }
        if (who.Loadout.InSlot((int)def.Slot) == null)
        {
            who.Loadout.Equip((int)def.Slot, state);
            who.Weapons.RefreshVisual();
        }
        else bag.Add(InvItem.Weapon(state));
        who.Stage.RemovePickupFromWorld(pickup);
        who.Stage.Effects.FloatingText(who.GlobalPosition + Vector3.Up * 2.6f, def.DisplayName, new Color("ffb347"));
        if (who.IsLocal) who.Stage.Sfx.Play("pickup");
        return true;
    }

    /// <summary>Outpost: reloads take rounds from the backpack (<see cref="WeaponController"/> calls this around a tick).</summary>
    public static int CarriedRounds(Player who, WeaponState state) =>
        who.Backpack is { } bag && state.Def.Caliber != "" && !state.HasInfiniteReserve && !state.IsMelee ? bag.Count(state.Def.Caliber) : -1;
}
