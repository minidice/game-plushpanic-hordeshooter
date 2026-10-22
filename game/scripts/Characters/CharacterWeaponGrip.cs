using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    private WeaponVisual.Built? _heldWeapon;
    private Node3D? _gripArms;
    private MeshInstance3D? _rightForearm, _leftForearm;
    private StandardMaterial3D? _gripMaterial;
    private float _runningCarry;
    private bool _supportGrip;
    private float _carryKick;
    private float _aimHold;
    private float _wingSize=1;
    public bool IsRunningCarry => _runningCarry>.5f;
    public bool UsesSupportGrip => _supportGrip;
    private bool HasAnimalArms => Definition.Id is "goose" or "duck" or "raccoon" or "redpanda";
    public bool WeaponArmsVisible => _gripArms?.Visible==true;

    /// <summary>Replace static wings/paws with compact shapes that meet the equipped weapon's hand positions.</summary>
    public void BindWeaponGrip(WeaponVisual.Built built)
    {
        ResetReloadPose();
        _heldWeapon=built;
        _leftGripHome=built.LeftGrip?.Transform??Transform3D.Identity;
        _meleePoseTime=-1;
        _supportGrip=built.Definition.Dual||built.Definition.Category==WeaponCategory.Heavy;
        if(_supportGrip||built.Definition.Category==WeaponCategory.Melee)_runningCarry=0;
        if(built.LeftGrip!=null)built.LeftGrip.Visible=_supportGrip;
        // The authored feather/paw tips form the hand; the old spherical palms would cover them.
        if(HasAnimalArms)
            foreach(var mesh in built.Root.FindChildren("Hand*",nameof(MeshInstance3D),true,false))((MeshInstance3D)mesh).Visible=false;
        HidePart(Definition.Id+"_leftArm");HidePart(Definition.Id+"_rightArm");
        if(_gripArms==null)
        {
            _gripArms=new Node3D{Name="GripArms"};Weapon.GetParent().AddChild(_gripArms);
            _gripMaterial=new StandardMaterial3D{AlbedoColor=HandColor,Roughness=.85f};
            _rightForearm=MakeForearm("RightForearm");_leftForearm=MakeForearm("LeftForearm");
        }
        ApplyCarryPose(0);UpdateWeaponGrip();
    }

    private MeshInstance3D MakeForearm(string name)
    {
        string side=name=="RightForearm"?"right":"left";
        if(HasAnimalArms&&Model.FindChild(Definition.Id+"_"+side+"Arm_mesh",true,false) is MeshInstance3D {Mesh:not null} source)
        {
            var pivot=source.GetParentNode3D();
            var home=_featureHomes.TryGetValue(pivot,out var authored)?authored:pivot.Transform;
            var rest=InRig(pivot.GetParentNode3D())*home*source.Transform;
            return CharacterGripSurface.Create(_gripArms!,source,InRig(_gripArms!).AffineInverse()*rest,name,side=="right"?1:-1,AppearanceCatalog.IsBird(Definition.Id));
        }
        var mesh=new CapsuleMesh{Radius=.09f,Height=.5f,RadialSegments=16,Rings=6};
        var arm=Mat.Mesh(_gripArms!,mesh,Vector3.Zero,_gripMaterial!,name);
        arm.Scale=new Vector3(HasAnimalArms?1.45f:1,1,.72f);return arm;
    }

    /// <summary>Gameplay supplies intent before shooting so a running muzzle is lowered before a shot is emitted.</summary>
    public void SetLocomotionPose(float dt,float movement,bool firing)
    {
        bool canRaise=_heldWeapon!=null&&!_supportGrip&&_heldWeapon.Definition.Category!=WeaponCategory.Melee;
        _aimHold=firing?.18f:Mathf.Max(0,_aimHold-dt);
        bool aiming=firing||_aimHold>0;
        float target=canRaise&&movement>.25f&&!aiming&&!ReloadPoseActive?1:0;
        _runningCarry=aiming?0:Mathf.MoveToward(_runningCarry,target,dt*7);
        ApplyCarryPose(_carryKick);UpdateWeaponGrip();
    }

    private void ApplyCarryPose(float kick)
    {
        _carryKick=kick;
        if(_heldWeapon==null||!HasAnimalArms)return;
        var def=_heldWeapon.Definition;
        if(def.Category==WeaponCategory.Melee){ApplyMeleeCarryPose();return;}
        Vector3 at=def.Dual?new Vector3(0,.94f,.42f):_supportGrip?new Vector3(.12f,.91f,.43f):new Vector3(.54f,1.22f,.65f);
        at+=new Vector3(0,_runningCarry*.14f,-_runningCarry*.12f-kick*.065f);
        Weapon.Position=at;
        Weapon.Rotation=new Vector3(-_runningCarry*1.05f,0,-.06f);
        ApplyReloadCarryPose();
    }

    public void UpdateWeaponGrip()
    {
        if(_gripArms==null||_heldWeapon==null||!IsInsideTree())return;
        _gripArms.Visible=IsInstanceValid(_heldWeapon.Root)&&!_heldWeapon.Root.IsQueuedForDeletion()&&_heldWeapon.Root.IsVisibleInTree();
        if(!_gripArms.Visible)return;
        _gripMaterial!.AlbedoColor=HandColor;
        ApplyReloadHand();
        Connect(_rightForearm!,_heldWeapon.RightGrip,1);
        Connect(_leftForearm!,_supportGrip||ReloadPoseActive?_heldWeapon.LeftGrip:null,-1);
    }

    private void Connect(MeshInstance3D arm,Node3D? target,float side)
    {
        arm.Visible=true;
        if(CharacterGripSurface.IsSurface(arm))
        {
            bool holding=target!=null&&IsInstanceValid(target);
            var gripPosition=holding?_gripArms!.ToLocal(target!.GlobalPosition):SpareHandPosition(side);
            CharacterGripSurface.Pose(arm,gripPosition,holding,_wingSize,_runningCarry,HandColor);
            return;
        }
        arm.Scale=new Vector3((HasAnimalArms?1.45f:1)*_wingSize,1,.72f*_wingSize);
        // Compact shoulder-to-wing shape; the spare wing rests or opens outward while running.
        var elbow=IsPlaceholder?new Vector3(side*Definition.ColliderRadius*.8f,Definition.ColliderHeight*.40f,.15f):new Vector3(side*.46f,1.00f,.13f);
        var hand=target!=null&&IsInstanceValid(target)?_gripArms!.ToLocal(target.GlobalPosition)
            :SpareHandPosition(side);
        var direction=hand-elbow;float length=direction.Length();
        if(length<.001f){arm.Visible=false;return;}
        arm.Position=(elbow+hand)*.5f;
        arm.Quaternion=new Quaternion(Vector3.Up,direction/length);
        var capsule=(CapsuleMesh)arm.Mesh;
        if(!Mathf.IsEqualApprox(capsule.Height,length+.18f))capsule.Height=length+.18f;
    }

    private Vector3 SpareHandPosition(float side=-1) => new(side*(.51f+_runningCarry*.20f),.72f+_runningCarry*(.26f+.035f*Mathf.Sin(_clock)),.13f);

    // Measure the wrist joint at the rounded cap's center, where it overlaps the palm.
    public float GripConnectionError
    {
        get
        {
            if(_heldWeapon==null||_rightForearm==null)return float.PositiveInfinity;
            float error=WristPosition(_rightForearm).DistanceTo(_heldWeapon.RightGrip.GlobalPosition);
            if((_supportGrip||ReloadPoseActive)&&_heldWeapon.LeftGrip!=null&&_leftForearm!=null)
                error=Mathf.Max(error,WristPosition(_leftForearm).DistanceTo(_heldWeapon.LeftGrip.GlobalPosition));
            return error;
        }
    }
    private static Vector3 WristPosition(MeshInstance3D arm)
    {
        if(CharacterGripSurface.IsSurface(arm))return CharacterGripSurface.Wrist(arm);
        var mesh=(CapsuleMesh)arm.Mesh;
        return arm.GlobalTransform*new Vector3(0,mesh.Height*.5f-mesh.Radius,0);
    }
}
