using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>One AI squadmate role from content/data/squad.csv: spacing, how far it may chase, damage and weapon pool.</summary>
public sealed class SquadRole
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public float FollowDistance { get; init; } = 2.5f;
    public float ChaseAhead { get; init; } = 4f;
    public float DamageMultiplier { get; init; } = .7f;
    public IReadOnlyList<(string Weapon, int Weight)> PrimaryPool { get; init; } = new List<(string, int)>();
    public string Secondary { get; init; } = "m1911";

    /// <summary>Weighted pick from the pool (decisions.md M-14).</summary>
    public string PickPrimary(RandomNumberGenerator rng)
    {
        int total = PrimaryPool.Sum(p => p.Weight);
        if (total <= 0) return Secondary;
        int roll = rng.RandiRange(1, total);
        foreach (var (weapon, weight) in PrimaryPool)
        {
            roll -= weight;
            if (roll <= 0) return weapon;
        }
        return PrimaryPool[^1].Weapon;
    }
}

/// <summary>Numbers for following, catching up, healing and reviving (content/data/squad_rules.json).</summary>
public sealed class SquadRules
{
    public float CatchupDistance { get; init; } = 18;
    public float CatchupAfter { get; init; } = 8;
    public float CatchupBehindMin { get; init; } = 8;
    public float CatchupBehindMax { get; init; } = 42;
    public float HealBelow { get; init; } = .4f;
    public float HealSafeRadius { get; init; } = 6;
    public float PickupRadius { get; init; } = 8;
    public float AmmoLow { get; init; } = .3f;
    public float EngageRange { get; init; } = 24;
    public int DownsPerLife { get; init; } = 2;
    public float BleedOut { get; init; } = 30;
    public float BleedHit { get; init; } = 3;
    public float ReviveTime { get; init; } = 4;
    public float ReviveRange { get; init; } = 1.5f;
    public int ReviveHealth { get; init; } = 30;
    public float RespawnDelay { get; init; } = 60;
    public int RespawnsPerStage { get; init; } = 2;
    public int RespawnHealth { get; init; } = 50;
}

/// <summary>Loads squad roles and rules. Errors are collected with file/row context, never thrown.</summary>
public sealed class SquadDatabase
{
    public const string TablePath = "res://content/data/squad.csv";
    public const string RulesPath = "res://content/data/squad_rules.json";
    private static readonly string[] Columns = { "role", "name", "follow_distance_m", "chase_ahead_m", "damage_multiplier", "primary_pool", "secondary" };

    private readonly List<SquadRole> _roles = new();
    private readonly List<string> _errors = new();
    public IReadOnlyList<SquadRole> Roles => _roles;
    public SquadRules Rules { get; private set; } = new();
    public IReadOnlyList<string> Errors => _errors;

    private static SquadDatabase? _current;
    public static SquadDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            _current = FromText(FileAccess.GetFileAsString(TablePath), FileAccess.FileExists(RulesPath) ? FileAccess.GetFileAsString(RulesPath) : "");
            foreach (var error in _current.Errors) GD.PushError("[squad] " + error);
            return _current;
        }
    }
    public static void Reload() => _current = null;

    public static SquadDatabase FromText(string csv, string rulesJson)
    {
        var db = new SquadDatabase();
        var (header, rows) = Csv.Parse(csv);
        foreach (var column in header)
            if (!Columns.Contains(column)) db._errors.Add($"squad.csv: unknown column '{column}'");
        foreach (var row in rows)
        {
            string id = row.Cells.GetValueOrDefault("role", "");
            string where = $"squad.csv row {row.Line} ({id})";
            if (id == "" || db._roles.Any(r => r.Id == id)) { db._errors.Add($"{where}: role is empty or duplicated"); continue; }
            var c = new CellReader(where, row.Cells, db._errors);
            var pool = new List<(string, int)>();
            foreach (var entry in row.Cells.GetValueOrDefault("primary_pool", "").Split(';', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                string weapon = parts[0].Trim();
                int weight = parts.Length > 1 && int.TryParse(parts[1], out int w) ? w : 1;
                if (!WeaponDatabase.Current.Weapons.ContainsKey(weapon)) { db._errors.Add($"{where}: weapon '{weapon}' is not in weapons.csv"); continue; }
                if (weight <= 0) { db._errors.Add($"{where}: weight for '{weapon}' must be positive"); continue; }
                pool.Add((weapon, weight));
            }
            string secondary = row.Cells.GetValueOrDefault("secondary", "m1911");
            if (!WeaponDatabase.Current.Weapons.ContainsKey(secondary)) { db._errors.Add($"{where}: secondary '{secondary}' is not in weapons.csv"); secondary = "m1911"; }
            string name = row.Cells.GetValueOrDefault("name", "");
            if (name.Length > 0 && !Loc.Has(name)) db._errors.Add($"{where}: name key '{name}' is not in the Korean text table");
            db._roles.Add(new SquadRole
            {
                Id = id,
                NameKey = name,
                FollowDistance = c.Float("follow_distance_m", 2.5f),
                ChaseAhead = c.Float("chase_ahead_m", 4f),
                DamageMultiplier = c.Float("damage_multiplier", .7f),
                PrimaryPool = pool,
                Secondary = secondary,
            });
        }
        if (db._roles.Count == 0) db._errors.Add("squad.csv: no roles");
        if (rulesJson.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(rulesJson);
                var j = new JsonFields(doc.RootElement, "squad_rules.json", db._errors);
                var d = new SquadRules();
                db.Rules = new SquadRules
                {
                    CatchupDistance = j.Float("catchup_distance_m", d.CatchupDistance),
                    CatchupAfter = j.Float("catchup_after_s", d.CatchupAfter),
                    CatchupBehindMin = j.Float("catchup_behind_min_m", d.CatchupBehindMin),
                    CatchupBehindMax = j.Float("catchup_behind_max_m", d.CatchupBehindMax),
                    HealBelow = j.Float("heal_below", d.HealBelow),
                    HealSafeRadius = j.Float("heal_safe_radius_m", d.HealSafeRadius),
                    PickupRadius = j.Float("pickup_radius_m", d.PickupRadius),
                    AmmoLow = j.Float("ammo_low", d.AmmoLow),
                    EngageRange = j.Float("engage_range_m", d.EngageRange),
                    DownsPerLife = j.Int("downs_per_life", d.DownsPerLife),
                    BleedOut = j.Float("bleed_out_s", d.BleedOut),
                    BleedHit = j.Float("bleed_hit_s", d.BleedHit),
                    ReviveTime = j.Float("revive_s", d.ReviveTime),
                    ReviveRange = j.Float("revive_range_m", d.ReviveRange),
                    ReviveHealth = j.Int("revive_health", d.ReviveHealth),
                    RespawnDelay = j.Float("respawn_delay_s", d.RespawnDelay),
                    RespawnsPerStage = j.Int("respawns_per_stage", d.RespawnsPerStage),
                    RespawnHealth = j.Int("respawn_health", d.RespawnHealth),
                };
            }
            catch (System.Exception e) { db._errors.Add($"squad_rules.json: {e.Message}"); }
        }
        return db;
    }
}
