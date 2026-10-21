using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>Small character presets, sharing original models and animation. No actor/AI/collision.</summary>
public partial class SidekickModel : Node3D
{
    private sealed class Preset
    {
        public string Id {get;set;}="";
        public string Species {get;set;}="";
        public string Name {get;set;}="";
        public string Weapon {get;set;}="";
        public float Scale {get;set;}=.6f;
        public AppearanceProfile Appearance {get;set;}=new();
    }
    public const string PresetsPath="res://content/data/sidekick_presets.json";
    // Parse a fresh profile per instance: CharacterRig normalizes and owns its appearance.
    private static Preset[] ReadPresets()
    {
        using var file=FileAccess.Open(PresetsPath,FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException("Missing sidekick presets");
        var entries=JsonSerializer.Deserialize<Preset[]>(file.GetAsText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})
            ?? throw new InvalidOperationException("Empty sidekick presets");
        if(entries.Select(p=>p.Id).Distinct().Count()!=entries.Length || entries.Any(p=>string.IsNullOrWhiteSpace(p.Id)||!float.IsFinite(p.Scale)||p.Scale<.4f||p.Scale>.8f))
            throw new InvalidOperationException("Invalid or duplicate sidekick preset");
        return entries;
    }
    public static IReadOnlyList<string> PresetIds=>Array.AsReadOnly(ReadPresets().Select(p=>p.Id).ToArray());
    public string PresetId {get;private set;}="";
    public string Species {get;private set;}="";
    public string DisplayNameKey {get;private set;}="";
    public string WeaponId {get;private set;}="";
    public float VisualScale {get;private set;}
    public float ColliderHeight=>Rig.Definition.ColliderHeight*VisualScale;
    public float ColliderRadius=>Rig.Definition.ColliderRadius*VisualScale;
    public CharacterRig Rig {get;private set;}=null!;
    public WeaponVisual.Built Weapon {get;private set;}=null!;
    public PetModelQuality Quality {get;private set;}
    private bool _followSettings;

    public static SidekickModel Create(string id,PetModelQuality? quality=null)
    {
        var preset=ReadPresets().SingleOrDefault(p=>p.Id==id)??throw new ArgumentException("Unknown sidekick preset",nameof(id));
        if(quality.HasValue&&!Enum.IsDefined(quality.Value))throw new ArgumentException("Unknown quality",nameof(quality));
        var source=CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir).SingleOrDefault(d=>d.Id==preset.Species)
            ??throw new InvalidOperationException("Unknown sidekick species: "+preset.Species);
        var weaponDef=ArmoryCatalog.Weapon(preset.Weapon);
        var companion=new SidekickModel{Name="Sidekick_"+id,PresetId=id,Species=preset.Species,DisplayNameKey=preset.Name,WeaponId=preset.Weapon,VisualScale=preset.Scale,_followSettings=quality==null};
        var visual=new Node3D{Name="ScaledVisual",Scale=Vector3.One*preset.Scale};companion.AddChild(visual);
        companion.Rig=CharacterRig.Create(source);visual.AddChild(companion.Rig);
        companion.Rig.ApplyAppearance(preset.Appearance);companion.Rig.HideBuiltInWeapon();
        companion.Weapon=WeaponVisual.Build(weaponDef,companion.Rig.HandColor);
        companion.Weapon.Root.Scale=Vector3.One/source.ModelScale;
        if(!source.ModelFacesPositiveZ)companion.Weapon.Root.RotationDegrees=new Vector3(0,180,0);
        companion.Rig.Weapon.AddChild(companion.Weapon.Root);companion.Rig.BindWeaponGrip(companion.Weapon);
        companion.SetQuality(quality??GameSettings.PetQuality);return companion;
    }
    public override void _Ready()=>Rig.UpdateWeaponGrip();
    public override void _EnterTree()
    {
        if(!_followSettings)return;
        SetQuality(GameSettings.PetQuality);GameSettings.PetQualityChanged+=SetQuality;
    }
    public override void _ExitTree(){if(_followSettings)GameSettings.PetQualityChanged-=SetQuality;}
    public void SetQuality(PetModelQuality quality)
    {
        if(!Enum.IsDefined(quality))throw new ArgumentException("Unknown quality",nameof(quality));
        Quality=quality;
        foreach(var node in FindChildren("*",nameof(MeshInstance3D),true,false))
            ((MeshInstance3D)node).LodBias=quality==PetModelQuality.High?4:.5f;
    }
    public void Animate(float delta,float moving)
    {
        moving=Mathf.Clamp(moving,0,1);
        Rig.SetLocomotionPose(delta,moving,false);
        Rig.Animate(delta,moving,0,0,Vector3.Zero);
    }
}
