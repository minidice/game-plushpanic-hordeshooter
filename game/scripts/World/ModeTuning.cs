using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

public sealed record SurvivalWave(float Duration, float Every, int Group);

public sealed record SurvivalTuning(float CoreHp, float Prep, float Break, int MaxAlive, IReadOnlyList<SurvivalWave> Waves)
{
    /// <summary>Seconds from the start to the end of the last wave (the clear).</summary>
    public float Total => Prep + Waves.Sum(w => w.Duration) + Break * System.Math.Max(0, Waves.Count - 1);
}

public sealed record SurvivorTuning(float BossAt, float SpawnEvery, int GroupStart, int GroupEnd, float BossGroupScale,
    float SpawnMin, float SpawnMax, float RestAfterNamed, IReadOnlyList<string> WeaponPool, int RackSize, float SpecialCooldown);

/// <summary>
/// Survival and Survivor timing from content/data/mode_rules.json (doc/menu-multiplayer-plan.md 3.1). Which monsters come
/// is in <see cref="ModeSpawnTable"/>, the boss's phases and skills in <see cref="BossDatabase"/>.
/// Bad or missing values fall back to the defaults below and are listed in <see cref="Errors"/> (decisions.md T-4).
/// </summary>
public sealed class ModeTuning
{
    public const string Path = "res://content/data/mode_rules.json";

    private static readonly string[] SurvivalKeys = { "core_hp", "prep_s", "break_s", "max_alive", "waves" };
    private static readonly string[] WaveKeys = { "duration_s", "every_s", "group" };
    private static readonly string[] SurvivorKeys =
        { "boss_at_s", "spawn_every_s", "group_start", "group_end", "boss_group_scale", "spawn_min_m", "spawn_max_m", "rest_after_named_s",
          "weapon_pool", "rack_size", "special_cooldown_s" };
    private static readonly string[] DefaultPool = { "mp5", "m4", "ak47", "m249", "r870", "benelli_m4", "svd", "alabong", "railgun" };

    private static readonly SurvivalWave DefaultWave = new(40, 4.5f, 5);

    public SurvivalTuning Survival { get; private set; } = new(900, 8, 10, 60, new[] { DefaultWave });
    public SurvivorTuning Survivor { get; private set; } = new(150, 1.3f, 3, 8, .5f, 14, 22, 12, DefaultPool, 3, 25);

    private readonly List<string> _errors = new();
    public IReadOnlyList<string> Errors => _errors;

    private static ModeTuning? _current;
    public static ModeTuning Current
    {
        get
        {
            if (_current != null) return _current;
            _current = FromText(FileAccess.FileExists(Path) ? FileAccess.GetFileAsString(Path) : "");
            foreach (var error in _current.Errors) GD.PushError("[modes] " + error);
            return _current;
        }
    }
    public static void Reload() => _current = null;

