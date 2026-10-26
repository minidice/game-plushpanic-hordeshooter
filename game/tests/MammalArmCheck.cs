using Godot;
using System;
using System.Linq;
namespace HordeShooter.Tests;

public partial class MammalArmCheck : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;}
    public override void _Ready()
    {
        int exit=0;
        try {
        foreach(string species in new[]{"tiger","lion","reindeer","rhino","redpanda","raccoon"})
        foreach(string weapon in new[]{"m4","mp5","ak47"})
        {
            bool player=species is "redpanda" or "raccoon";
            var identity=new CommonIdentity("armed",species,"ochre",weapon);
            var rig=CharacterRig.Create(player?CharacterCatalog.Player(species):CommonRoster.Current.LookFor(identity));AddChild(rig);
            if(player){var built=WeaponVisual.Build(ArmoryCatalog.Weapon(weapon),rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);rig.HideBuiltInWeapon();}
            else CommonRoster.Current.Apply(rig,identity);
            var originalSupport=rig.UsesSupportGrip;
            Check(originalSupport,"shared two-hand rifle policy for players and common monsters");
            var homes=new[]{"Right","Left"}.ToDictionary(side=>side,side=>((Node3D)rig.Model.FindChild((player?"":"common_")+species+"_"+side.ToLowerInvariant()+"Arm",true,false)).Transform);
            foreach(float size in new[]{1f,.8f,1.25f}) {
            rig.ApplyAppearance(new AppearanceProfile{WingSize=size});rig.Animate(0,0,0,0,Vector3.Zero);
            Check(rig.GripConnectionError<.002f,"actual weapon grips coincide");
            foreach(string side in new[]{"Right","Left"})
            {
                var paw=(CharacterLionPaw)rig.FindChild(side+"Forearm",true,false);
                var arm=(Node3D)rig.Model.FindChild((player?"":"common_")+species+"_"+side.ToLowerInvariant()+"Arm",true,false);
                var anchor=arm.GetNodeOrNull<Node3D>((player?"":"common_")+species+"_"+side.ToLowerInvariant()+"Shoulder")??arm;
                var parent=paw.GetParentNode3D();
                var shoulderGlobal=arm.GetParentNode3D().GlobalTransform*homes[side]*(anchor==arm?Vector3.Zero:anchor.Position);
                var restShoulder=parent.ToLocal(shoulderGlobal);
                var shoulder=paw.PosedShoulder;
                shoulderGlobal=parent.ToGlobal(shoulder);
                Check(Mathf.Abs(shoulder.Y-restShoulder.Y)<.0001f && Mathf.Abs(new Vector2(shoulder.X,shoulder.Z).Length()-new Vector2(restShoulder.X,restShoulder.Z).Length())<.0001f,"shoulder rolls on original torso radius at unchanged height");
                var wrist=parent.ToLocal(paw.GetNode<Node3D>("Wrist").GlobalPosition);
                var upper=paw.GetNode<MeshInstance3D>("UpperArm");var lower=paw.GetNode<MeshInstance3D>("Forearm");
                var elbow=parent.ToLocal(paw.GetNode<Node3D>("Elbow").GlobalPosition);
                bool held=side=="Right"||originalSupport;
                if(!player&&side=="Right") {
                    var stock=(Node3D)rig.Weapon.FindChild("StockContact",true,false);
                    Check(parent.ToLocal(paw.Grip).Z-parent.ToLocal(stock.GlobalPosition).Z>.12f,"trigger hand stays ahead of the shouldered butt, including compact SMG");
                }
                Check(upper.Visible==held&&lower.Visible==held&&paw.GetNode<MeshInstance3D>("RestArm").Visible!=held,"spare arm retains original continuous surface");
                if(held) {
                    float bend=elbow.DistanceTo(Geometry3D.GetClosestPointToSegment(elbow,shoulder,wrist));
                    Check(bend>.035f,$"{species}/{weapon}/{side}/{size}: visible elbow bend {bend}");
                    // A shoulder-mounted rifle requires the support arm to cross the torso.
                    // Small hands add reach; retain separate bounded sections and a measurable elbow bend.
                    float limit=(side=="Left"?.50f:.45f)*Mathf.Max(1,size);
                    Check(shoulder.DistanceTo(elbow)<limit&&elbow.DistanceTo(wrist)<limit,$"{species}/{weapon}/{side}/{size}: compact sections");
                }
                Check(LionLimbCheck.SurfaceDistance(upper,shoulderGlobal)<.10f,"upper surface reaches posed shoulder independently of wing size");
                Check(LionLimbCheck.SurfaceDistance(upper,paw.GetNode<Node3D>("Elbow").GlobalPosition)<.10f&&LionLimbCheck.SurfaceDistance(lower,paw.GetNode<Node3D>("Elbow").GlobalPosition)<.10f,"both real surfaces meet elbow");
                Check(LionLimbCheck.SurfaceDistance(lower,paw.GetNode<Node3D>("Wrist").GlobalPosition)<.10f,"lower surface meets wrist");
                var source=(MeshInstance3D)arm.FindChild((player?"":"common_")+species+"_"+side.ToLowerInvariant()+"Arm_mesh",true,false);
                Check(upper.GetActiveMaterial(0)==source.GetActiveMaterial(0)&&lower.GetActiveMaterial(0)==source.GetActiveMaterial(0),"both sections share the owned source color");
                Check(upper.Mesh.ResourcePath.Contains("mammal-arm-segments")&&lower.Mesh.ResourcePath.Contains("mammal-arm-segments"),"actual immutable Blender geometry");
                Check(paw.Transform.Basis.IsFinite()&&upper.Transform.Basis.IsFinite()&&lower.Transform.Basis.IsFinite(),"finite geometry");
            }
            }
            rig.Free();
        }
        GD.Print("MAMMAL_ARM_PASS "+_checks);
        }catch(Exception e){GD.PushError("MAMMAL_ARM_FAIL "+e);exit=1;}
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
}
