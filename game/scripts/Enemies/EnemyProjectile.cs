using Godot;

namespace HordeShooter;

/// <summary>Swept segment collision prevents fast enemy shots from passing through walls.</summary>
public partial class EnemyProjectile : Node3D
{
    private Stage _stage = null!;
    private Vector3 _direction;
    private RangedTrait _shot = null!;
    private float _remaining, _damage;
    public void Setup(Stage stage, Vector3 from, Vector3 target, RangedTrait shot, float damage)
    {
        _stage = stage; Position = from; _shot = shot; _damage = damage;
        _direction = (target - from).Normalized(); _remaining = from.DistanceTo(target);
    }
    public override void _Ready() => Mat.Mesh(this, new SphereMesh { Radius = .13f, Height = .26f }, Vector3.Zero,
        Mat.Glow(_shot.HazardSeconds > 0 ? new Color("b1df3a") : new Color("ffc768")));

    public override void _PhysicsProcess(double delta)
    {
        if (_stage.Player.IsDead) { QueueFree(); return; }
        float travel = Mathf.Min(_remaining, _shot.ProjectileSpeed * (float)delta);
        var next = GlobalPosition + _direction * travel;
        var hit = Rays.Cast(this, GlobalPosition, next, Layers.World | Layers.Props | Layers.Player);
        if (hit is { } collision)
        {
            if (collision.Collider is Player player) player.TakeDamage(_damage, _direction, .5f);
            Impact(collision.Position + collision.Normal * .16f);
            return;
        }
        GlobalPosition = next;
        _remaining -= travel;
        if (_remaining <= .001f) Impact(next);
    }
    private void Impact(Vector3 point)
    {
        if (_shot.HazardSeconds > 0)
        {
            var hazard = new VomitHazard();
            hazard.Setup(_stage, new Vector3(point.X, .06f, point.Z), _shot.HazardSeconds, _shot.HazardRadius, _damage * .5f);
            _stage.AddChild(hazard);
        }
        else _stage.Effects.Impact(point, -_direction);
        QueueFree();
    }
}
