using Godot;

namespace HordeShooter;

/// <summary>Local visual physics. Freezing removes simulation work, not rendering/collision storage.</summary>
public partial class Corpse : RigidBody3D
{
    public const uint SceneryCollisionMask = Layers.World | Layers.Corpse;
    public CharacterRig? Rig { get; set; }
    public NamedCorpseRecord? NamedRecord { get; internal set; }
    private float _age;

    public override void _PhysicsProcess(double delta)
    {
        if (Freeze) return;
        _age += (float)delta;
        if (_age > 5f || (_age > 1.2f && LinearVelocity.LengthSquared() < .02f))
        {
            FreezeMode = FreezeModeEnum.Static;
            Freeze = true;
            Rig?.SetSquash(.2f);
            SetPhysicsProcess(false);
        }
    }
}
