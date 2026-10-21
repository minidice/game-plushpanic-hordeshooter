using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Six-pet quality comparison and isolated rendering benchmark; never loads player settings.</summary>
public partial class PetQualityCapture : Node3D
{
    private readonly List<PetModel> _pets=new();
    private readonly List<Node3D> _models=new();
    private string _output="";
    private async Task Frames(int count)
    {
        for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private async Task Save(string name)
    {
        await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        if(image.SavePng($"{_output}/{name}.png")!=Error.Ok) throw new Exception("Capture failed");
    }
    public override async void _Ready()
    {
        try
        {
            var args=OS.GetCmdlineUserArgs();bool perf=args.Contains("--perf"), before=args.Contains("--before");
            bool low=args.Contains("--low"),cute=args.Contains("--cute"),gameScale=args.Contains("--game-scale");
            if(perf && (cute||gameScale||before))throw new ArgumentException("Use --perf alone (optionally --low) for the six-pet benchmark.");
            GetWindow().Size=new Vector2I(1600,gameScale?900:1000);
            _output=ProjectSettings.GlobalizePath("res://output/pets");DirAccess.MakeDirRecursiveAbsolute(_output);
            AddChild(new WorldEnvironment { Environment=new Godot.Environment {
                BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("1c2938"),
                AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("d6e2ed"),AmbientLightEnergy=.5f,
                TonemapMode=Godot.Environment.ToneMapper.Agx } });
            AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-55,-30,0),LightEnergy=1.25f,ShadowEnabled=true,DirectionalShadowMaxDistance=24 });
            AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-25,140,0),LightEnergy=.35f });
            Mat.Box(this,new Vector3(0,-.12f,0),new Vector3(18,.2f,17),Mat.Solid("344456"));
            var camera=new Camera3D { Projection=Camera3D.ProjectionType.Orthogonal,Size=gameScale?17:cute?6.8f:9,Position=new Vector3(0,10,13) };
            if(gameScale)camera.Position=new Vector3(0,.6f+16*Mathf.Sin(Mathf.DegToRad(55)),16*Mathf.Cos(Mathf.DegToRad(55)));
            AddChild(camera);camera.LookAt(new Vector3(0,.6f,0));
            string[] species=cute?new[]{"turtle","rabbit"}:new[]{"turtle","dog","rabbit"};
            for(int row=0;row<2;row++)
            for(int col=0;col<species.Length;col++)
            {
                Node3D model;string label;
                if(before && row==0)
                {
                    using var document=new GltfDocument();using var state=new GltfState();
                    string path=$"{_output}/{(cute?"cute-before":"quality-tier-before")}/{species[col]}.glb";
                    if(!System.IO.File.Exists(path) || document.AppendFromFile(path,state)!=Error.Ok) throw new Exception($"Missing previous model: {path}");
                    model=(Node3D)document.GenerateScene(state);label="PREVIOUS";
                    foreach(MeshInstance3D mesh in model.FindChildren("*",nameof(MeshInstance3D),true,false))
                    for(int s=0;s<mesh.Mesh.GetSurfaceCount();s++)
                    {
                        var colors=mesh.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Color];
                        if(colors.VariantType!=Variant.Type.Nil && mesh.GetActiveMaterial(s) is StandardMaterial3D material)
                            material.VertexColorUseAsAlbedo=colors.AsColorArray().Length>0;
                    }
                }
                else
                {
                    var quality=perf ? (low?PetModelQuality.Low:PetModelQuality.High) : before || row==0 ? PetModelQuality.High : PetModelQuality.Low;
                    var pet=PetModel.Create(species[col],quality);_pets.Add(pet);model=pet;
                    if(perf) pet.SetWeapon("rocket_pod");
                    label=before ? "REBUILT" : quality.ToString().ToUpperInvariant();
                }
                AddChild(model);model.Position=new Vector3((col-(species.Length-1)*.5f)*3.1f,0,(row-.5f)*3.2f);model.RotationDegrees=new Vector3(0,-25,0);_models.Add(model);
                if(!perf) AddChild(new Label3D { Text=$"{species[col].ToUpperInvariant()} / {label}",FontSize=36,PixelSize=.0045f,Position=model.Position+new Vector3(0,.04f,1.1f),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true });
            }
            var title=new Label { Text=before?"PET SHAPES / PREVIOUS + REBUILT":"PET QUALITY / HIGH + LOW",Position=new Vector2(35,24) };
            title.AddThemeFontSizeOverride("font_size",28);AddChild(title);
            if(perf)
            {
                title.Text=$"SIX PETS / {(low?"LOW":"HIGH")}";
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;
                await Frames(90);
                var samples=new List<double>();ulong start=Time.GetTicksUsec(),last=start;
                while(Time.GetTicksUsec()-start<4_000_000)
                {
                    foreach(var pet in _pets) pet.Pose((Time.GetTicksUsec()-start)/1_000_000f,1);
                    await Frames(1);ulong now=Time.GetTicksUsec();samples.Add((now-last)/1000.0);last=now;
                }
                var sorted=samples.Order().ToArray();
                var report=new { quality=low?"low":"high",pets=_pets.Count,resolution="1600x1000",gpu=RenderingServer.GetVideoAdapterName(),frames=samples.Count,
                    mean_ms=samples.Average(),p95_ms=sorted[(int)(sorted.Length*.95)],draw_calls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                    rendered_primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),note="Isolated six pets with rocket modules, no enemies, effects or network; 90 warmup frames then 4 seconds. VSync off. Includes automatic mesh LOD." };
                System.IO.File.WriteAllText($"{_output}/performance-{report.quality}.json",JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
                await Save($"six-pets-{report.quality}");GD.Print(JsonSerializer.Serialize(report));
            }
            else
            {
                string prefix=(cute?"cute-":"")+(before?"shape-comparison":"quality-tiers")+(gameScale?"-game-scale":"");
                await Save(prefix);
                foreach(var (view,angle) in new[]{("front",0f),("side",90f),("rear",180f)})
                {
                    foreach(var model in _models) model.RotationDegrees=new Vector3(0,angle,0);
                    await Save(prefix+"-"+view);
                }
                if(!before)
                {
                    foreach(var pet in _pets) {pet.RotationDegrees=new Vector3(0,-25,0);pet.SetWeapon("twin_blaster");}
                    await Save(prefix+"-blaster");
                    foreach(var pet in _pets) pet.SetWeapon("rocket_pod");
                    await Save(prefix+"-rocket");
                    if(args.Contains("--walk")) for(int f=0;f<48;f++)
                    {
                        foreach(var pet in _pets) pet.Pose(f/24f,1);
                        await Save($"{(cute?"cute-":"")}tier-walk-{f:000}");
                    }
                }
            }
            GD.Print("PET_QUALITY_TIERS_CAPTURE_PASS");GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
}
