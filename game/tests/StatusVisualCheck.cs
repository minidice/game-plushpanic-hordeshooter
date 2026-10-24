using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
namespace HordeShooter.Tests;
public partial class StatusVisualCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok,string label) {if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Frames(int n) {for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private IEnumerable<T> Nodes<T>(Node n) where T:Node {foreach(var c in n.GetChildren()){if(c is T t)yield return t;foreach(var x in Nodes<T>(c))yield return x;}}
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area=>new(-15,-15,30,30);
        protected override void BuildMap(){PlayerStart=new(0,0,0);Floor(-15,15,-15,15,"646c45");}
    }
    public override async void _Ready()
    {
        int exit=0;try {Data();await Run();GD.Print("STATUS_VISUAL_PASS "+_checks);}catch(Exception e){GD.PushError(e.ToString());GD.Print("STATUS_VISUAL_FAIL "+e.Message);exit=1;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.QueueFree();await Frames(3);QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Data()
    {
        var c=StatusVisualCatalog.Current;
        Check(c.All.Count==12,"twelve shared visual definitions");
        Check(c.All.Values.Count(d=>d.Binding=="preview")==5,"five unimplemented combat states explicitly preview-only");
        string json=FileAccess.GetFileAsString(StatusVisualCatalog.DataPath);
        foreach(string change in new[]{"duplicate","range","color","binding","shape","missing","unknown"})
        {
            var d=JsonNode.Parse(json)!;var first=d["statuses"]![0]!;
            if(change=="duplicate")d["statuses"]![1]!["id"]="poison";
            if(change=="range")first["surface_alpha"]=.9;
            if(change=="color")first["color"]="wrong";
            if(change=="binding")first["binding"]="live";
            if(change=="shape")first["shape"]="unknown";
            if(change=="missing")first.AsObject().Remove("priority");
            if(change=="unknown")first["damage"]=100;
            bool rejected=false;try{StatusVisualCatalog.Parse(d.ToJsonString());}catch(FormatException){rejected=true;}
            Check(rejected,"reject malformed "+change);
        }
        foreach(var def in c.All.Values)Check(StatusVisualIcons.Texture(def.Id).GetWidth()==128,"renderable shape icon "+def.Id);
    }
    private async Task Shot(string name)
    {
        if(!Windowed)return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/status-"+name+".png"))==Error.Ok,"render "+name);
    }
    private async Task Run()
    {
        _stage=new Stage{MapOverride=new TestMap(),CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false,
            ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};AddChild(_stage);await Frames(4);
        _stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;
        _stage.Cam.SetProcess(false);_stage.Cam.SetPhysicsProcess(false);
        var camera=new Camera3D{Position=new(3,3.5f,5),Projection=Camera3D.ProjectionType.Orthogonal,Size=4.5f};AddChild(camera);camera.LookAt(Vector3.Up*.8f);camera.MakeCurrent();
        var rig=_stage.Player.Rig;var control=StatusVisualController.Attach(rig);control.LiveSource=null;
        var originals=Nodes<MeshInstance3D>(rig.Pose).ToDictionary(m=>m,m=>(m.MaterialOverride,m.MaterialOverlay));
        foreach(var def in StatusVisualCatalog.Current.All.Values)
        {
            control.SetStates(new[]{new StatusVisualState(def.Id,3)});await Frames(2);control._Process(.3);
            Check(control.Active.Count==1&&control.DecorationCount==1,"single rig attachment "+def.Id);await Shot(def.Id);
        }
        control.SetStates(new[]{new StatusVisualState("poison",2,2),new StatusVisualState("poison",4,4),new StatusVisualState("burn",3),new StatusVisualState("freeze",2),new StatusVisualState("stun",2)});
        Check(control.Active.Count==4&&control.Active.Single(s=>s.Id=="poison").Stacks==4,"duplicates merge without effect duplication");
        Check(control.DecorationCount==2&&control.SurfaceId=="freeze","bounded decoration and dominant surface");
        foreach(float heading in new[]{0f,90f,180f,270f})
        {
            rig.RotationDegrees=new(0,heading,0);control._Process(0);
            var badges=Nodes<Sprite3D>(control).Where(b=>b.Visible&&!b.IsQueuedForDeletion()).ToArray();
            var spacing=(badges[1].GlobalPosition-badges[0].GlobalPosition).Normalized();
            var renderCamera=rig.GetViewport().GetCamera3D()!;
            Check(spacing.Dot(renderCamera.GlobalBasis.X.Normalized())>.999f,"badge row stays camera-facing at heading "+heading);
        }
        rig.Rotation=Vector3.Zero;
        bool rejected=false;try{control.SetStates(new[]{new StatusVisualState("burn",1,6)});}catch(ArgumentException){rejected=true;}
        Check(rejected&&control.Active.Count==4,"invalid input leaves existing states intact");
        await Frames(3);await Shot("stacked");
        rig.FlashHit();control._Process(0);
        Check(Nodes<MeshInstance3D>(rig.Pose).Where(m=>m.HasMeta(StatusVisualController.InternalMeta)).All(m=>!m.Visible),"status surface yields to hit flash");await Shot("hit");
        await Frames(10);control._Process(0);
        Check(originals.All(p=>!IsInstanceValid(p.Key)||(p.Key.MaterialOverride==p.Value.MaterialOverride&&p.Key.MaterialOverlay==p.Value.MaterialOverlay)),"hit restores original materials with status active");
        rig.ShowAs(VisionSystem.Visibility.Silhouette);control._Process(0);
        Check(!control.Displayed,"silhouette conceals status detail");await Shot("silhouette");
        rig.ShowAs(VisionSystem.Visibility.Hidden);control._Process(0);Check(!control.Displayed,"hidden actor has no status markers");
        rig.ShowAs(VisionSystem.Visibility.Visible);await Frames(30);control._Process(0);Check(control.Displayed,"reveal restores current state");
        control.Clear();await Frames(3);
        Check(!Nodes<MeshInstance3D>(rig.Pose).Any(m=>m.HasMeta(StatusVisualController.InternalMeta)),"clear removes added mesh layers");
        Check(originals.All(p=>!IsInstanceValid(p.Key)||(p.Key.MaterialOverride==p.Value.MaterialOverride&&p.Key.MaterialOverlay==p.Value.MaterialOverlay)),"clear preserves all original materials");await Shot("clear");
        foreach(var def in CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir).Where(d=>!d.Id.StartsWith("test_")&&d.Id!="goose"))
        {
            var other=CharacterRig.Create(def);AddChild(other);rig.Visible=false;
            var fx=StatusVisualController.Attach(other);fx.SetStates(new[]{new StatusVisualState("burn",2),new StatusVisualState("poison",2)});
            await Frames(2);await Shot(def.Id);Check(fx.Active.Count==2,"shared renderer supports "+def.Id);other.QueueFree();await Frames(2);rig.Visible=true;
        }
        var enemy=_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,new(1.8f,0,0),awake:true);enemy.Ignite(0,1.2f,_stage.Player);enemy.Stun(.9f);await Frames(5);
        var live=enemy.Rig.GetNodeOrNull<StatusVisualController>("StatusVisuals");
        Check(live!=null&&live.Active.Any(s=>s.Id=="burn")&&live.Active.Any(s=>s.Id=="stun"),"actual enemy burn and stun share renderer");
        await Shot("live-enemy");await Frames(85);Check(live!.Active.Count==0,"live visuals end with existing timers");
        StatusVisualBinding.Attach(_stage.Player);
        _stage.Player.Blind(.8f);_stage.Player.WeakTime=1;await Frames(5);
        Check(control.Active.Any(s=>s.Id=="blind")&&control.Active.Any(s=>s.Id=="weak"),"actual player blind and weak share renderer");
        await Frames(70);Check(control.Active.Count==0,"player effects expire without restoring stale colors");
        enemy.Ignite(0,5,_stage.Player);await Frames(5);enemy.Die(Vector3.Back,0);await Frames(5);
        Check(!IsInstanceValid(live)||live.Active.Count==0,"death clears effects on transferred corpse rig");
        var independent=CharacterRig.Create(rig.Definition);AddChild(independent);
        var isolated=Nodes<MeshInstance3D>(independent.Pose).ToDictionary(m=>m,m=>(m.MaterialOverride,m.MaterialOverlay));
        control.LiveSource=null;control.SetStates(new[]{new StatusVisualState("poison",3)});await Frames(3);
        Check(isolated.All(p=>p.Key.MaterialOverride==p.Value.MaterialOverride&&p.Key.MaterialOverlay==p.Value.MaterialOverlay),"shared asset materials stay independent across actors");
        control.QueueFree();await Frames(3);
        Check(!Nodes<MeshInstance3D>(rig.Pose).Any(m=>m.HasMeta(StatusVisualController.InternalMeta)),"removing controller cleans mesh children it does not own");
        independent.QueueFree();StatusVisualBinding.Attach(_stage.Player);
        if(Windowed&&OS.GetCmdlineUserArgs().Contains("--status-perf"))await Crowd(camera);
    }