    public static ModeTuning FromText(string json)
    {
        var t = new ModeTuning();
        if (json.Length == 0) { t._errors.Add("mode_rules.json: missing"); return t; }
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            t.Unknown(root, "mode_rules.json", new[] { "survival", "survivor" });
            if (root.TryGetProperty("survival", out var survival)) t.ReadSurvival(survival);
            if (root.TryGetProperty("survivor", out var survivor)) t.ReadSurvivor(survivor);
        }
        catch (JsonException e) { t._errors.Add($"mode_rules.json: JSON 오류 — {e.Message}"); }
        return t;
    }

    private void Unknown(JsonElement obj, string where, string[] keys)
    {
        foreach (var p in obj.EnumerateObject())
            if (p.Name != "description" && !keys.Contains(p.Name)) _errors.Add($"{where}: unknown key '{p.Name}' (typo?)");
    }

    private void Positive(string where, params (string Key, float Value)[] values)
    {
        foreach (var (key, value) in values)
            if (value <= 0) _errors.Add($"{where}: {key} must be positive");
    }

    private void ReadSurvival(JsonElement s)
    {
        const string where = "mode_rules.json survival";
        Unknown(s, where, SurvivalKeys);
        var d = Survival;
        var f = new JsonFields(s, where, _errors);
        var waves = new List<SurvivalWave>();
        if (s.TryGetProperty("waves", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var w in list.EnumerateArray())
            {
                string at = $"{where} waves[{waves.Count}]";
                Unknown(w, at, WaveKeys);
                var wf = new JsonFields(w, at, _errors);
                var wave = new SurvivalWave(wf.Float("duration_s", DefaultWave.Duration), wf.Float("every_s", DefaultWave.Every), wf.Int("group", DefaultWave.Group));
                Positive(at, ("duration_s", wave.Duration), ("every_s", wave.Every), ("group", wave.Group));
                waves.Add(wave);
            }
        if (waves.Count == 0) { _errors.Add($"{where}: waves needs at least one wave"); waves.AddRange(d.Waves); }
        Survival = new SurvivalTuning(f.Float("core_hp", d.CoreHp), f.Float("prep_s", d.Prep), f.Float("break_s", d.Break), f.Int("max_alive", d.MaxAlive), waves);
        Positive(where, ("core_hp", Survival.CoreHp), ("prep_s", Survival.Prep), ("break_s", Survival.Break), ("max_alive", Survival.MaxAlive));
    }

    /// <summary>The rack pool: weapon ids that exist and are not melee (doc/mode-monster-tables.md 5장).</summary>
    private IReadOnlyList<string> ReadPool(JsonElement s, string where, IReadOnlyList<string> fallback)
    {
        if (!s.TryGetProperty("weapon_pool", out var list)) return fallback;
        if (list.ValueKind != JsonValueKind.Array) { _errors.Add($"{where}: weapon_pool must be a list of weapon ids"); return fallback; }
        var pool = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            var id = item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "";
            if (!WeaponDatabase.Current.Weapons.TryGetValue(id, out var def) || def.Mode == FireMode.Melee)
                _errors.Add($"{where}: weapon_pool '{id}' is not a gun in weapons.csv");
            else pool.Add(id);
        }
        return pool.Count > 0 ? pool : fallback;
    }

    private void ReadSurvivor(JsonElement s)
    {
        const string where = "mode_rules.json survivor";
        Unknown(s, where, SurvivorKeys);
        var d = Survivor;
        var f = new JsonFields(s, where, _errors);
        Survivor = new SurvivorTuning(f.Float("boss_at_s", d.BossAt), f.Float("spawn_every_s", d.SpawnEvery), f.Int("group_start", d.GroupStart),
            f.Int("group_end", d.GroupEnd), f.Float("boss_group_scale", d.BossGroupScale),
            f.Float("spawn_min_m", d.SpawnMin), f.Float("spawn_max_m", d.SpawnMax), f.Float("rest_after_named_s", d.RestAfterNamed),
            ReadPool(s, where, d.WeaponPool), f.Int("rack_size", d.RackSize), f.Float("special_cooldown_s", d.SpecialCooldown));
        if (Survivor.RackSize < 1 || Survivor.RackSize > Survivor.WeaponPool.Count) _errors.Add($"{where}: rack_size must be between 1 and the weapon_pool size");
        if (Survivor.SpecialCooldown <= 0) _errors.Add($"{where}: special_cooldown_s must be positive");
        Positive(where, ("boss_at_s", Survivor.BossAt), ("spawn_every_s", Survivor.SpawnEvery), ("group_start", Survivor.GroupStart),
            ("spawn_min_m", Survivor.SpawnMin));
        if (Survivor.RestAfterNamed < 0) _errors.Add($"{where}: rest_after_named_s must not be negative");
        if (Survivor.SpawnMax < Survivor.SpawnMin) _errors.Add($"{where}: spawn_max_m is smaller than spawn_min_m");
    }
}
