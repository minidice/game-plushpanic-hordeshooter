using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

public sealed record CaliberDefinition(string Id, string NameKey, int Stack, Color Color)
{
    public string DisplayName => Loc.Text(NameKey);
}

/// <summary>Ammo kinds from content/data/calibers.csv: name, rounds per backpack cell, icon colour (decisions.md M-25).</summary>
public sealed class CaliberDatabase
{
    public const string Path = "res://content/data/calibers.csv";
    private readonly Dictionary<string, CaliberDefinition> _items = new();
    private readonly List<string> _errors = new();
    public IReadOnlyDictionary<string, CaliberDefinition> Calibers => _items;
    public IReadOnlyList<string> Errors => _errors;
    /// <summary>Calibers in file order (their network index).</summary>
    public IReadOnlyList<string> Order { get; private set; } = Array.Empty<string>();

    private static CaliberDatabase? _current;
    public static CaliberDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            _current = FromText(FileAccess.FileExists(Path) ? FileAccess.GetFileAsString(Path) : "");
            foreach (var error in _current.Errors) GD.PushError("[calibers] " + error);
            return _current;
        }
    }

    public static CaliberDefinition? Get(string id) => Current._items.GetValueOrDefault(id);

    public static CaliberDatabase FromText(string csv)
    {
        var db = new CaliberDatabase();
        if (csv.Length == 0) { db._errors.Add("calibers.csv: missing"); return db; }
        var (_, rows) = Csv.Parse(csv);
        var order = new List<string>();
        foreach (var row in rows)
        {
            var id = row.Cells.GetValueOrDefault("id", "");
            var where = $"calibers.csv {row.Line}행 ({id})";
            if (id == "" || db._items.ContainsKey(id)) { db._errors.Add($"{where}: id가 비었거나 중복입니다"); continue; }
            var c = new CellReader(where, row.Cells, db._errors);
            var def = new CaliberDefinition(id, row.Cells.GetValueOrDefault("name", ""), c.Int("stack", 30), c.Color("color", new Color("c8c8c8")));
            if (def.Stack < 1) db._errors.Add($"{where}: stack must be 1 or more");
            if (!Loc.Has(def.NameKey)) db._errors.Add($"{where}: 이름 키 '{def.NameKey}'가 content/lan/{Loc.Fallback}/ 에 없습니다");
            db._items[id] = def;
            order.Add(id);
        }
        db.Order = order;
        return db;
    }
}
