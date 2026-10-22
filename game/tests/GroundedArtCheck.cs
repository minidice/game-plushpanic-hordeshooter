using Godot;
using System;
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
                Check(def.Model!.ResourcePath.EndsWith("-fitted.glb")&&Part("vest_mesh").Mesh.SurfaceGetMaterial(0).ResourceName.StartsWith("custom_vest_fitted"),species+" fitted wrap vest loaded by catalog");
                var tail=Part("tail_mesh");var tailHome=tail.Mesh;
                if(!AppearanceCatalog.IsBird(species))
                {
                    var original=def.Model.Instantiate<Node3D>();
                    Check(tailHome==((MeshInstance3D)original.FindChild(species+"_tail_mesh",true,false)!).Mesh,species+" authored smooth default tail survives rig setup");original.Free();
                }
                var built=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);rig.HideBuiltInWeapon();
                var arm=(MeshInstance3D)rig.FindChild("RightForearm",true,false)!;
                Check(arm.Mesh is ArrayMesh && arm.MaterialOverride is ShaderMaterial,species+" held arm uses authored feather/paw surface");
                var edited=CharacterRig.Create(def);AddChild(edited);edited.ApplyAppearance(new AppearanceProfile{WingSize=1.25f,BodyWidth=.88f,BodyHeight=1.1f});
                var editedGun=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),edited.HandColor);edited.Weapon.AddChild(editedGun.Root);edited.BindWeaponGrip(editedGun);
                var editedArm=(MeshInstance3D)edited.FindChild("RightForearm",true,false)!;
                Check(arm.Mesh.GetAabb().IsEqualApprox(editedArm.Mesh.GetAabb()),species+" editing before equip keeps canonical rest surface; size applied once");edited.Free();
                foreach(var (width,height,wing) in new[]{(.88f,.9f,.8f),(.88f,1.1f,1.25f),(1.12f,.9f,1.25f),(1.12f,1.1f,.8f),(1f,1f,1f)})
                {
                    var p=new AppearanceProfile{Hat=0,Vest=true,BodyWidth=width,BodyHeight=height,WingSize=wing,TailStyle=4,EarStyle=2};rig.ApplyAppearance(p);
                    rig.SetLocomotionPose(.2f,1,false);rig.Animate(.016f,1,.5f,.2f,new Vector3(1,0,1));
                    Check(rig.GripConnectionError<.002f,species+" body/wing sizes keep grip connected");
                    var vestTransform=Part("vest_mesh").Transform;rig.ApplyAppearance(p);
                    Check(Part("vest_mesh").Transform.IsEqualApprox(vestTransform),species+" vest fit does not accumulate");
                }
                rig.ApplyAppearance(new AppearanceProfile{Hat=0});
                Check(tail.Mesh==tailHome,species+" alternate tail returns to authored default");
                rig.SetPreviewVisibility(false,false);Check(!rig.WeaponArmsVisible&&Part("leftArm_mesh").IsVisibleInTree(),species+" uncovered preview restores folded arm");
                rig.SetPreviewVisibility(true,true);Check(rig.WeaponArmsVisible,species+" equipped preview restores held shape");
                var gripParameter=arm.GetInstanceShaderParameter("grip");
                rig.FlashHit();Check(arm.MaterialOverlay is ShaderMaterial flash&&flash.Shader.ResourcePath.EndsWith("character_grip_flash.gdshader"),species+" hit overlay preserves wing deformation");
                rig.GetNode<Node>("HitFlash")._Process(.2);
                rig.ShowAs(VisionSystem.Visibility.Silhouette);rig.UpdateWeaponGrip();
                Check(rig.GripConnectionError<.002f&&arm.GetInstanceShaderParameter("grip").Equals(gripParameter),species+" silhouette override keeps grip parameters");
                rig.Free();
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("GROUNDED_ART_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitHygiene.Collect();GetTree().Quit(1);}
    }
}
