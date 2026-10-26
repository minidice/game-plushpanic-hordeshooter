using Godot;
using System;
using System.Linq;
namespace HordeShooter.Tests;

public partial class LionLimbCheck : Node
{
    private int _checks;
    private string Species => OS.GetCmdlineUserArgs().Contains("--rhino") ? "rhino" : OS.GetCmdlineUserArgs().Contains("--tiger") ? "tiger" : OS.GetCmdlineUserArgs().Contains("--reindeer") ? "reindeer" : "lion";
    private void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string message) { if(!ok)throw new Exception(message);_checks++; }
    private static Vector3[] Vertices(MeshInstance3D mesh) => Enumerable.Range(0,mesh.Mesh.GetSurfaceCount()).SelectMany(i=>mesh.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
    internal static float SurfaceDistance(MeshInstance3D mesh,Vector3 point)
    {
        // Contact is with the triangle surface, not only its perimeter vertices (wide capped shoulders).
        var faces=mesh.Mesh.GetFaces();float distance=float.PositiveInfinity;
        for(int i=0;i<faces.Length;i+=3)
        {
            var a=mesh.GlobalTransform*faces[i];var b=mesh.GlobalTransform*faces[i+1];var c=mesh.GlobalTransform*faces[i+2];
            var normal=(b-a).Cross(c-a);if(normal.LengthSquared()<1e-12f)continue;normal=normal.Normalized();
            float plane=(point-a).Dot(normal);var projected=point-normal*plane;
            bool inside=(b-a).Cross(projected-a).Dot(normal)>=-1e-6f && (c-b).Cross(projected-b).Dot(normal)>=-1e-6f && (a-c).Cross(projected-c).Dot(normal)>=-1e-6f;
            float edge=Mathf.Min(point.DistanceTo(Geometry3D.GetClosestPointToSegment(point,a,b)),
                Mathf.Min(point.DistanceTo(Geometry3D.GetClosestPointToSegment(point,b,c)),point.DistanceTo(Geometry3D.GetClosestPointToSegment(point,c,a))));
            distance=Mathf.Min(distance,inside?Mathf.Abs(plane):edge);
        }
        return distance;
    }
    public override void _Ready()
    {
        int exit=0;
        try {
            foreach(var weapon in new[]{"club","knife","woodbow","mp5","ak47","m4"}) Verify(weapon);
            GD.Print(Species.ToUpperInvariant()+"_LIMB_CHECK_PASS "+_checks);
        } catch(Exception e) { GD.PushError(Species.ToUpperInvariant()+"_LIMB_CHECK_FAIL "+e);exit=1; }
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Verify(string weapon)
    {
        bool hunter=weapon is "club" or "knife" or "woodbow";
        var identity=new CommonIdentity(hunter?"hunter":"armed",Species,"red",weapon);
        var rig=CharacterRig.Create(CommonRoster.Current.LookFor(identity));AddChild(rig);CommonRoster.Current.Apply(rig,identity);
        try {
            foreach(string side in new[]{"right","left"})
                Check(rig.Model.FindChild("common_"+Species+"_"+side+"Grip",true,false)!=null,"authored grip anchor exists");
            var paws=new[]{(MeshInstance3D)rig.Model.FindChild("RightForearm",true,false),(MeshInstance3D)rig.Model.FindChild("LeftForearm",true,false)};
            var baseline=paws.Select(p=>Vertices(p)).ToArray();
            float pawSize=1;
            void Sample() {
                Check(rig.GripConnectionError<.002f,weapon+": actual grip anchors coincide");
                for(int k=0;k<2;k++) {
                    var paw=paws[k];string side=k==0?"right":"left";
                    var original=(MeshInstance3D)rig.Model.FindChild("common_"+Species+"_"+side+"Paw_mesh",true,false);
                    Check(paw.Mesh==original.Mesh && !original.IsVisibleInTree(),"single visible original paw surface");
                    var points=baseline[k];
                    for(int i=1;i<points.Length;i+=Math.Max(1,points.Length/12)) {
                        float expected=points[0].DistanceTo(points[i])*pawSize;
                        float actual=(paw.Transform*points[0]).DistanceTo(paw.Transform*points[i]);
                        Check(Mathf.Abs(actual-expected)<.0001f,weapon+": actual paw vertex distances stay rigid in shared character visual space");
                    }
                    var forearm=paw.GetNode<MeshInstance3D>("Forearm");
                    var shoulder=(Node3D)rig.Model.FindChild("common_"+Species+"_"+side+"Arm",true,false);
                    var wrist=paw.GetNode<Node3D>("Wrist");
                    var shoulderMarker=shoulder.GetNodeOrNull<Node3D>("common_"+Species+"_"+side+"Shoulder");
                    float shoulderDistance=SurfaceDistance(paw.GetNode<MeshInstance3D>("UpperArm"),shoulderMarker?.GlobalPosition??shoulder.GlobalPosition);
                    Check(shoulderDistance<.10f,$"forearm surface meets anatomical shoulder: {weapon}/{side} {shoulderDistance:F5} size={pawSize}");
                    Check(SurfaceDistance(forearm,wrist.GlobalPosition)<.10f,"forearm surface meets actual paw wrist");
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
            foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm"),p.GetNode<MeshInstance3D>("UpperArm"),p.GetNode<MeshInstance3D>("RestArm")}))
                Check(mesh.GetNode<MeshInstance3D>("StatusSurface").Mesh==mesh.Mesh,"status uses paw and forearm actual surfaces");
            rig.FlashDeath();foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm"),p.GetNode<MeshInstance3D>("UpperArm"),p.GetNode<MeshInstance3D>("RestArm")}))Check(mesh.MaterialOverlay!=null,"hit flash includes paw and forearm");
            status.Clear();rig.Weapon.Visible=false;rig.UpdateWeaponGrip();Check(paws.All(p=>!p.IsVisibleInTree()),"holster hides complete limbs");
            rig.Weapon.Visible=true;rig.UpdateWeaponGrip();rig.ShowAs(VisionSystem.Visibility.Hidden);Check(paws.All(p=>!p.IsVisibleInTree()),"hidden limbs do not leak");
            rig.ShowAs(VisionSystem.Visibility.Silhouette);foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm"),p.GetNode<MeshInstance3D>("UpperArm"),p.GetNode<MeshInstance3D>("RestArm")}))Check(mesh.MaterialOverride==CharacterRig.SilhouetteMaterial,"silhouette includes both surfaces");
        } finally {rig.Free();}
    }
}
