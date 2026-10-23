using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter;

public sealed record ObjectInteraction(string Profile, bool BlocksMovement, bool BlocksProjectiles)
{
    // World keeps existing floors/walls and gun ray masks compatible. Movement-only geometry is transparent to gun rays.
    public uint CollisionLayer => BlocksProjectiles ? Layers.World : BlocksMovement ? Layers.MovementOnly : 0;
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
            bool movement = profile.Value.GetProperty("blocks_movement").GetBoolean();
            bool projectiles = profile.Value.GetProperty("blocks_projectiles").GetBoolean();
            // This first contract covers solid structures, movement-only fences and nonblocking decoration.
            if (projectiles && !movement) throw new FormatException("Projectile-only object profiles are not supported: " + profile.Name);
            if (string.IsNullOrWhiteSpace(profile.Name) || !result._profiles.TryAdd(profile.Name, new(profile.Name, movement, projectiles)))
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
        return result;
    }
}
