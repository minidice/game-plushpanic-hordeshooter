using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class GroundedArtCheck : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;}
    public override async void _Ready()
    {
        try
        {
            foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            {
                var def=CharacterCatalog.Player(species);var rig=CharacterRig.Create(def);AddChild(rig);
                MeshInstance3D Part(string suffix)=>(MeshInstance3D)rig.Model.FindChild(species+"_"+suffix,true,false)!;
                bool bird=AppearanceCatalog.IsBird(species);
                string expectedModel=$"res://assets/characters/{species}/{species}-clean.glb";
                Check(def.Model!.ResourcePath==expectedModel,species+" exact reviewed model loaded by catalog");
                Check(Part("vest_mesh").Mesh.SurfaceGetMaterial(0).ResourceName.StartsWith("custom_vest_fitted"),species+" wrapped vest material contract retained");
                var tail=Part("tail_mesh");var tailHome=tail.Mesh;
                var tailTransform=tail.Transform;
                if(!bird)
                {
                    var original=def.Model.Instantiate<Node3D>();
                    var source=(MeshInstance3D)original.FindChild(species+"_tail_mesh",true,false)!;
                    Check(tail.GetParentNode3D().GetParentNode3D().Transform.IsEqualApprox(source.GetParentNode3D().Transform),species+" canonical tail preserves imported rump attachment");
                    Check(tailHome.GetSurfaceCount()==2&&Enumerable.Range(0,2).All(i=>tailHome.SurfaceGetMaterial(i)==source.Mesh.SurfaceGetMaterial(i)),species+" canonical tail retains both imported stripe channels");
                    var points=tailHome.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    float radius=species=="raccoon"?.145f:.18f;
                    Check(Enumerable.Range(0,24).All(i=>Mathf.Abs(points[i*6].Length()-radius)<.0001f),species+" canonical default root has full uniform girth");
                    Check(points.Any(p=>p.DistanceTo(new Vector3(.1f,.1f,species=="raccoon"?-.95f:-1.15f))<.00001f),species+" canonical gentle profile ends with closed light tip");
                    Check(tail.Skin!=null&&tail.GetNodeOrNull<Skeleton3D>(tail.Skeleton)?.GetBoneCount()==4,species+" canonical default binds tail-only deformation");
                    original.Free();
                }
                var built=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);rig.HideBuiltInWeapon();
                var arm=(MeshInstance3D)rig.FindChild("RightForearm",true,false)!;
                bool rigidPaw=arm is CharacterLionPaw;
                Check(arm.Mesh is ArrayMesh&&(bird?arm.MaterialOverride is ShaderMaterial:rigidPaw&&arm.Mesh==Part("rightPaw_mesh").Mesh&&arm.GetNode<MeshInstance3D>("Forearm").Mesh==Part("rightArm_mesh").Mesh),species+" held arm uses correct authored feather or rigid paw plus forearm");
                var edited=CharacterRig.Create(def);AddChild(edited);edited.ApplyAppearance(new AppearanceProfile{WingSize=1.25f,BodyWidth=.88f,BodyHeight=1.1f});
                var editedGun=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),edited.HandColor);edited.Weapon.AddChild(editedGun.Root);edited.BindWeaponGrip(editedGun);
                var editedArm=(MeshInstance3D)edited.FindChild("RightForearm",true,false)!;
                Check(arm.Mesh.GetAabb().IsEqualApprox(editedArm.Mesh.GetAabb()),species+" editing before equip keeps canonical rest surface; size applied once");edited.Free();
                foreach(var (width,height,wing) in new[]{(.88f,.9f,.8f),(.88f,1.1f,1.25f),(1.12f,.9f,1.25f),(1.12f,1.1f,.8f),(1f,1f,1f)})
                {
                    var p=new AppearanceProfile{Hat=0,Vest=true,BodyWidth=width,BodyHeight=height,WingSize=wing,TailStyle=4,EarStyle=2};rig.ApplyAppearance(p);
                    rig.SetLocomotionPose(.2f,1,false);rig.Animate(.016f,1,.5f,.2f,new Vector3(1,0,1));
                    Check(rig.GripConnectionError<.002f,species+" body/wing sizes keep grip connected");
                    if(rigidPaw) {
                        var points=arm.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                        for(int i=1;i<points.Length;i+=Math.Max(1,points.Length/10))
                            Check(Mathf.Abs((arm.Transform*points[i]).DistanceTo(arm.Transform*points[0])-points[i].DistanceTo(points[0])*wing)<.0002f,species+" actual paw distances preserve authored shape and apply size once");
                        Check(arm.GetNode<MeshInstance3D>("Forearm").Transform.Basis.IsFinite(),species+" separate forearm pose remains finite");
                    }
                    var vestTransform=Part("vest_mesh").Transform;rig.ApplyAppearance(p);
                    Check(Part("vest_mesh").Transform.IsEqualApprox(vestTransform),species+" vest fit does not accumulate");
                }
                rig.ApplyAppearance(new AppearanceProfile{Hat=0});
                Check(tail.Mesh==tailHome&&tail.Transform.IsEqualApprox(tailTransform),species+" alternate tail restores canonical default mesh and transform");
                if(!bird)Check(Enumerable.Range(0,4).All(i=>tail.GetNode<Skeleton3D>(tail.Skeleton).GetBonePoseRotation(i).IsEqualApprox(Quaternion.Identity)),species+" default restoration clears tail bend history");
                rig.SetPreviewVisibility(false,false);Check(!rig.WeaponArmsVisible&&Part(rigidPaw?"leftPaw_mesh":"leftArm_mesh").IsVisibleInTree(),species+" uncovered preview restores folded arm or rigid paw");
                rig.SetPreviewVisibility(true,true);Check(rig.WeaponArmsVisible,species+" equipped preview restores held shape");
                var gripParameter=rigidPaw?default:arm.GetInstanceShaderParameter("grip");
                var armPose=arm.Transform;var originalOverlay=arm.MaterialOverlay;
                rig.FlashHit();
                if(rigidPaw)Check(arm.MaterialOverlay is StandardMaterial3D flash&&flash.AlbedoColor.A==CharacterHitFlashEffect.PeakAlpha&&arm.GetNode<MeshInstance3D>("Forearm").MaterialOverlay==arm.MaterialOverlay&&arm.Transform.IsEqualApprox(armPose),species+" flash covers rigid paw and forearm without changing geometry");
                else Check(arm.MaterialOverlay is ShaderMaterial flash&&flash.Shader.ResourcePath.EndsWith("character_grip_flash.gdshader"),species+" hit overlay preserves wing deformation");
                rig.GetNode<Node>("HitFlash")._Process(.2);
                Check(arm.MaterialOverlay==originalOverlay,species+" flash restores original arm overlay");
                rig.ShowAs(VisionSystem.Visibility.Silhouette);rig.UpdateWeaponGrip();
                Check(rig.GripConnectionError<.002f&&(rigidPaw?arm.Transform.IsEqualApprox(armPose):arm.GetInstanceShaderParameter("grip").Equals(gripParameter)),species+" silhouette preserves actual grip geometry or shader parameters");
                rig.Free();
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("GROUNDED_ART_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitHygiene.Collect();GetTree().Quit(1);}
    }
}
