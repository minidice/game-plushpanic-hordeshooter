using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    public const float MeleeWindupTime=.11f;
    public const float MeleeImpactTime=.17f;
    public const float MeleeFollowThroughTime=.23f;
    public const float MeleeDuration=.42f;
    private float _meleePoseTime=-1;

    /// <summary>Controller-owned elapsed seconds. Negative means idle/cancelled; damage uses the same impact marker.</summary>
    public void SetMeleePose(float elapsed)
    {
        _meleePoseTime=elapsed;
        ApplyCarryPose(0);UpdateWeaponGrip();
    }

    private void ApplyMeleeCarryPose()
    {
        var restHand=new Vector3(.51f,.90f,.28f);
        var restAngles=new Vector3(-.30f,.20f,-.12f);
        var hand=restHand;var angles=restAngles;
        float elapsed=_meleePoseTime;
        if(elapsed>=0&&elapsed<MeleeDuration)
        {
            var windHand=new Vector3(.63f,1.08f,.08f);
            var windAngles=new Vector3(-.62f,1.10f,-.22f);
            var impactHand=new Vector3(.38f,.96f,.46f);
            var impactAngles=new Vector3(.06f,-.10f,-.06f);
            var followHand=new Vector3(.12f,.94f,.43f);
            var followAngles=new Vector3(.14f,-1.04f,.08f);
            if(elapsed<MeleeWindupTime)
                Blend(restHand,windHand,restAngles,windAngles,elapsed/MeleeWindupTime,out hand,out angles);
            else if(elapsed<MeleeImpactTime)
                Blend(windHand,impactHand,windAngles,impactAngles,(elapsed-MeleeWindupTime)/(MeleeImpactTime-MeleeWindupTime),out hand,out angles,false);
            else if(elapsed<MeleeFollowThroughTime)
                Blend(impactHand,followHand,impactAngles,followAngles,(elapsed-MeleeImpactTime)/(MeleeFollowThroughTime-MeleeImpactTime),out hand,out angles,false);
            else
                Blend(followHand,restHand,followAngles,restAngles,(elapsed-MeleeFollowThroughTime)/(MeleeDuration-MeleeFollowThroughTime),out hand,out angles);
        }
        // Rotate around the actual right hand marker. The visual root is not the wrist.
        var gripLocal=InRig(Weapon).AffineInverse()*InRig(_heldWeapon!.RightGrip).Origin;
        var basis=Basis.FromEuler(angles);
        Weapon.Transform=new Transform3D(basis,hand-basis*gripLocal);
    }

    private static void Blend(Vector3 fromHand,Vector3 toHand,Vector3 fromAngles,Vector3 toAngles,float phase,out Vector3 hand,out Vector3 angles,bool ease=true)
    {
        if(ease)phase=Mathf.SmoothStep(0,1,phase);
        hand=fromHand.Lerp(toHand,phase);angles=fromAngles.Lerp(toAngles,phase);
    }
}
