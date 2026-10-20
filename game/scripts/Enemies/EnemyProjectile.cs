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
    /// <summary>Gunshots are a thin streak trailing behind the bullet's head (I-66).</summary>
    public const float TracerLength = .5f, TracerRadius = .025f;

    public override void _Ready()
    {
        // Vomit is a slow blob you are meant to watch and step away from.
        if (_shot.HazardSeconds > 0)
        {
            Mat.Mesh(this, new SphereMesh { Radius = .13f, Height = .26f }, Vector3.Zero, Mat.Glow(new Color("b1df3a")));
            return;
        }
        // Gunshots: the windup is the warning (C-13). The bullet itself is fast and should not glow like a ball.
        if (Mathf.Abs(_direction.Dot(Vector3.Up)) < .99f) LookAt(GlobalPosition + _direction, Vector3.Up);
        var streak = Mat.Mesh(this, new CylinderMesh { TopRadius = TracerRadius, BottomRadius = TracerRadius * .4f, Height = TracerLength, RadialSegments = 6, Rings = 1 },
            new Vector3(0, 0, TracerLength * .5f), Mat.Glow(new Color("f2dcb0"), 1.1f));
        streak.RotationDegrees = new Vector3(-90, 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_stage.NearestAlivePlayer(GlobalPosition) == null) { QueueFree(); return; }
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
