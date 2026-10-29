using System.Globalization;

namespace HordeShooter;

/// <summary>
/// An option as words, built from its condition and stat names (decisions.md C-36): "While aiming · Crit damage +20%".
/// Only developer mode shows it (M-41); play keeps options hidden.
/// </summary>
public static class EquipmentText
{
    /// <summary>Marks in front of a worn option in developer mode: on now, or waiting for its condition.</summary>
    public const string On = "●", Off = "○";

    public static string Describe(OptionDefinition option)
    {
        string value = option.Value.ToString("0.#", CultureInfo.InvariantCulture);
        string amount = option.Stat.Unit == OptionUnit.Pp ? Loc.Format("fmt_gear_value_pp", value) : Loc.Format("fmt_gear_value_pct", value);
        return Loc.Format("fmt_gear_option", Loc.Text(option.Condition.NameKey), Loc.Text(option.Stat.NameKey), amount);
    }

    /// <summary>A worn option with its on/off mark.</summary>
    public static string Line(OptionDefinition option, bool active) => (active ? On : Off) + " " + Describe(option);

    /// <summary>Whether a worn option's condition holds now (the same test the shot uses, focus shot = focus ready).</summary>
    public static bool Active(OptionDefinition option, Player who) => option.Condition.Condition switch
    {
        EquipmentCondition.Always => true,
        EquipmentCondition.Aiming => who.IsAiming,
        EquipmentCondition.FocusedShot => who.CritReady,
        _ => false,
    };
}