    private async Task Crowd(Camera3D camera)
    {
        camera.Position=new(12,18,22);camera.Size=25;camera.LookAt(Vector3.Up*.5f);
        var crowd=new Node3D();AddChild(crowd);var controls=new List<StatusVisualController>();
        for(int i=0;i<80;i++)
        {
            var rig=CharacterRig.Create(_stage.Player.Definition);crowd.AddChild(rig);rig.Position=new((i%10-4.5f)*2,0,(i/10-3.5f)*2);
            controls.Add(StatusVisualController.Attach(rig));
        }
        async Task<(double ms,double draws)> Sample()
        {
            await Frames(30);ulong start=Time.GetTicksUsec();double draws=0;
            for(int i=0;i<90;i++){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);draws+=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);}
            return((Time.GetTicksUsec()-start)/90000d,draws/90);
        }
        var baseline=await Sample();
        foreach(var c in controls)c.SetStates(new[]{new StatusVisualState("burn",0,5),new StatusVisualState("stun",0,5),new StatusVisualState("poison",0,5)});
        var active=await Sample();await Shot("crowd");
        Check(controls.All(c=>c.DecorationCount==2),"eighty actors still enforce two decorations at five stacks");
        GD.Print($"STATUS_PERF actors=80 baseline_ms={baseline.ms:0.00} active_ms={active.ms:0.00} baseline_draws={baseline.draws:0} active_draws={active.draws:0}");
        crowd.QueueFree();await Frames(4);
        Check(!IsInstanceValid(crowd),"crowd parent teardown removes all status visuals");
    }
}
