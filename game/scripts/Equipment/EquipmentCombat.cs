namespace HordeShooter;

/// <summary>
/// How worn gear joins a shot (decisions.md C-32 · C-34, equipment-implementation-plan.md Task 2). Pure, so the rules are
/// checked without a stage.
/// </summary>
public static class EquipmentCombat
{
    /// <summary>A focused shot always crits; otherwise the shot crits when its roll in 0..1 is under the gear's chance.</summary>
    public static bool IsCritical(bool guaranteed, float chance, float sample01) => guaranteed || sample01 < chance;

    /// <summary>
    /// Damage factor of a crit: the weapon's multiplier, the Gather card's bonus, then the gear's crit damage. The gear
    /// factor for a focused shot includes focus-only options; a chance crit uses the one without them.
    /// </summary>
    public static float CritFactor(float weaponMultiplier, float gatherCrit, float gearCritDamage) =>
        weaponMultiplier * (1 + gatherCrit) * gearCritDamage;
}
