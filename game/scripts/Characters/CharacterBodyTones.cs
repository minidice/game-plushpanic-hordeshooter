using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Two/three-tone body colors. The skin's vertex colors were painted by the model generator
/// (prototypes/character-lab/src/model.mjs `beanSkin`) as a blend of the species palette:
/// main fur shaded by height, a belly patch on the lower front, and face patches for the mammals.
/// The same formula, evaluated at each vertex position, gives how much of each tone a vertex holds.
/// A new tone adds (new − old) × that weight on top of the original color, so untouched tones and
/// the default look stay exactly as imported.
/// </summary>
public partial class CharacterRig
{
    private MeshInstance3D? _skin;
    private ArrayMesh? _skinOriginal;
    private string _toneSpecies="";
    private Godot.Collections.Array? _skinArrays;
    private Color[]? _skinColors;
    private ArrayMesh? _skinToned;
    private (string,string,string) _skinTonesShown=("","","");
    // Per-species tone weights for the imported skin (plain arrays, not Godot resources).
    private static readonly Dictionary<string,Vector3[]> ToneWeights=new();

    public string ToneSpecies=>_toneSpecies;

    private void PrepareBodyTones()
    {
        // Zombie definitions use composite IDs; the imported nodes keep the species name.
        foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            if(Model.FindChild(species+"_torso_mesh",true,false) is MeshInstance3D { Mesh: ArrayMesh mesh } skin && mesh.GetSurfaceCount()>0)
            {_skin=skin;_skinOriginal=mesh;_toneSpecies=species;return;}
    }

    /// <summary>Target color for a tone: the chosen one, or the species' own.</summary>
    private static Color Tone(string chosen,Color own)=>chosen.Length>0&&Color.HtmlIsValid(chosen)?new Color(chosen):own;

    private void ApplyBodyTones(AppearanceProfile p)
    {
        if(_skin==null||_skinOriginal==null||AppearanceCatalog.Palette(_toneSpecies) is not { } palette)return;
        var overrideMaterial=_skin.GetSurfaceOverrideMaterial(0);
        var tones=(p.Body,p.BodySecondary,p.BodyMarking);
        if(!p.HasCustomBodyTones)
        {
            if(_skin.Mesh!=_skinOriginal){_skin.Mesh=_skinOriginal;_skin.SetSurfaceOverrideMaterial(0,overrideMaterial);}
            _skinTonesShown=tones;
            return;
        }
        // Sliders re-apply the whole profile on every tick; only rebuild when a tone actually changed.
        if(tones==_skinTonesShown&&_skinToned!=null&&_skin.Mesh==_skinToned)return;
        // Reading a surface back can stall the renderer, so the imported arrays are fetched once per rig.
        _skinArrays??=_skinOriginal.SurfaceGetArrays(0);
        var arrays=_skinArrays.Duplicate();
        var positions=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        _skinColors??=arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        var colors=(Color[])_skinColors.Clone();
        if(colors.Length!=positions.Length)return;
        var weights=Weights(_toneSpecies,positions);
        // Vertex colors are linear; palette and picked colors are sRGB.
        Vector3 Delta(string chosen,Color own){var a=Tone(chosen,own).SrgbToLinear();var b=own.SrgbToLinear();return new Vector3(a.R-b.R,a.G-b.G,a.B-b.B);}
        var dPrimary=Delta(p.Body,palette.Primary);
        var dSecondary=Delta(p.BodySecondary,palette.Secondary);
        var dMarking=palette.Marking is { } marking?Delta(p.BodyMarking,marking):Vector3.Zero;
        for(int i=0;i<colors.Length;i++)
        {
            var w=weights[i];
            var d=dPrimary*w.X+dSecondary*w.Y+dMarking*w.Z;
            var c=colors[i];
            colors[i]=new Color(Mathf.Clamp(c.R+d.X,0,1),Mathf.Clamp(c.G+d.Y,0,1),Mathf.Clamp(c.B+d.Z,0,1),c.A);
        }
        arrays[(int)Mesh.ArrayType.Color]=colors;
        _skinToned??=new ArrayMesh();
        _skinToned.ClearSurfaces();
        // Uncompressed, and no shadow mesh: Godot also draws the shadow mesh in the depth pre-pass, and the
        // import's compressed one sits a hair off this rebuilt surface, which blacked out patches of skin (I-52).
        _skinToned.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        _skinToned.SurfaceSetMaterial(0,_skinOriginal.SurfaceGetMaterial(0));
        _skin.Mesh=_skinToned;
        _skin.SetSurfaceOverrideMaterial(0,overrideMaterial);
        _skinTonesShown=tones;
    }

