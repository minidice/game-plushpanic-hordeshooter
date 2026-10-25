using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HordeShooter;

// Glasses keeps its reserved wire value; new identities cannot equip it.
[Flags] public enum CommonGear { None = 0, Vest = 1, Glasses = 2 }
/// <summary>Value fixed at birth; never selected again by appearance, AI, snapshots or late join.</summary>
public sealed record CommonIdentity(string Faction, string Species, string Palette, string Weapon, int Variant = 0, CommonGear Gear = CommonGear.None);
public sealed record CommonSpecies(string Id, string Look, float Hp, float Speed, float Mass, float DamageScale, float WindupScale, float WakeRange, float TurnRate);
public sealed record CommonPalette(string Id, string[] Primary, string Secondary, string Accent, string Cloth, string Pants);
public sealed record CommonWeapon(string Id, string[] Factions, float Damage, float Reach, float Interval, float Windup, float Range, float ProjectileSpeed, string SharedWeapon);
public sealed record CommonPreset(string Id, string[] Modules);
public sealed record CommonPack(string Id, string Faction, string Species, string Palette, string[] Weapons);
public sealed record CommonRosterDocument(int Version, CommonSpecies[] Species, CommonPalette[] Palettes, CommonWeapon[] Weapons, CommonPreset[] Presets, CommonPack[] Packs);

/// <summary>Single validated source for runtime and Studio. Invalid catalogs fail closed at composition.</summary>
public sealed partial class CommonRoster
{
    public const string DataPath = "res://content/data/common_roster.json";
    private static CommonRoster? _current;
    public static CommonRoster Current => _current ??= FromText(FileAccess.GetFileAsString(DataPath));
    public static void Reload() => _current = null;
    public IReadOnlyDictionary<string, CommonSpecies> Species { get; private set; } = new Dictionary<string, CommonSpecies>();
    public IReadOnlyDictionary<string, CommonPalette> Palettes { get; private set; } = new Dictionary<string, CommonPalette>();
    public IReadOnlyDictionary<string, CommonWeapon> Weapons { get; private set; } = new Dictionary<string, CommonWeapon>();
    public IReadOnlyDictionary<string, CommonPack> Packs { get; private set; } = new Dictionary<string, CommonPack>();
    public IReadOnlyList<CommonPreset> Presets { get; private set; } = Array.Empty<CommonPreset>();
    private readonly List<string> _errors = new();
    public IReadOnlyList<string> Errors => _errors;
    private readonly Dictionary<string, CharacterDefinition> _looks = new();
    private static readonly HashSet<string> Modules = new() { "Outfit_Jeans", "Outfit_Chinos", "Outfit_Torn", "Outfit_Overalls", "Outfit_Suit", "Outfit_Shirt", "Outfit_Stains", "Outfit_Pattern", "Gear_Eyepatch" };

