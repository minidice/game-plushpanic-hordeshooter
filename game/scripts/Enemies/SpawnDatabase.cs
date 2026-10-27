using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>
/// How a placed monster reacts (decisions.md L-5). Set per placement, not per monster kind:
/// the same zombie is dormant when it was waiting in the next area and a hunter when the director sent it.
/// </summary>
public enum Reaction
{
    /// <summary>Wakes only when very close, hit, in a noise radius, or when a neighbour wakes.</summary>
    Dormant,
    /// <summary>Dormant rules plus its own eyes: a survivor in sight range with nothing in between.</summary>
    Watcher,
    /// <summary>Awake from the start and comes from far away along the flow field.</summary>
    Hunter,
}

/// <summary>Numbers for reactions, spawning and loud devices (content/data/spawn_rules.json).</summary>
public sealed class SpawnRules
{
    public float AlertRadius { get; init; } = 6;
    public float SightRange { get; init; } = 14;
    public float TrailMin { get; init; } = 15;
    public float TrailMax { get; init; } = 40;
    public float ProtectRadius { get; init; } = 8;
    public float AlarmTime { get; init; } = 20;
    public float AlarmWaveEvery { get; init; } = 6;
    public int AlarmWaveMin { get; init; } = 6;
    public int AlarmWaveMax { get; init; } = 9;
    public float NoiseExplosion { get; init; } = 30;
    public float NoiseFire { get; init; } = 18;
    public float NoiseBalloon { get; init; } = 20;
    public float NoiseFlash { get; init; } = 25;
    public float NoiseAlarm { get; init; } = 60;
}

public enum EntryKind
{
    /// <summary>A fixed spot out of the way (alley, doorway, behind cover). Used off screen.</summary>
    Hidden,
    /// <summary>Top of a wall the survivors cannot climb: monsters jump down, so it may be on screen.</summary>
    Ledge,
    /// <summary>A spot on the route the survivors already walked (made at run time, never stored).</summary>
    Trail,
}

/// <summary>Where a wave may come from. Ledges stand on the wall top and jump toward <see cref="Dir"/>.</summary>
public sealed class SpawnEntry
{
    public EntryKind Kind { get; init; }
    /// <summary>Ground spot, or for a ledge the wall top (Y = its height).</summary>
    public Vector3 At { get; init; }
    public Vector3 Dir { get; init; } = Vector3.Back;
    /// <summary>Where a monster stands once it is on the street.</summary>
    public Vector3 Ground => new Vector3(At.X, 0, At.Z) + (Kind == EntryKind.Ledge ? Dir * 1.2f : Vector3.Zero);
}

public sealed record EncounterArea(string Id, float X0, float X1);

/// <summary>Monsters waiting in an area before anyone gets there.</summary>
public sealed class EncounterGroup
{
    public string Area { get; init; } = "";
    public Reaction Reaction { get; init; } = Reaction.Dormant;
    public int Count { get; init; }
    /// <summary>x0, z0 → size along X and Z.</summary>
    public Rect2 Rect { get; init; }
    /// <summary>Index into the theme's special pool, or -1 for commons.</summary>
    public int Special { get; init; } = -1;
    public Vector3 At { get; init; }
    /// <summary>"mission": not placed when the area fills but when the stage's mission ends, on its way back (decisions.md L-27).</summary>
    public string After { get; init; } = "";
}

/// <summary>
/// A mid-stage safe zone (decisions.md L-24): nothing waits within <see cref="NoPlace"/>, no wave appears within
/// <see cref="NoWave"/>, and timed raids hold while a survivor is within <see cref="Inside"/>. Not a closed room: monsters
/// already chasing still come in.
/// </summary>
public sealed record SafeZone(Vector3 At, float Inside, float NoPlace, float NoWave);

