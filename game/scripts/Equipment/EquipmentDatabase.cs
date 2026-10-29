using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Equipment and its options from the five tables in content/data/equipment/ (decisions.md C-36,
/// doc/features/combat/item-option-data.md): stats → conditions → options → option groups → equipment. Stat and condition
/// ids must match the code (<see cref="EquipmentEffect"/>, <see cref="EquipmentCondition"/>); the data picks and tunes
/// them. Errors are collected, not thrown; a bad row is left out.
/// </summary>
public sealed class EquipmentDatabase
{
    public const string Dir = "res://content/data/equipment";
    public const string StatsFile = "option_stats.csv", ConditionsFile = "option_conditions.csv", OptionsFile = "options.csv",
        GroupsFile = "option_groups.csv", EquipmentFile = "equipment.csv";

    private static readonly Dictionary<string, string[]> Columns = new()
    {
        [StatsFile] = new[] { "id", "name", "unit", "min", "max", "slots" },
        [ConditionsFile] = new[] { "id", "name", "param", "param_min", "param_max" },
        [OptionsFile] = new[] { "id", "stat", "condition", "param", "value", "grade" },
        [GroupsFile] = new[] { "group", "option", "weight" },
        [EquipmentFile] = new[] { "id", "slot", "name", "icon", "option_group" },
    };

    public IReadOnlyDictionary<string, OptionStat> Stats => _stats;
    public IReadOnlyDictionary<string, OptionCondition> Conditions => _conditions;
    public IReadOnlyDictionary<string, OptionDefinition> Options => _options;
    public IReadOnlyDictionary<string, List<GroupEntry>> Groups => _groups;
    public IReadOnlyDictionary<string, EquipmentDefinition> Equipment => _equipment;
    public IReadOnlyList<string> Errors => _errors;

    private readonly Dictionary<string, OptionStat> _stats = new();
    private readonly Dictionary<string, OptionCondition> _conditions = new();
    private readonly Dictionary<string, OptionDefinition> _options = new();
    private readonly Dictionary<string, List<GroupEntry>> _groups = new();
    private readonly Dictionary<string, EquipmentDefinition> _equipment = new();
    private readonly List<string> _errors = new();

    private static EquipmentDatabase? _current;

    public static EquipmentDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            var files = Columns.Keys.ToDictionary(f => f, f => Godot.FileAccess.GetFileAsString($"{Dir}/{f}"));
            _current = FromText(files);
            foreach (var error in _current.Errors) GD.PushError("[equipment] " + error);
            return _current;
        }
    }

    public static void Reload() => _current = null;

    /// <summary>Builds from the five tables' text, keyed by file name.</summary>
    public static EquipmentDatabase FromText(IReadOnlyDictionary<string, string> files)
    {
        var db = new EquipmentDatabase();
        db.Read(files, StatsFile, db.ReadStat);
        db.Read(files, ConditionsFile, db.ReadCondition);
        db.Read(files, OptionsFile, db.ReadOption);
        db.Read(files, GroupsFile, db.ReadGroupEntry);
        db.Read(files, EquipmentFile, db.ReadEquipment);
        return db;
    }

    /// <summary>One option of <paramref name="group"/>, by weight. The group must exist (loaded equipment only names loaded groups).</summary>
    public OptionDefinition Roll(string group, RandomNumberGenerator rng)
    {
        var entries = _groups[group];
        int pick = rng.RandiRange(1, entries.Sum(e => e.Weight));
        foreach (var entry in entries)
        {
            pick -= entry.Weight;
            if (pick <= 0) return entry.Option;
        }
        return entries[^1].Option;
    }

    /// <summary>A piece of gear as it appears on the floor: the item with one option picked from its group. Null for an unknown item.</summary>
    public GearItem? Create(string equipmentId, RandomNumberGenerator rng) =>
        _equipment.TryGetValue(equipmentId, out var gear) ? new GearItem(gear, Roll(gear.Group, rng)) : null;

    private void Read(IReadOnlyDictionary<string, string> files, string file, Action<string, Dictionary<string, string>> row)
    {
        if (!files.TryGetValue(file, out var text) || string.IsNullOrEmpty(text))
        {
            _errors.Add($"{file}: 파일이 없거나 비었습니다");
            return;
        }
        var (header, rows) = Csv.Parse(text);
        foreach (var column in header)
            if (!Columns[file].Contains(column)) _errors.Add($"{file}: 알 수 없는 열 '{column}' (오타?)");
        foreach (var r in rows) row($"{file} {r.Line}행", r.Cells);
    }

    private bool NewId<T>(Dictionary<string, T> into, string id, string where)
    {
        if (id != "" && !into.ContainsKey(id)) return true;
        _errors.Add($"{where}: id가 비었거나 중복입니다 ('{id}')");
        return false;
    }

    private void ReadStat(string where, Dictionary<string, string> cells)
    {
        var id = cells.GetValueOrDefault("id", "");
        if (!NewId(_stats, id, where)) return;
        where += $" ({id})";
        if (!FromSnake<EquipmentEffect>(id, out var effect))
        {
            _errors.Add($"{where}: 코드에 없는 효과 '{id}' (가능: {string.Join(", ", Enum.GetValues<EquipmentEffect>().Select(Snake))})");
            return;
        }
        var c = new CellReader(where, cells, _errors);
        var unit = cells.GetValueOrDefault("unit", "") switch { "pp" => OptionUnit.Pp, "pct" => OptionUnit.Pct, _ => (OptionUnit?)null };
        if (unit == null) { _errors.Add($"{where}: unit은 pp 또는 pct입니다"); return; }
        float min = c.Float("min", float.NaN), max = c.Float("max", float.NaN);
        if (!(min <= max)) { _errors.Add($"{where}: min ~ max 범위가 잘못됐습니다"); return; }
        var slots = new HashSet<EquipmentSlot>();
        foreach (var name in cells.GetValueOrDefault("slots", "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (FromSnake<EquipmentSlot>(name, out var slot)) slots.Add(slot);
            else _errors.Add($"{where}: 알 수 없는 슬롯 '{name}' (head 또는 armor)");
        }
        if (slots.Count == 0) { _errors.Add($"{where}: slots가 비었습니다"); return; }
        var stat = new OptionStat(id, effect, cells.GetValueOrDefault("name", ""), unit.Value, min, max, slots);
        CheckName(where, stat.NameKey);
        _stats[id] = stat;
    }

    private void ReadCondition(string where, Dictionary<string, string> cells)
    {
        var id = cells.GetValueOrDefault("id", "");
        if (!NewId(_conditions, id, where)) return;
        where += $" ({id})";
        if (!FromSnake<EquipmentCondition>(id, out var condition))
        {
            _errors.Add($"{where}: 코드에 없는 조건 '{id}' (가능: {string.Join(", ", Enum.GetValues<EquipmentCondition>().Select(Snake))})");
            return;
        }
        if (!FromSnake<OptionParam>(cells.GetValueOrDefault("param", ""), out var param))
        {
            _errors.Add($"{where}: param은 none 또는 seconds입니다");
            return;
        }
        if (param != ExpectedParam(condition))
        {
            _errors.Add($"{where}: 조건 {id}의 변수는 코드에서 {Snake(ExpectedParam(condition))}입니다 (데이터: {Snake(param)})");
            return;
        }
        var c = new CellReader(where, cells, _errors);
        float min = c.Float("param_min", 0), max = c.Float("param_max", 0);
        if (param != OptionParam.None && !(min <= max)) { _errors.Add($"{where}: param_min ~ param_max 범위가 잘못됐습니다"); return; }
        var def = new OptionCondition(id, condition, cells.GetValueOrDefault("name", ""), param, min, max);
        CheckName(where, def.NameKey);
        _conditions[id] = def;
    }

    private void ReadOption(string where, Dictionary<string, string> cells)
    {
        var id = cells.GetValueOrDefault("id", "");
        if (!NewId(_options, id, where)) return;
        where += $" ({id})";
        var statId = cells.GetValueOrDefault("stat", "");
        var conditionId = cells.GetValueOrDefault("condition", "");
        if (!_stats.TryGetValue(statId, out var stat)) { _errors.Add($"{where}: option_stats.csv에 없는 효과 '{statId}'"); return; }
        if (!_conditions.TryGetValue(conditionId, out var condition)) { _errors.Add($"{where}: option_conditions.csv에 없는 조건 '{conditionId}'"); return; }
        var c = new CellReader(where, cells, _errors);
        float? param = null;
        if (condition.Param == OptionParam.None)
        {
            if (cells.GetValueOrDefault("param", "") != "") { _errors.Add($"{where}: 조건 {conditionId}은 param을 받지 않습니다 (칸을 비우세요)"); return; }
        }
        else
        {
            param = c.Float("param", float.NaN);
            if (!(param >= condition.ParamMin && param <= condition.ParamMax))
            {
                _errors.Add($"{where}: 조건 {conditionId}의 param은 {condition.ParamMin} ~ {condition.ParamMax}이어야 합니다 (빈칸 안 됨)");
                return;
            }
        }
        float value = c.Float("value", float.NaN);
        if (!(value >= stat.Min && value <= stat.Max))
        {
            _errors.Add($"{where}: 값 {cells.GetValueOrDefault("value", "")}이(가) {statId}의 범위 {stat.Min} ~ {stat.Max} 밖입니다");
            return;
        }
        // The shot after focus is already a sure crit (C-32), so more chance on it does nothing (C-34: not made).
        if (condition.Condition == EquipmentCondition.FocusedShot && stat.Effect == EquipmentEffect.CritChance)
        {
            _errors.Add($"{where}: focused_shot + crit_chance는 쓸 수 없습니다 (집중 한 발은 이미 100% 치명타)");
            return;
        }
        int grade = c.Int("grade", 1);
        if (grade < 1) { _errors.Add($"{where}: grade는 1 이상입니다"); return; }
        _options[id] = new OptionDefinition(id, stat, condition, param, value, grade);
    }

    private void ReadGroupEntry(string where, Dictionary<string, string> cells)
    {
        var group = cells.GetValueOrDefault("group", "");
        var optionId = cells.GetValueOrDefault("option", "");
        where += $" ({group})";
        if (group == "") { _errors.Add($"{where}: group이 비었습니다"); return; }
        if (!_options.TryGetValue(optionId, out var option)) { _errors.Add($"{where}: options.csv에 없는 옵션 '{optionId}'"); return; }
        int weight = new CellReader(where, cells, _errors).Int("weight", 0);
        if (weight <= 0) { _errors.Add($"{where}: weight는 1 이상입니다 ({optionId})"); return; }
        if (!_groups.TryGetValue(group, out var entries)) _groups[group] = entries = new List<GroupEntry>();
        if (entries.Any(e => e.Option == option)) { _errors.Add($"{where}: 옵션 '{optionId}'이(가) 그룹에 중복입니다 (확률은 weight로)"); return; }
        entries.Add(new GroupEntry(option, weight));
    }

    private void ReadEquipment(string where, Dictionary<string, string> cells)
    {
        var id = cells.GetValueOrDefault("id", "");
        if (!NewId(_equipment, id, where)) return;
        where += $" ({id})";
        var slotName = cells.GetValueOrDefault("slot", "");
        if (!FromSnake<EquipmentSlot>(slotName, out var slot)) { _errors.Add($"{where}: 알 수 없는 슬롯 '{slotName}' (head 또는 armor)"); return; }
        var group = cells.GetValueOrDefault("option_group", "");
        if (!_groups.TryGetValue(group, out var entries)) { _errors.Add($"{where}: option_groups.csv에 없는 그룹 '{group}'"); return; }
        var misfit = entries.Where(e => !e.Option.Stat.Slots.Contains(slot)).Select(e => e.Option.Id).ToList();
        if (misfit.Count > 0) { _errors.Add($"{where}: 옵션 {string.Join(", ", misfit)}의 효과는 슬롯 {Snake(slot)}에 붙을 수 없습니다"); return; }
        var def = new EquipmentDefinition(id, slot, cells.GetValueOrDefault("name", ""), cells.GetValueOrDefault("icon", ""), group);
        if (!CheckName(where, def.NameKey)) return;
        _equipment[id] = def;
    }

    private bool CheckName(string where, string key)
    {
        if (Loc.Has(key)) return true;
        _errors.Add($"{where}: 이름 키 '{key}'가 content/lan/{Loc.Fallback}/ 에 없습니다");
        return false;
    }

    /// <summary>The parameter each condition takes in code; the data must agree.</summary>
    private static OptionParam ExpectedParam(EquipmentCondition condition) => condition switch
    {
        _ => OptionParam.None,
    };

    /// <summary>CritChance → crit_chance: how the tables name the code's values.</summary>
    public static string Snake<T>(T value) where T : struct, Enum =>
        string.Concat(value.ToString().Select((ch, i) => i > 0 && char.IsUpper(ch) ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

    private static bool FromSnake<T>(string id, out T value) where T : struct, Enum
    {
        value = Enum.GetValues<T>().FirstOrDefault(v => Snake(v) == id);
        return id != "" && Snake(value) == id;
    }
}
