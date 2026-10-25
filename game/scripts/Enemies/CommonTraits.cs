using Godot;

namespace HordeShooter;

public partial class Enemy
{
    private bool HunterMelee => Monster.CommonIdentity?.Weapon is "club" or "knife";
    private bool _hunterSwingPlayed;
    private AudioStreamPlayer3D? _hunterReadySound;

    private void CommonWeaponPose(float dt)
    {
        if (HunterMelee)
            Rig.SetHunterAttackPose(Monster.CommonIdentity!.Weapon, _meleeTime, MeleeWindup, MeleeRecovery);
        else if (this is RangedEnemy ranged) ranged.AnimateHunterBow(dt);
    }

    private void BeginHunterMelee()
    {
        _hunterSwingPlayed = false;
        StopHunterReady();
        if (!HunterMelee) return;
        string id = Monster.CommonIdentity!.Weapon == "club" ? "hunter_club_raise" : "hunter_knife_ready";
        _hunterReadySound = Stage.Sfx.Attach(id, this);
    }

    private void HunterMeleeSwing()
    {
        if (!HunterMelee || _hunterSwingPlayed || _meleeTime < CharacterRig.HunterSwingStart(Monster.CommonIdentity!.Weapon, MeleeWindup)) return;
        _hunterSwingPlayed = true;
        StopHunterReady();
        Stage.Sfx.PlayAt("hunter_" + Monster.CommonIdentity!.Weapon + "_swing", GlobalPosition);
    }

    private void StopHunterReady()
    {
        if (IsInstanceValid(_hunterReadySound)) { _hunterReadySound!.Stop(); _hunterReadySound.QueueFree(); }
        _hunterReadySound = null;
    }

    /// <summary>Only confirmed contacts produce a thud/cut; the reliable event mirrors it on clients.</summary>
    public void ShowHunterMeleeImpact(Vector3 point)
    {
        if (HunterMelee) Stage.Sfx.PlayAt("hunter_" + Monster.CommonIdentity!.Weapon + "_hit", point);
    }

    private void HunterMeleeImpact(Vector3 point)
    {
        if (!HunterMelee) return;
        ShowHunterMeleeImpact(point);
        Stage.Net?.OnHunterMeleeImpact(this, point);
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
