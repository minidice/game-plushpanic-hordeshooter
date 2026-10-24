using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter;

public sealed record StatusVisualDefinition(string Id, string Name, Color Color, string Shape,
    int Priority, int Surface, float SurfaceAlpha, string Binding, string[] Sources);
public readonly record struct StatusVisualState(string Id, float Remaining = 0, int Stacks = 1);

/// <summary>Appearance only. A zero remaining value means an externally owned state with no known timer.</summary>
public sealed class StatusVisualCatalog
{
    public const string DataPath = "res://content/data/status_visuals.json";
    private static StatusVisualCatalog? _current;
    public static StatusVisualCatalog Current => _current ??= Parse(FileAccess.GetFileAsString(DataPath));
    private readonly Dictionary<string, StatusVisualDefinition> _all = new();
    public IReadOnlyDictionary<string, StatusVisualDefinition> All => _all;
    public StatusVisualDefinition Get(string id) => _all.TryGetValue(id, out var d) ? d : throw new ArgumentException("Unknown status: " + id);
    public static StatusVisualCatalog Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.GetProperty("schema_version").GetInt32() != 1) throw new FormatException("Unknown status schema");
        var catalog = new StatusVisualCatalog();
        foreach (var item in doc.RootElement.GetProperty("statuses").EnumerateArray())
        {
            var fields = new HashSet<string> { "id", "name", "color", "shape", "priority", "surface", "surface_alpha", "binding", "sources" };
            foreach (var p in item.EnumerateObject()) if (!fields.Remove(p.Name)) throw new FormatException("Unknown/duplicate status field");
            if (fields.Count != 0) throw new FormatException("Missing status field");
            string Text(string key) => item.GetProperty(key).GetString() is { Length: > 0 } s && !string.IsNullOrWhiteSpace(s) ? s : throw new FormatException("Empty " + key);
            string id = Text("id"), shape = Text("shape"), color = Text("color"), binding = Text("binding");
            if (shape is not ("bubble" or "drop" or "flame" or "frost" or "ice" or "eye" or "star" or "arc" or "fear" or "weak" or "chain" or "air")) throw new FormatException("Unknown status shape");
            if (color.Length != 6 || !uint.TryParse(color, System.Globalization.NumberStyles.HexNumber, null, out _)) throw new FormatException("Invalid status color");
            if (binding is not ("live" or "preview")) throw new FormatException("Invalid binding");
            int priority = item.GetProperty("priority").GetInt32(), surface = item.GetProperty("surface").GetInt32();
            float alpha = item.GetProperty("surface_alpha").GetSingle();
            if (priority < 0 || priority > 100 || surface < 0 || surface > 4 || !float.IsFinite(alpha) || alpha < 0 || alpha > .35f || surface == 0 && alpha != 0)
                throw new FormatException("Invalid visual range");
            var sources = new List<string>();
            foreach (var v in item.GetProperty("sources").EnumerateArray())
            { var s = v.GetString(); if (string.IsNullOrWhiteSpace(s) || sources.Contains(s)) throw new FormatException("Invalid source"); sources.Add(s); }
            if (binding == "live" && sources.Count == 0 || binding == "preview" && sources.Count != 0) throw new FormatException("Binding/source disagreement");
            if (!catalog._all.TryAdd(id, new(id, Text("name"), new Color(color), shape, priority, surface, alpha, binding, sources.ToArray()))) throw new FormatException("Duplicate status ID");
        }
        if (catalog._all.Count == 0) throw new FormatException("Empty status catalog");
        return catalog;
    }
}
