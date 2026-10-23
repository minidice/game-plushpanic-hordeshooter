using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>Common, named or boss (decisions.md T-21). Bosses keep their corpse like named ones.</summary>
public enum MonsterTier { Common, Named, Boss }

/// <summary>
/// How a body gets up a height tier (decisions.md L-16): ladders only (survivors, squadmates, armed animals), climbing
/// any climbable wall (zombies), jumping up to two tiers (leapers, beasts, bosses) or flying over (balloons).
/// </summary>
public enum MonsterClimb { Ladder, Wall, Jump, Fly }

public enum MonsterBehavior
{
    /// <summary>Common infected (<see cref="Enemy"/>).</summary>
    Common,
    /// <summary>Balloon parasite (<see cref="BalloonEnemy"/>), needs trait balloon_pop.</summary>
    Balloon,
    /// <summary>Headbutt bull (<see cref="BullEnemy"/>), needs trait bull_charge.</summary>
    Bull,
    Tether,
    Leaper,
    Spitter,
    Shooter,
    /// <summary>Survivor's skill boss (<see cref="BossEnemy"/>): a bull charge plus slam and summon, needs trait bull_charge.</summary>
    Boss,
}

public sealed record BalloonPopTrait(float TriggerRange, float InflateTime, float Radius, float HeliumTime, int SummonCount, float KnockbackPower);

public sealed record BullChargeTrait(float TriggerRange, float MinRange, float WindupTime, float Speed, float ChargeTime, float DazedTime,
    float Cooldown, float PlayerDamage, float DazedDamageMultiplier, float BowlPower);

public sealed record CaptureTrait(float Range, float Windup, float Cooldown, float Speed);
public sealed record RangedTrait(float Range, float Windup, float Cooldown, float ProjectileSpeed, float HazardSeconds, float HazardRadius);

/// <summary>
/// Monster stats from content/data/monsters.csv (+ monster_traits/*.json for special behaviour).
/// The look is a separate CharacterDefinition (content/enemies/&lt;look&gt;.tres). Times are seconds.
/// </summary>
public sealed class MonsterDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string DisplayName => Loc.Text(NameKey);
    public MonsterBehavior Behavior { get; set; }
    public MonsterTier Tier { get; set; }=MonsterTier.Common;
    /// <summary>How it gets up a height tier (the climb column, L-16).</summary>
    public MonsterClimb Climb { get; set; } = MonsterClimb.Wall;
    public string LookId { get; set; } = "";
    /// <summary>Use the player's species with its gear stripped (zombie version) instead of the look's own model.</summary>
    public bool MirrorPlayer { get; set; }
    public float Hp { get; set; } = 30;
    public float MoveSpeed { get; set; } = 4.5f;
    /// <summary>Divides knockback: 1 = normal, 5 = barely moves, 0.7 = light.</summary>
    public float Mass { get; set; } = 1;
    public float AttackDamage { get; set; } = 4;
    public float AttackInterval { get; set; } = 1;
    public float AttackReach { get; set; } = .4f;
    public float WakeRange { get; set; } = 6.5f;
    public BalloonPopTrait? BalloonPop { get; set; }
    public BullChargeTrait? BullCharge { get; set; }
    public CaptureTrait? Capture { get; set; }
    public RangedTrait? Ranged { get; set; }
    /// <summary>Named and boss corpses stay fixed and on their own timer; only commons are scenery (T-16, T-21).</summary>
    public bool KeepsCorpse => Tier != MonsterTier.Common;
    /// <summary>Named and boss monsters (the tier column): counted apart from commons, not lured or inflated by cards.</summary>
    public bool IsSpecial => Tier != MonsterTier.Common;
}

/// <summary>Loads <see cref="MonsterDefinition"/>s. Errors are collected with file/row context, not thrown.</summary>
public sealed class MonsterDatabase
{
    public const string TablePath = "res://content/data/monsters.csv";
    public const string TraitDir = "res://content/data/monster_traits";
    public const string CommonId = "plush";

