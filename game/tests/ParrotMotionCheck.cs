using Godot;
using System;

namespace HordeShooter.Tests;

// Continuous parrot neck must not expose a cut plane under the real acceleration/hit spring.
public partial class ParrotMotionCheck : Node
{
    private int _checks;
    private void Check(bool value,string message) { if(!value)throw new Exception(message);_checks++; }
    public override void _Ready()
    {
        int exit=0;
        try
        {
            foreach(string species in new[]{"parrot","eagle","lion"})
                foreach(string weapon in new[]{"club","woodbow","m4"}) Verify(species,weapon);
            GD.Print("PARROT_MOTION_CHECK_PASS "+_checks);
        }
        catch(Exception error){GD.PushError("PARROT_MOTION_CHECK_FAIL "+error);exit=1;}
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Verify(string species,string weapon)
    {
        var identity=new CommonIdentity(weapon=="m4"?"armed":"hunter",species,"red",weapon);
        var rig=CharacterRig.Create(CommonRoster.Current.LookFor(identity));AddChild(rig);CommonRoster.Current.Apply(rig,identity);
        try
        {
            var home=rig.Head!.Transform;float maxOffset=0,maxTilt=0;
            foreach(var direction in new[]{Vector3.Right,Vector3.Left,Vector3.Forward,Vector3.Back})
            {
                rig.ResetPose();rig.Jolt(direction*24);
                for(int i=0;i<100;i++)
                {
                    var velocity=i<30?direction*4.7f:i<60?-direction*4.7f:Vector3.Zero;
                    rig.Animate(1f/60,i<60?1:0,i<5?.8f:0,i<5?1:0,velocity);
                    var offset=(rig.Head.Position-home.Origin).Length();
                    var tilt=(home.Basis.Inverse()*rig.Head.Basis).GetRotationQuaternion().GetAngle();
                    Check(float.IsFinite(offset)&&float.IsFinite(tilt),"head motion finite");
                    maxOffset=Mathf.Max(maxOffset,offset);maxTilt=Mathf.Max(maxTilt,Mathf.Abs(tilt));
                    Check(rig.GripConnectionError<.002f,"motion keeps weapon grip attached");
                    if(species=="parrot")
                    {
                        Check(offset<.012f,"parrot neck remains within 12mm under acceleration and hit impulse");
                        Check(Mathf.Abs(tilt)<.02f,"parrot neck tilt stays subtle under spring impulse");
                    }
                }
                rig.ResetPose();Check(rig.Head.Transform.IsEqualApprox(home),"head reset restores authored pivot");
            }
            if(species!="parrot")Check(maxOffset>.10f&&maxTilt>.15f,"other species retain existing spring range");
            GD.Print($"HEAD_MOTION {species}/{weapon} offset={maxOffset:F6} tilt={maxTilt:F6}");
        }
        finally{rig.Free();}
    }
}
