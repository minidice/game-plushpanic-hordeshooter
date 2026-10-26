using Godot;
using System;
using System.Linq;
namespace HordeShooter.Tests;

public partial class SharedWeaponPoseCheck : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;}
    public override void _Ready(){int exit=0;try {
        foreach(string id in new[]{"goose","duck","raccoon","redpanda","common_parrot","common_eagle","common_lion","common_tiger","common_reindeer","common_rhino"})
        foreach(string weapon in new[]{"m4","mp5","ak47","m1911"}) {
            var def=id.StartsWith("common_")?CommonRoster.Current.LookFor(new CommonIdentity("armed",id[7..],"ochre","m4")):CharacterCatalog.Player(id);
            if(id is "goose" or "duck")Check(def.WeaponShoulderOffset.IsEqualApprox(new Vector3(.005f,.23f,-.05f)),id+": legacy wing pivot anatomical shoulder offset loads");
            var rig=CharacterRig.Create(def);AddChild(rig);rig.HideBuiltInWeapon();
            try {
                var built=WeaponVisual.Build(ArmoryCatalog.Weapon(weapon),rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);
                var arm=(Node3D)rig.Model.FindChild(id+"_rightArm",true,false);
                var armHome=arm.Transform;
                var localShoulder=arm.GetNodeOrNull<Node3D>(id+"_rightShoulder")?.Position??def.WeaponShoulderOffset;
                foreach(var profile in new[]{new AppearanceProfile(),new AppearanceProfile{BodyWidth=.88f,BodyHeight=1.1f,WingSize=.8f},new AppearanceProfile{BodyWidth=1.12f,BodyHeight=.9f,WingSize=1.25f}}) {
                    rig.ApplyAppearance(profile);rig.SetLocomotionPose(0,0,true);rig.Animate(0,0,0,0,Vector3.Zero);
                    var shoulderPoint=arm.GetParentNode3D().GlobalTransform*armHome*localShoulder;
                    if(weapon!="m1911") {
                        var contact=built.StockContact;
                        Check(contact!=null,id+"/"+weapon+": stock contact marker");
                        Check(contact!.GetParent()!=built.Root,"authored GLB contact, not procedural fallback");
                        Check(contact.GlobalPosition.DistanceTo(shoulderPoint)<.22f,id+"/"+weapon+": stock at right shoulder, not belly");
                        var distance=built.Root.FindChildren("*",nameof(MeshInstance3D),true,false).Cast<MeshInstance3D>()
                            .Where(n=>!n.Name.ToString().StartsWith("Hand")).Min(n=>LionLimbCheck.SurfaceDistance(n,contact.GlobalPosition));
                        Check(distance<.003f,"contact lies on actual stock pad surface");
                    }
                    else Check(Mathf.Abs(rig.ToLocal(built.RightGrip.GlobalPosition).X)<.12f,"pistol hand near centre");
                    Check(rig.UsesSupportGrip&&built.LeftGrip!.Visible,"shared two-hand pose");
                    Check(rig.GripConnectionError<.002f,$"{id}/{weapon}/{profile.WingSize}: actual hand contacts {rig.GripConnectionError}");
                    var idle=rig.Weapon.Transform;
                    rig.SetLocomotionPose(.3f,1,false);rig.Animate(.016f,1,.2f,.1f,Vector3.Zero);
                    Check(rig.IsRunningCarry&&rig.GripConnectionError<.002f,$"{id}/{weapon}/{profile.WingSize}: moving two-hand carry contact {rig.GripConnectionError}");
                    rig.SetLocomotionPose(0,1,true);rig.Animate(0,0,0,0,Vector3.Zero);
                    Check(rig.Weapon.Transform.IsEqualApprox(idle)&&rig.GripConnectionError<.002f,"firing restores aim before emission");
                    var reload=new WeaponState(built.Definition,0,30);reload.StartReload();reload.Tick(built.Definition.ReloadTime*.45f,false);
                    rig.SetReloadPose(.2f,reload);rig.Animate(.016f,0,0,0,Vector3.Zero);
                    Check(rig.GripConnectionError<.002f,"reload contact");
                    rig.SetReloadPose(.3f,null);rig.Animate(0,0,0,0,Vector3.Zero);
                    Check(rig.Weapon.Transform.IsEqualApprox(idle),"reload cancellation restores aim without accumulation");
                }
            }finally{rig.Free();}
        }
        GD.Print("SHARED_WEAPON_POSE_PASS "+_checks);
    }catch(Exception e){GD.PushError("SHARED_WEAPON_POSE_FAIL "+e);exit=1;}
    QuitHygiene.Collect();GetTree().Quit(exit);}
}
