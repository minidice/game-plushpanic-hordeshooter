using Godot;
using System;
namespace HordeShooter.Tests;

/// <summary>Face parts share a head pivot but the continuous torso does not: eyes must stay in front of its skin.</summary>
public partial class BirdFaceVisibilityCheck : Node
{
    private int _checks;
    private static float HitDistance(MeshInstance3D mesh, Vector3 origin, Vector3 direction)
    {
        float distance=float.PositiveInfinity;
        var faces=mesh.Mesh.GetFaces();
        for(int i=0;i<faces.Length;i+=3)
        {
            var hit=Geometry3D.RayIntersectsTriangle(origin,direction,
                mesh.GlobalTransform*faces[i],mesh.GlobalTransform*faces[i+1],mesh.GlobalTransform*faces[i+2]);
            if(hit.VariantType==Variant.Type.Vector3)distance=Mathf.Min(distance,origin.DistanceTo(hit.AsVector3()));
        }
        return distance;
    }
    public override void _Ready()
    {
        int exit=0;
        try
        {
            foreach(string species in new[]{"goose","duck"})
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(rig);
                try
                {
                    var torso=(MeshInstance3D)rig.Model.FindChild(species+"_torso_mesh",true,false);
                    foreach(var profile in new[]{new AppearanceProfile{Hat=0,EyeSize=.7f},new AppearanceProfile{Hat=0},new AppearanceProfile{Hat=0,EyeSize=1.45f},
                        new AppearanceProfile{Hat=0,EyeSize=.7f,EyeSpacing=.85f,FaceStyle=2,BodyWidth=.88f,BodyHeight=.9f},
                        new AppearanceProfile{Hat=0,EyeSize=1.45f,EyeSpacing=1.12f,FaceStyle=1,BodyWidth=1.12f,BodyHeight=1.1f}})
                    {
                        rig.ApplyAppearance(profile);rig.ResetPose();
                        for(int frame=0;frame<90;frame++)
                        {
                            var velocity=frame<15?Vector3.Zero:frame<30?Vector3.Forward*4.4f:frame<45?Vector3.Back*4.4f:Vector3.Zero;
                            rig.Animate(1f/60,velocity.Length()>0?1:0,frame>=45&&frame<65?1:0,0,velocity);
                            foreach(string side in new[]{"left","right"})
                            {
                                var eye=(MeshInstance3D)rig.Model.FindChild(species+"_"+side+"Eye_mesh",true,false);
                                var center=eye.GlobalTransform*eye.Mesh.GetAabb().GetCenter();
                                var origin=center+rig.Forward*2;
                                float eyeHit=HitDistance(eye,origin,-rig.Forward),skinHit=HitDistance(torso,origin,-rig.Forward);
                                if(!float.IsFinite(eyeHit)||eyeHit>=skinHit-.0001f)
                                    throw new Exception($"{species}/{side} eye hidden by torso: size={profile.EyeSize} frame={frame} eye={eyeHit:F5} skin={skinHit:F5}");
                                _checks++;
                            }
                        }
                    }
                }
                finally{rig.Free();}
            }
            GD.Print("BIRD_FACE_VISIBILITY_PASS "+_checks);
        }
        catch(Exception e){GD.PushError("BIRD_FACE_VISIBILITY_FAIL "+e);exit=1;}
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
}
