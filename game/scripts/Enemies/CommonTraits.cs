using Godot;

namespace HordeShooter;

public partial class Enemy
{
    private void CommonWeaponPose()
    {
        if (Monster.CommonIdentity?.Weapon is not ("club" or "knife")) return;
        float elapsed = !IsMeleeAttacking ? -1 : _meleeTime < MeleeWindup
            ? _meleeTime / MeleeWindup * CharacterRig.MeleeImpactTime
            : CharacterRig.MeleeImpactTime + (_meleeTime - MeleeWindup) / MeleeRecovery * (CharacterRig.MeleeDuration - CharacterRig.MeleeImpactTime);
        Rig.SetMeleePose(elapsed);
    }
    private void FaceToward(Vector3 direction, float dt)
    {
        if (Monster.CommonIdentity == null || IsMeleeAttacking) { Rig.Face(direction); return; }
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return;
        float current = Rig.Rotation.Y, goal = Mathf.Atan2(direction.X, direction.Z);
        float difference = Mathf.AngleDifference(current, goal);
        Rig.Rotation = new Vector3(0, current + Mathf.Clamp(difference, -Monster.TurnRate * dt, Monster.TurnRate * dt), 0);
    }
}
