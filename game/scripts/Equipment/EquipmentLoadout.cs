using Godot;

namespace HordeShooter;

/// <summary>
/// What a survivor's worn gear adds to one shot (C-34): crit chance added in 0..1, and factors for crit damage, spread and
/// recoil. <see cref="None"/> leaves everything as it was. The weapon's own definition is never changed.
/// </summary>
public readonly record struct GearBonus(float CritChance, float CritDamage, float Spread, float Recoil)
{
    public static readonly GearBonus None = new(0, 1, 1, 1);
}

/// <summary>
/// One survivor's two gear slots (decisions.md C-34 · C-36). No spares and no durability: equipping into a filled slot gives
/// the old piece back, with its option, to be dropped on the floor.
/// </summary>
public sealed class EquipmentLoadout
{
    public GearItem? Head { get; private set; }
    public GearItem? Armor { get; private set; }

    public GearItem? In(EquipmentSlot slot) => slot == EquipmentSlot.Head ? Head : Armor;

    /// <summary>Wears <paramref name="next"/> in its slot and returns what was there (null for an empty slot).</summary>
    public GearItem? Equip(GearItem next)
    {
        var old = In(next.Gear.Slot);
        if (next.Gear.Slot == EquipmentSlot.Head) Head = next;
        else Armor = next;
        return old;
    }

    /// <summary>
    /// The bonus for one shot. <paramref name="aiming"/> is true while aiming, focus charging included; <paramref name="focusedShot"/>
    /// is the one shot after focus completes. Same effects add up, then are clamped (equipment-implementation-plan.md, AI choice).
    /// </summary>
    public GearBonus Evaluate(bool aiming, bool focusedShot)
    {
        float chance = 0, critDamage = 0, spread = 0, recoil = 0;
        foreach (var item in new[] { Head, Armor })
        {
            if (item == null || !Active(item.Option.Condition.Condition, aiming, focusedShot)) continue;
            float share = item.Option.Value / 100f;
            switch (item.Option.Stat.Effect)
            {
                case EquipmentEffect.CritChance: chance += share; break;
                case EquipmentEffect.CritDamage: critDamage += share; break;
                case EquipmentEffect.SpreadReduction: spread += share; break;
                case EquipmentEffect.RecoilReduction: recoil += share; break;
            }
        }
        return new GearBonus(Mathf.Clamp(chance, 0, 1), 1 + Mathf.Max(0, critDamage), 1 - Mathf.Clamp(spread, 0, 1), 1 - Mathf.Clamp(recoil, 0, 1));
    }

    private static bool Active(EquipmentCondition condition, bool aiming, bool focusedShot) => condition switch
    {
        EquipmentCondition.Always => true,
        EquipmentCondition.Aiming => aiming,
        EquipmentCondition.FocusedShot => focusedShot,
        _ => false,
    };
}
