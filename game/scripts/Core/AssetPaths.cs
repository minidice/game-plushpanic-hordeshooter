using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>
/// The asset paths code reaches by name, from content/data/asset_paths.json (decisions.md T-26, doc/work-process.md 1.2):
/// single files, name patterns ({0} filled by the code) and asset kit folders. Art moves or renames these files by
/// editing the list, not the code. Read once; errors are collected with the key, never thrown — a missing key gives "".
/// </summary>
public static class AssetPaths
{
    public const string FilePath = "res://content/data/asset_paths.json";
    private static Dictionary<string, string>? _files;
    private static Dictionary<string, string> _patterns = new();
    private static Dictionary<string, string[]> _folders = new();
    private static readonly List<string> _errors = new();
    public static IReadOnlyList<string> Errors { get { Load(); return _errors; } }

    public static IReadOnlyDictionary<string, string> Files => Load();
    public static IReadOnlyDictionary<string, string> Patterns { get { Load(); return _patterns; } }
    public static IReadOnlyDictionary<string, string[]> FolderLists { get { Load(); return _folders; } }

    /// <summary>One file's path.</summary>
    public static string File(string key) => Load().TryGetValue(key, out var path) ? path : Missing("files", key, "");

    /// <summary>A pattern's path for <paramref name="name"/> (a card name, a model id).</summary>
    public static string Pattern(string key, string name)
    {
        Load();
        return _patterns.TryGetValue(key, out var pattern) ? pattern.Replace("{0}", name) : Missing("patterns", key, "");
    }

    /// <summary>A list of asset folders.</summary>
    public static string[] Folders(string key)
    {
        Load();
        return _folders.TryGetValue(key, out var list) ? list : Missing("folders", key, System.Array.Empty<string>());
    }

    private static T Missing<T>(string kind, string key, T fallback)
    {
        string error = $"asset_paths.json: no {kind} entry '{key}'";
        if (!_errors.Contains(error)) { _errors.Add(error); GD.PushError("[assets] " + error); }
        return fallback;
    }

    private static Dictionary<string, string> Load()
    {
        if (_files != null) return _files;
        _files = new();
        var text = FileAccess.FileExists(FilePath) ? FileAccess.GetFileAsString(FilePath) : "";
        if (text == "") { _errors.Add("asset_paths.json is missing"); return _files; }
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("files", out var files))
                foreach (var p in files.EnumerateObject()) _files[p.Name] = p.Value.GetString() ?? "";
            if (root.TryGetProperty("patterns", out var patterns))
                foreach (var p in patterns.EnumerateObject())
                {
                    _patterns[p.Name] = p.Value.GetString() ?? "";
                    if (!_patterns[p.Name].Contains("{0}")) _errors.Add($"asset_paths.json: pattern '{p.Name}' has no {{0}}");
                }
            if (root.TryGetProperty("folders", out var folders))
                foreach (var p in folders.EnumerateObject()) _folders[p.Name] = p.Value.EnumerateArray().Select(v => v.GetString() ?? "").ToArray();
        }
        catch (JsonException e) { _errors.Add("asset_paths.json: " + e.Message); }
        foreach (var error in _errors) GD.PushError("[assets] " + error);
        return _files;
    }
}