/// <summary>
/// Timed raids for a long stage (doc/stage2-floorplan.md 6장): mobs and specials on their own intervals, only while the
/// leader is in one of <see cref="Areas"/>, held at the start, after a crescendo and while anyone is in a safe zone.
/// </summary>
public sealed class RaidRules
{
    public HashSet<string> Areas { get; } = new();
    public float MobMin { get; init; } = 90;
    public float MobMax { get; init; } = 120;
    public float SpecialMin { get; init; } = 60;
    public float SpecialMax { get; init; } = 90;
    public float StartDelay { get; init; } = 60;
    public float AfterCrescendo { get; init; } = 45;
    /// <summary>Commons in one timed mob, before the party scaling (4 people = as written).</summary>
    public int MobCountMin { get; init; } = 8;
    public int MobCountMax { get; init; } = 12;
    /// <summary>Rest after a peak (intensity high or the leader hurt): no timed mob for this long (L4D 30-45 s, Alien Swarm 25-40 s).</summary>
    public float RestMin { get; init; } = 10;
    public float RestMax { get; init; } = 10;
    /// <summary>Walking into a raid area brings its first mob this soon (0 = keep the running timer). pacing-pressure-plan.md 4.2.</summary>
    public float FirstInAreaMin { get; init; }
    public float FirstInAreaMax { get; init; }
}

/// <summary>One stage's encounters (content/data/stages/&lt;stage&gt;_encounters.json).</summary>
public sealed class EncounterSet
{
    public Dictionary<string, CommonEncounterProfile> CommonProfiles { get; } = new();
    public List<EncounterArea> Areas { get; } = new();
    public List<EncounterGroup> Groups { get; } = new();
    public List<SpawnEntry> Entries { get; } = new();
    /// <summary>Stair and sewer landings: nothing is placed or spawned near them (decisions.md L-11).</summary>
    public List<Vector3> Protected { get; } = new();
    public List<SafeZone> SafeZones { get; } = new();
    /// <summary>Null: the director's own timers (Stage 1 and today's Stage 2).</summary>
    public RaidRules? Raids { get; set; }

    /// <summary>Area holding x (areas run along +X); before the first is 0, past the last is the last.</summary>
    public int AreaIndex(float x)
    {
        for (int i = 0; i < Areas.Count; i++)
            if (x < Areas[i].X1) return i;
        return Areas.Count - 1;
    }
}

/// <summary>Loads spawn rules and stage encounters. Errors are collected with file context, never thrown.</summary>
public sealed class SpawnDatabase
{
    public const string RulesPath = "res://content/data/spawn_rules.json";
    public const string Stage1Path = "res://content/data/stages/stage1_encounters.json";
    public const string Stage2Path = "res://content/data/stages/stage2_encounters.json";

    private static readonly string[] RuleKeys =
    {
        "alert_radius_m", "sight_range_m", "trail_min_m", "trail_max_m", "protect_radius_m", "alarm_s", "alarm_wave_every_s",
        "alarm_wave_min", "alarm_wave_max", "noise_explosion_m", "noise_fire_m", "noise_balloon_m", "noise_flash_m", "noise_alarm_m",
    };

    private readonly List<string> _errors = new();
    public SpawnRules Rules { get; private set; } = new();
    public EncounterSet Stage1 { get; private set; } = new();
    private EncounterSet? _stage2;
    /// <summary>Scenario Stage 2 (decisions.md L-14), read the first time it is played.</summary>
    public EncounterSet Stage2
    {
        get
        {
            if (_stage2 != null) return _stage2;
            int before = _errors.Count;
            string json = Read(Stage2Path);
            if (json.Length == 0) { _errors.Add("stage2_encounters.json: missing"); _stage2 = new EncounterSet(); }
            else _stage2 = ReadEncounters(json, "stage2_encounters.json");
            for (int i = before; i < _errors.Count; i++) GD.PushError("[spawn] " + _errors[i]);
            return _stage2;
        }
    }
    public IReadOnlyList<string> Errors => _errors;

    private readonly Dictionary<string, EncounterSet> _byPath = new();
    /// <summary>Any stage's encounters by file (the size draft's, decisions.md L-29), read once.</summary>
    public EncounterSet Load(string path)
    {
        if (_byPath.TryGetValue(path, out var set)) return set;
        int before = _errors.Count;
        string json = Read(path);
        string file = path.GetFile();
        if (json.Length == 0) { _errors.Add($"{file}: missing"); set = new EncounterSet(); }
        else set = ReadEncounters(json, file);
        for (int i = before; i < _errors.Count; i++) GD.PushError("[spawn] " + _errors[i]);
        return _byPath[path] = set;
    }

