using Godot;
using System.Collections.Generic;

namespace HordeShooter;

// One set of limits is shared by the editor, saved data and runtime. Values are cosmetic.
public sealed record AppearanceScaleOption(string Key,string LabelKey,string Tab,float Min,float Max,float Default=1f,string Unit="×",bool MammalOnly=false);

/// <summary>
/// The colors the model generator painted each species with (prototypes/character-lab/src/model.mjs `palette`).
/// Primary = main fur, Secondary = belly, Marking = face pattern (raccoon mask, red panda cheeks).
/// Body tones recolor these regions; parts painted with the same colors (ears, arms, crest) follow along.
/// </summary>
public sealed record BodyPalette(Color Primary,Color Secondary,Color? Marking);

public static class AppearanceCatalog
{
    public static bool IsBird(string species)=>species is "goose" or "duck";
    public const int EarStyleCount=4;   // 기본 · 둥근 · 뾰족 · 접힌
    public const int TailStyleCount=5;  // 기본 · 풍성 · 길고 가는 · 짧고 동근 · 말린
    public static readonly IReadOnlyList<AppearanceScaleOption> Scales=new[]
    {
        new AppearanceScaleOption("body_width","custom_body_width","body",.88f,1.12f),
        new AppearanceScaleOption("body_height","custom_body_height","body",.90f,1.10f),
        new AppearanceScaleOption("eye_size","custom_eye_size","face",.70f,1.45f),
        new AppearanceScaleOption("eye_spacing","custom_eye_spacing","face",.85f,1.12f),
        new AppearanceScaleOption("beak_size","custom_beak_size","face",.75f,1.25f),
        new AppearanceScaleOption("ear_size","custom_ear_size","head",.75f,1.25f,MammalOnly:true),
        new AppearanceScaleOption("ear_angle","custom_ear_angle","head",-20f,40f,0f,"°",MammalOnly:true),
        new AppearanceScaleOption("wing_size","custom_wing_size","limbs",.80f,1.25f),
        new AppearanceScaleOption("feet_size","custom_feet_size","limbs",.80f,1.25f),
        new AppearanceScaleOption("tail_size","custom_tail_size","tail",.65f,1.30f),
    };

    public static BodyPalette? Palette(string species)=>species switch
    {
        "goose"=>new BodyPalette(new Color("e5e1cf"),new Color("f5ebd0"),null),
        "duck"=>new BodyPalette(new Color("bb7934"),new Color("edbd68"),null),
        "raccoon"=>new BodyPalette(new Color("78858a"),new Color("e6d7b9"),new Color("30383b")),
        "redpanda"=>new BodyPalette(new Color("aa4b2c"),new Color("382e2a"),new Color("efd6a7")),
        _=>null,
    };
    public static bool HasMarking(string species)=>Palette(species)?.Marking!=null;

    /// <summary>Presets come from content/data/appearance_presets.json (shared first, then the species' own).</summary>
    public static AppearanceProfile CreatePreset(string species,int index)=>AppearancePresets.Create(species,index);
}