    public static CommonRoster FromText(string text)
    {
        var c = new CommonRoster();
        try
        {
            using (var json = JsonDocument.Parse(text)) RejectDuplicates(json.RootElement, "root");
            var d = JsonSerializer.Deserialize<CommonRosterDocument>(text, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new FormatException("empty catalog");
            if (d.Version != 1) throw new FormatException("version must be 1");
            c.Species = Unique(d.Species, s => s.Id); c.Palettes = Unique(d.Palettes, p => p.Id);
            c.Weapons = Unique(d.Weapons, w => w.Id); c.Packs = Unique(d.Packs, p => p.Id);
            c.Presets = d.Presets ?? throw new FormatException("presets missing");
            _ = Unique(d.Presets, p => p.Id);
            if (c.Species.Count != 7 || new[] { "human", "eagle", "parrot", "lion", "tiger", "reindeer", "rhino" }.Any(id => !c.Species.ContainsKey(id))) throw new FormatException("requires human and six animal species");
            if (c.Presets.Count != 10 || c.Presets.Select(p => p.Id).Distinct().Count() != 10) throw new FormatException("requires ten unique zombie presets");
            foreach (var s in c.Species.Values)
            {
                if (string.IsNullOrWhiteSpace(s.Look) || !Positive(s.Hp, s.Mass, s.DamageScale, s.WindupScale, s.WakeRange, s.TurnRate) || !float.IsFinite(s.Speed) || s.Speed < 4.55f || s.Speed > 4.75f)
                    throw new FormatException($"species {s.Id}: invalid stats (speed 4.55..4.75)");
                string path = $"{CharacterCatalog.EnemyDir}/{s.Look}.tres";
                if (!s.Look.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_') || !ResourceLoader.Exists(path) || ContentCache.Load(path) is not CharacterDefinition look || look.Id != s.Look || look.Model == null)
                    throw new FormatException($"species {s.Id}: exact look resource/model missing: {s.Look}");
            }
            foreach (var p in c.Palettes.Values)
            {
                if (p.Primary == null || p.Primary.Length < 1 || p.Primary.Length > 10) throw new FormatException($"palette {p.Id}: 1..10 reviewed shades required");
                foreach (var color in p.Primary.Concat(new[] { p.Secondary, p.Accent, p.Cloth, p.Pants }))
                    if (color == null || color.Length != 6 || !color.All(Uri.IsHexDigit)) throw new FormatException($"palette {p.Id}: invalid RGB hex");
            }
            foreach (var w in c.Weapons.Values)
            {
                if (w.Factions == null || w.Factions.Length == 0 || w.Factions.Any(f => f is not ("zombie" or "hunter" or "armed")) || !Positive(w.Damage, w.Reach, w.Interval, w.Windup) || w.Range < 0 || w.ProjectileSpeed < 0 || !float.IsFinite(w.Range + w.ProjectileSpeed)) throw new FormatException($"weapon {w.Id}: invalid stats/factions");
                if ((w.Range > 0) != (w.ProjectileSpeed > 0)) throw new FormatException($"weapon {w.Id}: ranged requires projectile speed");
                if (w.SharedWeapon is not ("" or "mp5" or "ak47" or "m4")) throw new FormatException($"weapon {w.Id}: unknown shared weapon");
            }
            foreach (var p in c.Presets)
            {
                if (p.Modules == null || p.Modules.Distinct().Count() != p.Modules.Length || p.Modules.Any(m => !Modules.Contains(m))) throw new FormatException($"preset {p.Id}: unknown/duplicate module");
                if (p.Modules.Count(m => m is "Outfit_Jeans" or "Outfit_Chinos" or "Outfit_Torn" or "Outfit_Overalls" or "Outfit_Suit") > 1) throw new FormatException($"preset {p.Id}: overlapping pants");
                if (p.Modules.Any(m => m is "Outfit_Stains" or "Outfit_Pattern") && !p.Modules.Contains("Outfit_Shirt")) throw new FormatException($"preset {p.Id}: decoration requires Shirt");
            }
            foreach (var p in c.Packs.Values)
            {
                if (p.Weapons == null || p.Weapons.Length == 0) throw new FormatException($"pack {p.Id}: empty weapons");
                foreach (var w in p.Weapons) c.ValidateIdentity(new(p.Faction, p.Species, p.Palette, w));
            }
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException or InvalidOperationException or NullReferenceException)
        { c._errors.Add("common_roster.json: " + e.Message); }
        return c;
    }

    private static bool Positive(params float[] values) => values.All(v => float.IsFinite(v) && v > 0);
    private static Dictionary<string, T> Unique<T>(T[] items, Func<T, string> id)
    {
        if (items == null || items.Length == 0) throw new FormatException("missing/empty catalog array");
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            string key = id(item);
            if (string.IsNullOrWhiteSpace(key) || !key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') || !result.TryAdd(key, item)) throw new FormatException("empty, unsafe or duplicate id: " + key);
        }
        return result;
    }
    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>();
            foreach (var p in element.EnumerateObject()) { if (!seen.Add(p.Name)) throw new FormatException(path + ": duplicate key " + p.Name); RejectDuplicates(p.Value, path + "." + p.Name); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) RejectDuplicates(child, path);
    }

    public void ValidateIdentity(CommonIdentity id)
    {
        if (_errors.Count > 0) throw new InvalidOperationException(string.Join("; ", _errors));
        if (!Species.ContainsKey(id.Species) || !Palettes.ContainsKey(id.Palette) || !Weapons.TryGetValue(id.Weapon, out var weapon)) throw new ArgumentException("unknown species/palette/weapon");
        if (!weapon.Factions.Contains(id.Faction) || (id.Faction == "zombie") != (id.Species == "human") || (id.Faction == "zombie" && id.Weapon != "unarmed")) throw new ArgumentException("impossible faction/species/weapon combination");
        if (id.Faction == "zombie" && id.Palette is not ("green" or "ochre")) throw new ArgumentException("zombie clothing palettes are brown/green only");
        if (id.Variant is < 0 or > 9 || (id.Gear & ~CommonGear.Vest) != 0 || (id.Faction != "armed" && id.Gear != CommonGear.None)) throw new ArgumentException("invalid variant or gear (eyewear disabled)");
    }

    public MonsterDefinition Compose(CommonIdentity id)
    {
        ValidateIdentity(id);
        var s = Species[id.Species]; var w = Weapons[id.Weapon];
        return new MonsterDefinition
        {
            Id = id.Faction == "zombie" ? "humanoid" : id.Faction == "hunter" ? "wild_animal" : "armed_animal",
            CommonIdentity = id, LookId = s.Look, NameKey = "name_monster_" + s.Look, Tier = MonsterTier.Common,
            Behavior = w.Range > 0 ? MonsterBehavior.Shooter : MonsterBehavior.Common,
            Climb = id.Faction == "zombie" ? MonsterClimb.Wall : MonsterClimb.Ladder,
            Hp = s.Hp, MoveSpeed = s.Speed, Mass = s.Mass, WakeRange = s.WakeRange, TurnRate = s.TurnRate,
            AttackDamage = w.Damage * s.DamageScale, AttackReach = w.Reach, AttackInterval = w.Interval,
            MeleeWindup = w.Windup * s.WindupScale,
            Ranged = w.Range > 0 ? new RangedTrait(w.Range, w.Windup * s.WindupScale, w.Interval, w.ProjectileSpeed, 0, 0) : null,
        };
    }

    public CharacterDefinition LookFor(CommonIdentity id)
    {
        ValidateIdentity(id);
        // Only model/collider data is cached; per-identity modules and materials belong to the rig.
        string look = Species[id.Species].Look;
        if (!_looks.TryGetValue(look, out var def))
        {
            def = ContentCache.Load($"{CharacterCatalog.EnemyDir}/{look}.tres") as CharacterDefinition;
            if (def == null || def.Id != look || def.Model == null) throw new InvalidOperationException("missing exact common look: " + look);
            _looks[look] = def;
        }
        return def;
    }

    public CommonIdentity[] CreatePack(string packId, int count, int variantOffset = 0)
    {
        if (!Packs.TryGetValue(packId, out var p)) throw new ArgumentException("unknown pack: " + packId);
        return CreatePack(p, count, variantOffset);
    }

    public CommonIdentity[] CreatePack(CommonPack p, int count, int variantOffset = 0)
    {
        if (p.Weapons.Length == 0) throw new ArgumentException("empty pack");
        foreach (var weapon in p.Weapons) ValidateIdentity(new(p.Faction, p.Species, p.Palette, weapon));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return Enumerable.Range(0, count).Select(i => new CommonIdentity(p.Faction, p.Species, p.Palette, p.Weapons[i % p.Weapons.Length],
            (i + Math.Abs(variantOffset % 10)) % 10, p.Faction == "armed" && i % 3 == 0 ? CommonGear.Vest : CommonGear.None)).ToArray();
    }

    public static void WriteIdentity(NetWriter w, CommonIdentity? id)
    {
        w.U8(id == null ? 0 : 1); if (id == null) return;
        w.Str(id.Faction); w.Str(id.Species); w.Str(id.Palette); w.Str(id.Weapon); w.U8(id.Variant); w.U8((int)id.Gear);
    }
    public static CommonIdentity? ReadIdentity(NetReader r)
    {
        if (r.U8() == 0) return null;
        var id = new CommonIdentity(r.Str(), r.Str(), r.Str(), r.Str(), r.U8(), (CommonGear)r.U8());
        Current.ValidateIdentity(id); return id;
    }
}
