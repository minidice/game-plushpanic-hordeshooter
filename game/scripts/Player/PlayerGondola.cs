using Godot;

namespace HordeShooter;

public partial class Player
{
    private Gondola? _gondola;
    private Vector3 _gondolaPosition;
    public bool OnGondola => _gondola != null;

    internal void BeginGondolaRide(Gondola gondola)
    {
        _gondola=gondola;
        _gondolaPosition=GlobalPosition;
        _rollTime=0;
        _push=Vector3.Zero;
        Weapons.EndThrowAim(); Weapons.CancelMeleeSwing();
        IsHealing=false; IsAiming=false; AimBlend=0;
        StopReviving();
        if (!IsDowned) Rig.ResetPose();
        HoldGondolaPosition();
    }

    internal void HoldGondolaPosition()
    {
        if (!OnGondola) return;
        GlobalPosition=_gondolaPosition;
        Velocity=Vector3.Zero; NetMotion=null;
        ClearGondolaControls();
    }

    internal void EndGondolaRide(Gondola gondola)
    {
        if (_gondola!=gondola) return;
        _gondola=null;
        Velocity=Vector3.Zero; NetMotion=null;
        ClearGondolaControls();
    }

    private void ClearGondolaControls()
    {
        _move=Vector2.Zero;
        _fire=_firePressed=_reloadPressed=_healPressed=_throwPressed=_rollPressed=_cancelPressed=_aimHeld=_reviveHeld=false;
        _cycle=_slotPressed=0;
    }
}
