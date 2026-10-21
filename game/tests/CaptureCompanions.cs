using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class CaptureCompanions : Node3D
{
    private readonly List<DroneModel> _drones=new();
    private readonly List<SidekickModel> _buddies=new();
    private readonly List<Node3D> _labels=new();
    private string _output="";
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Save(string name)
    {
        await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var im=GetViewport().GetTexture().GetImage();
        if(im.SavePng($"{_output}/{name}.png")!=Error.Ok)throw new Exception("Capture failed");
    }
    private void Label(string text,Vector3 at)
    {
        var label=new Label3D{Text=text,FontSize=30,PixelSize=.005f,Position=at,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true};AddChild(label);_labels.Add(label);
    }
    private void ClearModels()
    {
        foreach(var p in _drones)p.Free();foreach(var p in _buddies)p.Free();foreach(var l in _labels)l.Free();
        _drones.Clear();_buddies.Clear();_labels.Clear();
    }
    public override async void _Ready()
    {
        try
        {
            var args=OS.GetCmdlineUserArgs();bool perf=args.Contains("--perf"),low=args.Contains("--low");
            GetWindow().Size=new Vector2I(1600,1000);
            _output=ProjectSettings.GlobalizePath("res://output/companions");DirAccess.MakeDirRecursiveAbsolute(_output);
            AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("243447"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("d6e2ed"),AmbientLightEnergy=.55f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
            AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-55,-30,0),LightEnergy=1.25f,ShadowEnabled=true,DirectionalShadowMaxDistance=26});
            AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-20,140,0),LightEnergy=.4f});
            Mat.Box(this,new Vector3(0,-.15f,0),new Vector3(25,.2f,23),Mat.Solid("344456"));
            var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=12,Position=new Vector3(0,10,13)};AddChild(camera);camera.LookAt(new Vector3(0,.7f,0));
            var title=new Label{Text="DRONES / HIGH + LOW",Position=new Vector2(35,24)};title.AddThemeFontSizeOverride("font_size",28);AddChild(title);
            if(perf)
            {
                var quality=low?PetModelQuality.Low:PetModelQuality.High;
                for(int i=0;i<6;i++)
                {
                    var p=DroneModel.Create(DroneModel.BodyIds[i%4],quality);AddChild(p);p.Position=new Vector3((i%3-1)*3,1.3f,(i/3-.5f)*3);p.SetEquipment("blaster");_drones.Add(p);
                }
                title.Text=$"SIX DRONES / {quality}";DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;await Frames(90);
                var samples=new List<double>();ulong start=Time.GetTicksUsec(),last=start;
                while(Time.GetTicksUsec()-start<4_000_000)
                {
                    foreach(var p in _drones)p.Pose((Time.GetTicksUsec()-start)/1_000_000f,1);
                    await Frames(1);ulong now=Time.GetTicksUsec();samples.Add((now-last)/1000.0);last=now;
                }
                var sorted=samples.Order().ToArray();
                var report=new{quality=quality.ToString(),count=6,gpu=RenderingServer.GetVideoAdapterName(),resolution="1600x1000",mean_ms=samples.Average(),p95_ms=sorted[(int)(sorted.Length*.95)],draw_calls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),note="Six isolated drones with blasters, VSync off, 90 warmup frames + 4 sec; automatic LOD; not combat or low-end hardware."};
                System.IO.File.WriteAllText($"{_output}/performance-{quality}.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));GD.Print(JsonSerializer.Serialize(report));
                await Save($"six-drones-{quality}");
            }
            else
            {
                for(int row=0;row<2;row++)for(int col=0;col<4;col++)
                {
                    var p=DroneModel.Create(DroneModel.BodyIds[col],row==0?PetModelQuality.High:PetModelQuality.Low);AddChild(p);
                    p.Position=new Vector3((col-1.5f)*2.8f,1.2f,(row-.5f)*3.3f);p.RotationDegrees=new Vector3(0,-20,0);_drones.Add(p);
                    Label($"{p.BodyId.ToUpperInvariant()} / {(row==0?"HIGH":"LOW")}",p.Position+new Vector3(0,-.75f,.9f));
                }
                await Save("drones-quality");
                foreach(var (view,angle) in new[]{("front",0f),("side",90f),("rear",180f)})
                {
                    foreach(var p in _drones)p.RotationDegrees=new Vector3(0,angle,0);await Save("drones-"+view);
                }
                foreach(var module in DroneModel.EquipmentIds)
                {
                    foreach(var p in _drones){p.RotationDegrees=new Vector3(0,-20,0);p.SetEquipment(module);}
                    title.Text="SHARED DRONE EQUIPMENT / "+module.ToUpperInvariant();await Save("drones-"+module);
                }
                // One distinctive module on each body for the overview, all modules already checked on all bodies.
                for(int i=0;i<_drones.Count;i++)_drones[i].SetEquipment(DroneModel.EquipmentIds[i%4]);
                title.Text="DRONES / INTERCHANGEABLE EQUIPMENT";await Save("drones-equipped");
                foreach(var p in _drones.Skip(4))p.Visible=false;
                foreach(var label in _labels.Skip(4))label.Visible=false;
                await Save("drones-overview");
                if(args.Contains("--motion"))for(int f=0;f<48;f++)
                {
                    foreach(var p in _drones)p.Pose(f/24f,.8f);await Save($"drone-motion-{f:000}");
                }
                ClearModels();camera.Size=8;camera.Position=new Vector3(0,6,11);camera.LookAt(new Vector3(0,.6f,0));title.Text="SIDEKICKS / 60% CHARACTER PRESETS";
                for(int i=0;i<4;i++)
                {
                    var p=SidekickModel.Create(SidekickModel.PresetIds[i],PetModelQuality.High);AddChild(p);p.Position=new Vector3((i-1.5f)*2,0,0);p.RotationDegrees=new Vector3(0,-20,0);p.Rig.UpdateWeaponGrip();_buddies.Add(p);
                    Label(p.PresetId.ToUpperInvariant()+" / "+p.Species.ToUpperInvariant(),p.Position+new Vector3(0,-.02f,.85f));
                }
                await Save("sidekicks");
                foreach(var (view,angle) in new[]{("front",0f),("side",90f),("rear",180f)})
                {
                    foreach(var p in _buddies){p.RotationDegrees=new Vector3(0,angle,0);p.Rig.UpdateWeaponGrip();}await Save("sidekicks-"+view);
                }
                foreach(var p in _buddies){p.RotationDegrees=new Vector3(0,-20,0);p.SetQuality(PetModelQuality.Low);p.Rig.UpdateWeaponGrip();}
                await Save("sidekicks-low");
                foreach(var p in _buddies)p.SetQuality(PetModelQuality.High);
                if(args.Contains("--motion"))for(int f=0;f<48;f++)
                {
                    foreach(var p in _buddies)p.Animate(1/24f,1);await Save($"sidekick-motion-{f:000}");
                }
            }
            ClearModels();GD.Print("COMPANION_CAPTURE_PASS");QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
