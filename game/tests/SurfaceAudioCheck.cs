using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class SurfaceAudioCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-8, -8, 32, 24);
        public override SurfaceProfile SurfaceAt(Vector3 at) => SurfaceProfiles.Current.Profiles[at.Y > 1 ? "wood_solid" : at.X < 4 ? "grass" : "asphalt"];
        protected override void BuildMap()
        {
            PlayerStart = new Vector3(0, 0, -5);
            Floor(-7, 23, -7, 15, "546852");
            Prop("sandbags", Vector3.Zero);
            Prop("corrugated_fence", new Vector3(10, 0, 0));
            Prop("wooden_crate", new Vector3(16, 0, 0), solid: false);
            // Same XZ, two different heights: the contacted floor chooses the material.
            var deck = Solid(new Vector3(0,2.9f,9), new Vector3(8,.2f,6), "546852", drawCargo:false);
            deck.RemoveMeta(SurfaceAudio.ProfileMeta); deck.SetMeta(SurfaceAudio.GroundMeta,true);
        }
    }
    private Stage? _stage;
    private int _checks;
    private void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i=0;i<n;i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    public override async void _Ready()
    {
        int exit=0;
        try { await Run(); GD.Print("SURFACE_AUDIO_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); exit=1; }
        if (_stage != null) { _stage.QueueFree(); await Frames(3); }
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }
    private async Task Run()
    {
        Check(FileAccess.FileExists(SurfaceProfiles.DataPath), "surface data is shipped");
        string json = FileAccess.GetFileAsString(SurfaceProfiles.DataPath);
        var data = SurfaceProfiles.Current;
        Check(data.Families.Count == 7 && data.Profiles.Count == 15, "seven families and fifteen named presets");
        foreach (var prop in PropLibrary.All.Values) Check(data.Objects.ContainsKey(prop.Id), prop.Id + " explicitly mapped");
        foreach (var change in new Action<JsonNode>[] {
            j=>j["profiles"]!["sandbag"]!["family"]="missing",
            j=>j["objects"]!["sandbags"]="missing",
            j=>j["profiles"]!["sandbag"]!["hit_db"]=99,
            j=>j["profiles"]!["sandbag"]!["hit"]="missing_sound",
            j=>j["ground"]!["StageOneLayout"]!["default"]="missing",
            j=>j["layouts"]!["1"]!["main_half_width"]=0,
        })
        {
            var bad=JsonNode.Parse(json)!; change(bad); bool rejected=false;
            try { SurfaceProfiles.Parse(bad.ToJsonString()); } catch (FormatException) { rejected=true; }
            Check(rejected, "malformed surface data rejected");
        }
        var decorative=new Node(); SurfaceAudio.Tag(decorative,"water_tile");
        Check(!decorative.HasMeta(SurfaceAudio.ProfileMeta), "decorative water has no contact material"); decorative.Free();
        Check(data.ForObject("unknown").Id=="concrete", "unassigned objects use explicit concrete fallback");
        Check(data.GroundAt("StageTwoMap",new(64,0,0)).Id=="wood_solid", "small forest bridge is wood");
        foreach (string path in new[] { LayoutMap.Stage1Path, LayoutMap.Stage2Path, LayoutMap.Stage3Path })
        {
            var layout=StageLayout.Load(path);
            var authored=JsonNode.Parse(json)!["layouts"]![layout.Stage.ToString()]!["regions"]!;
            foreach (var r in layout.Regions) Check(authored[r.Id]!=null, "layout region mapped: " + layout.Stage + "/" + r.Id);
        }
        _stage=new Stage { CharacterOverride="goose", ThemeOverride="zombie", DirectorEnabled=false, MapOverride=new TestMap(), ModeOverride=GameMode.Scenario, SquadOverride=0 };
        AddChild(_stage); await Frames(5);
        var player=_stage.Player; player.ManualControl=true; player.Invulnerable=true;
        foreach (var profile in data.Profiles.Values)
            foreach (string id in new[] { profile.Step,profile.Hit,profile.Breaking })
                Check(SoundRecipes.Find(id)!=null && (id=="step" || _stage.Sfx.Overridden.Contains(id)), profile.Id+" sound available: "+id);
        foreach (var sound in SoundRecipes.All.Where(d=>d.Id.StartsWith("surface_")))
        {
            Check(_stage.Sfx.Stream(sound.Id) is AudioStreamWav { Format: AudioStreamWav.FormatEnum.Format16Bits }, sound.Id+" is imported as PCM16");
            Check(sound.NearCap && !sound.RestartWhenFull && sound.Variants==2, sound.Id+" bounded repeated playback");
            Check(FileAccess.FileExists("res://content/sounds/"+sound.Id+"_2.wav"), sound.Id+" second recording exists");
        }
        var probe=new Node3D(); _stage.AddChild(probe);
        probe.GlobalPosition=new(0,.05f,9);
        Check(SurfaceAudio.Under(_stage,probe).Id=="grass", "lower ground under bridge stays grass");
        probe.GlobalPosition=new(0,3.05f,9);
        Check(SurfaceAudio.Under(_stage,probe).Id=="wood_solid", "upper deck uses actual contacted height");
        var sand=_stage.Map.KitProps.OfType<ObjectBody>().Single();
        var screen=sand.GetChildren().OfType<CoverScreen>().Single();
        Check(SurfaceAudio.Resolve(_stage,screen,Vector3.Zero,Vector3.Forward).Id=="sandbag", "cover screen resolves to sandbag owner");
        Check(sand.Health==50 && sand.Interaction.Cover, "audio binding preserves sandbag health and cover");
        int steps=_stage.Sfx.Played.GetValueOrDefault("surface_step_grass");
        player.SetControls(new Vector2(1,0),new Vector3(10,1,-5),false); await Frames(30);
        player.SetControls(Vector2.Zero,new Vector3(10,1,-5),false);
        Check(_stage.Sfx.Played.GetValueOrDefault("surface_step_grass")>steps, "real player walking uses grass sound");
        player.TeleportTo(new Vector3(10,0,-4));
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null); await Frames(12);
        player.SetControls(Vector2.Zero,new Vector3(10,Player.AimHeight,3),true); await Frames(30);
        player.SetControls(Vector2.Zero,new Vector3(10,1,3),false);
        Check(_stage.Sfx.Played.GetValueOrDefault("surface_hit_metal")>0, "real rifle hit uses metal collider");
        player.TeleportTo(new Vector3(0,0,-4)); await Frames(12);
        player.SetControls(Vector2.Zero,new Vector3(0,Player.AimHeight,3),true); await Frames(50);
        player.SetControls(Vector2.Zero,new Vector3(0,1,3),false);
        Check(_stage.Sfx.Played.GetValueOrDefault("surface_hit_soft")>0, "rifle hitting cover uses soft material");
        Check(_stage.Sfx.Played.GetValueOrDefault("surface_break_soft")==1 && _stage.Breakables.Count==0, "sandbag breaks once and frees its collision");
        Check(_stage.Map.KitProps.Where(IsInstanceValid).Single(p=>p.Name=="wooden_crate") is not StaticBody3D, "decorative wood remains nonblocking");
    }
}