    public static readonly string[] Columns =
    {
        "id", "name", "behavior", "look", "mirror_player", "hp", "move_speed_mps", "mass", "attack_damage",
        "attack_interval_ms", "attack_reach_m", "wake_range_m", "traits", "tier", "climb",
    };

    private static readonly Dictionary<string, string[]> TraitKeys = new()
    {
        ["balloon_pop"] = new[] { "trigger_range_m", "inflate_ms", "radius_m", "helium_ms", "summon_count", "knockback_power" },
        ["bull_charge"] = new[] { "trigger_range_m", "min_range_m", "windup_ms", "speed_mps", "duration_ms", "dazed_ms", "cooldown_ms",
                                  "player_damage", "dazed_damage_multiplier", "bowl_power" },
        ["capture"] = new[] { "range_m", "windup_ms", "cooldown_ms", "speed_mps" },
        ["ranged"] = new[] { "range_m", "windup_ms", "cooldown_ms", "projectile_speed_mps", "hazard_ms", "hazard_radius_m" },
    };

    public IReadOnlyDictionary<string, MonsterDefinition> Monsters => _items;
    public IReadOnlyList<string> Errors => _errors;

    private readonly Dictionary<string, MonsterDefinition> _items = new();
    private readonly Dictionary<string, (string kind, JsonElement json)> _traits = new();
    private readonly List<string> _errors = new();

    private static MonsterDatabase? _current;

