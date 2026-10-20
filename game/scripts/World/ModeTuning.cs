using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

public sealed record SurvivalWave(float Duration, float Every, int Group, float ArmedPct, int Veterans);

public sealed record SurvivalTuning(float CoreHp, float Prep, float Break, int MaxAlive, IReadOnlyList<SurvivalWave> Waves)
{
    /// <summary>Seconds from the start to the end of the last wave (the clear).</summary>
    public float Total => Prep + Waves.Sum(w => w.Duration) + Break * System.Math.Max(0, Waves.Count - 1);
}

public sealed record BossTuning(float Scale, float SlamRange, float SlamRadius, float SlamWindup, float SlamDamage, float SlamCooldown,
    float SummonEvery, int SummonCount, float EnrageHpPct, float EnrageSpeed, float EnrageCooldownScale);

public sealed record SurvivorTuning(float BossAt, float SpawnEvery, int GroupStart, int GroupEnd, float BossGroupScale, float CommonHpScale,
    float SpawnMin, float SpawnMax, BossTuning Boss);

/// <summary>
/// Survival and Survivor numbers from content/data/mode_rules.json (doc/menu-multiplayer-plan.md 3.1).
/// Bad or missing values fall back to the defaults below and are listed in <see cref="Errors"/> (decisions.md T-4).
/// </summary>
public sealed class ModeTuning
{
    public const string Path = "res://content/data/mode_rules.json";

    private static readonly string[] SurvivalKeys = { "core_hp", "prep_s", "break_s", "max_alive", "waves" };
    private static readonly string[] WaveKeys = { "duration_s", "every_s", "group", "armed_pct", "veterans" };
    private static readonly string[] SurvivorKeys =
        { "boss_at_s", "spawn_every_s", "group_start", "group_end", "boss_group_scale", "common_hp_scale", "spawn_min_m", "spawn_max_m", "boss" };
    private static readonly string[] BossKeys =
    {
        "scale", "slam_range_m", "slam_radius_m", "slam_windup_s", "slam_damage", "slam_cooldown_s", "summon_every_s", "summon_count",
        "enrage_hp_pct", "enrage_speed", "enrage_cooldown_scale",
    };

    private static readonly SurvivalWave DefaultWave = new(40, 4.5f, 5, 0, 0);

    public SurvivalTuning Survival { get; private set; } = new(900, 8, 10, 60, new[] { DefaultWave });
    public SurvivorTuning Survivor { get; private set; } = new(150, 1.3f, 3, 8, .5f, .6f, 14, 22,
        new BossTuning(1.7f, 4.5f, 5.5f, 1.1f, 22, 6, 13, 6, 50, 1.35f, .6f));
    public BossTuning Boss => Survivor.Boss;

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
                var wave = new SurvivalWave(wf.Float("duration_s", DefaultWave.Duration), wf.Float("every_s", DefaultWave.Every),
                    wf.Int("group", DefaultWave.Group), Mathf.Clamp(wf.Float("armed_pct", 0), 0, 100), System.Math.Max(0, wf.Int("veterans", 0)));
                Positive(at, ("duration_s", wave.Duration), ("every_s", wave.Every), ("group", wave.Group));
                waves.Add(wave);
            }
        if (waves.Count == 0) { _errors.Add($"{where}: waves needs at least one wave"); waves.AddRange(d.Waves); }
        Survival = new SurvivalTuning(f.Float("core_hp", d.CoreHp), f.Float("prep_s", d.Prep), f.Float("break_s", d.Break), f.Int("max_alive", d.MaxAlive), waves);
        Positive(where, ("core_hp", Survival.CoreHp), ("prep_s", Survival.Prep), ("break_s", Survival.Break), ("max_alive", Survival.MaxAlive));
    }

    private void ReadSurvivor(JsonElement s)
    {
        const string where = "mode_rules.json survivor";
        Unknown(s, where, SurvivorKeys);
        var d = Survivor;
        var f = new JsonFields(s, where, _errors);
        var boss = d.Boss;
        if (s.TryGetProperty("boss", out var b))
        {
            const string at = where + " boss";
            Unknown(b, at, BossKeys);
            var bf = new JsonFields(b, at, _errors);
            boss = new BossTuning(bf.Float("scale", boss.Scale), bf.Float("slam_range_m", boss.SlamRange), bf.Float("slam_radius_m", boss.SlamRadius),
                bf.Float("slam_windup_s", boss.SlamWindup), bf.Float("slam_damage", boss.SlamDamage), bf.Float("slam_cooldown_s", boss.SlamCooldown),
                bf.Float("summon_every_s", boss.SummonEvery), bf.Int("summon_count", boss.SummonCount), bf.Float("enrage_hp_pct", boss.EnrageHpPct),
                bf.Float("enrage_speed", boss.EnrageSpeed), bf.Float("enrage_cooldown_scale", boss.EnrageCooldownScale));
            Positive(at, ("scale", boss.Scale), ("slam_radius_m", boss.SlamRadius), ("slam_windup_s", boss.SlamWindup),
                ("slam_cooldown_s", boss.SlamCooldown), ("summon_every_s", boss.SummonEvery), ("enrage_speed", boss.EnrageSpeed));
        }
        Survivor = new SurvivorTuning(f.Float("boss_at_s", d.BossAt), f.Float("spawn_every_s", d.SpawnEvery), f.Int("group_start", d.GroupStart),
            f.Int("group_end", d.GroupEnd), f.Float("boss_group_scale", d.BossGroupScale), f.Float("common_hp_scale", d.CommonHpScale),
            f.Float("spawn_min_m", d.SpawnMin), f.Float("spawn_max_m", d.SpawnMax), boss);
        Positive(where, ("boss_at_s", Survivor.BossAt), ("spawn_every_s", Survivor.SpawnEvery), ("group_start", Survivor.GroupStart),
            ("common_hp_scale", Survivor.CommonHpScale), ("spawn_min_m", Survivor.SpawnMin));
        if (Survivor.SpawnMax < Survivor.SpawnMin) _errors.Add($"{where}: spawn_max_m is smaller than spawn_min_m");
    }
}
