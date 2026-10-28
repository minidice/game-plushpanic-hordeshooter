using System.Collections.Generic;
using Godot;

namespace HordeShooter;

/// <summary>
/// What the local survivor takes from one scenario stage into the next (decisions.md L-14): the three weapon slots with
/// their rounds, throwables, the medkit and health — walking out of the safehouse with what you had.
/// </summary>
public sealed class StageCarry
{
    public WeaponState? Secondary { get; private init; }
    public WeaponState? Primary { get; private init; }
    public WeaponState? Special { get; private init; }
    public int ActiveSlot { get; private init; } = 1;
    public List<(ThrowableDefinition Def, int Count)> Throwables { get; } = new();
    public int Medkits { get; private init; }
    public int Health { get; private init; }

    public static StageCarry From(Player player)
    {
        var loadout = player.Loadout;
        loadout.Current.Holster();
        var carry = new StageCarry
        {
            Secondary = loadout.Secondary,
            Primary = loadout.Primary,
            Special = loadout.Special,
            ActiveSlot = loadout.ActiveSlot,
            Medkits = player.Medkits,
            Health = player.Health,
        };
        foreach (var (def, count) in loadout.Throwables) carry.Throwables.Add((def, count));
        return carry;
    }

    public byte[] Encode()
    {
        var w = new NetWriter();
        foreach (var weapon in new[] { Secondary, Primary, Special })
        {
            w.S16(weapon == null ? -1 : NetIds.Weapon(weapon.Def.Id));
            if (weapon != null) { w.I32(weapon.Mag); w.I32(weapon.Reserve); }
        }
        w.U8(ActiveSlot); w.U8(Medkits); w.U8(Health); w.U8(Throwables.Count);
        foreach (var (def, count) in Throwables) { w.U16(NetIds.Throwable(def.Id)); w.U16(count); }
        return w.ToArray();
    }

    public static StageCarry Decode(byte[] bytes)
    {
        var r = new NetReader(bytes);
        WeaponState? ReadWeapon() { int id = r.S16(); return id < 0 ? null : new WeaponState(NetIds.WeaponDef(id), r.I32(), r.I32()); }
        var carry = new StageCarry { Secondary = ReadWeapon(), Primary = ReadWeapon(), Special = ReadWeapon(), ActiveSlot = r.U8(), Medkits = r.U8(), Health = r.U8() };
        int count = r.U8();
        for (int i = 0; i < count; i++) carry.Throwables.Add((ArmoryCatalog.Throwable(NetIds.ThrowableAt(r.U16())), r.U16()));
        return carry;
    }

    public void ApplyTo(Player player)
    {
        var loadout = player.Loadout;
        if (Special != null) loadout.Equip(3, Special);
        if (Primary != null) loadout.Equip(2, Primary);
        if (Secondary != null) loadout.Equip(1, Secondary);
        // The G slot keeps its order: the kind thrown last goes in last.
        for (int i = Throwables.Count - 1; i >= 0; i--)
            for (int n = 0; n < Throwables[i].Count; n++) loadout.AddThrowable(Throwables[i].Def);
        player.Medkits = Medkits;
        if (Health > 0) player.SetSpawnHealth(Health);
        if (!player.Weapons.SelectSlot(ActiveSlot)) player.Weapons.RefreshVisual();
    }
}
