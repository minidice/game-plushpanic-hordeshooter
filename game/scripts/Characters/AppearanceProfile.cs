using Godot;
using System;

namespace HordeShooter;
public sealed class AppearanceProfile
{
    /// <summary>Body tones (absolute colors). Empty = the species' own palette color.</summary>
    public string Body {get;set;}="";
    public string BodySecondary {get;set;}="";
    public string BodyMarking {get;set;}="";
    public string Beak {get;set;}="";
    public string HatColor {get;set;}="";
    public string VestColor {get;set;}="";
    public string PackColor {get;set;}="";
    /// <summary>Kept for old saves only: arms now always follow the body tones.</summary>
    public string Wing {get;set;}="";
    public string Tail {get;set;}="";
    public string Feet {get;set;}="";
    public string Eyes {get;set;}="";
    public string HairColor {get;set;}="";
    public float BodyWidth {get;set;}=1;
    public float BodyHeight {get;set;}=1;
    public float EyeSize {get;set;}=1;
    public float EyeSpacing {get;set;}=1;
    public float BeakSize {get;set;}=1;
    public float EarSize {get;set;}=1;
    /// <summary>Degrees of outward tilt added to both ears (no hat).</summary>
    public float EarAngle {get;set;}
    public float WingSize {get;set;}=1;
    public float FeetSize {get;set;}=1;
    public float TailSize {get;set;}=1;
    public int FaceStyle {get;set;}
    public int BeakStyle {get;set;}
    public int HairStyle {get;set;}
    public int EarStyle {get;set;}
    public int TailStyle {get;set;}
    public bool Goggles {get;set;}=true;
    public bool Headset {get;set;}
    public int VestStyle {get;set;}
    public int Hat {get;set;}=1;
    public bool Vest {get;set;}=true;
    public bool Pack {get;set;}=true;
    public bool HasCustomBodyTones=>Body.Length>0||BodySecondary.Length>0||BodyMarking.Length>0;
    public string ColorFor(string channel)=>channel switch {"body"=>Body,"body2"=>BodySecondary,"body3"=>BodyMarking,"beak"=>Beak,"hat"=>HatColor,"vest"=>VestColor,"pack"=>PackColor,"wing"=>Wing,"tail"=>Tail,"feet"=>Feet,"eyes"=>Eyes,"hair"=>HairColor,_=>""};
    public void SetColor(string channel,string color)
    {switch(channel){case "body":Body=color;break;case "body2":BodySecondary=color;break;case "body3":BodyMarking=color;break;case "beak":Beak=color;break;case "hat":HatColor=color;break;case "vest":VestColor=color;break;case "pack":PackColor=color;break;case "wing":Wing=color;break;case "tail":Tail=color;break;case "feet":Feet=color;break;case "eyes":Eyes=color;break;case "hair":HairColor=color;break;}}
    public float ScaleFor(string key)=>key switch
    {"body_width"=>BodyWidth,"body_height"=>BodyHeight,"eye_size"=>EyeSize,"eye_spacing"=>EyeSpacing,"beak_size"=>BeakSize,"ear_size"=>EarSize,"ear_angle"=>EarAngle,"wing_size"=>WingSize,"feet_size"=>FeetSize,"tail_size"=>TailSize,_=>1};
    public void SetScale(string key,float value)
    {
        foreach(var option in AppearanceCatalog.Scales)if(option.Key==key)
        {value=float.IsFinite(value)?Math.Clamp(value,option.Min,option.Max):option.Default;break;}
        switch(key){case "body_width":BodyWidth=value;break;case "body_height":BodyHeight=value;break;case "eye_size":EyeSize=value;break;case "eye_spacing":EyeSpacing=value;break;case "beak_size":BeakSize=value;break;case "ear_size":EarSize=value;break;case "ear_angle":EarAngle=value;break;case "wing_size":WingSize=value;break;case "feet_size":FeetSize=value;break;case "tail_size":TailSize=value;break;}
    }
    public void Normalize()
    {
        foreach(var option in AppearanceCatalog.Scales)SetScale(option.Key,ScaleFor(option.Key));
        FaceStyle=Math.Clamp(FaceStyle,0,2);BeakStyle=Math.Clamp(BeakStyle,0,2);HairStyle=Math.Clamp(HairStyle,0,2);
        EarStyle=Math.Clamp(EarStyle,0,AppearanceCatalog.EarStyleCount-1);TailStyle=Math.Clamp(TailStyle,0,AppearanceCatalog.TailStyleCount-1);
        Hat=Math.Clamp(Hat,0,2);VestStyle=Math.Clamp(VestStyle,0,1);
    }

