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
    public const uint MovementWorld = World | MovementOnly;

    public const uint BulletMask = World | Enemy | Props;
    public const uint SightMask = World;
}
