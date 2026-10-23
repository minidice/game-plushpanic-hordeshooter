using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter;

/// <summary>What may break an object: any gunfire (and blasts), or only blasts (grenades, rockets, mortar, barrels).</summary>
public enum BreakableBy { Gun, Explosive }

/// <summary>Kinds of harm an object can take (melee goes over low cover and does not break it).</summary>
public enum ObjectDamage { Gun, Explosion }

/// <summary>An object that breaks: what it takes and how much (object_interactions.json <c>destruction</c>).</summary>
public sealed record ObjectDestruction(BreakableBy BreakableBy, float MaxHealth)
{
    public bool Accepts(ObjectDamage kind) => BreakableBy == BreakableBy.Gun || kind == ObjectDamage.Explosion;
}

public sealed record ObjectInteraction(string Profile, bool BlocksMovement, bool BlocksProjectiles, bool Cover = false, ObjectDestruction? Destruction = null)
{
    // World keeps existing floors/walls and gun ray masks compatible. Movement-only geometry is transparent to gun rays.
    // Low cover has its own layer: walking stops at it, sight and melee pass over it (Layers.Cover); shots meet its screen.
    public uint CollisionLayer => Cover ? Layers.Cover : BlocksProjectiles ? Layers.World : BlocksMovement ? Layers.MovementOnly : 0;
}

/// <summary>Shared runtime/Studio collision policy. Shape and placement still belong to PropLibrary/StageMap.</summary>
public sealed class ObjectInteractions
{
    public const string DataPath = "res://content/data/object_interactions.json";
    private static ObjectInteractions? _current;
    public static ObjectInteractions Current => _current ??= Parse(FileAccess.GetFileAsString(DataPath));
    private readonly Dictionary<string, ObjectInteraction> _profiles = new();
    private readonly Dictionary<string, string> _objects = new();
    public ObjectInteraction DefaultSolid { get; private set; } = null!;
    /// <summary>
    /// A shooter whose body is closer to low cover than this many of its own body widths fires over it
    /// (decisions.md C-29: one body width — room for another survivor in between and the cover counts again).
    /// </summary>
    public float ShootOverGapBodies { get; private set; } = 1f;

    public ObjectInteraction? ForProp(PropInfo info)
    {
        if (_objects.TryGetValue(info.Id, out var profile)) return _profiles[profile];
        return info.Collision.Kind is "box" or "cylinder" ? DefaultSolid : null;
    }

    public static ObjectInteractions Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = new ObjectInteractions();
        foreach (var profile in root.GetProperty("profiles").EnumerateObject())
        {
            var p = profile.Value;
            bool movement = p.GetProperty("blocks_movement").GetBoolean();
            bool projectiles = p.GetProperty("blocks_projectiles").GetBoolean();
            // This first contract covers solid structures, movement-only fences and nonblocking decoration.
            if (projectiles && !movement) throw new FormatException("Projectile-only object profiles are not supported: " + profile.Name);
            bool cover = p.TryGetProperty("cover", out var c) && c.GetBoolean();
            if (cover && !(movement && projectiles)) throw new FormatException("Low cover must block movement and projectiles: " + profile.Name);
            ObjectDestruction? destruction = null;
            if (p.TryGetProperty("destruction", out var d))
            {
                var by = d.GetProperty("breakable_by").GetString() switch
                {
                    "gun" => BreakableBy.Gun,
                    "explosive" => BreakableBy.Explosive,
                    var other => throw new FormatException($"breakable_by must be gun or explosive: {profile.Name} ({other})"),
                };
                float health = d.GetProperty("max_health").GetSingle();
                if (health <= 0) throw new FormatException("max_health must be positive: " + profile.Name);
                destruction = new ObjectDestruction(by, health);
            }
            if (string.IsNullOrWhiteSpace(profile.Name) || !result._profiles.TryAdd(profile.Name, new(profile.Name, movement, projectiles, cover, destruction)))
                throw new FormatException("Empty or duplicate object profile: " + profile.Name);
        }
        string defaultId = root.GetProperty("default_solid_profile").GetString() ?? "";
        if (!result._profiles.TryGetValue(defaultId, out var defaultRule)) throw new FormatException("Unknown default object profile: " + defaultId);
        result.DefaultSolid = defaultRule;
        foreach (var item in root.GetProperty("objects").EnumerateObject())
        {
            string profile = item.Value.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(item.Name) || !result._profiles.ContainsKey(profile) || !result._objects.TryAdd(item.Name, profile))
                throw new FormatException("Invalid object profile binding: " + item.Name);
        }
        if (root.TryGetProperty("cover_rules", out var rules))
        {
            result.ShootOverGapBodies = rules.GetProperty("shoot_over_gap_bodies").GetSingle();
            if (result.ShootOverGapBodies < 0) throw new FormatException("shoot_over_gap_bodies must not be negative");
        }
        return result;
    }
}
