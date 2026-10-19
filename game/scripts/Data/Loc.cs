using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Text lookup for multiple languages. Data files hold keys (name_weapon_ak47); the text lives in
/// content/lan/&lt;language&gt;/*.csv with the columns key,text. Every csv in a language folder is merged,
/// so tables can be split by topic (weapon.csv, monster.csv…). Missing text falls back to Korean, then
/// to the key itself, so a missing translation is visible but never crashes. See doc/localization.md.
/// </summary>
public static class Loc
{
    public const string Dir = "res://content/lan";
    public const string Fallback = "kor";

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new();
    private static string _language = Fallback;

    public static string Language
    {
        get => _language;
        set => _language = string.IsNullOrEmpty(value) ? Fallback : value;
    }

    public static IReadOnlyList<string> Languages => DirAccess.GetDirectoriesAt(Dir).OrderBy(d => d).ToList();

    public static IReadOnlyDictionary<string, string> Table(string language)
    {
        if (Tables.TryGetValue(language, out var table)) return table;
        table = new Dictionary<string, string>();
        var dir = $"{Dir}/{language}";
        if (DirAccess.DirExistsAbsolute(dir))
            foreach (var file in DirAccess.GetFilesAt(dir).Where(f => f.EndsWith(".csv")))
                foreach (var row in Csv.Parse(Godot.FileAccess.GetFileAsString($"{dir}/{file}")).rows)
                {
                    var key = row.Cells.GetValueOrDefault("key", "");
                    if (key != "") table[key] = row.Cells.GetValueOrDefault("text", "");
                }
        Tables[language] = table;
        return table;
    }

    public static bool Has(string key, string? language = null) => Table(language ?? Fallback).ContainsKey(key);

    /// <summary>Text for a key in the current language; plain text (not a key) is returned unchanged.</summary>
    public static string Text(string keyOrText)
    {
        if (string.IsNullOrEmpty(keyOrText)) return "";
        if (Table(_language).TryGetValue(keyOrText, out var text) && text != "") return text;
        if (Table(Fallback).TryGetValue(keyOrText, out text) && text != "") return text;
        return keyOrText;
    }

    public static string Format(string key, params object[] args) => string.Format(Text(key), args);

    /// <summary>Forget loaded tables (after editing language files).</summary>
    public static void Reload() => Tables.Clear();
}
