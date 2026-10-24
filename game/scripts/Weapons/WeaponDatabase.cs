using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>
/// Builds every <see cref="WeaponDefinition"/> from data:
///   content/data/weapon_types.csv   — defaults per weapon type (one row per type)
///   content/data/weapons.csv        — real weapons; an empty cell means "use the type's value"
///   content/data/weapon_traits/*.json — reusable special behaviour, attached by name in the traits column
/// Problems are collected in <see cref="Errors"/> (with file, line and column) instead of crashing, so a
/// typo in a spreadsheet shows up as a readable message. See doc/weapon-spec.md.
/// </summary>
public sealed class WeaponDatabase
{
    public const string TypesPath = "res://content/data/weapon_types.csv";
    public const string WeaponsPath = "res://content/data/weapons.csv";
    public const string TraitDir = "res://content/data/weapon_traits";

    public IReadOnlyDictionary<string, WeaponDefinition> Weapons => _weapons;
    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyDictionary<string, string> TraitKinds => _traitKinds;

    private readonly Dictionary<string, WeaponDefinition> _weapons = new();
    private readonly Dictionary<string, WeaponCategory> _typeCategories = new();
    private readonly List<string> _errors = new();
    private readonly Dictionary<string, JsonElement> _traits = new();
    private readonly Dictionary<string, string> _traitKinds = new();

    // Plain C# data only (no Godot objects), so a static cache is safe.
    private static WeaponDatabase? _current;

    public static WeaponDatabase Current
    {
        get
        {
            if (_current != null) return _current;
            _current = LoadFiles();
            foreach (var error in _current.Errors) GD.PushError("[weapons] " + error);
            return _current;
        }
    }

    /// <summary>Drop the cache so the next access re-reads the files (e.g. after editing data).</summary>
    public static void Reload() => _current = null;

    /// <summary>The category column of a weapon_types.csv row (null for an unknown or empty type).</summary>
    public WeaponCategory? CategoryOfType(string? typeId) =>
        typeId != null && _typeCategories.TryGetValue(typeId, out var category) ? category : null;

    private static WeaponDatabase LoadFiles()
    {
        var traits = new Dictionary<string, string>();
        foreach (var file in DirAccess.GetFilesAt(TraitDir))
            if (file.EndsWith(".json")) traits[file[..^5]] = Godot.FileAccess.GetFileAsString($"{TraitDir}/{file}");
        return FromText(Godot.FileAccess.GetFileAsString(TypesPath), Godot.FileAccess.GetFileAsString(WeaponsPath), traits);
    }

    // ---------- columns ----------

    /// <summary>Every column a type or weapon row may have. Order is the documented order.</summary>
    public static readonly string[] Columns =
    {
        "name", "slot", "category", "fire_mode", "automatic", "damage", "max_range_m", "effective_range_m", "falloff_min",
        "fire_interval_ms", "reload_ms", "reload_shell_ms", "mag_size", "max_mags", "start_mags", "refillable", "pierce",
        "spread_deg", "spread_per_shot_deg", "move_spread_deg", "max_spread_deg", "recoil_power", "hit_power",
        "traits", "fire_sound", "model", "icon", "color", "accent_color", "length_m", "noise_m",
        "move_speed_pct", "aim_range_mult", "aim_move_pct", "aim_focus_mult", "crit_mult", "caliber", "aim_view",
        "aim_spread_deg", "aim_time_s", "spread_recovery_dps", "recoil_up", "recoil_side", "recoil_return",
    };

    private static readonly string[] Required = { "name", "slot", "category", "fire_mode", "damage", "max_range_m", "fire_interval_ms" };

    private static readonly Dictionary<WeaponCategory, float> Thickness = new()
    {
        [WeaponCategory.Melee] = .07f, [WeaponCategory.Pistol] = .1f, [WeaponCategory.Smg] = .12f, [WeaponCategory.Rifle] = .12f,
        [WeaponCategory.Lmg] = .17f, [WeaponCategory.Shotgun] = .13f, [WeaponCategory.Sniper] = .1f, [WeaponCategory.Heavy] = .2f,
    };

