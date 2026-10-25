using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    private bool _zipPose, _zipWeaponVisible;
    private Node3D? _zipHandle;
    public bool ZiplinePoseActive => _zipPose;
    public bool ZiplineReleasing { get; private set; }

    /// <summary>Hands meet a shared overhead grip; release opens the arms and bends into the landing.</summary>
    public void ZiplinePose(bool releasing,float phase,float speed)
    {
        if (!_zipPose) { _zipWeaponVisible=Weapon.Visible; _zipPose=true; ResetReloadPose(); }
        ZiplineReleasing=releasing;
        Weapon.Visible=false;
        if (_zipHandle==null)
        {
            _zipHandle=new Node3D { Name="ZiplineGrip" }; AddChild(_zipHandle);
            var steel=Mat.Solid("727f83",.4f);
            Mat.Box(_zipHandle,new Vector3(0,Zipline.Hang-.08f,.1f),new Vector3(.62f,.065f,.065f),steel);
            Mat.Box(_zipHandle,new Vector3(0,Zipline.Hang-.025f,.05f),new Vector3(.06f,.11f,.12f),steel);
        }
        _zipHandle.Visible=!releasing;
        float crouch=releasing?Mathf.Sin(Mathf.Clamp((phase-.55f)/.45f,0,1)*Mathf.Pi):0;
        Pose.Position=Vector3.Down*(releasing?.20f*crouch:.22f);
        Pose.Rotation=new Vector3(releasing?.2f*Mathf.Sin(phase*Mathf.Pi):-.12f*speed,0,0);
        Pose.Scale=new Vector3(1,1-.13f*crouch,1);
        LeftFoot.Position+=new Vector3(0,releasing?.12f*crouch:.25f,-.16f*_forwardSign);
        RightFoot.Position+=new Vector3(0,releasing?.12f*crouch:.18f,-.22f*_forwardSign);
        LeftFoot.Rotation+=new Vector3(-.55f*_forwardSign,0,0);
        RightFoot.Rotation+=new Vector3(-.45f*_forwardSign,0,0);
        if (_gripArms==null) return;
        _gripArms.Visible=true;
        foreach (var side in new[] {-1f,1f})
        {
            var arm=side<0?_leftForearm!:_rightForearm!;
            var hand=releasing?new Vector3(side*(1.05f-.35f*phase),1.6f-.35f*phase,.20f)
                :new Vector3(side*.24f,Zipline.Hang-.08f,.1f);
            PoseTraversalArm(arm,_gripArms.ToLocal(ToGlobal(hand)),side);
        }
    }

    private void PoseTraversalArm(MeshInstance3D arm,Vector3 hand,float side)
    {
        arm.Visible=true;
        if (arm is CharacterAuthoredWing wing)
        {
            wing.PoseWing(hand,true,_wingSize,0);
            return;
        }
        if (CharacterGripSurface.IsSurface(arm))
        {
            CharacterGripSurface.Pose(arm,hand,true,_wingSize,0,HandColor);
            return;
        }
        var elbow=new Vector3(side*.46f,1,.13f);
        var direction=hand-elbow;
        arm.Position=(elbow+hand)*.5f;
        arm.Quaternion=new Quaternion(Vector3.Up,direction.Normalized());
        ((CapsuleMesh)arm.Mesh).Height=direction.Length()+.18f;
    }

    public void EndZiplinePose()
    {
        if (!_zipPose) return;
        _zipPose=false; ZiplineReleasing=false;
        if (_zipHandle!=null) _zipHandle.Visible=false;
        Weapon.Visible=_zipWeaponVisible;
        Pose.Transform=Transform3D.Identity;
        Animate(0,0,0,0,Vector3.Zero);
    }
}
