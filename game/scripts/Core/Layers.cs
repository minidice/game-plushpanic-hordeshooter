namespace HordeShooter;

/// <summary>Physics layer bits. Keep in sync with [layer_names] in project.godot.</summary>
public static class Layers
{
    public const uint World = 1;
    public const uint Player = 2;
    public const uint Enemy = 4;
    public const uint Props = 8;
    public const uint Corpse = 16;
    public const uint MovementOnly = 32;
    /// <summary>
    /// Low cover (sandbags, barriers; object_interactions.json <c>cover</c>): its real, low shape. Blocks walking and
    /// thrown things, not sight or melee.
    /// </summary>
    public const uint Cover = 64;
    /// <summary>
    /// Low cover's shot screen: a tall invisible box over the same footprint that only shots meet, so a bullet at chest
    /// height stops at knee-high sandbags — unless the shooter is right behind them and fires over (<see cref="CoverRule"/>).
    /// </summary>
    public const uint CoverShot = 128;
    public const uint MovementWorld = World | MovementOnly | Cover;

    public const uint BulletMask = World | Enemy | Props | CoverShot;
    /// <summary>What a monster's shot stops at (survivors are added where the shot can hit them).</summary>
    public const uint EnemyShotMask = World | Props | CoverShot;
    public const uint SightMask = World;
    /// <summary>What stops a melee swing or a grab: walls and fences, not low cover (you hit over sandbags).</summary>
    public const uint MeleeMask = World | MovementOnly | Props;
}
