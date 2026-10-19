using Godot;

namespace HordeShooter;

public partial class VomitHazard : Node3D
{
    private Stage _stage = null!;
    private float _remaining, _radius, _damage, _tick;
    public void Setup(Stage stage, Vector3 at, float seconds, float radius, float damage)
    { _stage = stage; Position = at; _remaining = seconds; _radius = radius; _damage = damage; }
    public override void _Ready() => Mat.Mesh(this,
        new CylinderMesh { TopRadius = _radius, BottomRadius = _radius, Height = .035f }, Vector3.Zero,
        Mat.Fade(new Color(.42f, .66f, .08f, .7f)));
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _remaining -= dt;
        if (_remaining <= 0 || _stage.Player.IsDead) { QueueFree(); return; }
        _tick -= dt;
        if (_tick > 0) return;
        _tick = .6f;
        var player = _stage.Player;
        var offset = player.GlobalPosition - GlobalPosition;
        offset.Y = 0;
        if (offset.Length() <= _radius && player.GlobalPosition.Y < GlobalPosition.Y + .8f &&
            Rays.Clear(this, GlobalPosition + Vector3.Up * .2f, player.GlobalPosition + Vector3.Up * .2f, Layers.World))
            player.TakeDamage(_damage, Vector3.Zero, 0);
    }
}
