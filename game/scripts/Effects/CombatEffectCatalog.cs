using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter;

/// <summary>Visual-only recipes. Gameplay owns damage, radius and active hazard lifetime.</summary>
public sealed record CombatEffectDefinition(string Id, string Name, string Kind, string Family,
    Color Color, Color Accent, Color SmokeColor, int Lobes, int Particles, float Spread, float Rise,
    float LightEnergy, float Duration, float PreviewRadius, float PreviewLifetime, string[] Sources, string[] UsedBy);

public sealed class CombatEffectCatalog
{
    public const string DataPath = "res://content/data/combat_effects.json";
    private static CombatEffectCatalog? _current;
    public static CombatEffectCatalog Current => _current ??= Parse(FileAccess.GetFileAsString(DataPath));
    private readonly Dictionary<string, CombatEffectDefinition> _all = new(), _sources = new();
    public IReadOnlyDictionary<string, CombatEffectDefinition> All => _all;
    public CombatEffectDefinition ForSource(string source) => _sources.TryGetValue(source, out var effect)
        ? effect : throw new ArgumentException("Unknown combat effect source: " + source);

    public static CombatEffectCatalog Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1) throw new FormatException("Unknown combat effect schema");
        var result = new CombatEffectCatalog();
        foreach (var item in root.GetProperty("effects").EnumerateArray())
        {
            string Text(string key) => item.GetProperty(key).GetString() is { Length: > 0 } s ? s : throw new FormatException("Missing " + key);
            float Number(string key, float min, float max)
            {
                float value = item.GetProperty(key).GetSingle();
                return float.IsFinite(value) && value >= min && value <= max ? value : throw new FormatException("Invalid " + key);
            }
            int Count(string key, int max)
            {
                int value = item.GetProperty(key).GetInt32();
                return value >= 0 && value <= max ? value : throw new FormatException("Invalid " + key);
            }
            Color Tint(string key)
            {
                string value = Text(key);
                return value.Length == 6 && uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out _)
                    ? new Color(value) : throw new FormatException("Invalid color " + key);
            }
            string[] List(string key)
            {
                var values = new List<string>();
                foreach (var e in item.GetProperty(key).EnumerateArray())
                {
                    string value = e.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(value) || values.Contains(value)) throw new FormatException("Empty/duplicate " + key);
                    values.Add(value);
                }
                if (values.Count == 0) throw new FormatException("Empty " + key);
                return values.ToArray();
            }
            string kind = Text("kind"), family = Text("family");
            if (kind is not ("burst" or "surface" or "projectile") || family is not ("blast" or "fire" or "poison")
                || kind == "surface" && family == "blast" || kind == "projectile" && family != "poison")
                throw new FormatException("Unsupported effect kind/family");
            var fields = new HashSet<string> { "id", "name", "kind", "family", "color", "accent", "particles", "preview_radius_m", "sources", "used_by" };
            if (kind != "projectile") fields.UnionWith(new[] { "lobes", "spread", "rise" });
            if (kind == "burst") fields.UnionWith(new[] { "smoke_color", "light_energy", "duration_s" });
            else fields.Add("preview_lifetime_s");
            foreach (var field in item.EnumerateObject())
                if (!fields.Remove(field.Name)) throw new FormatException("Unknown/duplicate field for " + kind + ": " + field.Name);
            if (fields.Count != 0) throw new FormatException("Missing effect fields: " + string.Join(", ", fields));
            var def = new CombatEffectDefinition(Text("id"), Text("name"), kind, family, Tint("color"), Tint("accent"), kind == "burst" ? Tint("smoke_color") : Colors.Transparent,
                kind != "projectile" ? Count("lobes", 24) : 0, Count("particles", 48), kind != "projectile" ? Number("spread", .1f, .85f) : 0, kind != "projectile" ? Number("rise", 0, 3) : 0,
                kind == "burst" ? Number("light_energy", 0, 6) : 0, kind == "burst" ? Number("duration_s", .1f, 3) : 0, Number("preview_radius_m", .05f, 10),
                kind != "burst" ? Number("preview_lifetime_s", .01f, 15) : 0, List("sources"), List("used_by"));
            if (kind != "burst" && def.PreviewLifetime <= 0) throw new FormatException("Sustained effect needs preview lifetime");
            if (!result._all.TryAdd(def.Id, def)) throw new FormatException("Duplicate effect ID: " + def.Id);
            foreach (string source in def.Sources)
                if (!result._sources.TryAdd(source, def)) throw new FormatException("Duplicate effect source: " + source);
        }
        if (result._all.Count == 0) throw new FormatException("Empty combat effect catalog");
        return result;
    }
}
