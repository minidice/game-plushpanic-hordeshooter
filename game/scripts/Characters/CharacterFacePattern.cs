using Godot;

namespace HordeShooter;

/// <summary>CPU reference of the species face design. Keep shader and authoring formulas in sync.
/// Result: light face region, then dark region painted over it. Distances are in authored metres.</summary>
internal static class CharacterFacePattern
{
    public static Vector2 Regions(string species,Vector2 p,float softness=.25f)
    {
        float x=Mathf.Abs(p.X),v=p.Y;
        float Oval(float cx,float cy,float rx,float ry)=>new Vector2((x-cx)/rx,(v-cy)/ry).Length()*Mathf.Min(rx,ry)-Mathf.Min(rx,ry);
        float edge=.002f+.022f*Mathf.Clamp(softness,0,1);
        float Fill(float d){float t=Mathf.Clamp((d+edge)/(2*edge),0,1);return 1-t*t*(3-2*t);}
        if(species=="raccoon")
        {
            float center=.045f+.070f*Step(x,.08f,.38f);
            float thick=.042f+.050f*Step(x,.035f,.17f);
            float q=Mathf.Pow(x/.445f,6)+Mathf.Pow((v-center)/thick,2);
            return new Vector2(Fill(Oval(0,-.045f,.445f,.26f)),Fill((Mathf.Sqrt(q)-1)*.065f));
        }
        if(species=="redpanda")
        {
            float cheek=SmoothMin(Oval(.285f,-.075f,.18f,.175f),Oval(0,-.155f,.31f,.105f),.075f);
            float tearX=.20f+.035f*Mathf.Clamp((.06f-v)/.22f,0,1);
            float tear=Oval(tearX,-.025f,.032f,.100f);
            return new Vector2(Fill(cheek),Fill(tear));
        }
        return Vector2.Zero;
    }
    private static float Step(float x,float a,float b){float t=Mathf.Clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);}
    private static float SmoothMin(float a,float b,float k){float h=Mathf.Max(k-Mathf.Abs(a-b),0)/k;return Mathf.Min(a,b)-h*h*k*.25f;}
}
