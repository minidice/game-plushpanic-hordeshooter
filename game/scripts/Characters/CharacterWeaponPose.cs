using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    // Presentation tuning shared by all mammals; gameplay aim remains on the rig forward axis.
    private const float CompactStanceDegrees=40;
    private const float CompactHeldLength=.60f;
    private const float CompactWeaponOverlayScale=.15f;

    /// <summary>Model anatomy in the weapon parent's space, independent of the current arm pose/size.</summary>
    private Vector3 RestShoulder(string side)
    {
        var arm=Model.FindChild(Definition.Id+"_"+side+"Arm",true,false) as Node3D;
        if(arm==null)return new Vector3((side=="right"?1:-1)*Definition.ColliderRadius*.8f,Definition.ColliderHeight*.56f,0);
        var home=_featureHomes.TryGetValue(arm,out var authored)?authored:arm.Transform;
        var offset=Definition.WeaponShoulderOffset;offset.X*=side=="right"?1:-1;
        var local=arm.GetNodeOrNull<Node3D>(Definition.Id+"_"+side+"Shoulder")?.Position??offset;
        return InRig(Weapon.GetParentNode3D()).AffineInverse()*InRig(arm.GetParentNode3D())*home*local;
    }

    /// <summary>Same contact solve for every animal; only anatomy and authored weapon markers vary.</summary>
    private bool ApplySharedWeaponPose(float kick)
    {
        if(_heldWeapon==null)return false;
        var family=_heldWeapon.Pose;
        if(family is not (WeaponVisual.PoseFamily.Shouldered or WeaponVisual.PoseFamily.Pistol))return false;
        var right=RestShoulder("right");var left=RestShoulder("left");
        var centre=(right+left)*.5f;float halfWidth=Mathf.Abs(right.X-left.X)*.5f;
        bool compact=_rightForearm is CharacterLionPaw;
        if(family==WeaponVisual.PoseFamily.Shouldered&&_heldWeapon.StockContact is {} stock) {
            // Solve stock-first: moving/rotating a weapon never leaves its butt behind at the belly.
            var localContact=InRig(Weapon).AffineInverse()*InRig(stock).Origin;
            var pocket=right+new Vector3(-halfWidth*(compact?.40f:.32f),.08f,compact?.03f:.06f);
            pocket+=new Vector3(0,-_runningCarry*(compact?.025f:.12f),_runningCarry*(compact?.02f:.04f)-kick*.02f);
            Weapon.Rotation=new Vector3(_runningCarry*(compact?.30f:.55f),compact?-Mathf.DegToRad(CompactStanceDegrees):0,0);
            Weapon.Position=pocket-Weapon.Basis*localContact;
        }
        else {
            // Pistols share a centred two-hand silhouette, never a rifle shoulder attachment.
            var localGrip=InRig(Weapon).AffineInverse()*InRig(_heldWeapon.RightGrip).Origin;
            var target=centre+new Vector3(.04f,.09f,compact?.38f:Mathf.Max(.48f,halfWidth*1.3f));
            target+=new Vector3(0,-_runningCarry*.15f,-kick*.035f);
            Weapon.Rotation=new Vector3(_runningCarry*.40f,0,0);
            Weapon.Position=target-Weapon.Basis*localGrip;
        }
        ApplyReloadCarryPose();return true;
    }
}
