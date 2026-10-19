using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;
/// <summary>Plain managed configuration. Invalid/missing fields retain safe defaults with diagnostics.</summary>
public sealed class CorpseRetentionSettings
{
    public const string Path="res://content/data/corpse_retention.json";
    public int CommonTargetCount { get; set; }=100;
    public float KeepDistance { get; private set; }=28;
    public float MinimumAge { get; private set; }=10;
    public float UnseenGrace { get; private set; }=5;
    public int RemovalsPerPass { get; private set; }=8;
    public float CleanupInterval { get; private set; }=1;
    public float NamedLifetime { get; private set; }=300;
    private readonly List<string> _errors=new();
    public IReadOnlyList<string> Errors=>_errors;

    public static CorpseRetentionSettings Load()
    {
        var settings=Godot.FileAccess.FileExists(Path)?FromText(Godot.FileAccess.GetFileAsString(Path)):new();
        if(!Godot.FileAccess.FileExists(Path))settings._errors.Add(Path+": file missing; defaults used");
        foreach(var error in settings.Errors)GD.PushWarning(error);
        return settings;
    }
    public static CorpseRetentionSettings FromText(string text)
    {
        var s=new CorpseRetentionSettings();
        try
        {
            using var doc=JsonDocument.Parse(text);var root=doc.RootElement;
            if(!s.Keys(root,"root","cleanup_interval_seconds","common","named"))return s;
            s.CleanupInterval=s.Number(root,"cleanup_interval_seconds",1,true,"root");
            if(root.TryGetProperty("common",out var common)&&s.Keys(common,"common","target_count","keep_distance_m","minimum_age_seconds","unseen_grace_seconds","removals_per_pass"))
            {
                s.CommonTargetCount=s.Integer(common,"target_count",100,false,"common");
                s.KeepDistance=s.Number(common,"keep_distance_m",28,false,"common");
                s.MinimumAge=s.Number(common,"minimum_age_seconds",10,false,"common");
                s.UnseenGrace=s.Number(common,"unseen_grace_seconds",5,false,"common");
                s.RemovalsPerPass=s.Integer(common,"removals_per_pass",8,true,"common");
            }
            if(root.TryGetProperty("named",out var named)&&s.Keys(named,"named","lifetime_seconds"))
                s.NamedLifetime=s.Number(named,"lifetime_seconds",300,true,"named");
        }
        catch(JsonException e){s._errors.Add(Path+": invalid JSON: "+e.Message);}
        return s;
    }
    private bool Keys(JsonElement value,string section,params string[] allowed)
    {
        if(value.ValueKind!=JsonValueKind.Object){_errors.Add($"{Path}: {section} must be an object; defaults used");return false;}
        foreach(var p in value.EnumerateObject())if(!allowed.Contains(p.Name))_errors.Add($"{Path}: unknown field {section}.{p.Name}");
        return true;
    }
    private float Number(JsonElement section,string key,float fallback,bool positive,string where)
    {
        if(!section.TryGetProperty(key,out var value))return fallback;
        if(value.ValueKind==JsonValueKind.Number&&value.TryGetSingle(out var number)&&float.IsFinite(number)&&
            (positive?number>0:number>=0))return number;
        _errors.Add($"{Path}: {where}.{key} requires a {(positive?"positive":"nonnegative")} finite number; default {fallback} used");return fallback;
    }
    private int Integer(JsonElement section,string key,int fallback,bool positive,string where)
    {
        if(!section.TryGetProperty(key,out var value))return fallback;
        if(value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out var number)&&(positive?number>0:number>=0))return number;
        _errors.Add($"{Path}: {where}.{key} requires a {(positive?"positive":"nonnegative")} integer; default {fallback} used");return fallback;
    }
}
