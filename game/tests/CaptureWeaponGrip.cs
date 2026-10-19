using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HordeShooter.Tests;
public partial class CaptureWeaponGrip : Node
{
    public override async void _Ready()
    {
        AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("18242b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("cbdde4"),AmbientLightEnergy=.65f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-45,-35,0),LightEnergy=1.5f,LightColor=new Color("ffe7c6"),ShadowEnabled=true});
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-20,150,0),LightEnergy=.5f,LightColor=new Color("b8d5ff")});
        Mat.Box(this,new Vector3(0,-.12f,0),new Vector3(12,.2f,5),Mat.Solid("415652"));
        var cam=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=9.2f,Position=new Vector3(8,4.8f,10)};AddChild(cam);cam.LookAt(new Vector3(0,1,0));
        var ids=new[]{"goose","duck","raccoon","redpanda"};var guns=new[]{"m4","r870","m1911","mortar"};
        var rigs=new List<CharacterRig>();
        for(int i=0;i<4;i++)
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player(ids[i]));AddChild(rig);rigs.Add(rig);rig.Position=new Vector3(-3.3f+i*2.2f,0,0);rig.ApplyAppearance(new AppearanceProfile{VestStyle=i%2,Headset=i==1||i==3,Goggles=i!=1});rig.HideBuiltInWeapon();
            var def=ArmoryCatalog.Weapon(guns[i]);if(i==2)def=def.DualVariant!;
            var built=WeaponVisual.Build(def,rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);rig.Animate(.016f,.2f,.3f,0,Vector3.Zero);
        }
        await Frames(15);await Shot("weapon-grip-quarter");
        cam.Position=new Vector3(0,2.3f,11);cam.LookAt(new Vector3(0,1,0));await Frames(3);await Shot("weapon-grip-front");
        cam.Size=3.2f;cam.Position=new Vector3(-.3f,2.8f,4);cam.LookAt(new Vector3(-3.3f,1,0));await Frames(3);await Shot("weapon-grip-close");
        foreach(var rig in rigs)rig.Free();
        for(int i=0;i<3;i++)
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player("goose"));AddChild(rig);rig.Position=new Vector3(-2.8f+i*2.8f,0,0);rig.RotationDegrees=new Vector3(0,-25,0);
            rig.ApplyAppearance(new AppearanceProfile{Goggles=true,Headset=i==2,VestStyle=i==2?1:0,Body=i==2?"#66c4bc":"",Wing=i==2?"#4b9c92":"",HatColor="#28383c",VestColor="#3c4650"});rig.HideBuiltInWeapon();
            var built=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),rig.HandColor);rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);
            rig.SetLocomotionPose(.2f,i==1?1:0,i==2);rig.Animate(.08f,i==1?1:0,i==2?.7f:0,0,Vector3.Zero);
            var label=new Label3D{Text=new[]{"IDLE","RUN","FIRE + CUSTOM"}[i],FontSize=32,PixelSize=.006f,Position=rig.Position+new Vector3(0,2.8f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true};AddChild(label);
        }
        cam.Size=6.3f;cam.Position=new Vector3(0,4.5f,11);cam.LookAt(new Vector3(0,1.2f,0));await Frames(5);await Shot("weapon-poses-reference");
        GD.Print("WEAPON_GRIP_CAPTURE_PASS");QuitHygiene.Collect();GetTree().Quit();
    }
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/"+name+".png"));}
}