    public static WeaponDatabase FromText(string typesCsv, string weaponsCsv, IReadOnlyDictionary<string, string> traitJson)
    {
        var db = new WeaponDatabase();
        db.LoadTraits(traitJson);

        var (typeHeader, typeRows) = Csv.Parse(typesCsv);
        db.CheckHeader("weapon_types.csv", typeHeader, "type");
        var types = new Dictionary<string, Csv.Row>();
        foreach (var row in typeRows)
        {
            var id = row.Cells.GetValueOrDefault("type", "");
            if (id == "") db._errors.Add($"weapon_types.csv {row.Line}행: type이 비어 있습니다");
            else if (!types.TryAdd(id, row)) db._errors.Add($"weapon_types.csv {row.Line}행: type '{id}'가 중복입니다");
            else if (Enum.TryParse<WeaponCategory>(row.Cells.GetValueOrDefault("category", ""), true, out var category)) db._typeCategories[id] = category;
        }

        var (weaponHeader, weaponRows) = Csv.Parse(weaponsCsv);
        db.CheckHeader("weapons.csv", weaponHeader, "id", "type");
        foreach (var row in weaponRows)
        {
            var id = row.Cells.GetValueOrDefault("id", "");
            var where = $"weapons.csv {row.Line}행 ({id})";
            if (id == "")
            {
                db._errors.Add($"weapons.csv {row.Line}행: id가 비어 있습니다");
                continue;
            }
            if (db._weapons.ContainsKey(id))
            {
                db._errors.Add($"{where}: id가 중복입니다");
                continue;
            }
            var typeId = row.Cells.GetValueOrDefault("type", "");
            if (!types.TryGetValue(typeId, out var type))
            {
                db._errors.Add($"{where}: type '{typeId}'가 weapon_types.csv에 없습니다");
                continue;
            }
            var def = db.Resolve(id, typeId, type.Cells, row.Cells, where);
            if (def != null) db._weapons[id] = def;
        }
        return db;
    }

    private void CheckHeader(string file, List<string> header, params string[] keys)
    {
        foreach (var key in keys)
            if (!header.Contains(key)) _errors.Add($"{file}: '{key}' 열이 없습니다");
        foreach (var column in header)
            if (!keys.Contains(column) && !Columns.Contains(column) && column != "type")
                _errors.Add($"{file}: 알 수 없는 열 '{column}' (오타?)");
    }

    // ---------- resolution ----------

