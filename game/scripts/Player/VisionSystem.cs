using Godot;

namespace HordeShooter;

/// <summary>
/// Fan-shaped vision (doc/idea.md 5.3 ①). Enemies outside the cone or behind walls are hidden;
/// hidden enemies that are moving nearby leave sonar ripples so the player hears what they can't see.
/// </summary>
public partial class VisionSystem : Node
{
    public const float NormalHalfAngle = 57f;
    public const float HeliumHalfAngle = 30f;
    public const float Range = 24f;
    public const float NearRadius = 2.6f;
    public const float SonarRadius = 15f;
    public const float EyeHeight = 1.3f;

    private Stage Stage => GetParent<Stage>();
    private int _cursor;

    public float HalfAngle => Stage.Player.HeliumTime > 0 ? HeliumHalfAngle : NormalHalfAngle;

    public bool CanSee(Vector3 point)
    {
        var player = Stage.Player;
        var eye = player.GlobalPosition + Vector3.Up * EyeHeight;
        var to = point - player.GlobalPosition;
        to.Y = 0;
        float dist = to.Length();
        if (dist < NearRadius) return true;
        if (dist > Range) return false;
        if (Mathf.RadToDeg(player.AimDirection.AngleTo(to / dist)) > HalfAngle) return false;
        return Rays.Clear(player, eye, new Vector3(point.X, eye.Y, point.Z), Layers.SightMask);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var enemies = Stage.Enemies;
        if (enemies.Count == 0) return;
        bool dead = Stage.Player.IsDead;
        // Re-test a third of the crowd per frame; plenty at 60 Hz and cheap with 70+ enemies.
        int batch = Mathf.Max(1, enemies.Count / 3 + 1);
        for (int i = 0; i < batch; i++)
        {
            _cursor = (_cursor + 1) % enemies.Count;
            var e = enemies[_cursor];
            if (e.IsDead) continue;
            e.IsSeen = dead || CanSee(e.GlobalPosition + Vector3.Up * .8f);
            e.Rig.Visible = e.IsSeen;
        }

        if (dead) return;
        var playerPos = Stage.Player.GlobalPosition;
        foreach (var e in enemies)
        {
            if (e.IsDead || e.IsSeen || !e.Awake) continue;
            e.PingTimer -= dt;
            if (e.PingTimer > 0) continue;
            e.PingTimer = e.IsSpecial ? .7f : 1.2f;
            if (e.GlobalPosition.DistanceTo(playerPos) < SonarRadius && e.Velocity.LengthSquared() > .2f)
            {
                Stage.Effects.SonarPing(e.GlobalPosition);
                Stage.Sfx.PlayAt("sonar", e.GlobalPosition + Vector3.Up, 0, e.IsSpecial ? .7f : 1f);
            }
        }
    }
}
