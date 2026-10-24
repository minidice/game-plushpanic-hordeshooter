using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
namespace HordeShooter.Tests;

/// <summary>Uncapped renderer stress, fixed actors, no claims about six-player or low-end hardware.</summary>
public partial class CombatEffectsPerformance : Node
{
    private Stage _stage=null!;
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds){var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<seconds)await Frame();}
    public override async void _Ready()
    {
        int exit=0;
        try
        {
            if(DisplayServer.GetName()=="headless")throw new Exception("GPU window required");
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);OS.LowProcessorUsageMode=false;Engine.MaxFps=0;
            _stage=new Stage{MapOverride=new LayoutMap(),DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};
            AddChild(_stage);await Wait(.5);
            var at=new Vector3(94,1.25f,-24);var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;p.TeleportTo(at);
            _stage.Cam.SnapTo(at+Vector3.Up);
            foreach(var enemy in _stage.AllEnemies.ToArray())enemy.QueueFree();
            for(int i=0;i<20;i++){var e=_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,at+new Vector3((i%5-2)*1.2f,0,-3-i/5),awake:false);e.SetPhysicsProcess(false);}
            await Wait(2);
            var rows=new List<object>();
            foreach(bool stress in new[]{false,true})
            {
                if(stress)
                    for(int i=0;i<6;i++)
                    {
                        var holder=new Node3D{Position=at+new Vector3((i%3-1)*3,0,-2-(i/3)*3)};_stage.Effects.AddChild(holder);
                        CombatSurfaceEffect.Create(holder,CombatEffectCatalog.Current.ForSource(i%2==0?"fire_pool":"poison_pool"),2.5f,20);
                    }
                var watch=Stopwatch.StartNew();double previous=0,nextBurst=0;
                var frames=new List<double>();var draws=new List<double>();var cpu=new List<double>();
                int sequence=0;
                while(watch.Elapsed.TotalSeconds<6)
                {
                    if(stress&&watch.Elapsed.TotalSeconds>=nextBurst)
                    {
                        nextBurst+=.1;
                        var def=CombatEffectCatalog.Current.ForSource(sequence%2==0?"barrel":"grenade");
                        CombatBurstEffect.Play(_stage.Effects,def,at+new Vector3((sequence%3-1)*2,0,-4),3);
                        sequence++;
                    }
                    await Frame();double now=watch.Elapsed.TotalMilliseconds;
                    if(now>1500){frames.Add(now-previous);draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));cpu.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);}
                    previous=now;
                }
                frames.Sort();
                var row=new{scenario=stress?"six_surfaces_ten_bursts_per_second":"forest_twenty_fixed_actors",samples=frames.Count,
                    mean_ms=frames.Average(),p95_ms=frames[(int)((frames.Count-1)*.95)],draw_calls=draws.Average(),process_ms=cpu.Average()};
                rows.Add(row);GD.Print(JsonSerializer.Serialize(row));
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=GetViewport().GetTexture().GetImage();image.SavePng(ProjectSettings.GlobalizePath("res://output/vfx-performance-"+(stress?"stress":"base")+".png"));
            }
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://output/vfx-performance.json"),JsonSerializer.Serialize(new{
                adapter=RenderingServer.GetVideoAdapterName(),resolution=GetViewport().GetVisibleRect().Size.ToString(),
                note="Uncapped Forward+, same forest camera and 20 fixed enemy bodies; render stress, not AI/network benchmark. 1.5s warmup excluded in each 6s sample.",rows},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("COMBAT_EFFECTS_PERFORMANCE_PASS");
        }
        catch(Exception e){GD.PushError(e.ToString());exit=1;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.Free();QuitHygiene.Collect();GetTree().Quit(exit);
    }
}
