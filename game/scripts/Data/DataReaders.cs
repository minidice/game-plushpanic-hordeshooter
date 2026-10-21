using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace HordeShooter;

// Shared readers for the CSV/JSON data tables. They never throw: bad values are appended to the
// caller's error list with the file/row context and the fallback value is used.

/// <summary>Typed access to one merged CSV row.</summary>
public sealed class CellReader
{
    private readonly string _where;
    private readonly Dictionary<string, string> _cells;
    private readonly List<string> _errors;

    public CellReader(string where, Dictionary<string, string> cells, List<string> errors)
    {
        _where = where;
        _cells = cells;
        _errors = errors;
    }

    private string Raw(string column) => _cells.GetValueOrDefault(column, "");

    private void Bad(string column, string expected) => _errors.Add($"{_where}: {column} 값 '{Raw(column)}'는 {expected}이(가) 아닙니다");

    public bool IsInfinite(string column) => Raw(column).ToLowerInvariant() is "inf" or "∞" or "-" or "무제한";

    public float Float(string column, float fallback)
    {
        var raw = Raw(column);
        if (raw == "" || raw == "-") return fallback;
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
        Bad(column, "숫자");
        return fallback;
    }

    public int Int(string column, int fallback)
    {
        var raw = Raw(column);
        if (raw == "" || raw == "-" || IsInfinite(column)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;
        Bad(column, "정수");
        return fallback;
    }

    public bool Bool(string column, bool fallback)
    {
        switch (Raw(column).ToLowerInvariant())
        {
            case "": case "-": return fallback;
            case "true": case "1": case "y": case "yes": case "o": return true;
            case "false": case "0": case "n": case "no": case "x": return false;
        }
        Bad(column, "true/false");
        return fallback;
    }

    public Color Color(string column, Color fallback)
    {
        var raw = Raw(column);
        if (raw == "") return fallback;
        if (Godot.Color.HtmlIsValid(raw)) return new Color(raw);
        Bad(column, "색 코드(예: 2f3330)");
        return fallback;
    }

    public WeaponSlot Slot(string column) => Raw(column).ToLowerInvariant() switch
    {
        "1" or "secondary" or "보조" or "보조무기" => WeaponSlot.Secondary,
        "2" or "primary" or "주" or "주무기" => WeaponSlot.Primary,
        "3" or "special" or "특수" or "특수무기" => WeaponSlot.Special,
        _ => BadSlot(column),
    };

    private WeaponSlot BadSlot(string column)
    {
        Bad(column, "1/2/3");
        return WeaponSlot.Primary;
    }

    public FireMode Mode(string column) => Raw(column).ToLowerInvariant() switch
    {
        "melee" => FireMode.Melee,
        "hitscan" => FireMode.Hitscan,
        "projectile" => FireMode.Projectile,
        "lob" => FireMode.Lob,
        "shout" => FireMode.Shout,
        "throw" => FireMode.Throw,
        _ => BadMode(column),
    };

    private FireMode BadMode(string column)
    {
        Bad(column, "melee/hitscan/projectile/lob");
        return FireMode.Hitscan;
    }

    public T Enum<T>(string column) where T : struct, System.Enum
    {
        if (System.Enum.TryParse<T>(Raw(column), true, out var value)) return value;
        Bad(column, string.Join("/", System.Enum.GetNames<T>()).ToLowerInvariant());
        return default;
    }
}

/// <summary>Typed access to one trait/effect JSON object.</summary>
public sealed class JsonFields
{
    private readonly JsonElement _json;
    private readonly string _where;
    private readonly List<string> _errors;

    public JsonFields(JsonElement json, string where, List<string> errors)
    {
        _json = json;
        _where = where;
        _errors = errors;
    }

    public float Float(string key, float fallback)
    {
        if (!_json.TryGetProperty(key, out var el)) return fallback;
        if (el.ValueKind == JsonValueKind.Number) return el.GetSingle();
        _errors.Add($"{_where}: {key}는 숫자여야 합니다");
        return fallback;
    }

    public int Int(string key, int fallback) => (int)MathF.Round(Float(key, fallback));

    public string String(string key, string fallback) =>
        _json.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? fallback : fallback;
}
