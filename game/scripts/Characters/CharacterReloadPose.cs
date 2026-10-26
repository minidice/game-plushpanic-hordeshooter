using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    private float _reloadWeight, _reloadPhase;
    private bool _reloadShell;
    private Transform3D _leftGripHome;
    public bool ReloadPoseActive => _reloadWeight > .001f;

    /// <summary>Visual only: timing comes from the current ammo state, including each inserted shell.</summary>
    public void SetReloadPose(float dt, WeaponState? state)
    {
        var def = _heldWeapon?.Definition;
        bool supported = HasAnimalArms && def != null && !def.Dual && def.Mode == FireMode.Hitscan &&
            (def.Category is WeaponCategory.Pistol or WeaponCategory.Smg or WeaponCategory.Rifle or WeaponCategory.Lmg or WeaponCategory.Sniper || def.ShellReloadTime > 0);
        bool active = supported && state is { Reloading: true } && state.Def == def;
        if (active)
        {
            _reloadPhase = Mathf.Clamp(state!.ReloadFraction, 0, 1);
            _reloadShell = def!.ShellReloadTime > 0;
        }
        _reloadWeight = Mathf.MoveToward(_reloadWeight, active ? 1 : 0, Mathf.Max(0,dt)*12);
        ApplyCarryPose(0);
        UpdateWeaponGrip();
    }

    /// <summary>Shots, holstering and death must restore the real muzzle/grips immediately.</summary>
    public void ResetReloadPose()
    {
        _reloadWeight = _reloadPhase = 0;
        if (_heldWeapon?.LeftGrip is { } left && IsInstanceValid(left))
        {
            left.Transform = _leftGripHome;
            left.Visible = _supportGrip;
        }
    }

    private void ApplyReloadCarryPose()
    {
        if (!ReloadPoseActive) return;
        // Move clear of the vest; both short wings can reach the underside without crossing the face.
        var position = new Vector3(.16f, .99f, .58f);
        var angles = _reloadShell ? new Vector3(-.16f,.12f,-.24f) : new Vector3(-.22f,.16f,-.32f);
        if (!_reloadShell)
            angles.Z += .07f*Mathf.Sin(Mathf.Pi*Mathf.Clamp((_reloadPhase-.78f)/.13f,0,1));
        Weapon.Position = Weapon.Position.Lerp(position,_reloadWeight);
        Weapon.Rotation = Weapon.Rotation.Lerp(angles,_reloadWeight);
    }

    private void ApplyReloadHand()
    {
        if (_heldWeapon?.LeftGrip is not { } left) return;
        left.Transform = _leftGripHome;
        left.Visible = _supportGrip || ReloadPoseActive;
        // Rest the support palm near the rear of a long forend, preserving barrel length.
        if(CompactGunPose && _heldWeapon.Pose==WeaponVisual.PoseFamily.Shouldered)
            left.Position=new Vector3(left.Position.X,left.Position.Y,Mathf.Min(left.Position.Z,.32f));
        if (!ReloadPoseActive) return;
        var def = _heldWeapon.Definition;
        float length = def.Length;
        // Local to the weapon; optional authored marker lets future models refine their contact point.
        var port = _heldWeapon.Root.FindChild("ReloadContact",true,false) as Node3D;
        var at = def.Category == WeaponCategory.Pistol
            ? new Vector3(-.025f,-.43f*length,.04f*length)
            : new Vector3(-.035f,(_reloadShell?-.12f:-.22f)*length,.23f*length);
        var contact = _gripArms!.ToLocal(port?.GlobalPosition ?? _heldWeapon.Root.ToGlobal(at));
        var belt = new Vector3(-.43f,.64f,.40f);
        float phase = _reloadPhase;
        Vector3 hand;
        if (_reloadShell)
            hand = phase < .38f ? contact.Lerp(belt,Ease(phase/.38f))
                : belt.Lerp(contact,Ease((phase-.48f)/.46f));
        else if (phase < .18f) hand = contact;
        else if (phase < .42f) hand = contact.Lerp(belt,Ease((phase-.18f)/.24f));
        else hand = belt.Lerp(contact,Ease((phase-.48f)/.27f));
        var rest = _supportGrip ? _gripArms.ToLocal(left.GlobalPosition) : SpareHandPosition();
        // Leave the port before the reload completes; the remaining weight fades out on completion/cancel.
        float weight = _reloadWeight * (_reloadShell ? 1 : 1-Ease((phase-.90f)/.10f));
        left.GlobalPosition = _gripArms.ToGlobal(rest.Lerp(hand,weight));
    }

    private static float Ease(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp(t,0,1));
}
