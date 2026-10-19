using System.Collections.Generic;

namespace HordeShooter;

// One set of limits is shared by the editor, saved data and runtime. Values are cosmetic.
public sealed record AppearanceScaleOption(string Key,string LabelKey,string Tab,float Min,float Max);
public static class AppearanceCatalog
{
    public static bool IsBird(string species)=>species is "goose" or "duck";
    public static readonly IReadOnlyList<AppearanceScaleOption> Scales=new[]
    {
        new AppearanceScaleOption("body_width","custom_body_width","body",.88f,1.12f),
        new AppearanceScaleOption("body_height","custom_body_height","body",.90f,1.10f),
        new AppearanceScaleOption("eye_size","custom_eye_size","face",.70f,1.45f),
        new AppearanceScaleOption("eye_spacing","custom_eye_spacing","face",.85f,1.12f),
        new AppearanceScaleOption("beak_size","custom_beak_size","face",.75f,1.25f),
        new AppearanceScaleOption("ear_size","custom_ear_size","head",.75f,1.25f),
        new AppearanceScaleOption("wing_size","custom_wing_size","limbs",.80f,1.25f),
        new AppearanceScaleOption("feet_size","custom_feet_size","limbs",.80f,1.25f),
        new AppearanceScaleOption("tail_size","custom_tail_size","tail",.65f,1.30f),
    };
    public static AppearanceProfile CreatePreset(string species,int index)
    {
        var p=new AppearanceProfile();
        if(index==1)
        {
            p.BodyWidth=1.10f;p.BodyHeight=.93f;p.EyeSize=1.35f;p.EyeSpacing=1.04f;
            p.BeakSize=.90f;p.BeakStyle=1;p.EarSize=.90f;p.TailSize=1.15f;
            p.Hat=0;p.Goggles=false;p.HairStyle=1;p.Vest=false;p.Pack=false;
        }
        else if(index==2)
        {
            p.BodyWidth=.94f;p.BodyHeight=1.06f;p.EyeSize=1.10f;p.FaceStyle=1;
            p.EarSize=1.10f;p.TailSize=.85f;p.VestStyle=1;p.Headset=true;p.Goggles=false;
            p.HatColor="#344b45";p.VestColor="#3a4a45";p.PackColor="#4c5c4b";
        }
        return p;
    }
}
