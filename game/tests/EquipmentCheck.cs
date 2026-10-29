using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>
/// Equipment data and condition evaluation (decisions.md C-34 · C-36, equipment-implementation-plan.md Task 1): the five
/// option tables load, each of the nine test items gives its accepted value only under its condition, a group picks by
/// weight, swapping returns the old item with its option, and bad data is reported instead of loaded. Headless.
/// </summary>
public partial class EquipmentCheck : Node
{
    private int _checks;

    private void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }

    public override void _Ready()
    {
        try
        {
            Production();
            Evaluation();
            Swapping();
            Rolling();
            BadData();
            GD.Print("EQUIPMENT_PASS " + _checks);
            QuitHygiene.Collect();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("FAIL: " + e.Message); GetTree().Quit(1); }
    }

    private static EquipmentDatabase Db => EquipmentDatabase.Current;

    private static GearItem Gear(string id) => Db.Create(id, new RandomNumberGenerator { Seed = 1 }) ?? throw new Exception($"no equipment '{id}'");

    private void Production()
    {
        Check(Db.Errors.Count == 0, "production option tables load without errors" + (Db.Errors.Count > 0 ? ": " + string.Join(" | ", Db.Errors) : ""));
        Check(Db.Equipment.Count == 9, $"nine test items ({Db.Equipment.Count})");
        Check(Db.Equipment.Values.Count(e => e.Slot == EquipmentSlot.Head) == 5 && Db.Equipment.Values.Count(e => e.Slot == EquipmentSlot.Armor) == 4,
            "five hats (crit chance and damage), four armors (spread and recoil)");
        Check(Db.Equipment.Values.All(e => Db.Groups[e.Group].Count == 1), "each test item has a one-line group, so its option is fixed");
        Check(!Db.Options.Values.Any(o => o.Condition.Condition == EquipmentCondition.FocusedShot && o.Stat.Effect == EquipmentEffect.CritChance),
            "no focused-shot crit chance option (already 100%, C-34)");
        Check(Db.Stats.Values.All(s => Loc.Has(s.NameKey)) && Db.Conditions.Values.All(c => Loc.Has(c.NameKey)) && Db.Equipment.Values.All(e => Loc.Has(e.NameKey)),
            "stats, conditions and items have Korean names");
    }

    private static GearBonus Alone(string id, bool aiming, bool focused)
    {
        var loadout = new EquipmentLoadout();
        loadout.Equip(Gear(id));
        return loadout.Evaluate(aiming, focused);
    }

    private void Evaluation()
    {
        var none = new EquipmentLoadout().Evaluate(true, true);
        Check(none == GearBonus.None, "an empty loadout changes nothing");
        // The nine accepted test values (C-34): crit chance +3%p / +6%p aiming, crit damage +10% / +20% aiming / +30% focused,
        // spread −10% / −15% aiming, recoil −15% / −25% aiming.
        foreach (var (id, effect, idle, aiming, focused) in new (string, EquipmentEffect, float, float, float)[]
        {
            ("keen_cap", EquipmentEffect.CritChance, .03f, .03f, .03f),
            ("scope_goggles", EquipmentEffect.CritChance, 0, .06f, .06f),
            ("lucky_cap", EquipmentEffect.CritDamage, 1.1f, 1.1f, 1.1f),
            ("hunter_hat", EquipmentEffect.CritDamage, 1, 1.2f, 1.2f),
            ("focus_band", EquipmentEffect.CritDamage, 1, 1, 1.3f),
            ("steady_vest", EquipmentEffect.SpreadReduction, .9f, .9f, .9f),
            ("marksman_vest", EquipmentEffect.SpreadReduction, 1, .85f, .85f),
            ("brace_vest", EquipmentEffect.RecoilReduction, .85f, .85f, .85f),
            ("shoulder_harness", EquipmentEffect.RecoilReduction, 1, .75f, .75f),
        })
        {
            float Value(GearBonus b) => effect switch
            {
                EquipmentEffect.CritChance => b.CritChance,
                EquipmentEffect.CritDamage => b.CritDamage,
                EquipmentEffect.SpreadReduction => b.Spread,
                _ => b.Recoil,
            };
            var states = new[] { Alone(id, false, false), Alone(id, true, false), Alone(id, true, true) };
            Check(Mathf.IsEqualApprox(Value(states[0]), idle) && Mathf.IsEqualApprox(Value(states[1]), aiming) && Mathf.IsEqualApprox(Value(states[2]), focused),
                $"{id}: idle {Value(states[0])}, aiming {Value(states[1])}, focused shot {Value(states[2])}");
            var others = states.Select(s => effect == EquipmentEffect.CritChance ? s with { CritChance = 0 } : effect == EquipmentEffect.CritDamage ? s with { CritDamage = 1 }
                : effect == EquipmentEffect.SpreadReduction ? s with { Spread = 1 } : s with { Recoil = 1 });
            Check(others.All(s => s == GearBonus.None), $"{id} touches only its own effect");
        }
        var both = new EquipmentLoadout();
        both.Equip(Gear("hunter_hat"));
        both.Equip(Gear("shoulder_harness"));
        var b = both.Evaluate(true, false);
        Check(Mathf.IsEqualApprox(b.CritDamage, 1.2f) && Mathf.IsEqualApprox(b.Recoil, .75f), "a hat and an armor both count");
    }

    private void Swapping()
    {
        var mine = new EquipmentLoadout();
        var other = new EquipmentLoadout();
        Check(mine.Equip(Gear("keen_cap")) == null && mine.Head?.Gear.Id == "keen_cap" && mine.Armor == null, "an empty head slot takes a hat and gives nothing back");
        var old = mine.Equip(Gear("scope_goggles"));
        Check(old?.Gear.Id == "keen_cap" && old.Option.Id == Db.Groups["keen_cap"][0].Option.Id && mine.Head?.Gear.Id == "scope_goggles",
            "a second hat swaps: the old one comes back with its option, to be dropped");
        mine.Equip(Gear("brace_vest"));
        Check(mine.Head?.Gear.Id == "scope_goggles" && mine.Armor?.Gear.Id == "brace_vest", "an armor goes to its own slot");
        other.Equip(Gear("lucky_cap"));
        Check(mine.Evaluate(true, false).CritDamage == 1 && Mathf.IsEqualApprox(other.Evaluate(false, false).CritDamage, 1.1f),
            "two players' loadouts do not share anything");
        var spread = ArmoryCatalog.Weapon("m4").SpreadPerShot;
        mine.Evaluate(true, true);
        Check(ArmoryCatalog.Weapon("m4").SpreadPerShot == spread, "evaluating never edits the shared weapon definition");
        var a = new Player();
        var c = new Player();
        Check(a.Gear != c.Gear && a.Gear.Head == null && a.Gear.Armor == null, "every survivor owns an empty loadout");
        a.Free();
        c.Free();
    }

    private void Rolling()
    {
        var db = EquipmentDatabase.FromText(Tables(
            options: Option("crit_dmg_a", "crit_damage", "always", "", "10") + Option("crit_dmg_b", "crit_damage", "always", "", "15"),
            groups: "mix,crit_dmg_a,3\nmix,crit_dmg_b,1\n",
            equipment: "mystery_cap,head,name_gear_keen_cap,,mix\n"));
        Check(db.Errors.Count == 0, "a two-option group loads" + string.Join(" | ", db.Errors));
        var rng = new RandomNumberGenerator { Seed = 20261030 };
        int a = 0;
        const int rolls = 4000;
        for (int i = 0; i < rolls; i++) if (db.Roll("mix", rng).Id == "crit_dmg_a") a++;
        Check(Mathf.Abs(a / (float)rolls - .75f) < .03f, $"weights 3 : 1 pick the first about 75% of the time ({a}/{rolls})");
        var made = db.Create("mystery_cap", rng)!;
        Check(made.Gear.Id == "mystery_cap" && made.Option.Id is "crit_dmg_a" or "crit_dmg_b", "an item made on the floor carries one option of its group");
        Check(db.Create("nowhere", rng) == null, "an unknown item makes nothing");
    }

    private void BadData()
    {
        void Bad(string what, EquipmentDatabase db, string expect) =>
            Check(db.Errors.Any(e => e.Contains(expect)), $"{what} is reported ({expect}): {string.Join(" | ", db.Errors)}");

        Bad("an unknown stat in code", EquipmentDatabase.FromText(Tables(stats: Stats + "move_speed,name_stat_crit_chance,pct,0,10,head|armor\n")), "move_speed");
        Bad("an unknown condition in code", EquipmentDatabase.FromText(Tables(conditions: Conditions + "on_roll,name_cond_always,none,,\n")), "on_roll");
        Bad("a condition given the wrong kind of parameter", EquipmentDatabase.FromText(Tables(conditions: "id,name,param,param_min,param_max\nalways,name_cond_always,seconds,1,5\naiming,name_cond_aiming,none,,\nfocused_shot,name_cond_focused_shot,none,,\n")), "always");
        Bad("a value outside the stat's range", EquipmentDatabase.FromText(Tables(options: Option("x", "crit_chance", "always", "", "150"))), "150");
        Bad("a parameter on a condition without one", EquipmentDatabase.FromText(Tables(options: Option("x", "crit_chance", "always", "3", "3"))), "param");
        Bad("a focused-shot crit chance", EquipmentDatabase.FromText(Tables(options: Option("x", "crit_chance", "focused_shot", "", "3"))), "focused_shot");
        Bad("an option of an unknown stat", EquipmentDatabase.FromText(Tables(options: Option("x", "nothing", "always", "", "3"))), "nothing");
        Bad("a duplicate option id", EquipmentDatabase.FromText(Tables(options: Option("x", "crit_chance", "always", "", "3") + Option("x", "crit_chance", "always", "", "4"))), "중복");
        Bad("a weight of zero", EquipmentDatabase.FromText(Tables(groups: "g,x,0\n")), "weight");
        Bad("a group naming an unknown option", EquipmentDatabase.FromText(Tables(groups: "g,ghost,1\n")), "ghost");
        Bad("an item with an unknown group", EquipmentDatabase.FromText(Tables(equipment: "hat,head,name_gear_keen_cap,,nowhere\n")), "nowhere");
        Bad("an item in an unknown slot", EquipmentDatabase.FromText(Tables(equipment: "hat,boots,name_gear_keen_cap,,g\n")), "boots");
        Bad("an item name missing from the language files", EquipmentDatabase.FromText(Tables(equipment: "hat,head,name_gear_missing,,g\n")), "name_gear_missing");
        Bad("an option whose stat cannot go on that slot",
            EquipmentDatabase.FromText(Tables(stats: "id,name,unit,min,max,slots\ncrit_chance,name_stat_crit_chance,pp,0,100,head\n", equipment: "vest,armor,name_gear_keen_cap,,g\n")), "armor");
        Bad("an unknown column", EquipmentDatabase.FromText(Tables(stats: "id,name,unit,min,max,slots,colour\ncrit_chance,name_stat_crit_chance,pp,0,100,head|armor,red\n")), "colour");
    }

    private const string Stats = "id,name,unit,min,max,slots\ncrit_chance,name_stat_crit_chance,pp,0,100,head|armor\ncrit_damage,name_stat_crit_damage,pct,0,100,head|armor\n";
    private const string Conditions = "id,name,param,param_min,param_max\nalways,name_cond_always,none,,\naiming,name_cond_aiming,none,,\nfocused_shot,name_cond_focused_shot,none,,\n";

    private static string Option(string id, string stat, string condition, string param, string value) => $"{id},{stat},{condition},{param},{value},1\n";

    /// <summary>A small, valid set of the five tables (one option, group <c>g</c>, one hat); each argument replaces one table's rows.</summary>
    private static Dictionary<string, string> Tables(string? stats = null, string? conditions = null, string? options = null, string? groups = null, string? equipment = null) => new()
    {
        ["option_stats.csv"] = stats ?? Stats,
        ["option_conditions.csv"] = conditions ?? Conditions,
        ["options.csv"] = "id,stat,condition,param,value,grade\n" + (options ?? Option("x", "crit_chance", "always", "", "3")),
        ["option_groups.csv"] = "group,option,weight\n" + (groups ?? "g,x,1\n"),
        ["equipment.csv"] = "id,slot,name,icon,option_group\n" + (equipment ?? "hat,head,name_gear_keen_cap,,g\n"),
    };
}
