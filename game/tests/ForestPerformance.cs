using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Wall-clock, uncapped Forward+ comparison. Never run with --fixed-fps.</summary>
public partial class ForestPerformance : Node
{
    private Stage _stage=null!;
    private async Task Seconds(double seconds)
    {var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<seconds)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            if(DisplayServer.GetName()=="headless")throw new Exception("Real renderer required");
            if(OS.GetCmdlineArgs().Contains("--fixed-fps"))throw new Exception("Wall-clock performance requires no --fixed-fps");
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);DisplayServer.WindowMoveToForeground();OS.LowProcessorUsageMode=false;Engine.MaxFps=0;
            string label=OS.GetCmdlineUserArgs().FirstOrDefault()??"expanded";
            var map=new LayoutMap{ForestExpanded=label!="before"};var load=Stopwatch.StartNew();
            _stage=new Stage {MapOverride=map,DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};AddChild(_stage);
            double buildMs=load.Elapsed.TotalMilliseconds;
            if(label=="before"?map.ForestTreeCount!=5:map.ForestTreeCount<40)throw new Exception("Benchmark label does not match the actual forest mode");
            var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;
            foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();
            var rows=new List<object>();
            foreach(var (name,at,combat) in new (string,Vector3,bool)[]{
                ("low",new(54,.05f,-6),false),("mid",new(94,1.25f,-24),false),
                ("high",new(146,2.45f,-38),false),("west",new(176,2.45f,-43),false),
                ("east",new(242,2.45f,-51),false),("yard",new(272,1.25f,-47),false),
                ("combat_mid",new(94,1.25f,-24),true)})
            {
                p.TeleportTo(at);p.SetControls(Vector2.Zero,at+Vector3.Right*5,false);_stage.Cam.SnapTo(at+Vector3.Up);
                await Seconds(1.5);
                if(combat)for(int i=0;i<30;i++)_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,at+new Vector3((i%6-2.5f)*1.15f,0,-6-i/6),awake:true);
                await Seconds(.5);
                var frames=new List<double>();var physics=new List<double>();var process=new List<double>();var calls=new List<double>();var primitives=new List<double>();
                var watch=Stopwatch.StartNew();double previous=0;
                while(watch.Elapsed.TotalSeconds<4)
                {
                    if(combat)
                    {
                        float x=(float)Math.Sin(watch.Elapsed.TotalSeconds*1.5)*.35f;
                        p.SetControls(new Vector2(x,0),at+new Vector3(0,1,-10),false);
                    }
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    double now=watch.Elapsed.TotalMilliseconds;frames.Add(now-previous);previous=now;
                    physics.Add(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)*1000);
                    process.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);
                    calls.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
                    primitives.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
                }
                frames.Sort();
                var row=new {scene=name,samples=frames.Count,mean_ms=frames.Average(),p95_ms=frames[(int)((frames.Count-1)*.95)],fps=1000/frames.Average(),physics_ms=physics.Average(),process_ms=process.Average(),draw_calls=calls.Average(),primitives=primitives.Average(),enemies=_stage.AllEnemies.Count(e=>!e.IsDead)};
                rows.Add(row);GD.Print(JsonSerializer.Serialize(row));
                p.SetControls(Vector2.Zero,p.GlobalPosition+Vector3.Right*5,false);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/forest-expand-{label}-{name}.png"));
            }
            var report=new {label,adapter=RenderingServer.GetVideoAdapterName(),resolution=GetViewport().GetVisibleRect().Size.ToString(),build_ms=buildMs,trees=map.ForestTreeCount,tufts=map.ForestGrassCount,note="Forward+; VSync disabled, MaxFps0; 2s warmup +4s wall-clock per view; 30 active enemies in combat; no fixed-fps. Single PC, not low-end/network benchmark.",rows};
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://output/forest-performance-{label}.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("FOREST_PERFORMANCE_PASS "+label);
        }
        catch(Exception e){GD.PushError(e.ToString());failed=true;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.Free();QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