    private static SpawnDatabase? _current;
    public static SpawnDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            _current = FromText(Read(RulesPath), Read(Stage1Path));
            foreach (var error in _current.Errors) GD.PushError("[spawn] " + error);
            return _current;
        }
    }
    public static void Reload() => _current = null;

    private static string Read(string path) => FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : "";

    public static SpawnDatabase FromText(string rulesJson, string encountersJson)
    {
        var db = new SpawnDatabase();
        if (rulesJson.Length > 0) db.ReadRules(rulesJson);
        else db._errors.Add("spawn_rules.json: missing");
        if (encountersJson.Length > 0) db.Stage1 = db.ReadEncounters(encountersJson, "stage1_encounters.json");
        else db._errors.Add("stage1_encounters.json: missing");
        return db;
    }

    private void ReadRules(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var j = new JsonFields(doc.RootElement, "spawn_rules.json", _errors);
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Name != "description" && !RuleKeys.Contains(p.Name)) _errors.Add($"spawn_rules.json: unknown key '{p.Name}' (typo?)");
            var d = new SpawnRules();
            Rules = new SpawnRules
            {
                AlertRadius = j.Float("alert_radius_m", d.AlertRadius),
                SightRange = j.Float("sight_range_m", d.SightRange),
                TrailMin = j.Float("trail_min_m", d.TrailMin),
                TrailMax = j.Float("trail_max_m", d.TrailMax),
                ProtectRadius = j.Float("protect_radius_m", d.ProtectRadius),
                AlarmTime = j.Float("alarm_s", d.AlarmTime),
                AlarmWaveEvery = j.Float("alarm_wave_every_s", d.AlarmWaveEvery),
                AlarmWaveMin = j.Int("alarm_wave_min", d.AlarmWaveMin),
                AlarmWaveMax = j.Int("alarm_wave_max", d.AlarmWaveMax),
                NoiseExplosion = j.Float("noise_explosion_m", d.NoiseExplosion),
                NoiseFire = j.Float("noise_fire_m", d.NoiseFire),
                NoiseBalloon = j.Float("noise_balloon_m", d.NoiseBalloon),
                NoiseFlash = j.Float("noise_flash_m", d.NoiseFlash),
                NoiseAlarm = j.Float("noise_alarm_m", d.NoiseAlarm),
            };
            foreach (var (key, value) in new (string, float)[]
            {
                ("alert_radius_m", Rules.AlertRadius), ("sight_range_m", Rules.SightRange), ("trail_min_m", Rules.TrailMin),
                ("trail_max_m", Rules.TrailMax), ("protect_radius_m", Rules.ProtectRadius), ("alarm_s", Rules.AlarmTime),
                ("alarm_wave_every_s", Rules.AlarmWaveEvery), ("alarm_wave_min", Rules.AlarmWaveMin), ("alarm_wave_max", Rules.AlarmWaveMax),
                ("noise_explosion_m", Rules.NoiseExplosion), ("noise_fire_m", Rules.NoiseFire), ("noise_balloon_m", Rules.NoiseBalloon),
                ("noise_flash_m", Rules.NoiseFlash), ("noise_alarm_m", Rules.NoiseAlarm),
            })
                if (value <= 0) _errors.Add($"spawn_rules.json: {key} must be positive");
            if (Rules.AlarmWaveEvery is > 0 and < 1) _errors.Add("spawn_rules.json: alarm_wave_every_s below 1 s would spawn a wave almost every frame");
            if (Rules.TrailMax < Rules.TrailMin) _errors.Add("spawn_rules.json: trail_max_m is smaller than trail_min_m");
            if (Rules.AlarmWaveMax < Rules.AlarmWaveMin) _errors.Add("spawn_rules.json: alarm_wave_max is smaller than alarm_wave_min");
        }
        catch (System.Exception e) { _errors.Add($"spawn_rules.json: {e.Message}"); }
    }

    private EncounterSet ReadEncounters(string json, string file)
    {
        var set = new EncounterSet();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int i = 0;
            foreach (var a in Array(root, "areas"))
            {
                string where = $"{file} areas[{i++}]";
                var j = new JsonFields(a, where, _errors);
                string id = j.String("id", "");
                if (id == "" || set.Areas.Any(x => x.Id == id)) { _errors.Add($"{where}: id is empty or duplicated"); continue; }
                var area = new EncounterArea(id, j.Float("x0", 0), j.Float("x1", 0));
                if (area.X1 <= area.X0) _errors.Add($"{where}: x1 must be larger than x0");
                if (set.Areas.Count > 0 && Mathf.Abs(set.Areas[^1].X1 - area.X0) > .01f) _errors.Add($"{where}: must start where '{set.Areas[^1].Id}' ends");
                set.Areas.Add(area);
            }
            if (set.Areas.Count == 0) _errors.Add($"{file}: no areas");

            if (root.TryGetProperty("common_profiles", out var profiles))
                foreach (var profile in profiles.EnumerateObject())
                {
                    if (profile.Name is not ("zombie" or "animals" or "armed")) throw new System.FormatException("unknown common profile theme " + profile.Name);
                    var value = profile.Value;
                    string defaultPack = value.GetProperty("default_pack").GetString() ?? "";
                    if (!CommonRoster.Current.Packs.TryGetValue(defaultPack, out var pack)) throw new System.FormatException("unknown default pack " + defaultPack);
                    string faction = profile.Name == "animals" ? "hunter" : profile.Name;
                    if (pack.Faction != faction) throw new System.FormatException("default pack must match stage faction");
                    var areas = new Dictionary<string, string>();
                    foreach (var area in value.GetProperty("areas").EnumerateObject())
                    {
                        string packId = area.Value.GetProperty("pack").GetString() ?? "";
                        if (!set.Areas.Any(a => a.Id == area.Name) || !CommonRoster.Current.Packs.TryGetValue(packId, out var regional)) throw new System.FormatException("invalid regional pack/area " + area.Name);
                        bool exception = area.Value.TryGetProperty("faction_exception", out var exceptionValue) && exceptionValue.GetBoolean();
                        if (regional.Faction != faction && !exception) throw new System.FormatException("foreign faction requires explicit faction_exception");
                        areas.Add(area.Name, packId);
                    }
                    set.CommonProfiles.Add(profile.Name, new(defaultPack, areas));
                }

            i = 0;
            foreach (var g in Array(root, "groups"))
            {
                string where = $"{file} groups[{i++}]";
                var j = new JsonFields(g, where, _errors);
                string area = j.String("area", "");
                if (set.Areas.All(a => a.Id != area)) { _errors.Add($"{where}: area '{area}' is not in areas"); continue; }
                if (!ParseReaction(j.String("reaction", "dormant"), out var reaction)) { _errors.Add($"{where}: reaction must be dormant, watcher or hunter"); continue; }
                int special = j.Int("special", -1);
                string after = j.String("after", "");
                if (after is not ("" or "mission")) { _errors.Add($"{where}: after must be mission (or left out)"); continue; }
                if (special >= 0)
                {
                    if (Pair(g, "at", where) is not { } at) continue;
                    set.Groups.Add(new EncounterGroup { Area = area, Reaction = reaction, Special = special, At = new Vector3(at[0], 0, at[1]), After = after });
                    continue;
                }
                if (Numbers(g, "rect", where) is not { } r) continue;
                if (r.Length != 4) { _errors.Add($"{where}: rect must be [x0, x1, z0, z1]"); continue; }
                int count = j.Int("count", 0);
                if (count <= 0) { _errors.Add($"{where}: count must be positive"); continue; }
                if (r[1] <= r[0] || r[3] <= r[2]) { _errors.Add($"{where}: rect is [x0, x1, z0, z1] with x1 > x0 and z1 > z0"); continue; }
                set.Groups.Add(new EncounterGroup { Area = area, Reaction = reaction, Count = count, Rect = new Rect2(r[0], r[2], r[1] - r[0], r[3] - r[2]), After = after });
            }

            i = 0;
            foreach (var e in Array(root, "entries"))
            {
                string where = $"{file} entries[{i++}]";
                var j = new JsonFields(e, where, _errors);
                if (Pair(e, "at", where) is not { } at) continue;
                switch (j.String("kind", ""))
                {
                    case "hidden":
                        set.Entries.Add(new SpawnEntry { Kind = EntryKind.Hidden, At = new Vector3(at[0], 0, at[1]) });
                        break;
                    case "ledge":
                        float height = j.Float("height", 0);
                        if (height <= 0) { _errors.Add($"{where}: a ledge needs a positive height"); continue; }
                        var dir = Pair(e, "dir", where) is { } d ? new Vector3(d[0], 0, d[1]) : Vector3.Zero;
                        if (dir.LengthSquared() < .01f) { _errors.Add($"{where}: a ledge needs a jump dir"); continue; }
                        set.Entries.Add(new SpawnEntry { Kind = EntryKind.Ledge, At = new Vector3(at[0], height, at[1]), Dir = dir.Normalized() });
                        break;
                    default:
                        _errors.Add($"{where}: kind must be hidden or ledge");
                        break;
                }
            }

            i = 0;
            foreach (var p in Array(root, "protected"))
            {
                string where = $"{file} protected[{i++}]";
                if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() == 2 && p[0].ValueKind == JsonValueKind.Number && p[1].ValueKind == JsonValueKind.Number)
                    set.Protected.Add(new Vector3(p[0].GetSingle(), 0, p[1].GetSingle()));
                else _errors.Add($"{where}: must be [x, z]");
            }

            i = 0;
            foreach (var z in Array(root, "safe_zones"))
            {
                string where = $"{file} safe_zones[{i++}]";
                var j = new JsonFields(z, where, _errors);
                if (Pair(z, "at", where) is not { } at) continue;
                var zone = new SafeZone(new Vector3(at[0], 0, at[1]), j.Float("inside_m", 0), j.Float("no_place_m", 0), j.Float("no_wave_m", 0));
                if (zone.Inside <= 0 || zone.NoPlace < zone.Inside || zone.NoWave < zone.NoPlace)
                {
                    _errors.Add($"{where}: needs 0 < inside_m <= no_place_m <= no_wave_m");
                    continue;
                }
                set.SafeZones.Add(zone);
            }

            if (root.TryGetProperty("raids", out var raids))
            {
                string where = $"{file} raids";
                var j = new JsonFields(raids, where, _errors);
                var rules = new RaidRules
                {
                    MobMin = j.Float("mob_min_s", 90), MobMax = j.Float("mob_max_s", 120),
                    SpecialMin = j.Float("special_min_s", 60), SpecialMax = j.Float("special_max_s", 90),
                    StartDelay = j.Float("start_delay_s", 60), AfterCrescendo = j.Float("after_crescendo_s", 45),
                    MobCountMin = j.Int("mob_count_min", 8), MobCountMax = j.Int("mob_count_max", 12),
                    RestMin = j.Float("rest_min_s", 10), RestMax = j.Float("rest_max_s", 10),
                    FirstInAreaMin = j.Float("first_in_area_min_s", 0), FirstInAreaMax = j.Float("first_in_area_max_s", 0),
                };
                foreach (var a in Array(raids, "areas"))
                {
                    string id = a.GetString() ?? "";
                    if (set.Areas.All(x => x.Id != id)) _errors.Add($"{where}: area '{id}' is not in areas");
                    else rules.Areas.Add(id);
                }
                if (rules.MobMin <= 0 || rules.MobMax < rules.MobMin || rules.SpecialMin <= 0 || rules.SpecialMax < rules.SpecialMin)
                    _errors.Add($"{where}: intervals need 0 < min <= max");
                else set.Raids = rules;
            }
        }
        catch (System.Exception e) { _errors.Add($"{file}: {e.Message}"); }
        foreach (var g in set.Groups.Where(g => g.Special >= 0))
        {
            if (set.Protected.Any(p => new Vector2(p.X - g.At.X, p.Z - g.At.Z).Length() < Rules.ProtectRadius))
                _errors.Add($"{file}: the special in '{g.Area}' waits inside a protected landing");
            if (set.SafeZones.Any(z => new Vector2(z.At.X - g.At.X, z.At.Z - g.At.Z).Length() < z.NoPlace))
                _errors.Add($"{file}: the special in '{g.Area}' waits inside a safe zone");
        }
        return set;
    }

    public static bool ParseReaction(string text, out Reaction reaction)
    {
        reaction = text switch { "dormant" => Reaction.Dormant, "watcher" => Reaction.Watcher, "hunter" => Reaction.Hunter, _ => (Reaction)(-1) };
        return (int)reaction >= 0;
    }

    private static IEnumerable<JsonElement> Array(JsonElement root, string key) =>
        root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Array ? el.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();

    /// <summary>[x, z] on the ground plane; three numbers (x, y, z) are a mistake and reported.</summary>
    private float[]? Pair(JsonElement el, string key, string where)
    {
        if (Numbers(el, key, where) is not { } n) return null;
        if (n.Length == 2) return n;
        _errors.Add($"{where}: {key} must be [x, z]");
        return null;
    }

    private float[]? Numbers(JsonElement el, string key, string where)
    {
        if (el.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Number))
            return arr.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        _errors.Add($"{where}: {key} must be a list of numbers");
        return null;
    }
}
