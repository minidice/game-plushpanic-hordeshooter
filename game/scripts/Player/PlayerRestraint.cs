using Godot;

namespace HordeShooter;

public partial class Player
{
    public Enemy? RestrainingEnemy { get; private set; }
    public bool IsRestrained => RestrainingEnemy != null;
    public Vector3? RestraintPullTarget { get; set; }

    public bool TryRestrain(Enemy owner)
    {
        if (IsDead || IsDowned || IsRolling || IsRestrained || owner.IsDead || owner.StunTime > 0 || owner.IsQueuedForDeletion()) return false;
        RestrainingEnemy = owner;
        RestraintPullTarget = null;
        IsHealing = false;
        _rollTime = 0;
        _push = Vector3.Zero;
        Velocity = Vector3.Zero;
        Gun.CancelReload();
        Weapons.EndThrowAim();
        Rig.ResetPose();
        return true;
    }

    public void ReleaseRestraint(Enemy? owner = null)
    {
        if (owner != null && RestrainingEnemy != owner) return;
        if (IsRestrained && IsInstanceValid(Rig) && !IsDead) Rig.ResetPose();
        RestrainingEnemy = null;
        RestraintPullTarget = null;
        _push = Vector3.Zero;
    }

    // Temporary local testing cheat. There is deliberately no timeout or normal self-escape.
    public bool DebugEscapeRestraint()
    {
        if (!GameSession.DebugEscapeEnabled || !IsRestrained) return false;
        var owner = RestrainingEnemy;
        ReleaseRestraint();
        if (owner != null && IsInstanceValid(owner) && !owner.IsDead) owner.Stun(2f);
        return true;
    }

    private void UpdateRestraint(float dt)
    {
        var owner = RestrainingEnemy;
        if (owner == null) return;
        if (IsDead || !IsInstanceValid(owner) || owner.IsDead || owner.IsQueuedForDeletion() || !owner.IsInsideTree())
        {
            ReleaseRestraint();
            return;
        }
    }

    private void ApplyRestraintPose()
    {
        if (RestrainingEnemy is CaptureEnemy { IsLeaper: true })
            Rig.Pose.Rotation = new Vector3(-1.15f, 0, 0);
    }

    private Vector3 RestraintVelocity()
    {
        if (RestraintPullTarget is not { } target) return Vector3.Zero;
        var to = target - GlobalPosition;
        to.Y = 0;
        return to.Length() > .8f ? to.Normalized() * Mathf.Min(3.5f, (to.Length() - .8f) * 4) : Vector3.Zero;
    }
}
