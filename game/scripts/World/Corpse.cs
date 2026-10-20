using Godot;

namespace HordeShooter;

/// <summary>Cosmetic physics: common bodies sleep naturally and can wake; named visuals stay fixed.</summary>
public partial class Corpse : RigidBody3D
{
    public const uint SceneryCollisionMask = Layers.World | Layers.Corpse;
    /// <summary>
    /// Assisted sleep (I-71): bodies in a dense pile keep nudging each other and never reach the engine's own sleep
    /// threshold, and Jolt only lets a pile sleep when every body in it can. A body that has stayed this slow for
    /// <see cref="CalmSeconds"/> and was not kicked in the last <see cref="KickGrace"/> seconds is <see cref="ReadyToRest"/>;
    /// <see cref="Effects"/> puts all such bodies to sleep in the same step. They still wake when walked into or blasted.
    /// </summary>
    public const float CalmSpeed = .25f, CalmSpin = .6f, CalmSeconds = .8f, KickGrace = 1.5f;

    public CharacterRig? Rig { get; set; }
    public NamedCorpseRecord? NamedRecord { get; internal set; }
    private float _calm, _sinceKick;

    public override void _Ready()
    {
        SleepingStateChanged += OnSleepingChanged;
    }

    private void OnSleepingChanged()
    {
        Rig?.SetSquash(Sleeping ? .2f : 0);
        SetPhysicsProcess(!Sleeping && !Freeze);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Freeze || Sleeping) return;
        float dt = (float)delta;
        _sinceKick += dt;
        bool calm = LinearVelocity.LengthSquared() < CalmSpeed * CalmSpeed && AngularVelocity.LengthSquared() < CalmSpin * CalmSpin;
        _calm = calm ? _calm + dt : 0;
    }

    public bool ReadyToRest => !Sleeping && !Freeze && _calm >= CalmSeconds && _sinceKick >= KickGrace;

    public void Kick(Vector3 impulse, Vector3 offset)
    {
        if (NamedRecord != null || Freeze) return;
        Sleeping = false;
        _sinceKick = 0;
        _calm = 0;
        Rig?.SetSquash(0);
        SetPhysicsProcess(true);
        ApplyImpulse(impulse, offset);
    }
}
