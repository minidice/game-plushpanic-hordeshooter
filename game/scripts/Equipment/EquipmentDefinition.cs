using System.Collections.Generic;

namespace HordeShooter;

/// <summary>The two gear slots (decisions.md C-34). Separate from the weapon slots (C-3).</summary>
public enum EquipmentSlot { Head, Armor }

/// <summary>
/// What an option changes. The code decides how each one applies (see <see cref="EquipmentLoadout.Evaluate"/>); the data
/// row in option_stats.csv names it by its snake_case id (crit_chance …) and gives its unit and allowed range.
/// </summary>
public enum EquipmentEffect { CritChance, CritDamage, SpreadReduction, RecoilReduction }

/// <summary>
/// When an option is on (C-34): always, while aiming (focus charging counts as aiming), or the one shot after focus is
/// complete. A new condition needs code here and a row in option_conditions.csv.
/// </summary>
public enum EquipmentCondition { Always, Aiming, FocusedShot }

/// <summary>Display unit of a stat's values: percentage points (crit chance) or percent of the base.</summary>
public enum OptionUnit { Pp, Pct }

/// <summary>What a condition's parameter means; none means the option row leaves it empty.</summary>
public enum OptionParam { None, Seconds }

/// <summary>One adjustable stat (option_stats.csv). Values outside <see cref="Min"/>..<see cref="Max"/> are refused.</summary>
public sealed record OptionStat(string Id, EquipmentEffect Effect, string NameKey, OptionUnit Unit, float Min, float Max, IReadOnlySet<EquipmentSlot> Slots);

/// <summary>One activation condition (option_conditions.csv).</summary>
public sealed record OptionCondition(string Id, EquipmentCondition Condition, string NameKey, OptionParam Param, float ParamMin, float ParamMax);

/// <summary>A finished option (options.csv): a stat under a condition with a fixed value, e.g. crit damage +10% always.</summary>
public sealed record OptionDefinition(string Id, OptionStat Stat, OptionCondition Condition, float? Param, float Value, int Grade);

/// <summary>One candidate of an option group (option_groups.csv); the chance is its weight over the group's total.</summary>
public sealed record GroupEntry(OptionDefinition Option, int Weight);

/// <summary>A hat or an armor (equipment.csv). Its option is picked from <see cref="Group"/> when it appears on the floor.</summary>
public sealed record EquipmentDefinition(string Id, EquipmentSlot Slot, string NameKey, string IconPath, string Group);

/// <summary>
/// One piece of gear in the world or worn: the item and the option it got when it appeared (C-36). Dropping it on a swap
/// keeps the same option; nothing rolls it again.
/// </summary>
public sealed record GearItem(EquipmentDefinition Gear, OptionDefinition Option);
