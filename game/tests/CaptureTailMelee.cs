using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class CaptureTailMelee : Node
{
    public override async void _Ready()
    {
        AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("18242b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("cbdde4"),AmbientLightEnergy=.7f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-45,-35,0),LightEnergy=1.5f,ShadowEnabled=true});
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-20,150,0),LightEnergy=.6f});
        Mat.Box(this,new Vector3(0,-.12f,0),new Vector3(15,.2f,5),Mat.Solid("415652"));
        var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=9.4f,Position=new Vector3(8,4,-8)};
        AddChild(camera);camera.LookAt(new Vector3(0,1,0));
        var nodes=new List<Node3D>();
        var species=new[]{"goose","duck","raccoon","redpanda"};
        for(int i=0;i<4;i++)
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player(species[i]));AddChild(rig);nodes.Add(rig);
            rig.Position=new Vector3(-3.3f+i*2.2f,0,0);
            rig.ApplyAppearance(new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false});rig.HideBuiltInWeapon();
            rig.Animate(.16f,.7f,0,0,Vector3.Zero);
        }
        await Frames(5);await Shot("tails-rear-quarter");
        foreach(var node in nodes)node.Free();
        var times=new[]{-1f,.11f,.17f,.23f,.42f};
        var labels=new[]{"READY","WINDUP 0.11s","IMPACT 0.17s","FOLLOW 0.23s","RECOVER 0.42s"};
        for(int i=0;i<5;i++)
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player("goose"));AddChild(rig);
            rig.Position=new Vector3(-4.4f+i*2.2f,0,0);rig.RotationDegrees=new Vector3(0,-15,0);
            rig.HideBuiltInWeapon();
            var built=WeaponVisual.Build(ArmoryCatalog.Weapon("frying_pan"),rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);
            rig.SetMeleePose(times[i]);rig.Animate(.016f,0,0,0,Vector3.Zero);
            AddChild(new Label3D{Text=labels[i],FontSize=30,PixelSize=.005f,Position=rig.Position+Vector3.Up*2.6f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true});
        }
        camera.Size=11.8f;camera.Position=new Vector3(0,4,11);camera.LookAt(new Vector3(0,1.2f,0));
        await Frames(5);await Shot("melee-pose-sequence");
        GD.Print("TAIL_MELEE_CAPTURE_PASS");QuitHygiene.Collect();GetTree().Quit();
    }
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/"+name+".png"));
    }
}
