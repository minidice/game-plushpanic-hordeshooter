using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
namespace HordeShooter.Tests;

/// <summary>Frozen real combat/HUD comparison. Pixel change is a coverage proxy, never GPU overdraw or a readability score.</summary>
public partial class AreaEffectsReadabilityCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private string _output = "";
    private void Check(bool ok, string text)
    { if (!ok) throw new Exception(text); _checks++; GD.Print("PASS " + text); }
    private async Task Frames(int count)
    { for (int i=0;i<count;i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static IEnumerable<Node> Descendants(Node parent)
    { foreach (var child in parent.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-16,-16,32,32);
        protected override void BuildMap()
        { PlayerStart=new(0,0,2); Floor(-16,16,-16,16,"656e48"); }
    }
    private async Task<Image> Capture(string name)
    {
        await Frames(4);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image=GetViewport().GetTexture().GetImage();
        image.Convert(Image.Format.Rgb8);
        Check(image.SavePng(_output+"/"+name+".png")==Error.Ok, "capture "+name);
        return image;
    }
    private static (int pixels, double percent) Difference(Image baseline, Image actual)
    {
        byte[] a=baseline.GetData(), b=actual.GetData();
        if(a.Length!=b.Length) throw new Exception("comparison sizes differ");
        int changed=0;
        // Mean absolute RGB difference > 3/255, including translucent tint; not a visibility/occlusion test.
        for(int i=0;i<a.Length;i+=3)
            if(Math.Abs(a[i]-b[i])+Math.Abs(a[i+1]-b[i+1])+Math.Abs(a[i+2]-b[i+2])>9) changed++;
        return (changed,100.0*changed/(a.Length/3));
    }
    public override async void _Ready()
    {
        int exit=0;
        try { await Run(); GD.Print("AREA_READABILITY_PASS "+_checks); }
        catch(Exception e) { GD.PushError(e.ToString()); GD.Print("AREA_READABILITY_FAIL "+e.Message); exit=1; }
        if(_stage!=null&&IsInstanceValid(_stage)) _stage.QueueFree();
        await Frames(3); QuitHygiene.Collect(); GetTree().Quit(exit);
    }
    private async Task Run()
    {
        Check(DisplayServer.GetName()!="headless","real GPU render required");
        bool reduced=OS.GetCmdlineUserArgs().Contains("--reduced-render-cost");
        var viewport=GetViewport();
        if(reduced) { viewport.Scaling3DScale=.67f; viewport.Msaa3D=Viewport.Msaa.Disabled; }
        string mode=reduced?"reduced":"normal";
        _output=ProjectSettings.GlobalizePath("res://output/area-readability/"+mode);
        System.IO.Directory.CreateDirectory(_output);
        _stage=new Stage { MapOverride=new TestMap(), CharacterOverride="goose", ThemeOverride="zombie",
            DirectorEnabled=false, ModeOverride=GameMode.Scenario, SquadOverride=0, ArrivalOverride=false, NextStageOverride=false };
        AddChild(_stage); await Frames(5);
        var player=_stage.Player; player.ManualControl=true; player.Invulnerable=true;
        player.TeleportTo(new(0,0,2)); player.SetControls(Vector2.Zero,new(1.5f,1,-1),false);
        _stage.Cam.SetProcess(false); _stage.Cam.SetPhysicsProcess(false); _stage.Cam.SnapTo(new(0,1,0));
        var boss=_stage.SpawnEnemy<BossEnemy>(MonsterDatabase.Get(BossEnemy.MonsterId),new(3,0,-1),awake:true);
        boss.SetPhysicsProcess(false);
        for(int i=0;i<3;i++)
        {
            var enemy=_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,new(-3+i*2.5f,0,-3),awake:false);
            enemy.SetPhysicsProcess(false);
        }
        await Frames(8);
        Check(boss.StartSkill(BossSkillKind.Slam)&&boss.Casting,"actual boss slam telegraph starts");
        // Advance its actual windup a little, then freeze before the leap/impact. No new telegraph drawing code.
        var slam=boss.Data.Skills.Values.First(s=>s.Kind==BossSkillKind.Slam);
        boss._PhysicsProcess(slam.Windup*.35f);
        Check(boss.Casting,"capture remains inside real slam windup");
        // One GroundMarker draws the boundary and the filling interior since 2026-10-27 (was a disc + fill cylinder pair, I-129).
        Check(Descendants(_stage.Effects).OfType<GroundMarker>().Any(m=>Mathf.IsEqualApprox(m.Footprint.X,slam.Radius*2,.05f)),
            "actual boss slam marker covers the slam radius");
        _stage.Hud!._Process(0);
        _stage.ProcessMode=ProcessModeEnum.Disabled;
        foreach(var node in Descendants(_stage)) node.ProcessMode=ProcessModeEnum.Disabled;
        Check(!_stage.Hud.Paused&&_stage.State==StageState.Playing&&!player.IsDead,"game HUD/reticle remains in playing state");
        using var baseline=await Capture("baseline");
        using var repeat=await Capture("baseline-repeat");
        var drift=Difference(baseline,repeat);
        Check(drift.pixels==0,"frozen baseline is pixel-identical");

        var holders=new List<Node3D>();
        var effects=new List<CombatSurfaceEffect>();
        // Nested subsets preserve exactly the same slots at 1/6/12. Intentional overlap around the player and telegraph.
        Vector3[] offsets={new(0,0,1),new(-2.5f,0,1),new(2.5f,0,1),new(0,0,-2),
            new(-2.5f,0,-2),new(2.5f,0,-2),new(0,0,4),new(-2.5f,0,4),new(2.5f,0,4),
            new(-5,0,0),new(5,0,0),new(0,0,-5)};
        for(int i=0;i<offsets.Length;i++)
        {
            var holder=new Node3D { Position=offsets[i] }; _stage.Effects.AddChild(holder); holders.Add(holder);
            effects.Add(CombatSurfaceEffect.Create(holder,CombatEffectCatalog.Current.ForSource(i%2==0?"fire_pool":"smoke_preview"),2.5f,8));
        }
        for(int frame=0;frame<90;frame++)
        { foreach(var effect in effects) effect._Process(1.0/60); await Frames(1); }
        var rows=new List<object>();
        foreach(int count in new[]{1,6,12})
        {
            for(int i=0;i<holders.Count;i++) holders[i].Visible=i<count;
            using var shot=await Capture("areas-"+count);
            var difference=Difference(baseline,shot);
            Check(difference.pixels>0,count+" area visuals change rendered pixels");
            rows.Add(new { areas=count,fire=(count+1)/2,smoke=count/2,changed_pixels=difference.pixels,changed_screen_percent=difference.percent });
        }
        foreach(var holder in holders) holder.Visible=false;
        using var after=await Capture("baseline-after");
        Check(Difference(baseline,after).pixels==0,"hiding all effects restores identical scene/HUD");
        System.IO.File.WriteAllText(_output+"/coverage.json",JsonSerializer.Serialize(new {
            adapter=RenderingServer.GetVideoAdapterName(),condition=mode,output_resolution=viewport.GetVisibleRect().Size.ToString(),
            render_scale=viewport.Scaling3DScale,msaa=viewport.Msaa3D.ToString(),effect_age_s=1.5,effect_radius_m=2.5,
            baseline_drift_pixels=drift.pixels,rows,
            note="Same GPU, frozen real boss slam/player/HUD. Mean RGB difference >3/255 is a screen-change proxy, NOT GPU overdraw, true alpha coverage, recognition score, low-end hardware or performance benchmark. Smoke is visual-only. No gameplay/settings changes."
        },new JsonSerializerOptions { WriteIndented=true }));
    }
}
