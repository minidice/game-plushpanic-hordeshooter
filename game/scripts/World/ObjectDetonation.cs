using Godot;

namespace HordeShooter;

/// <summary>Data-driven delayed detonation, outlives the broken body. Clients replay only the blast visuals.</summary>
public partial class ObjectDetonation : Node3D
{
    private Stage _stage = null!;
    private ObjectExplosion _data = null!;
    private Player? _by;
    private float _remaining;

    public static void Schedule(Stage stage, Vector3 at, ObjectExplosion data, Player? by)
    {
        var effect = new ObjectDetonation { Name = "ObjectDetonation", _stage = stage, _data = data, _by = by, _remaining = data.DelaySeconds };
        stage.AddChild(effect);
        effect.GlobalPosition = at;
    }

    public override void _PhysicsProcess(double delta)
    {
        _remaining -= (float)delta;
        if (_remaining > 0 || IsQueuedForDeletion()) return;
        Blast.Explode(_stage, GlobalPosition, _data.Radius, _data.Damage, new Color("ff9a3c"), this,
            knockbackPower: _data.Impulse, by: _by != null && IsInstanceValid(_by) ? _by : null);
        QueueFree();
    }
}
