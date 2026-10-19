using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>One preset: a name key and the profile fields it sets (save-file keys). Unlisted fields stay at defaults.</summary>
public sealed record AppearancePreset(string Id,string NameKey,IReadOnlyList<(string Key,object Value)> Fields);

/// <summary>
/// Workshop presets from content/data/appearance_presets.json. "shared" presets (shape and gear)
/// are offered to every species, followed by that species' own color presets.
/// Errors are collected with the preset id, never thrown.
/// </summary>
public static class AppearancePresets
{
    public const string FilePath="res://content/data/appearance_presets.json";
    private static Dictionary<string,List<AppearancePreset>>? _bySpecies;
    private static List<AppearancePreset> _shared=new();
    private static readonly List<string> _errors=new();
    public static IReadOnlyList<string> Errors{get{Ensure();return _errors;}}

    public static IReadOnlyList<AppearancePreset> For(string species)
    {
        Ensure();
        return _bySpecies!.TryGetValue(species,out var own)?_shared.Concat(own).ToList():_shared;
    }

    public static AppearanceProfile Create(string species,int index)
    {
        var list=For(species);
        var profile=new AppearanceProfile();
        if(index<0||index>=list.Count)return profile;
        foreach(var (key,value) in list[index].Fields)profile.TrySet(key,ToVariant(value));
        profile.Normalize();
        return profile;
    }

    public static void Reload(){_bySpecies=null;}

    private static void Ensure()
    {
        if(_bySpecies!=null)return;
        var bySpecies=new Dictionary<string,List<AppearancePreset>>();var shared=new List<AppearancePreset>();_errors.Clear();
        if(!FileAccess.FileExists(FilePath))_errors.Add(FilePath+" is missing");
        else try
        {
            using var doc=JsonDocument.Parse(FileAccess.GetFileAsString(FilePath));
            if(doc.RootElement.TryGetProperty("shared",out var sharedEl))shared=ReadList(sharedEl,"shared");
            if(doc.RootElement.TryGetProperty("species",out var species))
            {
                if(species.ValueKind!=JsonValueKind.Object)_errors.Add("species: must be an object of species lists");
                else foreach(var entry in species.EnumerateObject())bySpecies[entry.Name]=ReadList(entry.Value,entry.Name);
            }
        }
        catch(System.Exception e){_errors.Add($"{FilePath}: {e.Message}");}
        // The workshop always has a "default" preset, even if the file is missing or broken.
        if(!shared.Exists(p=>p.Id=="default"))shared.Insert(0,new AppearancePreset("default","custom_preset_default",new List<(string,object)>()));
        _shared=shared;_bySpecies=bySpecies;
        foreach(var error in _errors)GD.PushError("[appearance presets] "+error);
    }

    // Presets keep plain C# values; Variants (native strings) are made only when a preset is applied.
    private static Variant ToVariant(object value)=>value switch{string s=>s,double d=>d,bool b=>b,_=>default};

    private static List<AppearancePreset> ReadList(JsonElement array,string group)
    {
        var list=new List<AppearancePreset>();
        if(array.ValueKind!=JsonValueKind.Array){_errors.Add(group+": must be a list of presets");return list;}
        int n=0;
        foreach(var item in array.EnumerateArray())
        {
            n++;
            if(item.ValueKind!=JsonValueKind.Object){_errors.Add($"{group}/#{n}: a preset must be an object");continue;}
            string id=item.TryGetProperty("id",out var idEl)&&idEl.ValueKind==JsonValueKind.String?idEl.GetString()!:"";
            string name=item.TryGetProperty("name",out var nameEl)&&nameEl.ValueKind==JsonValueKind.String?nameEl.GetString()!:"";
            string where=$"{group}/{(id.Length>0?id:"?")}";
            if(id.Length==0)_errors.Add(where+": missing id");
            if(name.Length==0)_errors.Add(where+": missing name key");
            else if(!Loc.Has(name))_errors.Add($"{where}: name key '{name}' is not in the Korean text table");
            var fields=new List<(string,object)>();
            var probe=new AppearanceProfile();
            foreach(var field in item.EnumerateObject())
            {
                if(field.Name is "id" or "name" or "description")continue;
                object value=field.Value.ValueKind switch
                {
                    JsonValueKind.String=>field.Value.GetString()!,
                    JsonValueKind.Number=>field.Value.GetDouble(),
                    JsonValueKind.True=>true,
                    JsonValueKind.False=>false,
                    _=>"",
                };
                if(probe.TrySet(field.Name,ToVariant(value)) is { } error){_errors.Add($"{where}: {error}");continue;}
                fields.Add((field.Name,value));
            }
            list.Add(new AppearancePreset(id,name,fields));
        }
        return list;
    }
}
