using Godot;

namespace HordeShooter;

public partial class VomitHazard : Node3D
{
    private Stage _stage = null!;
    private float _remaining, _radius, _damage, _tick;
    public void Setup(Stage stage, Vector3 at, float seconds, float radius, float damage)
    { _stage = stage; Position = at; _remaining = seconds; _radius = radius; _damage = damage; }
    public override void _EnterTree()
    {
        AddToGroup(SquadGroups.Hazard);
        SetMeta("radius", _radius);
    }
    public override void _Ready() => CombatSurfaceEffect.Create(this,
        CombatEffectCatalog.Current.ForSource("poison_pool"), _radius, _remaining);
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _remaining -= dt;
        if (_remaining <= 0 || _stage.NearestAlivePlayer(GlobalPosition) == null) { QueueFree(); return; }
        _tick -= dt;
        if (_tick > 0) return;
        _tick = .6f;
        foreach (var player in System.Linq.Enumerable.ToList(_stage.AlivePlayers))
        {
            var offset = player.GlobalPosition - GlobalPosition;
            offset.Y = 0;
            if (offset.Length() <= _radius && player.GlobalPosition.Y < GlobalPosition.Y + .8f &&
                Rays.Clear(this, GlobalPosition + Vector3.Up * .2f, player.GlobalPosition + Vector3.Up * .2f, Layers.World))
                player.TakeDamage(_damage, Vector3.Zero, 0);
        }
    }
}
