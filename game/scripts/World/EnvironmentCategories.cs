using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>Explicit authoring intent only. No clock, weather simulation or map lighting changes.</summary>
public sealed record EnvironmentSelection(string Domain, string? IndoorLight, string? OutdoorTime, string Weather, string Ground)
{
    /// <summary>Indoor weather describes conditions outside the shelter, not indoor particles.</summary>
    public bool HasVisiblePrecipitation => Domain == "outdoor" && Weather is "rain" or "snow";
}

public sealed record EnvironmentCategory(string Id, string Kor, string Eng);
public sealed record EnvironmentExample(string Id, string Kor, string Eng, EnvironmentSelection Selection);

/// <summary>Versioned category vocabulary. Loading is explicit and never modifies existing maps.</summary>
public sealed class EnvironmentCategories
{
    public const string DataPath = "res://content/data/environment_categories.json";
    public IReadOnlyDictionary<string, EnvironmentCategory> Domains { get; private init; } = EmptyCategories();
    public IReadOnlyDictionary<string, EnvironmentCategory> IndoorLight { get; private init; } = EmptyCategories();
    public IReadOnlyDictionary<string, EnvironmentCategory> OutdoorTime { get; private init; } = EmptyCategories();
    public IReadOnlyDictionary<string, EnvironmentCategory> Weather { get; private init; } = EmptyCategories();
    public IReadOnlyDictionary<string, EnvironmentCategory> Ground { get; private init; } = EmptyCategories();
    public IReadOnlyDictionary<string, EnvironmentExample> Examples { get; private init; } =
        new ReadOnlyDictionary<string, EnvironmentExample>(new Dictionary<string, EnvironmentExample>());
    public IReadOnlyList<string> Errors { get; private init; } = Array.AsReadOnly(Array.Empty<string>());

    private EnvironmentCategories() { }
    private static IReadOnlyDictionary<string, EnvironmentCategory> EmptyCategories() =>
        new ReadOnlyDictionary<string, EnvironmentCategory>(new Dictionary<string, EnvironmentCategory>());

    public static EnvironmentCategories Load(string path = DataPath) => FileAccess.FileExists(path)
        ? FromText(FileAccess.GetFileAsString(path), path)
        : new EnvironmentCategories { Errors = Array.AsReadOnly(new[] { path + ": file not found" }) };

    public static EnvironmentCategories FromText(string text, string where = DataPath)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            Fields(root, "version", "domain", "indoor_light", "outdoor_time", "weather", "ground", "examples");
            if (root.GetProperty("version").GetInt32() != 1) throw new FormatException("version must be 1");
            var examples = new Dictionary<string, EnvironmentExample>(StringComparer.Ordinal);
            var catalog = new EnvironmentCategories
            {
                Domains = Categories(root, "domain", "indoor", "outdoor"),
                IndoorLight = Categories(root, "indoor_light", "dark", "daylit"),
                OutdoorTime = Categories(root, "outdoor_time", "dawn", "morning", "noon", "sunset", "dusk", "night"),
                Weather = Categories(root, "weather", "clear", "sunny", "overcast", "rain", "snow"),
                Ground = Categories(root, "ground", "dry", "wet", "snow_covered"),
                Examples = new ReadOnlyDictionary<string, EnvironmentExample>(examples),
            };
            foreach (var item in root.GetProperty("examples").EnumerateArray())
            {
                Fields(item, "id", "kor", "eng", "selection");
                var selection = item.GetProperty("selection");
                Fields(selection, "domain", "indoor_light", "outdoor_time", "weather", "ground");
                var value = new EnvironmentSelection(Required(selection, "domain"), Optional(selection, "indoor_light"),
                    Optional(selection, "outdoor_time"), Required(selection, "weather"), Required(selection, "ground"));
                string id = Required(item, "id");
                var errors = catalog.Validate(value);
                if (errors.Count > 0) throw new FormatException($"example '{id}': {string.Join("; ", errors)}");
                if (!examples.TryAdd(id, new(id, Required(item, "kor"), Required(item, "eng"), value)))
                    throw new FormatException("duplicate example ID: " + id);
            }
            if (examples.Count == 0) throw new FormatException("examples must not be empty");
            return catalog;
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            // Never expose a partially valid catalog to future authoring consumers.
            return new EnvironmentCategories { Errors = Array.AsReadOnly(new[] { where + ": " + e.Message }) };
        }
    }

    /// <summary>Only domain/light compatibility is constrained. Weather and ground remain independent.</summary>
    public IReadOnlyList<string> Validate(EnvironmentSelection selection)
    {
        var errors = new List<string>();
        if (Errors.Count > 0) errors.Add("catalog is invalid");
        void Known(IReadOnlyDictionary<string, EnvironmentCategory> category, string? id, string name)
        {
            if (id == null || !category.ContainsKey(id)) errors.Add($"unknown or missing {name}: '{id}'");
        }
        Known(Domains, selection.Domain, "domain");
        Known(Weather, selection.Weather, "weather");
        Known(Ground, selection.Ground, "ground");
        if (selection.Domain == "indoor")
        {
            Known(IndoorLight, selection.IndoorLight, "indoor_light");
            if (selection.OutdoorTime != null) errors.Add("indoor requires outdoor_time to be omitted or null");
        }
        else if (selection.Domain == "outdoor")
        {
            Known(OutdoorTime, selection.OutdoorTime, "outdoor_time");
            if (selection.IndoorLight != null) errors.Add("outdoor requires indoor_light to be omitted or null");
        }
        return errors.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, EnvironmentCategory> Categories(JsonElement root, string key, params string[] requiredIds)
    {
        var result = new Dictionary<string, EnvironmentCategory>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty(key).EnumerateArray())
        {
            Fields(item, "id", "kor", "eng");
            string id = Required(item, "id");
            if (!requiredIds.Contains(id, StringComparer.Ordinal)) throw new FormatException($"unknown {key} ID: '{id}'");
            if (!result.TryAdd(id, new(id, Required(item, "kor"), Required(item, "eng"))))
                throw new FormatException($"duplicate {key} ID: '{id}'");
        }
        foreach (string id in requiredIds)
            if (!result.ContainsKey(id)) throw new FormatException($"missing {key} ID: '{id}'");
        return new ReadOnlyDictionary<string, EnvironmentCategory>(result);
    }

    private static string Required(JsonElement item, string key)
    {
        string? value = item.GetProperty(key).GetString();
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException(key + " must not be empty");
        return value;
    }

    private static string? Optional(JsonElement item, string key) => item.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static void Fields(JsonElement item, params string[] allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in item.EnumerateObject())
        {
            if (!seen.Add(field.Name)) throw new FormatException("duplicate field: " + field.Name);
            if (!allowed.Contains(field.Name, StringComparer.Ordinal)) throw new FormatException("unknown field: " + field.Name);
        }
    }
}