    /// <summary>
    /// Sets one field by its save-file key (the same keys appearance.cfg and the preset JSON use).
    /// Returns an error message for unknown keys or values of the wrong type; never throws.
    /// </summary>
    public string? TrySet(string key,Variant value)
    {
        string? color=key switch{"body"=>"body","body_secondary"=>"body2","body_marking"=>"body3","beak"=>"beak","hat_color"=>"hat","vest_color"=>"vest","pack_color"=>"pack","wing"=>"wing","tail"=>"tail","feet"=>"feet","eyes"=>"eyes","hair"=>"hair",_=>null};
        if(color!=null)
        {
            if(value.VariantType!=Variant.Type.String||!Color.HtmlIsValid(value.AsString()))return $"{key}: '{value}' is not a color like #a1b2c3";
            SetColor(color,value.AsString());return null;
        }
        foreach(var option in AppearanceCatalog.Scales)if(option.Key==key)
        {
            if(value.VariantType is not (Variant.Type.Float or Variant.Type.Int))return $"{key}: '{value}' is not a number";
            SetScale(key,(float)value.AsDouble());return null;
        }
        bool isNumber=value.VariantType is Variant.Type.Float or Variant.Type.Int;
        switch(key)
        {
            case "hat":case "vest_style":case "face_style":case "beak_style":case "hair_style":case "ear_style":case "tail_style":
                if(!isNumber)return $"{key}: '{value}' is not a whole number";
                int n=(int)value.AsDouble();
                switch(key){case "hat":Hat=n;break;case "vest_style":VestStyle=n;break;case "face_style":FaceStyle=n;break;case "beak_style":BeakStyle=n;break;case "hair_style":HairStyle=n;break;case "ear_style":EarStyle=n;break;default:TailStyle=n;break;}
                return null;
            case "vest":case "pack":case "goggles":case "headset":
                if(value.VariantType!=Variant.Type.Bool)return $"{key}: '{value}' is not true/false";
                bool b=value.AsBool();
                switch(key){case "vest":Vest=b;break;case "pack":Pack=b;break;case "goggles":Goggles=b;break;default:Headset=b;break;}
                return null;
        }
        return $"unknown appearance key '{key}'";
    }
}
public static class AppearanceStore
{
    public const string Path="user://appearance.cfg";
    /// <summary>2 = absolute body tones (2026-10-20). Sections without it saved "body" as a tint over the whole skin.</summary>
    public const int ToneVersion=2;
    public static AppearanceProfile ReadProfile(string species,string path=Path)
    {
        var cfg=new ConfigFile();cfg.Load(path);
        string ReadColor(string key){string value=cfg.GetValue(species,key,"").AsString();return value.Length>0&&Color.HtmlIsValid(value)?value:"";}
        var p=new AppearanceProfile{Body=ReadColor("body"),BodySecondary=ReadColor("body_secondary"),BodyMarking=ReadColor("body_marking"),Beak=ReadColor("beak"),HatColor=ReadColor("hat_color"),VestColor=ReadColor("vest_color"),PackColor=ReadColor("pack_color"),Wing=ReadColor("wing"),Tail=ReadColor("tail"),Feet=ReadColor("feet"),Eyes=ReadColor("eyes"),HairColor=ReadColor("hair"),Goggles=cfg.GetValue(species,"goggles",true).AsBool(),Headset=cfg.GetValue(species,"headset",false).AsBool(),VestStyle=Math.Clamp(cfg.GetValue(species,"vest_style",0).AsInt32(),0,1),Hat=Math.Clamp(cfg.GetValue(species,"hat",1).AsInt32(),0,2),Vest=cfg.GetValue(species,"vest",true).AsBool(),Pack=cfg.GetValue(species,"pack",true).AsBool(),FaceStyle=cfg.GetValue(species,"face_style",0).AsInt32(),BeakStyle=cfg.GetValue(species,"beak_style",0).AsInt32(),HairStyle=cfg.GetValue(species,"hair_style",0).AsInt32(),EarStyle=cfg.GetValue(species,"ear_style",0).AsInt32(),TailStyle=cfg.GetValue(species,"tail_style",0).AsInt32()};
        foreach(var option in AppearanceCatalog.Scales)p.SetScale(option.Key,cfg.GetValue(species,option.Key,option.Default).AsSingle());
        if(cfg.GetValue(species,"tone_version",1).AsInt32()<ToneVersion)MigrateTint(species,p);
        p.Normalize();return p;
    }

