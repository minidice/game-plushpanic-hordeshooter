using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

public enum BossSkillKind
{
    /// <summary>The bull charge (numbers from the monster's bull_charge trait).</summary>
    Charge,
    /// <summary>Red disc fills, then everyone inside is hit and launched.</summary>
    Slam,
    /// <summary>Roar and call commons in a ring around itself.</summary>
    Summon,
    /// <summary>Wind up, then a fan of shots toward the target.</summary>
    FanShot,
}

/// <param name="Pose">Body motion while winding up (<see cref="CharacterRig.BossPoses"/>); for a charge, the pawing before it.</param>
/// <param name="RunPose">Charge only: the body motion while running.</param>
/// <param name="Leap">Slam only: how far it jumps toward the target before landing (0 = slams where it stands).</param>
/// <param name="LeapHeight">Slam only: how high the jump goes.</param>
public sealed record BossSkill(string Id, BossSkillKind Kind, float Range, float Radius, float Windup, float Damage, float Cooldown,
    int Count, float SpreadDegrees, float Speed, float HpScale, string Pose = "", string RunPose = "", float Leap = 0, float LeapHeight = 0);

/// <summary>One phase: starts when health falls to <see cref="FromHpPct"/>% and uses <see cref="Skills"/>.</summary>
/// <param name="Pose">Body motion when the phase starts (it stands still for <paramref name="PoseTime"/> seconds); empty = none.</param>
public sealed record BossPhase(float FromHpPct, IReadOnlyList<string> Skills, float SpeedScale, float CooldownScale, string NoticeKey,
    string Pose = "", float PoseTime = 0);

/// <summary>
/// An RPG-style boss (decisions.md M-24): phases by health and skills as data, content/data/bosses/&lt;monster id&gt;.json.
/// See doc/mode-monster-tables.md.
/// </summary>
public sealed class BossDefinition
{
    public string Id { get; init; } = "";
    /// <summary>Look and collider scale against the monster's look (.tres).</summary>
    public float Scale { get; init; } = 1;
    /// <summary>Highest health first: phase 0 starts at 100%.</summary>
    public IReadOnlyList<BossPhase> Phases { get; init; } = Array.Empty<BossPhase>();
    public IReadOnlyDictionary<string, BossSkill> Skills { get; init; } = new Dictionary<string, BossSkill>();

    /// <summary>The phase for a health fraction (0–1).</summary>
    public int PhaseAt(float healthFraction)
    {
        int phase = 0;
        for (int i = 0; i < Phases.Count; i++)
            if (healthFraction * 100f <= Phases[i].FromHpPct + .001f) phase = i;
        return phase;
    }
}

/// <summary>Loads every content/data/bosses/*.json. Problems become readable errors (decisions.md T-4).</summary>
public sealed class BossDatabase
{
    public const string Dir = "res://content/data/bosses";

    private static readonly Dictionary<BossSkillKind, string[]> KindKeys = new()
    {
        [BossSkillKind.Charge] = new[] { "pose", "run_pose" },
        [BossSkillKind.Slam] = new[] { "range_m", "radius_m", "windup_s", "damage", "cooldown_s", "pose", "leap_m", "leap_height_m" },
        [BossSkillKind.Summon] = new[] { "count", "cooldown_s", "hp_scale", "windup_s", "pose" },
        [BossSkillKind.FanShot] = new[] { "range_m", "windup_s", "damage", "cooldown_s", "count", "spread_deg", "speed_mps", "pose" },
    };

    private readonly Dictionary<string, BossDefinition> _bosses = new();
    private readonly List<string> _errors = new();
    public IReadOnlyDictionary<string, BossDefinition> Bosses => _bosses;
    public IReadOnlyList<string> Errors => _errors;

