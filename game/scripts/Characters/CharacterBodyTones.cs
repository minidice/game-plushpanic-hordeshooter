using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Continuous body/face masks evaluated per fragment. Palette edits only change uniforms;
/// geometry, shadows and other characters' materials stay untouched.</summary>
public partial class CharacterRig
{
    private MeshInstance3D? _skin;
    private ArrayMesh? _skinOriginal;
    private string _toneSpecies="";
    private ShaderMaterial? _skinMaterial;

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
        if(_skinMaterial==null)
        {
            _skinMaterial=new ShaderMaterial { Shader=(Shader)ContentCache.Load("res://shaders/character_skin.gdshader")! };
            _skinMaterial.SetShaderParameter("species",_toneSpecies=="raccoon"?1:_toneSpecies=="redpanda"?2:0);
            _skinMaterial.SetShaderParameter("body_height",_toneSpecies=="goose"?1.74f:1.62f);
            _skin.SetSurfaceOverrideMaterial(0,_skinMaterial);
        }
        _skinMaterial.SetShaderParameter("marking_softness",p.MarkingSoftness);
        _skinMaterial.SetShaderParameter("eye_spacing",p.EyeSpacing);
        _skinMaterial.SetShaderParameter("primary_color",Tone(p.Body,palette.Primary));
        _skinMaterial.SetShaderParameter("secondary_color",Tone(p.BodySecondary,palette.Secondary));
        _skinMaterial.SetShaderParameter("marking_color",Tone(p.BodyMarking,palette.Marking??palette.Secondary));
    }

    /// <summary>CPU reference samples of the continuous shader, for palette/save regression checks.
    /// These are not vertex colors used by the renderer. GPU mask precision has a separate render check.</summary>
    public Vector3[] SkinToneWeights()=>_skinOriginal==null?Array.Empty<Vector3>():(Vector3[])Weights(_toneSpecies,_skinOriginal.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array(),_appliedAppearance??new AppearanceProfile()).Clone();
    public Color[] SkinColors()
    {
        if(AppearanceCatalog.Palette(_toneSpecies) is not { } palette)return Array.Empty<Color>();
        var p=_appliedAppearance??new AppearanceProfile();
        var a=Tone(p.Body,palette.Primary).SrgbToLinear();
        var b=Tone(p.BodySecondary,palette.Secondary).SrgbToLinear();
        var c=Tone(p.BodyMarking,palette.Marking??palette.Secondary).SrgbToLinear();
        var weights=SkinToneWeights();var colors=new Color[weights.Length];
        for(int i=0;i<weights.Length;i++){var w=weights[i];colors[i]=new Color(a.R*w.X+b.R*w.Y+c.R*w.Z,a.G*w.X+b.G*w.Y+c.G*w.Z,a.B*w.X+b.B*w.Y+c.B*w.Z,1);}
        return colors;
    }

    private static Vector3[] Weights(string species,Vector3[] positions,AppearanceProfile profile)
    {
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
                float faceFront=Step(radius>1e-5f?at.Z/(.86f*radius):0,.02f,.35f);
                var regions=CharacterFacePattern.Regions(species,new Vector2(at.X/profile.EyeSpacing,at.Y-1.34f),profile.MarkingSoftness);
                cream=regions.X*faceFront;fur*=1-cream;belly*=1-cream;
                dark=regions.Y*faceFront;fur*=1-dark;belly*=1-dark;cream*=1-dark;
            }
            // Roles: raccoon face cream is the belly color; red panda's belly is dark and its cheeks cream.
            weights[i]=species switch
            {
                "raccoon"=>new Vector3(fur,belly+cream,dark),
                "redpanda"=>new Vector3(fur+dark*.50f,belly+dark*.50f,cream),
                _=>new Vector3(fur,belly,0),
            };
        }
        return weights;
    }

    private static float Step(float x,float a,float b)
    {
        if(x<=a)return 0;if(x>=b)return 1;
        float t=(x-a)/(b-a);return t*t*(3-2*t);
    }
}
