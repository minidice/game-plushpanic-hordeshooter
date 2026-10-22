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
