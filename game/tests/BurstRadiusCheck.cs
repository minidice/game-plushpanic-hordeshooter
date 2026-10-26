using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace HordeShooter.Tests;

public partial class BurstRadiusCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private static IEnumerable<T> Nodes<T>(Node node) where T:Node
    { foreach(var child in node.GetChildren()){if(child is T t)yield return t;foreach(var nested in Nodes<T>(child))yield return nested;} }
    private async Task Frame() => await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);_checks++;GD.Print("PASS "+text);}
    private sealed class Map:StageMap
    {
        public override Rect2 Area=>new(-18,-18,36,36);
        protected override void BuildMap(){PlayerStart=new(0,0,9);Floor(-18,18,-18,18,"4c5752");}
    }
    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await Frame();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/burst-radius/"+name+".png"))==Error.Ok,"render "+name);
    }
    public override async void _Ready()
    {
        int exit=0;
        try{await Run();GD.Print("BURST_RADIUS_PASS "+_checks);}
        catch(Exception e){GD.PushError(e.ToString());GD.Print("BURST_RADIUS_FAIL "+e.Message);exit=1;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.QueueFree();await Frame();await Frame();
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private async Task Run()
    {
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output/burst-radius"));
        _stage=new Stage{MapOverride=new Map(),CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false,
            ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
        AddChild(_stage);for(int i=0;i<5;i++)await Frame();
        _stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;
        _stage.Cam.SetProcess(false);_stage.Cam.SetPhysicsProcess(false);
        var camera=new Camera3D{Position=new(8,13,17),Projection=Camera3D.ProjectionType.Orthogonal,Size=15};
        AddChild(camera);camera.LookAt(Vector3.Zero);camera.MakeCurrent();
        const float radius=4;
        var origin=new Vector3(0,.05f,0);
        // Inspection aids only: sparse white points at the true radius; no production ring.
        var markers=new Node3D();_stage.AddChild(markers);
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.Tau/16;
            Mat.Mesh(markers,new SphereMesh{Radius=.035f,Height=.07f},new(Mathf.Cos(angle)*radius,.06f,Mathf.Sin(angle)*radius),Mat.Solid("d4ded7"));
        }
        foreach(string id in new[]{"blast","grenade","rocket","mortar","lure","barrel"})
        {
            var targets=new List<Enemy>();
            foreach(var at in new[]{new Vector3(-radius*.95f,0,0),new Vector3(radius*.95f,0,0),new Vector3(0,0,radius*1.05f)})
            {var target=_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,at,awake:false);target.SetPhysicsProcess(false);targets.Add(target);}
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var health=targets.Select(t=>t.Health).ToArray();
            Blast.Explode(_stage,origin,radius,40,Colors.Orange,knockbackPower:0,effectSource:id,effectDirection:Vector3.Right);
            Check(targets[0].Health<health[0]&&targets[1].Health<health[1],id+" hits front and rear targets at .95R");
            Check(targets[2].Health==health[2],id+" leaves target at 1.05R unharmed");
            var visual=Nodes<CombatBurstEffect>(_stage).Single(v=>v.EffectId==id&&!v.IsQueuedForDeletion());
            visual.SetProcess(false);visual._Process(.07);
            var reach=Nodes<BurstReachVisual>(visual).Single();
            var puffs=Nodes<MeshInstance3D>(reach).ToArray();
            Check(puffs.Count(p=>new Vector2(p.Position.X,p.Position.Z).Length()>=radius*.7f)>=12,id+" has substantial puffs near damaging edge");
            Check(puffs.All(p=>((StandardMaterial3D)p.MaterialOverride).AlbedoColor.A>.3f),id+" edge puffs are visible during initial impact");
            for(int quadrant=0;quadrant<4;quadrant++)
            {
                int q=quadrant;
                Check(puffs.Any(p=>((int)Mathf.Floor(Mathf.PosMod(Mathf.Atan2(p.Position.Z,p.Position.X),Mathf.Tau)/(Mathf.Pi/2)))==q),id+" covers quadrant "+q);
            }
            await Shot(id+"-007");visual._Process(.08);await Shot(id+"-015");
            visual.QueueFree();foreach(var target in targets)target.QueueFree();await Frame();await Frame();
        }
        var fire=CombatSurfaceEffect.Create(_stage.Effects,CombatEffectCatalog.Current.ForSource("fire_pool"),radius,5);
        fire.SetProcess(false);
        var flames=Nodes<GpuParticles3D>(fire).Single(p=>p.Name=="Flames");
        var shader=(ShaderMaterial)flames.ProcessMaterial;
        Check(Mathf.IsEqualApprox((float)shader.GetShaderParameter("root_radius"),radius*.94f),"persistent flame roots follow damage extent");
        for(int i=0;i<90;i++){fire._Process(1.0/60);await Frame();}
        await Shot("fire-150");fire.QueueFree();await Frame();
    }
}
