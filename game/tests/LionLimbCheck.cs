using Godot;
using System;
using System.Linq;
namespace HordeShooter.Tests;

public partial class LionLimbCheck : Node
{
    private int _checks;
    private void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string message) { if(!ok)throw new Exception(message);_checks++; }
    private static Vector3[] Vertices(MeshInstance3D mesh) => Enumerable.Range(0,mesh.Mesh.GetSurfaceCount()).SelectMany(i=>mesh.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
    public override void _Ready()
    {
        int exit=0;
        try {
            foreach(var weapon in new[]{"club","knife","woodbow","mp5","ak47","m4"}) Verify(weapon);
            GD.Print("LION_LIMB_CHECK_PASS "+_checks);
        } catch(Exception e) { GD.PushError("LION_LIMB_CHECK_FAIL "+e);exit=1; }
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Verify(string weapon)
    {
        bool hunter=weapon is "club" or "knife" or "woodbow";
        var identity=new CommonIdentity(hunter?"hunter":"armed","lion","red",weapon);
        var rig=CharacterRig.Create(CommonRoster.Current.LookFor(identity));AddChild(rig);CommonRoster.Current.Apply(rig,identity);
        try {
            foreach(string side in new[]{"right","left"})
                Check(rig.Model.FindChild("common_lion_"+side+"Grip",true,false)!=null,"authored lion grip anchor exists");
            var paws=new[]{(MeshInstance3D)rig.Model.FindChild("RightForearm",true,false),(MeshInstance3D)rig.Model.FindChild("LeftForearm",true,false)};
            var baseline=paws.Select(p=>Vertices(p)).ToArray();
            float pawSize=1;
            void Sample() {
                Check(rig.GripConnectionError<.002f,weapon+": actual grip anchors coincide");
                for(int k=0;k<2;k++) {
                    var paw=paws[k];string side=k==0?"right":"left";
                    var original=(MeshInstance3D)rig.Model.FindChild("common_lion_"+side+"Paw_mesh",true,false);
                    Check(paw.Mesh==original.Mesh && !original.IsVisibleInTree(),"single visible original paw surface");
                    var points=baseline[k];
                    for(int i=1;i<points.Length;i+=Math.Max(1,points.Length/12)) {
                        float expected=points[0].DistanceTo(points[i])*pawSize;
                        float actual=(paw.Transform*points[0]).DistanceTo(paw.Transform*points[i]);
                        Check(Mathf.Abs(actual-expected)<.0001f,weapon+": actual paw vertex distances stay rigid in shared character visual space");
                    }
                    var forearm=paw.GetNode<MeshInstance3D>("Forearm");
                    var shoulder=(Node3D)rig.Model.FindChild("common_lion_"+side+"Arm",true,false);
                    var wrist=paw.GetNode<Node3D>("Wrist");
                    var vertices=Vertices(forearm).Select(v=>forearm.GlobalTransform*v).ToArray();
                    Check(vertices.Min(v=>v.DistanceTo(shoulder.GlobalPosition))<.10f,"forearm surface meets anatomical shoulder");
                    Check(vertices.Min(v=>v.DistanceTo(wrist.GlobalPosition))<.10f,"forearm surface meets actual paw wrist");
                }
            }
            var idle=paws.Select(p=>p.Transform).ToArray();
            foreach(float elapsed in new[]{-1f,0f,.16f,.32f,.49f,.5f,.6f,.89f,.9f}) {
                rig.Animate(0,0,0,0,Vector3.Zero);if(hunter)rig.SetHunterAttackPose(weapon,elapsed,.5f,.4f);Sample();
            }
            if(!hunter) {
                var state=new WeaponState(ArmoryCatalog.Weapon(weapon),0,30);state.StartReload();
                for(int i=0;i<20;i++){state.Tick(state.Def.ReloadTime/22,false);rig.SetReloadPose(.1f,state);rig.Animate(.016f,1,.4f,.6f,Vector3.Zero);Sample();}
            }
            rig.ResetPose();
            Check(paws.Select((p,i)=>p.Transform.IsEqualApprox(idle[i])).All(v=>v),"reset restores rigid paws");
            rig.ZiplinePose(false,.4f,.5f);Check(paws.All(p=>p.IsVisibleInTree()),"traversal visible");rig.EndZiplinePose();rig.ResetPose();
            foreach(float size in new[]{.8f,1.2f,1f}) {
                pawSize=size;rig.ApplyAppearance(new AppearanceProfile{WingSize=size});rig.Animate(0,0,0,0,Vector3.Zero);Sample();
            }
            var status=StatusVisualController.Attach(rig);status.SetStates(new[]{new StatusVisualState("poison",5,1)});
            foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm")}))
                Check(mesh.GetNode<MeshInstance3D>("StatusSurface").Mesh==mesh.Mesh,"status uses paw and forearm actual surfaces");
            rig.FlashDeath();foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm")}))Check(mesh.MaterialOverlay!=null,"hit flash includes paw and forearm");
            status.Clear();rig.Weapon.Visible=false;rig.UpdateWeaponGrip();Check(paws.All(p=>!p.IsVisibleInTree()),"holster hides complete limbs");
            rig.Weapon.Visible=true;rig.UpdateWeaponGrip();rig.ShowAs(VisionSystem.Visibility.Hidden);Check(paws.All(p=>!p.IsVisibleInTree()),"hidden limbs do not leak");
            rig.ShowAs(VisionSystem.Visibility.Silhouette);foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm")}))Check(mesh.MaterialOverride==CharacterRig.SilhouetteMaterial,"silhouette includes both surfaces");
        } finally {rig.Free();}
    }
}