    /// <summary>
    /// An old "body" tint multiplied every skin tone. The skin is linear in its tones, so tinting each
    /// palette tone in linear space reproduces that look exactly with the new absolute tones.
    /// </summary>
    private static void MigrateTint(string species,AppearanceProfile p)
    {
        if(p.Body.Length==0||p.BodySecondary.Length>0||p.BodyMarking.Length>0||AppearanceCatalog.Palette(species) is not { } palette)return;
        var tint=new Color(p.Body).SrgbToLinear();
        string Tinted(Color tone){var t=tone.SrgbToLinear();return "#"+new Color(t.R*tint.R,t.G*tint.G,t.B*tint.B).LinearToSrgb().ToHtml(false);}
        p.Body=Tinted(palette.Primary);
        p.BodySecondary=Tinted(palette.Secondary);
        if(palette.Marking is { } marking)p.BodyMarking=Tinted(marking);
    }
    public static void SaveProfile(string species,AppearanceProfile p,string path=Path)
    {
        p.Normalize();
        var cfg=new ConfigFile();cfg.Load(path);
        cfg.SetValue(species,"body",p.Body);cfg.SetValue(species,"body_secondary",p.BodySecondary);cfg.SetValue(species,"body_marking",p.BodyMarking);
        cfg.SetValue(species,"beak",p.Beak);cfg.SetValue(species,"hat_color",p.HatColor);cfg.SetValue(species,"vest_color",p.VestColor);cfg.SetValue(species,"pack_color",p.PackColor);
        cfg.SetValue(species,"hat",p.Hat);cfg.SetValue(species,"vest",p.Vest);cfg.SetValue(species,"pack",p.Pack);
        cfg.SetValue(species,"wing",p.Wing);cfg.SetValue(species,"tail",p.Tail);cfg.SetValue(species,"feet",p.Feet);cfg.SetValue(species,"eyes",p.Eyes);
        cfg.SetValue(species,"goggles",p.Goggles);cfg.SetValue(species,"headset",p.Headset);cfg.SetValue(species,"vest_style",p.VestStyle);
        cfg.SetValue(species,"hair",p.HairColor);cfg.SetValue(species,"face_style",p.FaceStyle);cfg.SetValue(species,"beak_style",p.BeakStyle);cfg.SetValue(species,"hair_style",p.HairStyle);
        cfg.SetValue(species,"ear_style",p.EarStyle);cfg.SetValue(species,"tail_style",p.TailStyle);
        cfg.SetValue(species,"tone_version",ToneVersion);
        foreach(var option in AppearanceCatalog.Scales)cfg.SetValue(species,option.Key,p.ScaleFor(option.Key));
        var error=cfg.Save(path);if(error!=Error.Ok)GD.PushWarning("Appearance save failed: "+error);
    }
}