    private static BossDatabase? _current;
    public static BossDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            var files = new Dictionary<string, string>();
            if (DirAccess.DirExistsAbsolute(Dir))
                foreach (var file in DirAccess.GetFilesAt(Dir))
                    if (file.EndsWith(".json")) files[file[..^5]] = FileAccess.GetFileAsString($"{Dir}/{file}");
            _current = FromText(files);
            foreach (var error in _current.Errors) GD.PushError("[bosses] " + error);
            return _current;
        }
    }
    public static void Reload() => _current = null;

    /// <summary>The boss data for a monster id; a plain one-phase slam-and-charge kit when the file is missing.</summary>
    public static BossDefinition Get(string id) => Current._bosses.TryGetValue(id, out var boss) ? boss : Fallback(id);

    private static BossDefinition Fallback(string id) => new()
    {
        Id = id,
        Phases = new[] { new BossPhase(100, new[] { "charge" }, 1, 1, "") },
        Skills = new Dictionary<string, BossSkill> { ["charge"] = new("charge", BossSkillKind.Charge, 0, 0, 0, 0, 0, 0, 0, 0, 1) },
    };

    public static BossDatabase FromText(IReadOnlyDictionary<string, string> files)
    {
        var db = new BossDatabase();
        foreach (var (id, text) in files) db.Read(id, text);
        return db;
    }

    private static string Kebab(BossSkillKind kind) => kind switch { BossSkillKind.FanShot => "fan_shot", _ => kind.ToString().ToLowerInvariant() };

    private void Read(string id, string text)
    {
        string where = $"bosses/{id}.json";
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            foreach (var p in root.EnumerateObject())
                if (p.Name is not ("description" or "scale" or "phases" or "skills")) _errors.Add($"{where}: unknown key '{p.Name}' (typo?)");
            var f = new JsonFields(root, where, _errors);
            float scale = f.Float("scale", 1);
            if (scale <= 0) _errors.Add($"{where}: scale must be positive");

            var skills = new Dictionary<string, BossSkill>();
            if (root.TryGetProperty("skills", out var skillList) && skillList.ValueKind == JsonValueKind.Object)
                foreach (var s in skillList.EnumerateObject())
                {
                    string at = $"{where} skills.{s.Name}";
                    var kindName = s.Value.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
                    var kind = Enum.GetValues<BossSkillKind>().Cast<BossSkillKind?>().FirstOrDefault(x => Kebab(x!.Value) == kindName);
                    if (kind == null)
                    {
                        _errors.Add($"{at}: unknown kind '{kindName}' (가능: {string.Join(", ", KindKeys.Keys.Select(Kebab))})");
                        continue;
                    }
                    foreach (var p in s.Value.EnumerateObject())
                        if (p.Name is not ("kind" or "description") && !KindKeys[kind.Value].Contains(p.Name))
                            _errors.Add($"{at}: unknown key '{p.Name}' (가능: {string.Join(", ", KindKeys[kind.Value])})");
                    var sf = new JsonFields(s.Value, at, _errors);
                    var skill = new BossSkill(s.Name, kind.Value, sf.Float("range_m", 5), sf.Float("radius_m", 5),
                        sf.Float("windup_s", kind == BossSkillKind.Summon ? 0 : 1), sf.Float("damage", 10),
                        sf.Float("cooldown_s", 8), sf.Int("count", 6), sf.Float("spread_deg", 60), sf.Float("speed_mps", 14), sf.Float("hp_scale", 1),
                        Pose(sf.String("pose", ""), at), Pose(sf.String("run_pose", ""), at), sf.Float("leap_m", 0), sf.Float("leap_height_m", 0));
                    if (skill.Leap < 0 || skill.LeapHeight < 0) _errors.Add($"{at}: leap_m and leap_height_m must not be negative");
                    if (kind != BossSkillKind.Charge && (skill.Cooldown <= 0 || skill.Windup < 0 || skill.Count < 0))
                        _errors.Add($"{at}: cooldown_s must be positive, windup_s and count not negative");
                    skills[s.Name] = skill;
                }

            var phases = new List<BossPhase>();
            if (root.TryGetProperty("phases", out var phaseList) && phaseList.ValueKind == JsonValueKind.Array)
                foreach (var ph in phaseList.EnumerateArray())
                {
                    string at = $"{where} phases[{phases.Count}]";
                    foreach (var p in ph.EnumerateObject())
                        if (p.Name is not ("from_hp_pct" or "skills" or "speed" or "cooldown_scale" or "notice" or "pose" or "pose_s")) _errors.Add($"{at}: unknown key '{p.Name}' (typo?)");
                    var pf = new JsonFields(ph, at, _errors);
                    var names = ph.TryGetProperty("skills", out var arr) && arr.ValueKind == JsonValueKind.Array
                        ? arr.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>();
                    foreach (var name in names.Where(n => !skills.ContainsKey(n))) _errors.Add($"{at}: skill '{name}' is not in skills");
                    var notice = pf.String("notice", "");
                    if (notice != "" && !Loc.Has(notice)) _errors.Add($"{at}: notice key '{notice}' is not in content/lan/{Loc.Fallback}/");
                    var phase = new BossPhase(pf.Float("from_hp_pct", 100), names.Where(skills.ContainsKey).ToList(), pf.Float("speed", 1), pf.Float("cooldown_scale", 1), notice,
                        Pose(pf.String("pose", ""), at), pf.Float("pose_s", 0));
                    if (phase.PoseTime < 0) _errors.Add($"{at}: pose_s must not be negative");
                    if (phase.SpeedScale <= 0 || phase.CooldownScale <= 0) _errors.Add($"{at}: speed and cooldown_scale must be positive");
                    phases.Add(phase);
                }
            if (phases.Count == 0) _errors.Add($"{where}: phases needs at least one phase");
            else if (Math.Abs(phases[0].FromHpPct - 100) > .001f) _errors.Add($"{where}: the first phase must start at from_hp_pct 100");
            for (int i = 1; i < phases.Count; i++)
                if (phases[i].FromHpPct >= phases[i - 1].FromHpPct) _errors.Add($"{where}: phases must go down in from_hp_pct");
            _bosses[id] = new BossDefinition { Id = id, Scale = scale, Phases = phases.Count > 0 ? phases : Fallback(id).Phases, Skills = skills };
        }
        catch (JsonException e) { _errors.Add($"{where}: JSON 오류 — {e.Message}"); }
    }

    /// <summary>A pose name from the data, reported when the toolkit does not know it (then no pose).</summary>
    private string Pose(string name, string at)
    {
        if (name == "" || CharacterRig.BossPoses.Contains(name)) return name;
        _errors.Add($"{at}: unknown pose '{name}' (가능: {string.Join(", ", CharacterRig.BossPoses)})");
        return "";
    }
}
