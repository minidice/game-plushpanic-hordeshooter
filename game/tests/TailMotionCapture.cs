using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual GPU silhouette comparison, reproducible multi-frame rigs, then live Stage 2 controls.</summary>
public partial class TailMotionCapture : Node
{
    private const string Output="res://output/mammal-tails/";
    private int _checks;
    private readonly List<string> _measurements=new(){"species,phase,near_yaw,middle_yaw,tip_yaw"};
    private void Check(bool ok,string message){if(!ok)throw new Exception(message);_checks++;}
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private async Task<Image> Shot(string name)
    {
        await Frames(2);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath(Output+name+".png"))==Error.Ok,"saved "+name);return image;
    }
    private void Measure(CharacterRig rig,string species,string phase)
    {
        var skeleton=(Skeleton3D)rig.Model.FindChild("TailSkeleton",true,false)!;
        _measurements.Add(species+","+phase+","+string.Join(",",Enumerable.Range(1,3).Select(i=>skeleton.GetBonePoseRotation(i).GetEuler().Y.ToString("F6",System.Globalization.CultureInfo.InvariantCulture))));
    }
    public override async void _Ready()
    {
        try {
            Check(DisplayServer.GetName()!="headless","run windowed to verify actual GPU deformation");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output));
            foreach(string species in new[]{"raccoon","redpanda"})await Studio(species);
            foreach(string species in new[]{"raccoon","redpanda"})await Play(species);
            System.IO.File.WriteAllLines(ProjectSettings.GlobalizePath(Output+"runtime-poses.csv"),_measurements);
            GD.Print("TAIL_MOTION_CAPTURE_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }catch(Exception e){GD.PushError("TAIL_MOTION_CAPTURE_FAIL "+e);GetTree().Quit(1);}
    }
    private async Task Studio(string species)
    {
        var scene=new Node3D();AddChild(scene);
        scene.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("26343b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.65f}});
        scene.AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-45,-35,0),LightEnergy=1.4f,ShadowEnabled=true});
        Mat.Box(scene,new Vector3(0,-.11f,0),new Vector3(8,.2f,8),Mat.Solid("53625b"));
        var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=3.7f,Position=new Vector3(3.4f,2.5f,-4.8f),Current=true};scene.AddChild(camera);camera.LookAt(new Vector3(0,.9f,-.3f));
        var rig=CharacterRig.Create(CharacterCatalog.Player(species));scene.AddChild(rig);
        rig.ApplyAppearance(new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false});rig.HideBuiltInWeapon();
        rig.Animate(0,0,0,0,Vector3.Zero);Measure(rig,species,"rest");(await Shot(species+"-rest")).Dispose();
        for(int i=0;i<40;i++){rig.Animate(1f/60,.8f,0,0,Vector3.Back*3);await Frames(1);}
        Measure(rig,species,"move");(await Shot(species+"-move")).Dispose();
        for(int i=0;i<24;i++){
            rig.Rotation=new Vector3(0,(i+1)*.025f,0);rig.Animate(1f/60,.8f,0,0,Vector3.Back*3);await Frames(1);
            if(i==0||i==7||i==23){Measure(rig,species,"turn"+(i+1));(await Shot(species+"-turn-"+(i+1))).Dispose();}
        }
        // Keep every other transform unchanged: only neutralize the real skeleton between images.
        // A detached skin / shader that ignores bones gives identical pixels and fails this check.
        foreach(var node in rig.Model.FindChildren("*",nameof(MeshInstance3D),true,false))
            if(node is MeshInstance3D mesh&&mesh.Name!=species+"_tail_mesh")mesh.Visible=false;
        camera.Size=2.1f;camera.Position=new Vector3(2,1.4f,-3);camera.LookAt(new Vector3(0,.65f,-.55f));
        using var bent=await Shot(species+"-gpu-bent");
        var skeleton=(Skeleton3D)rig.Model.FindChild("TailSkeleton",true,false)!;skeleton.ResetBonePoses();
        using var neutral=await Shot(species+"-gpu-neutral");
        int changed=0;var a=bent.GetData();var b=neutral.GetData();
        for(int i=0;i<a.Length;i+=4)if(Math.Abs(a[i]-b[i])+Math.Abs(a[i+1]-b[i+1])+Math.Abs(a[i+2]-b[i+2])>25)changed++;
        Check(changed>100,species+" actual GPU tail surface must move, changed pixels="+changed);
        GD.Print("TAIL_GPU_PIXELS "+species+" "+changed);
        foreach(var node in rig.Model.FindChildren("*",nameof(MeshInstance3D),true,false))if(node is MeshInstance3D mesh)mesh.Visible=true;
        rig.ApplyAppearance(new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false});rig.HideBuiltInWeapon();
        camera.Size=3.7f;camera.Position=new Vector3(3.4f,2.5f,-4.8f);camera.LookAt(new Vector3(0,.9f,-.3f));
        for(int i=0;i<60;i++)rig.Animate(1f/60,0,0,0,Vector3.Zero);
        Measure(rig,species,"settled");(await Shot(species+"-settled")).Dispose();scene.Free();await Frames(2);
    }
    private async Task Play(string species)
    {
        var stage=new Stage{MapOverride=new LayoutMap(),CharacterOverride=species,DirectorEnabled=false,ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};AddChild(stage);await Frames(5);
        var player=stage.Player;player.ManualControl=true;player.Invulnerable=true;
        var start=new Vector3(11,stage.Tiers.GroundY(new Vector3(11,0,-2))+.05f,-2);player.TeleportTo(start);
        player.Rig.ApplyAppearance(new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false});
        stage.Cam.SetProcess(false);var focus=start+new Vector3(1,1,0);
        stage.Cam.GlobalPosition=focus+new Vector3(5,3,-7);stage.Cam.LookAt(focus);stage.Cam.Size=7;
        player.SetControls(Vector2.Right,start+new Vector3(10,1,0),false);await Frames(25);
        Check(player.GlobalPosition.DistanceTo(start)>.6f,"live Stage2 player moves");Measure(player.Rig,species,"stage-move");(await Shot(species+"-stage-move")).Dispose();
        player.SetControls(Vector2.Right,player.GlobalPosition+new Vector3(0,1,10),false);await Frames(5);
        Measure(player.Rig,species,"stage-turn");(await Shot(species+"-stage-turn")).Dispose();
        player.SetControls(Vector2.Zero,player.GlobalPosition+new Vector3(0,1,10),false);await Frames(65);
        var skeleton=(Skeleton3D)player.Rig.Model.FindChild("TailSkeleton",true,false)!;
        Check(Enumerable.Range(1,3).All(i=>skeleton.GetBonePoseRotation(i).GetEuler().Length()<.004f),"live Stage2 tail settles after controls stop");
        Measure(player.Rig,species,"stage-settled");(await Shot(species+"-stage-settled")).Dispose();stage.Free();await Frames(2);
    }
}