    /// <summary>Exposed for checks: (primary, secondary, marking) weight of each skin vertex.</summary>
    public Vector3[] SkinToneWeights()=>_skinOriginal==null?Array.Empty<Vector3>():(Vector3[])Weights(_toneSpecies,_skinOriginal.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).Clone();
    public Color[] SkinColors()=>_skin?.Mesh is ArrayMesh m?m.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Color].AsColorArray():Array.Empty<Color>();

    private static Vector3[] Weights(string species,Vector3[] positions)
    {
        string key=species+":"+positions.Length;
        if(ToneWeights.TryGetValue(key,out var cached))return cached;
        var weights=new Vector3[positions.Length];
        // Generator defaults (head, width, height, neck = 1). The goose has a taller neck.
        float total=1.62f+(species=="goose"?.12f:0f);
        bool mammal=species is "raccoon" or "redpanda";
        for(int i=0;i<positions.Length;i++)
        {
            var at=positions[i];
            float t=Mathf.Clamp((at.Y-.22f)/total,0,1),y=2*t-1;
            float round=Mathf.Sqrt(Mathf.Max(0,1-y*y));
            float radius=.47f*Mathf.Pow(round,.62f)*(1.08f-.14f*t);
            float front=Step(radius>1e-5f?at.Z/(.86f*radius):0,.2f,.7f);
            float belly=front*(1-Step(t,.48f,.66f))*.65f;
            float fur=(.88f+.12f*t)*(1-belly);
            float cream=0,dark=0;
            if(mammal)
            {
                float u=at.X,v=at.Y-1.34f;
                float Ellipse(float cx,float cy,float rx,float ry)=>1-Step(Sq((u-cx)/rx)+Sq((v-cy)/ry),.85f,1.12f);
                float patch=species=="raccoon"?Ellipse(0,-.045f,.43f,.24f)
                    :Mathf.Max(Mathf.Max(Ellipse(-.26f,-.06f,.17f,.17f),Ellipse(.26f,-.06f,.17f,.17f)),Mathf.Max(Ellipse(0,-.12f,.23f,.11f),Mathf.Max(Ellipse(-.19f,.13f,.11f,.055f),Ellipse(.19f,.13f,.11f,.055f))));
                cream=patch*front;fur*=1-cream;belly*=1-cream;
                if(species=="raccoon")
                {
                    dark=Mathf.Max(Mathf.Max(Ellipse(-.20f,.055f,.18f,.105f),Ellipse(.20f,.055f,.18f,.105f)),Ellipse(0,.04f,.20f,.045f))*front;
                    fur*=1-dark;belly*=1-dark;cream*=1-dark;
                }
            }
            // Roles: raccoon face cream is the belly color; red panda's belly is dark and its cheeks cream.
            weights[i]=species switch
            {
                "raccoon"=>new Vector3(fur,belly+cream,dark),
                "redpanda"=>new Vector3(fur,belly,cream),
                _=>new Vector3(fur,belly,0),
            };
        }
        ToneWeights[key]=weights;
        return weights;
    }

    private static float Sq(float x)=>x*x;
    private static float Step(float x,float a,float b)
    {
        if(x<=a)return 0;if(x>=b)return 1;
        float t=(x-a)/(b-a);return t*t*(3-2*t);
    }
}
