using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
// Actual Forward+ scene measurement, not a headless FPS benchmark.
public partial class CorpsePerformance : Node3D
{
    private Stage _stage=null!;
    private readonly List<Corpse> _bodies=new();
    private readonly List<object> _results=new();
    public override async void _Ready()
    {
        try
        {
            if(DisplayServer.GetName()=="headless")throw new Exception("Run with a renderer for performance measurements");
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;
            _stage=new Stage{CharacterOverride="goose",DirectorEnabled=false};AddChild(_stage);
            _stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;_stage.Player.SetPhysicsProcess(false);
            foreach(var enemy in _stage.AllEnemies.ToArray())enemy.QueueFree();
            _stage.Player.Position=new Vector3(65,0,0);_stage.Cam.SnapTo(_stage.Player.Position);_stage.Cam.SetProcess(false);
            await Seconds(2);
            foreach(var scenario in new[]{("baseline",0,false,false),("45_infected_settled",45,false,false),
                ("100_infected_settled",100,false,false),("160_infected_settled",160,false,false),
                ("100_animals_settled",100,true,false),("100_infected_tumbling",100,false,true),
                // Current policy (decisions.md, corpse reactions): settled bodies sleep natively and wake when touched.
                ("100_infected_sleeping",100,false,false),("100_infected_walked_through",100,false,false),
                // A tumbling pile measured after it has had 20 s to come to rest (assisted sleep, I-71).
                ("100_infected_pile_after_20s",100,false,true)})
            {
                foreach(var b in _bodies)if(IsInstanceValid(b))b.QueueFree();_bodies.Clear();
                await Seconds(.2);
                for(int i=0;i<scenario.Item2;i++)
                {
                    var def=scenario.Item3?CharacterDefinition.MakeZombie(CharacterCatalog.Player(new[]{"goose","duck","raccoon","redpanda"}[i%4]),CharacterCatalog.Enemy("plush")):
                        CharacterCatalog.Enemy(i%4==0?"crawler":"humanoid");
                    var rig=CharacterRig.Create(def);AddChild(rig);
                    rig.Position=new Vector3(65+(i%12-5.5f)*.9f,scenario.Item4?1:0,(i/12-5)*.8f);
                    var b=(Corpse)_stage.Effects.SpawnCorpse(rig,def.ColliderRadius,def.ColliderHeight,
                        scenario.Item4?new Vector3((i%3-1)*2,2,1):Vector3.Zero,new Vector3(.7f,1,.4f));
                    bool native=scenario.Item1.StartsWith("100_infected_sleeping")||scenario.Item1.StartsWith("100_infected_walked");
                    if(!scenario.Item4&&native){b.Rotation=new Vector3(.8f,(i%7)*.6f,.7f);b.Sleeping=true;rig.SetSquash(.2f);}
                    else if(!scenario.Item4){b.Freeze=true;b.SetPhysicsProcess(false);rig.SetSquash(.2f);b.Rotation=new Vector3(.8f,(i%7)*.6f,.7f);}
                    _bodies.Add(b);
                }
                if(!scenario.Item4)await Seconds(1);
                if(scenario.Item1=="100_infected_pile_after_20s")await Seconds(20);
                bool walking=scenario.Item1=="100_infected_walked_through";
                if(walking)
                {
                    // The goose walks back and forth through the pile: every step queries and kicks corpses.
                    _stage.Player.Position=new Vector3(59,0,-.4f);_stage.Player.SetPhysicsProcess(true);
                }
                var frames=new List<double>();var physics=new List<double>();var clock=Stopwatch.StartNew();double previous=clock.Elapsed.TotalMilliseconds;
                while(clock.Elapsed.TotalSeconds<4)
                {
                    if(walking){float dir=(int)(clock.Elapsed.TotalSeconds/2)%2==0?1:-1;
                        _stage.Player.SetControls(new Vector2(dir,0),_stage.Player.Position+new Vector3(dir*10,1,0),false);}
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    double now=clock.Elapsed.TotalMilliseconds;frames.Add(now-previous);previous=now;
                    physics.Add(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)*1000);
                }
                frames.Sort();physics.Sort();
                int awake=_bodies.Count(x=>IsInstanceValid(x)&&!x.Sleeping&&!x.Freeze);
                var result=new{scenario=scenario.Item1,count=scenario.Item2,awake_at_end=awake,samples=frames.Count,
                    average_frame_ms=frames.Average(),p95_frame_ms=frames[(int)((frames.Count-1)*.95)],
                    average_fps=1000/frames.Average(),average_physics_ms=physics.Average(),p95_physics_ms=physics[(int)((physics.Count-1)*.95)]};
                if(walking){_stage.Player.SetControls(Vector2.Zero,_stage.Player.Position,false);_stage.Player.SetPhysicsProcess(false);}
                _results.Add(result);GD.Print(JsonSerializer.Serialize(result));
                if(scenario.Item2==100&&!scenario.Item3&&!scenario.Item4)
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/corpses-100.png"));
                }
            }
            var report=new{recorded_at=DateTimeOffset.Now,adapter=RenderingServer.GetVideoAdapterName(),
                engine=Engine.GetVersionInfo()["string"].AsString(),viewport=GetViewport().GetVisibleRect().Size.ToString(),
                notes="Forward+; Jolt; VSync off; actual Stage1 warehouse; no living horde or network; 4 seconds per scenario; settled cases warmed 1 second; tumbling includes initial contacts",
                results=_results};
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://output/corpse-performance.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            _stage.QueueFree();await Seconds(.1);GD.Print("CORPSE_PERFORMANCE_PASS");QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async Task Seconds(double count)
    {var clock=Stopwatch.StartNew();while(clock.Elapsed.TotalSeconds<count)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
}
