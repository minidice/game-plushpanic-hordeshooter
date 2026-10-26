using Godot;
using System;
using System.Collections.Generic;
namespace HordeShooter.Tests;
public partial class CompactWeaponPoseCheck : Node
{
    private int _checks;
    public override void _Ready(){int exit=0;try {
        var failures=new List<string>();float worst=0,error=0;
        foreach(string id in new[]{"raccoon","redpanda","common_tiger","common_lion","common_reindeer","common_rhino"})
        foreach(string weapon in new[]{"m4","mp5","ak47","m1911","glock17","r870","benelli_m4","double_barrel","m249","awm","svd","m82"}) {
            var def=id.StartsWith("common_")?CommonRoster.Current.LookFor(new("armed",id[7..],"ochre","m4")):CharacterCatalog.Player(id);
            var rig=CharacterRig.Create(def);AddChild(rig);
            var rest=new Dictionary<string,float>();
            foreach(string side in new[]{"Left","Right"}) {
                var arm=(Node3D)rig.Model.FindChild(id+"_"+side.ToLowerInvariant()+"Arm",true,false);
                var shoulder=arm.GetNodeOrNull<Node3D>(id+"_"+side.ToLowerInvariant()+"Shoulder")?.GlobalPosition??arm.GlobalPosition;
                var wrist=(Node3D)rig.Model.FindChild(id+"_"+side.ToLowerInvariant()+"Wrist",true,false);
                rest[side]=shoulder.DistanceTo(wrist.GlobalPosition)/def.ModelScale;
            }
            var gun=WeaponVisual.Build(ArmoryCatalog.Weapon(weapon),rig.HandColor);rig.HideBuiltInWeapon();rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
            foreach(float size in new[]{1f,.8f,1.25f}) {
                rig.ApplyAppearance(new AppearanceProfile{WingSize=size,BodyWidth=size<1?.88f:1.12f,BodyHeight=size<1?1.1f:.9f});
                foreach(bool moving in new[]{false,true})
                foreach(string overlay in new[]{"none","aim","dodge","strike","anticipation"}) {
                    rig.ResetPose();
                    rig.SetLocomotionPose(.3f,moving?1:0,!moving);rig.Animate(.016f,moving?1:0,.6f,.2f,new Vector3(2,0,1));
                    if(overlay=="aim")rig.AimStance(1);else if(overlay=="dodge")rig.DodgePose(.5f);
                    else if(overlay=="strike")rig.ApplyEnemyAttackPose(.5f,.5f,.4f);
                    else if(overlay=="anticipation")rig.ApplyEnemyAttackPose(.36f,.5f,.4f);
                    foreach(string side in new[]{"Left","Right"}) {
                        var paw=(CharacterLionPaw)rig.FindChild(side+"Forearm",true,false);var parent=paw.GetParentNode3D();
                        var elbow=parent.ToLocal(paw.GetNode<Node3D>("Elbow").GlobalPosition);
                        var wrist=parent.ToLocal(paw.GetNode<Node3D>("Wrist").GlobalPosition);
                        float held=paw.PosedShoulder.DistanceTo(elbow)+elbow.DistanceTo(wrist);
                        float ratio=held/(rest[side]*size);worst=Mathf.Max(worst,ratio);
                        if(ratio>1.15f)failures.Add($"{id}/{weapon}/{size}/{moving}/{overlay}/{side}: ratio {ratio}");
                        if(!paw.Transform.Basis.IsFinite())failures.Add("nonfinite paw");
                        _checks+=2;
                    }
                    float gap=rig.GripConnectionError;error=Mathf.Max(error,gap);
                    if(gap>.002f)failures.Add($"{id}/{weapon}/{size}/{moving}/{overlay}: grip error {gap}");
                    _checks++;
                }
            }
            rig.ResetPose();rig.SetLocomotionPose(0,0,true);rig.Animate(0,0,0,0,Vector3.Zero);
            var forward=rig.GlobalBasis.Z.Normalized();
            if(gun.Root.GlobalBasis.Z.Normalized().Dot(forward)<.999f)failures.Add($"{id}/{weapon}: gun heading differs from aim");
            rig.SetPreviewVisibility(false,false);
            float home=def.ModelFacesPositiveZ?0:Mathf.Pi;
            if(Mathf.Abs(rig.Model.Rotation.Y-home)>.0001f)failures.Add($"{id}/{weapon}: hidden weapon keeps stance");
            rig.SetPreviewVisibility(true,true);rig.Animate(0,0,0,0,Vector3.Zero);
            if(rig.GripConnectionError>.002f)failures.Add($"{id}/{weapon}: restore grip");
            _checks+=3;
            rig.Free();
        }
        GD.Print($"COMPACT_METRICS maxRatio={worst} maxGripError={error}");
        foreach(string fail in failures)GD.Print("FAIL "+fail);
        if(failures.Count>0)throw new Exception($"{failures.Count} compact pose checks failed");
        GD.Print("COMPACT_WEAPON_POSE_PASS "+_checks);
    }catch(Exception e){GD.PushError("COMPACT_WEAPON_POSE_FAIL "+e);exit=1;}
    QuitHygiene.Collect();GetTree().Quit(exit);}
}