    private WeaponDefinition? Resolve(string id, string typeId, Dictionary<string, string> type, Dictionary<string, string> own, string where)
    {
        // Empty weapon cell → type value. Traits merge instead of replacing.
        var cells = new Dictionary<string, string>();
        foreach (var column in Columns)
        {
            var mine = own.GetValueOrDefault(column, "");
            cells[column] = column == "traits" ? "" : mine != "" ? mine : type.GetValueOrDefault(column, "");
        }
        int errorsBefore = _errors.Count;
        foreach (var column in Required)
            if (cells[column] == "") _errors.Add($"{where}: '{column}' 값이 무기에도 분류에도 없습니다");
        if (_errors.Count > errorsBefore) return null;

        var p = new CellReader(where, cells, _errors);
        var def = new WeaponDefinition
        {
            Id = id,
            BaseId = id,
            TypeId = typeId,
            NameKey = cells["name"],
            Slot = p.Slot("slot"),
            Category = p.Enum<WeaponCategory>("category"),
            Mode = p.Mode("fire_mode"),
            Automatic = p.Bool("automatic", true),
            Damage = p.Float("damage", 0),
            MaxRange = p.Float("max_range_m", 30),
            FalloffMin = p.Float("falloff_min", .6f),
            FireInterval = p.Float("fire_interval_ms", 100) / 1000f,
            ReloadTime = p.Float("reload_ms", 2000) / 1000f,
            ShellReloadTime = Math.Max(0, p.Float("reload_shell_ms", 0)) / 1000f,
            MagSize = p.Int("mag_size", 0),
            Refillable = p.Bool("refillable", true),
            Pierce = Math.Max(1, p.Int("pierce", 1)),
            BaseSpread = p.Float("spread_deg", 1.5f),
            SpreadPerShot = p.Float("spread_per_shot_deg", 1.5f),
            MoveSpread = p.Float("move_spread_deg", 3f),
            MaxSpread = p.Float("max_spread_deg", 12f),
            AimTime = p.Float("aim_time_s", .4f),
            SpreadRecovery = p.Float("spread_recovery_dps", 18f),
            RecoilUp = p.Float("recoil_up", 0),
            RecoilSide = p.Float("recoil_side", 0),
            RecoilReturn = p.Float("recoil_return", 200),
            RecoilPower = p.Float("recoil_power", 0),
            HitPower = p.Float("hit_power", 20),
            FireSound = cells["fire_sound"],
            ModelPath = cells["model"],
            IconPath = cells["icon"],
            BodyColor = p.Color("color", new Color("2f3330")),
            AccentColor = p.Color("accent_color", new Color("6b5a3f")),
            Length = p.Float("length_m", .75f),
            Noise = p.Float("noise_m", 16f),
            MoveSpeedScale = p.Float("move_speed_pct", 100) / 100f,
            AimRangeMultiplier = p.Float("aim_range_mult", 1),
            AimMoveScale = p.Float("aim_move_pct", 50) / 100f,
            AimFocusMultiplier = p.Float("aim_focus_mult", 1),
            CritMultiplier = p.Float("crit_mult", 2.5f),
            Caliber = cells["caliber"],
            AimView = p.Float("aim_view", 1),
        };
        def.EffectiveRange = p.Float("effective_range_m", def.MaxRange);
        def.AimSpread = p.Float("aim_spread_deg", def.BaseSpread);
        if (def.AimSpread < 0 || def.AimSpread > def.BaseSpread) _errors.Add($"{where}: aim_spread_deg({def.AimSpread})는 0~spread_deg({def.BaseSpread})여야 합니다 (조준이 비조준보다 좁음)");
        if (def.AimTime < 0 || def.SpreadRecovery <= 0) _errors.Add($"{where}: aim_time_s는 0 이상, spread_recovery_dps는 0보다 커야 합니다");
        if (def.RecoilUp < 0 || def.RecoilSide < 0 || def.RecoilReturn < 0) _errors.Add($"{where}: recoil_up·recoil_side·recoil_return은 0 이상이어야 합니다");
        def.Thickness = Thickness[def.Category];
        if (def.Noise <= 0) _errors.Add($"{where}: noise_m은 0보다 커야 합니다 (소리 없는 무기는 아주 작은 값)");
        if (def.MoveSpeedScale <= 0 || def.AimMoveScale <= 0) _errors.Add($"{where}: move_speed_pct·aim_move_pct는 0보다 커야 합니다");
        if (def.AimRangeMultiplier < 1 || def.AimFocusMultiplier < 1 || def.CritMultiplier < 1)
            _errors.Add($"{where}: aim_range_mult·aim_focus_mult·crit_mult는 1 이상이어야 합니다");
        if (def.AimView is < 0 or > 3) _errors.Add($"{where}: aim_view는 0~3이어야 합니다 (0 = 조준해도 화면이 그대로, 1 = 소총)");

        // Slot 1 never runs dry (L4D rule); elsewhere "inf" also means unlimited.
        bool infinite = def.Slot == WeaponSlot.Secondary || p.IsInfinite("max_mags");
        def.InfiniteReserve = infinite;
        if (!infinite)
        {
            int maxMags = p.Int("max_mags", 0);
            def.ReserveMax = maxMags * def.MagSize;
            def.ReserveStart = Math.Min(def.ReserveMax, p.Int("start_mags", maxMags) * def.MagSize);
        }

        if (def.Mode != FireMode.Melee && def.MagSize < 1) _errors.Add($"{where}: mag_size는 1 이상이어야 합니다");
        if (!Loc.Has(def.NameKey)) _errors.Add($"{where}: 이름 키 '{def.NameKey}'가 content/lan/{Loc.Fallback}/ 에 없습니다");
        if (def.EffectiveRange > def.MaxRange) _errors.Add($"{where}: effective_range_m({def.EffectiveRange})가 max_range_m({def.MaxRange})보다 큽니다");

        var traits = MergeTraits(type.GetValueOrDefault("traits", ""), own.GetValueOrDefault("traits", ""), where);
        def.Traits = traits;
        JsonElement? dual = null;
        foreach (var name in traits)
        {
            var json = _traits[name];
            if (_traitKinds[name] == "dual_wield") dual = json;
            else ApplyTrait(def, name, json, where);
        }
        if (def.Mode == FireMode.Lob && !HasKind(traits, "lob")) _errors.Add($"{where}: fire_mode lob에는 lob 특성이 필요합니다");
        if (def.Mode == FireMode.Projectile && !HasKind(traits, "projectile")) _errors.Add($"{where}: fire_mode projectile에는 projectile 특성이 필요합니다");
        if (def.Mode == FireMode.Shout && !HasKind(traits, "shout")) _errors.Add($"{where}: fire_mode shout에는 shout 특성이 필요합니다");
        if (def.Mode == FireMode.Throw && !HasKind(traits, "throw")) _errors.Add($"{where}: fire_mode throw에는 throw 특성이 필요합니다");
        if (dual is { } d) def.DualVariant = MakeDual(def, d, where);
        return def;
    }