    public static MonsterDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            var traits = new Dictionary<string, string>();
            foreach (var file in DirAccess.GetFilesAt(TraitDir))
                if (file.EndsWith(".json")) traits[file[..^5]] = Godot.FileAccess.GetFileAsString($"{TraitDir}/{file}");
            _current = FromText(Godot.FileAccess.GetFileAsString(TablePath), traits);
            foreach (var error in _current.Errors) GD.PushError("[monsters] " + error);
            return _current;
        }
    }

    public static MonsterDefinition Get(string id) =>
        Current.Monsters.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException($"monster '{id}' not in {TablePath}");

    public static void Reload() => _current = null;

    public static MonsterDatabase FromText(string csv, IReadOnlyDictionary<string, string> traitJson)
    {
        var db = new MonsterDatabase();
        foreach (var (name, text) in traitJson) db.LoadTrait(name, text);
        var (header, rows) = Csv.Parse(csv);
        foreach (var column in header)
            if (!Columns.Contains(column)) db._errors.Add($"monsters.csv: 알 수 없는 열 '{column}' (오타?)");
        foreach (var row in rows)
        {
            var id = row.Cells.GetValueOrDefault("id", "");
            var where = $"monsters.csv {row.Line}행 ({id})";
            if (id == "" || db._items.ContainsKey(id))
            {
                db._errors.Add($"{where}: id가 비었거나 중복입니다");
                continue;
            }
            var c = new CellReader(where, row.Cells, db._errors);
            var tier = row.Cells.GetValueOrDefault("tier", "").ToLowerInvariant();
            if (tier is not ("" or "common" or "named" or "boss")) db._errors.Add($"{where}: tier '{tier}' must be common/named/boss; common used");
            var climb = row.Cells.GetValueOrDefault("climb", "").ToLowerInvariant();
            if (climb is not ("" or "ladder" or "wall" or "jump" or "fly")) db._errors.Add($"{where}: climb '{climb}' must be ladder/wall/jump/fly; wall used");
            var m = new MonsterDefinition
            {
                Id = id,
                NameKey = row.Cells.GetValueOrDefault("name", ""),
                Behavior = c.Enum<MonsterBehavior>("behavior"),
                Tier = tier switch { "named" => MonsterTier.Named, "boss" => MonsterTier.Boss, _ => MonsterTier.Common },
                Climb = climb switch { "ladder" => MonsterClimb.Ladder, "jump" => MonsterClimb.Jump, "fly" => MonsterClimb.Fly, _ => MonsterClimb.Wall },
                LookId = row.Cells.GetValueOrDefault("look", id),
                MirrorPlayer = c.Bool("mirror_player", false),
                Hp = c.Float("hp", 30),
                MoveSpeed = c.Float("move_speed_mps", 4.5f),
                Mass = Math.Max(.05f, c.Float("mass", 1)),
                AttackDamage = c.Float("attack_damage", 4),
                AttackInterval = c.Float("attack_interval_ms", 1000) / 1000f,
                AttackReach = c.Float("attack_reach_m", .4f),
                WakeRange = c.Float("wake_range_m", 6.5f),
            };
            if (!Loc.Has(m.NameKey)) db._errors.Add($"{where}: 이름 키 '{m.NameKey}'가 content/lan/{Loc.Fallback}/ 에 없습니다");
            foreach (var name in row.Cells.GetValueOrDefault("traits", "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!db._traits.TryGetValue(name, out var trait))
                {
                    db._errors.Add($"{where}: 특성 '{name}' 파일이 monster_traits/에 없습니다");
                    continue;
                }
                var f = new JsonFields(trait.json, $"{where} 특성 {name}", db._errors);
                if (trait.kind == "balloon_pop")
                    m.BalloonPop = new BalloonPopTrait(f.Float("trigger_range_m", 2.3f), f.Float("inflate_ms", 700) / 1000f, f.Float("radius_m", 4.2f),
                        f.Float("helium_ms", 15000) / 1000f, f.Int("summon_count", 12), f.Float("knockback_power", 90));
                else if (trait.kind == "bull_charge")
                    m.BullCharge = new BullChargeTrait(f.Float("trigger_range_m", 11), f.Float("min_range_m", 2.5f), f.Float("windup_ms", 900) / 1000f,
                        f.Float("speed_mps", 12.5f), f.Float("duration_ms", 1500) / 1000f, f.Float("dazed_ms", 1900) / 1000f, f.Float("cooldown_ms", 5000) / 1000f,
                        f.Float("player_damage", 18), f.Float("dazed_damage_multiplier", 1.6f), f.Float("bowl_power", 130));
                else if (trait.kind == "capture")
                    m.Capture = new CaptureTrait(f.Float("range_m", 9), f.Float("windup_ms", 1100) / 1000f,
                        f.Float("cooldown_ms", 6000) / 1000f, f.Float("speed_mps", 12));
                else if (trait.kind == "ranged")
                    m.Ranged = new RangedTrait(f.Float("range_m", 13), f.Float("windup_ms", 1200) / 1000f,
                        f.Float("cooldown_ms", 4000) / 1000f, f.Float("projectile_speed_mps", 10),
                        f.Float("hazard_ms", 0) / 1000f, f.Float("hazard_radius_m", 0));
            }
            if (m.Behavior == MonsterBehavior.Balloon && m.BalloonPop == null) db._errors.Add($"{where}: behavior balloon에는 balloon_pop 특성이 필요합니다");
            if (m.Behavior is MonsterBehavior.Bull or MonsterBehavior.Boss && m.BullCharge == null) db._errors.Add($"{where}: behavior bull에는 bull_charge 특성이 필요합니다");
            if (m.Behavior is MonsterBehavior.Tether or MonsterBehavior.Leaper && m.Capture == null)
                db._errors.Add($"{where}: capture trait required");
            if (m.Behavior is MonsterBehavior.Spitter or MonsterBehavior.Shooter && m.Ranged == null)
                db._errors.Add($"{where}: ranged trait required");
            db._items[id] = m;
        }
        return db;
    }

    private void LoadTrait(string name, string text)
    {
        var where = $"monster_traits/{name}.json";
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.Clone();
            var kind = root.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
            if (!TraitKeys.TryGetValue(kind, out var keys))
            {
                _errors.Add($"{where}: 알 수 없는 kind '{kind}' (가능: {string.Join(", ", TraitKeys.Keys)})");
                return;
            }
            foreach (var prop in root.EnumerateObject())
                if (prop.Name is not ("kind" or "description") && !keys.Contains(prop.Name))
                    _errors.Add($"{where}: 알 수 없는 항목 '{prop.Name}' (가능: {string.Join(", ", keys)})");
            _traits[name] = (kind, root);
        }
        catch (JsonException e)
        {
            _errors.Add($"{where}: JSON 오류 — {e.Message}");
        }
    }
}
