using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>
/// Builds every <see cref="ThrowableDefinition"/> from content/data/throwables.csv and the effect files
/// in content/data/throwable_effects/. Effects run in the order listed. Errors are collected, not thrown.
/// </summary>
public sealed class ThrowableDatabase
{
    public const string TablePath = "res://content/data/throwables.csv";
    public const string EffectDir = "res://content/data/throwable_effects";

    public static readonly string[] Columns =
        { "id", "name", "look", "max_carry", "max_range_m", "fuse_ms", "detonate_on_impact", "effects", "color", "model", "icon" };

    private static readonly Dictionary<string, string[]> EffectKeys = new()
    {
        ["explode"] = new[] { "radius_m", "damage", "knockback_power", "self_damage_share" },
        ["burn_pool"] = new[] { "radius_m", "duration_ms", "enemy_dps", "player_dps" },
        ["stun"] = new[] { "radius_m", "duration_ms" },
        ["blind_self"] = new[] { "radius_m", "max_ms", "facing_threshold" },
        ["attract"] = new[] { "radius_m", "duration_ms", "beep_interval_ms" },
    };

    public IReadOnlyDictionary<string, ThrowableDefinition> Throwables => _items;
    public IReadOnlyList<string> Errors => _errors;

    private readonly Dictionary<string, ThrowableDefinition> _items = new();
    private readonly Dictionary<string, ThrowEffect> _effects = new();
    private readonly List<string> _errors = new();

    private static ThrowableDatabase? _current;

    public static ThrowableDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            var effects = new Dictionary<string, string>();
            foreach (var file in DirAccess.GetFilesAt(EffectDir))
                if (file.EndsWith(".json")) effects[file[..^5]] = Godot.FileAccess.GetFileAsString($"{EffectDir}/{file}");
            _current = FromText(Godot.FileAccess.GetFileAsString(TablePath), effects);
            foreach (var error in _current.Errors) GD.PushError("[throwables] " + error);
            return _current;
        }
    }

    public static void Reload() => _current = null;

    public static ThrowableDatabase FromText(string csv, IReadOnlyDictionary<string, string> effectJson)
    {
        var db = new ThrowableDatabase();
        foreach (var (name, text) in effectJson) db.LoadEffect(name, text);
        var (header, rows) = Csv.Parse(csv);
        foreach (var column in header)
            if (!Columns.Contains(column)) db._errors.Add($"throwables.csv: 알 수 없는 열 '{column}' (오타?)");
        foreach (var row in rows)
        {
            var id = row.Cells.GetValueOrDefault("id", "");
            var where = $"throwables.csv {row.Line}행 ({id})";
            if (id == "" || db._items.ContainsKey(id))
            {
                db._errors.Add($"{where}: id가 비었거나 중복입니다");
                continue;
            }
            var c = new CellReader(where, row.Cells, db._errors);
            var def = new ThrowableDefinition
            {
                Id = id,
                NameKey = row.Cells.GetValueOrDefault("name", ""),
                Kind = c.Enum<ThrowableKind>("look"),
                MaxCarry = Math.Max(1, c.Int("max_carry", 2)),
                MaxRange = c.Float("max_range_m", 16),
                Fuse = c.Float("fuse_ms", 0) / 1000f,
                DetonateOnImpact = c.Bool("detonate_on_impact", false),
                Color = c.Color("color", new Color("5d6b3a")),
                ModelPath = row.Cells.GetValueOrDefault("model", ""),
                IconPath = row.Cells.GetValueOrDefault("icon", ""),
            };
            if (!Loc.Has(def.NameKey)) db._errors.Add($"{where}: 이름 키 '{def.NameKey}'가 content/lan/{Loc.Fallback}/ 에 없습니다");
            foreach (var effect in row.Cells.GetValueOrDefault("effects", "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (db._effects.TryGetValue(effect, out var e)) def.Effects.Add(e);
                else db._errors.Add($"{where}: 효과 '{effect}' 파일이 throwable_effects/에 없습니다");
            }
            if (def.Effects.Count == 0) db._errors.Add($"{where}: 효과가 하나도 없습니다");
            if (def.Fuse <= 0 && !def.DetonateOnImpact && def.Attract == null)
                db._errors.Add($"{where}: fuse_ms가 0이면 detonate_on_impact나 attract 효과가 있어야 합니다 (안 그러면 터지지 않음)");
            db._items[id] = def;
        }
        return db;
    }

    private void LoadEffect(string name, string text)
    {
        var where = $"throwable_effects/{name}.json";
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.Clone();
            var kind = root.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
            if (!EffectKeys.TryGetValue(kind, out var keys))
            {
                _errors.Add($"{where}: 알 수 없는 kind '{kind}' (가능: {string.Join(", ", EffectKeys.Keys)})");
                return;
            }
            foreach (var prop in root.EnumerateObject())
                if (prop.Name is not ("kind" or "description") && !keys.Contains(prop.Name))
                    _errors.Add($"{where}: 알 수 없는 항목 '{prop.Name}' (가능: {string.Join(", ", keys)})");
            var f = new JsonFields(root, where, _errors);
            _effects[name] = kind switch
            {
                "explode" => new ExplodeEffect(name, f.Float("radius_m", 4.5f), f.Float("damage", 150), f.Float("knockback_power", 120), f.Float("self_damage_share", .2f)),
                "burn_pool" => new BurnPoolEffect(name, f.Float("radius_m", 3.4f), f.Float("duration_ms", 8000) / 1000f, f.Float("enemy_dps", 45), f.Float("player_dps", 9)),
                "stun" => new StunEffect(name, f.Float("radius_m", 8), f.Float("duration_ms", 4000) / 1000f),
                "blind_self" => new BlindSelfEffect(name, f.Float("radius_m", 8), f.Float("max_ms", 3000) / 1000f, f.Float("facing_threshold", .3f)),
                _ => new AttractEffect(name, f.Float("radius_m", 30), f.Float("duration_ms", 6000) / 1000f, f.Float("beep_interval_ms", 500) / 1000f),
            };
        }
        catch (JsonException e)
        {
            _errors.Add($"{where}: JSON 오류 — {e.Message}");
        }
    }
}