    private bool HasKind(List<string> traits, string kind) => traits.Any(t => _traitKinds[t] == kind);

    /// <summary>Type traits, then weapon traits. "-name" removes an inherited trait; a later trait of the same kind replaces an earlier one.</summary>
    private List<string> MergeTraits(string typeTraits, string ownTraits, string where)
    {
        var list = new List<string>();
        foreach (var raw in Split(typeTraits).Concat(Split(ownTraits)))
        {
            if (raw.StartsWith('-'))
            {
                list.Remove(raw[1..]);
                continue;
            }
            if (!_traits.ContainsKey(raw))
            {
                _errors.Add($"{where}: 특성 '{raw}' 파일이 weapon_traits/에 없습니다");
                continue;
            }
            list.RemoveAll(t => _traitKinds[t] == _traitKinds[raw]);
            list.Add(raw);
        }
        return list;
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ---------- traits ----------

    private static readonly Dictionary<string, string[]> TraitKeys = new()
    {
        ["melee_arc"] = new[] { "arc_deg", "max_targets" },
        ["pellets"] = new[] { "count", "spread_deg" },
        ["burst"] = new[] { "count", "interval_ms" },
        ["projectile"] = new[] { "speed_mps", "max_flight_ms" },
        ["lob"] = new[] { "flight_ms", "min_range_m", "setup_ms", "recover_ms" },
        ["charge"] = new[] { "charge_ms" },
        ["explode_on_hit"] = new[] { "radius_m", "damage", "self_damage_share", "knockback_power" },
        ["shout"] = new[] { "radius_m", "damage", "knockback_power" },
        ["throw"] = new[] { "throwable" },
        ["dual_wield"] = new[] { "name_format_key", "mag_multiplier", "fire_interval_multiplier", "spread_add_deg", "recoil_multiplier" },
    };

    private void LoadTraits(IReadOnlyDictionary<string, string> traitJson)
    {
        foreach (var (name, text) in traitJson)
        {
            var where = $"weapon_traits/{name}.json";
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement.Clone();
                if (!root.TryGetProperty("kind", out var kindEl) || kindEl.GetString() is not { } kind)
                {
                    _errors.Add($"{where}: \"kind\"가 없습니다");
                    continue;
                }
                if (!TraitKeys.TryGetValue(kind, out var keys))
                {
                    _errors.Add($"{where}: 알 수 없는 kind '{kind}' (가능: {string.Join(", ", TraitKeys.Keys)})");
                    continue;
                }
                foreach (var prop in root.EnumerateObject())
                    if (prop.Name is not ("kind" or "description") && !keys.Contains(prop.Name))
                        _errors.Add($"{where}: 알 수 없는 항목 '{prop.Name}' (가능: {string.Join(", ", keys)})");
                _traits[name] = root;
                _traitKinds[name] = kind;
            }
            catch (JsonException e)
            {
                _errors.Add($"{where}: JSON 오류 — {e.Message}");
            }
        }
    }

