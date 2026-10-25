using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public enum SpawnRole { Common, Named }

/// <summary>
/// One row of a mode's monster table. <see cref="Monster"/> is a monsters.csv id, or <c>@common</c> / <c>@special</c>
/// for the stage theme's own pools. <see cref="From"/>/<see cref="Until"/> are wave numbers in Outpost (survival) and
/// seconds of the run clock in Onslaught (survivor: the stretches end to end, standing still while a boss is out, see
/// <see cref="SurvivorRules.RunTime"/>); <see cref="Until"/> is exclusive, infinity when the cell is empty.
/// </summary>
public sealed record ModeSpawnRow(string Monster, SpawnRole Role, string Theme, float Weight, int Count, float From, float Until,
    float HpScale, float DamageScale)
{
    public bool Fits(string theme) => Theme == "any" || Theme == theme;
    public bool ActiveAt(float at) => at >= From && at < Until;
}

/// <summary>
/// Per-mode monster tables, content/data/modes/&lt;mode&gt;_spawns.csv (decisions.md M-24, doc/mode-monster-tables.md):
/// which commons fill the groups when, and which named ones arrive at a set wave or second.
/// </summary>
public sealed class ModeSpawnTable
{
    public const string Dir = "res://content/data/modes";
    public static readonly string[] Columns = { "monster", "role", "theme", "weight", "count", "from", "until", "hp_scale", "damage_scale" };
    private static readonly string[] Themes = { "any", "zombie", "animals", "armed" };

    public string Mode { get; private set; } = "";
    public IReadOnlyList<ModeSpawnRow> Rows => _rows;
    public IReadOnlyList<string> Errors => _errors;
    private readonly List<ModeSpawnRow> _rows = new();
    private readonly List<string> _errors = new();

    private static readonly Dictionary<GameMode, ModeSpawnTable> Cache = new();

    public static string FileFor(GameMode mode) => $"{Dir}/{mode.ToString().ToLowerInvariant()}_spawns.csv";

    public static ModeSpawnTable For(GameMode mode)
    {
        if (Cache.TryGetValue(mode, out var table)) return table;
        var path = FileFor(mode);
        table = FromText(mode.ToString().ToLowerInvariant(), FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : "");
        foreach (var error in table.Errors) GD.PushError("[mode spawns] " + error);
        return Cache[mode] = table;
    }

    public static void Reload() => Cache.Clear();

    public static ModeSpawnTable FromText(string mode, string csv)
    {
        var t = new ModeSpawnTable { Mode = mode };
        string file = $"{mode}_spawns.csv";
        if (csv.Length == 0) { t._errors.Add($"{file}: missing"); return t; }
        var (header, rows) = Csv.Parse(csv);
        foreach (var column in header)
            if (!Columns.Contains(column)) t._errors.Add($"{file}: 알 수 없는 열 '{column}' (오타?)");
        foreach (var row in rows)
        {
            var where = $"{file} {row.Line}행";
            var c = new CellReader(where, row.Cells, t._errors);
            var monster = row.Cells.GetValueOrDefault("monster", "");
            if (monster is not ("@common" or "@special") && !MonsterDatabase.Current.Monsters.ContainsKey(monster))
            {
                t._errors.Add($"{where}: monster '{monster}'가 monsters.csv에 없습니다 (@common·@special도 가능)");
                continue;
            }
            var roleText = row.Cells.GetValueOrDefault("role", "common");
            if (roleText is not ("common" or "named")) t._errors.Add($"{where}: role '{roleText}' must be common/named");
            var theme = row.Cells.GetValueOrDefault("theme", "any");
            if (theme == "") theme = "any";
            if (!Themes.Contains(theme)) t._errors.Add($"{where}: theme '{theme}' must be {string.Join("/", Themes)}");
            var untilText = row.Cells.GetValueOrDefault("until", "");
            var r = new ModeSpawnRow(monster, roleText == "named" ? SpawnRole.Named : SpawnRole.Common, theme,
                c.Float("weight", 0), c.Int("count", 1), c.Float("from", 0), untilText == "" ? float.PositiveInfinity : c.Float("until", float.PositiveInfinity),
                c.Float("hp_scale", 1), c.Float("damage_scale", 1));
            if (r.Role == SpawnRole.Common && r.Weight <= 0) t._errors.Add($"{where}: a common row needs a positive weight");
            if (r.Role == SpawnRole.Named && r.Count < 1) t._errors.Add($"{where}: a named row needs count 1 or more");
            if (r.HpScale <= 0 || r.DamageScale <= 0) t._errors.Add($"{where}: hp_scale and damage_scale must be positive");
            if (r.Until <= r.From) t._errors.Add($"{where}: until must be after from");
            t._rows.Add(r);
        }
        if (!t._rows.Any(r => r.Role == SpawnRole.Common)) t._errors.Add($"{file}: needs at least one common row");
        return t;
    }

    /// <summary>A common row by weight among those active at <paramref name="at"/> for the theme; null if none.</summary>
    public ModeSpawnRow? PickCommon(float at, string theme, RandomNumberGenerator rng)
    {
        var active = _rows.Where(r => r.Role == SpawnRole.Common && r.Fits(theme) && r.ActiveAt(at)).ToList();
        float total = active.Sum(r => r.Weight);
        if (total <= 0) return null;
        float roll = rng.Randf() * total;
        foreach (var r in active)
        {
            roll -= r.Weight;
            if (roll < 0) return r;
        }
        return active[^1];
    }

    /// <summary>Named rows whose start lies in (<paramref name="after"/>, <paramref name="upTo"/>] for the theme.</summary>
    public IEnumerable<ModeSpawnRow> NamedDue(float after, float upTo, string theme) =>
        _rows.Where(r => r.Role == SpawnRole.Named && r.Fits(theme) && r.From > after && r.From <= upTo);

    /// <summary>Spawns one row's monster at <paramref name="at"/>, awake, with the row's health and damage scale.</summary>
    public static Enemy Spawn(Stage stage, ModeSpawnRow row, Vector3 at)
    {
        var enemy = row.Role == SpawnRole.Common ? stage.SpawnCommon(at, awake: true) : row.Monster switch
        {
            "@common" => stage.SpawnCommon(at, awake: true),
            "@special" => stage.SpawnThemeSpecial(at, awake: true),
            _ => stage.Spawn(MonsterDatabase.Get(row.Monster), at, awake: true),
        };
        if (!Mathf.IsEqualApprox(row.HpScale, 1)) enemy.ScaleHealth(row.HpScale);
        if (!Mathf.IsEqualApprox(row.DamageScale, 1)) enemy.ScaleDamage(row.DamageScale);
        return enemy;
    }
}
