using Godot;
using System;

namespace HordeShooter;
public sealed class AppearanceProfile
{
    public string Body {get;set;}="";
    public string Beak {get;set;}="";
    public string HatColor {get;set;}="";
    public string VestColor {get;set;}="";
    public string PackColor {get;set;}="";
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
    public float WingSize {get;set;}=1;
    public float FeetSize {get;set;}=1;
    public float TailSize {get;set;}=1;
    public int FaceStyle {get;set;}
    public int BeakStyle {get;set;}
    public int HairStyle {get;set;}
    public bool Goggles {get;set;}=true;
    public bool Headset {get;set;}
    public int VestStyle {get;set;}
    public int Hat {get;set;}=1;
    public bool Vest {get;set;}=true;
    public bool Pack {get;set;}=true;
    public string ColorFor(string channel)=>channel switch {"body"=>Body,"beak"=>Beak,"hat"=>HatColor,"vest"=>VestColor,"pack"=>PackColor,"wing"=>Wing,"tail"=>Tail,"feet"=>Feet,"eyes"=>Eyes,"hair"=>HairColor,_=>""};
    public void SetColor(string channel,string color)
    {switch(channel){case "body":Body=color;break;case "beak":Beak=color;break;case "hat":HatColor=color;break;case "vest":VestColor=color;break;case "pack":PackColor=color;break;case "wing":Wing=color;break;case "tail":Tail=color;break;case "feet":Feet=color;break;case "eyes":Eyes=color;break;case "hair":HairColor=color;break;}}
    public float ScaleFor(string key)=>key switch
    {"body_width"=>BodyWidth,"body_height"=>BodyHeight,"eye_size"=>EyeSize,"eye_spacing"=>EyeSpacing,"beak_size"=>BeakSize,"ear_size"=>EarSize,"wing_size"=>WingSize,"feet_size"=>FeetSize,"tail_size"=>TailSize,_=>1};
    public void SetScale(string key,float value)
    {
        foreach(var option in AppearanceCatalog.Scales)if(option.Key==key)
        {value=float.IsFinite(value)?Math.Clamp(value,option.Min,option.Max):1;break;}
        switch(key){case "body_width":BodyWidth=value;break;case "body_height":BodyHeight=value;break;case "eye_size":EyeSize=value;break;case "eye_spacing":EyeSpacing=value;break;case "beak_size":BeakSize=value;break;case "ear_size":EarSize=value;break;case "wing_size":WingSize=value;break;case "feet_size":FeetSize=value;break;case "tail_size":TailSize=value;break;}
    }
    public void Normalize()
    {
        foreach(var option in AppearanceCatalog.Scales)SetScale(option.Key,ScaleFor(option.Key));
        FaceStyle=Math.Clamp(FaceStyle,0,2);BeakStyle=Math.Clamp(BeakStyle,0,2);HairStyle=Math.Clamp(HairStyle,0,2);
        Hat=Math.Clamp(Hat,0,2);VestStyle=Math.Clamp(VestStyle,0,1);
    }
}
public static class AppearanceStore
{
    public const string Path="user://appearance.cfg";
    public static AppearanceProfile ReadProfile(string species,string path=Path)
    {
        var cfg=new ConfigFile();cfg.Load(path);
        string ReadColor(string key){string value=cfg.GetValue(species,key,"").AsString();return value.Length>0&&Color.HtmlIsValid(value)?value:"";}
        var p=new AppearanceProfile{Body=ReadColor("body"),Beak=ReadColor("beak"),HatColor=ReadColor("hat_color"),VestColor=ReadColor("vest_color"),PackColor=ReadColor("pack_color"),Wing=ReadColor("wing"),Tail=ReadColor("tail"),Feet=ReadColor("feet"),Eyes=ReadColor("eyes"),HairColor=ReadColor("hair"),Goggles=cfg.GetValue(species,"goggles",true).AsBool(),Headset=cfg.GetValue(species,"headset",false).AsBool(),VestStyle=Math.Clamp(cfg.GetValue(species,"vest_style",0).AsInt32(),0,1),Hat=Math.Clamp(cfg.GetValue(species,"hat",1).AsInt32(),0,2),Vest=cfg.GetValue(species,"vest",true).AsBool(),Pack=cfg.GetValue(species,"pack",true).AsBool(),FaceStyle=cfg.GetValue(species,"face_style",0).AsInt32(),BeakStyle=cfg.GetValue(species,"beak_style",0).AsInt32(),HairStyle=cfg.GetValue(species,"hair_style",0).AsInt32()};
        foreach(var option in AppearanceCatalog.Scales)p.SetScale(option.Key,cfg.GetValue(species,option.Key,1f).AsSingle());
        p.Normalize();return p;
    }
    public static void SaveProfile(string species,AppearanceProfile p,string path=Path)
    {
        p.Normalize();
        var cfg=new ConfigFile();cfg.Load(path);
        cfg.SetValue(species,"body",p.Body);cfg.SetValue(species,"beak",p.Beak);cfg.SetValue(species,"hat_color",p.HatColor);cfg.SetValue(species,"vest_color",p.VestColor);cfg.SetValue(species,"pack_color",p.PackColor);
        cfg.SetValue(species,"hat",p.Hat);cfg.SetValue(species,"vest",p.Vest);cfg.SetValue(species,"pack",p.Pack);
        cfg.SetValue(species,"wing",p.Wing);cfg.SetValue(species,"tail",p.Tail);cfg.SetValue(species,"feet",p.Feet);cfg.SetValue(species,"eyes",p.Eyes);
        cfg.SetValue(species,"goggles",p.Goggles);cfg.SetValue(species,"headset",p.Headset);cfg.SetValue(species,"vest_style",p.VestStyle);
        cfg.SetValue(species,"hair",p.HairColor);cfg.SetValue(species,"face_style",p.FaceStyle);cfg.SetValue(species,"beak_style",p.BeakStyle);cfg.SetValue(species,"hair_style",p.HairStyle);
        foreach(var option in AppearanceCatalog.Scales)cfg.SetValue(species,option.Key,p.ScaleFor(option.Key));
        var error=cfg.Save(path);if(error!=Error.Ok)GD.PushWarning("Appearance save failed: "+error);
    }
}
