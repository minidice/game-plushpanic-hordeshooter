using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Look (and flight feel) of a throwable; the behaviour comes from its effects.</summary>
public enum ThrowableKind
{
    Frag,
    Fire,
    Flash,
    Lure,
}

/// <summary>One effect a throwable applies, from content/data/throwable_effects/*.json.</summary>
public abstract record ThrowEffect(string Name)
{
    /// <summary>Area this effect covers (shown as the aim ring).</summary>
    public abstract float Radius { get; }
}

/// <summary>Damage + push, falling off to the edge.</summary>
public sealed record ExplodeEffect(string Name, float RadiusM, float Damage, float KnockbackPower, float SelfDamageShare) : ThrowEffect(Name)
{
    public override float Radius => RadiusM;
}

/// <summary>Burning pool: damage per second to zombies (and to the player standing in it).</summary>
public sealed record BurnPoolEffect(string Name, float RadiusM, float Duration, float EnemyDps, float PlayerDps) : ThrowEffect(Name)
{
    public override float Radius => RadiusM;
}

/// <summary>Zombies in line of sight stand dazed.</summary>
public sealed record StunEffect(string Name, float RadiusM, float Duration) : ThrowEffect(Name)
{
    public override float Radius => RadiusM;
}

/// <summary>The thrower is blinded if close and looking at it.</summary>
public sealed record BlindSelfEffect(string Name, float RadiusM, float MaxSeconds, float FacingThreshold) : ThrowEffect(Name)
{
    public override float Radius => RadiusM;
}

/// <summary>After landing, beeps and pulls common zombies for a while; then the remaining effects fire.</summary>
public sealed record AttractEffect(string Name, float RadiusM, float Duration, float BeepInterval) : ThrowEffect(Name)
{
    // The pull radius is huge; the aim ring shows the effects that follow instead.
    public override float Radius => 0;
}

/// <summary>
/// One throwable (key G) after data resolution: a row of content/data/throwables.csv plus its effect
/// files. Built by <see cref="ThrowableDatabase"/>. Times are seconds, distances metres.
/// </summary>
public sealed class ThrowableDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string DisplayName => Loc.Text(NameKey);
    public ThrowableKind Kind { get; set; } = ThrowableKind.Frag;
    public int MaxCarry { get; set; } = 2;
    public float MaxRange { get; set; } = 16;
    /// <summary>Seconds from the throw to detonation (0 = decided by its effects: impact or attract).</summary>
    public float Fuse { get; set; }
    public bool DetonateOnImpact { get; set; }
    public List<ThrowEffect> Effects { get; set; } = new();
    public Color Color { get; set; } = new("5d6b3a");
    public string ModelPath { get; set; } = "";
    public string IconPath { get; set; } = "";

    public AttractEffect? Attract => Effects.OfType<AttractEffect>().FirstOrDefault();
    /// <summary>Everything that happens at detonation (all effects except the attract phase).</summary>
    public IEnumerable<ThrowEffect> DetonationEffects => Effects.Where(e => e is not AttractEffect);
    /// <summary>Radius of the aim ring: the largest area any effect covers.</summary>
    public float AimRadius => Effects.Count == 0 ? 0 : Effects.Max(e => e.Radius);

    public PackedScene? LoadModel() => ModelPath != "" && ResourceLoader.Exists(ModelPath) ? ResourceLoader.Load<PackedScene>(ModelPath) : null;
}