    private void ApplyTrait(WeaponDefinition def, string name, JsonElement json, string where)
    {
        var t = new JsonFields(json, $"{where} 특성 {name}", _errors);
        switch (_traitKinds[name])
        {
            case "melee_arc":
                def.MeleeArc = t.Float("arc_deg", 110);
                def.MeleeMaxTargets = t.Int("max_targets", 4);
                break;
            case "pellets":
                def.Pellets = Math.Max(1, t.Int("count", 8));
                def.PelletSpread = t.Float("spread_deg", def.BaseSpread);
                break;
            case "burst":
                def.BurstCount = Math.Max(1, t.Int("count", 3));
                def.BurstInterval = t.Float("interval_ms", 65) / 1000f;
                break;
            case "projectile":
                def.ProjectileSpeed = t.Float("speed_mps", 24);
                def.ProjectileLifetime = t.Float("max_flight_ms", 2500) / 1000f;
                break;
            case "lob":
                def.FlightTime = t.Float("flight_ms", 1300) / 1000f;
                def.MinRange = t.Float("min_range_m", 0);
                def.SetupTime = Math.Max(0, t.Float("setup_ms", 0)) / 1000f;
                def.RecoverTime = Math.Max(0, t.Float("recover_ms", 0)) / 1000f;
                break;
            case "charge":
                def.ChargeTime = Math.Max(0, t.Float("charge_ms", 700)) / 1000f;
                break;
            case "shout":
                def.ShoutRadius = t.Float("radius_m", 4);
                def.ShoutDamage = t.Float("damage", 0);
                def.ShoutPower = t.Float("knockback_power", 150);
                break;
            case "throw":
                // Checked against throwables.csv by the checks (the two tables load separately).
                def.ThrowableId = t.String("throwable", "");
                break;
            case "explode_on_hit":
                def.ExplosionRadius = t.Float("radius_m", 4);
                def.ExplosionDamage = t.Float("damage", 100);
                def.ExplosionSelfShare = t.Float("self_damage_share", .2f);
                def.ExplosionKnockback = t.Float("knockback_power", 150);
                break;
        }
    }

    private WeaponDefinition MakeDual(WeaponDefinition single, JsonElement json, string where)
    {
        var t = new JsonFields(json, $"{where} 특성 dual_wield", _errors);
        var dual = single.Clone();
        dual.Id = single.Id + "_dual";
        dual.Dual = true;
        dual.DualVariant = null;
        dual.DualFormatKey = t.String("name_format_key", "fmt_weapon_dual");
        if (!Loc.Has(dual.DualFormatKey)) _errors.Add($"{where}: 이름 형식 키 '{dual.DualFormatKey}'가 content/lan/{Loc.Fallback}/ 에 없습니다");
        dual.MagSize = (int)MathF.Round(single.MagSize * t.Float("mag_multiplier", 2));
        dual.FireInterval = single.FireInterval * t.Float("fire_interval_multiplier", .6f);
        dual.BaseSpread = single.BaseSpread + t.Float("spread_add_deg", 1);
        dual.AimSpread = single.AimSpread + t.Float("spread_add_deg", 1);
        dual.MaxSpread = single.MaxSpread + t.Float("spread_add_deg", 1);
        dual.RecoilPower = single.RecoilPower * t.Float("recoil_multiplier", 1.3f);
        return dual;
    }
}
