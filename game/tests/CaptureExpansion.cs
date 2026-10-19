using Godot;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class CaptureExpansion : Node
{
    public override async void _Ready()
    {
        var menu=GD.Load<PackedScene>("res://scenes/Menu.tscn").Instantiate<MainMenu>();AddChild(menu);
        await Frames(5);menu.ShowPage(MenuPage.Customize);await Frames(30);await Shot("expansion-menu");menu.QueueFree();await Frames(3);
        var world=new Node3D();AddChild(world);
        world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("172127"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("c6dcdf"),AmbientLightEnergy=.6f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
        world.AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-50,-35,0),LightEnergy=1.5f,LightColor=new Color("ffe2be"),ShadowEnabled=true});
        world.AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-20,150,0),LightEnergy=.45f,LightColor=new Color("a8c8ff")});
        Mat.Box(world,new Vector3(0,-.15f,0),new Vector3(16,.2f,10),Mat.Solid("3e504e"));
        var cam=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=12,Position=new Vector3(9,10,17)};world.AddChild(cam);cam.LookAt(new Vector3(0,1,0));
        int i=0;
        foreach(var id in new[]{"goose","duck","raccoon","redpanda"})
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player(id));world.AddChild(rig);rig.Position=new Vector3(-4.2f+i*2.8f,0,1.4f);
            rig.ApplyAppearance(new AppearanceProfile{Body=i==0?"#a4d7ed":"",Hat=i%3,Vest=i!=2,Pack=i!=1,HatColor=i==1?"#8d4252":"",VestColor=i==3?"#3c7780":""});
            rig.HideBuiltInWeapon();var gun=WeaponVisual.Build(ArmoryCatalog.Weapon(i==0?"m4":i==1?"r870":i==2?"mp5":"ak47"),rig.HandColor);rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
            rig.Animate(.016f,0,0,0,Vector3.Zero);if(i==0)rig.DodgePose(.45f);
            i++;
        }
        i=0;foreach(var id in new[]{"humanoid","crawler","tether","leaper","spitter","bull","balloon"})
        {var rig=CharacterRig.Create(CharacterCatalog.Enemy(id));world.AddChild(rig);rig.Position=new Vector3(-5.2f+i*1.7f,0,-2.2f);i++;}
        await Frames(20);await Shot("expansion-roster");world.QueueFree();await Frames(3);
        var stage=new Stage{CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false};AddChild(stage);
        stage.Player.ManualControl=true;stage.Player.Invulnerable=true;stage.LeaveSafeRoom();
        stage.Player.GlobalPosition=new Vector3(21,0,0);stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
        stage.Player.SetControls(Vector2.Zero,new Vector3(30,1,0),false);stage.Cam.SnapTo(stage.Player.GlobalPosition);
        await Frames(40);await Shot("expansion-gameplay");stage.Cam.Close=true;await Frames(35);await Shot("expansion-close");
        GD.Print("EXPANSION_CAPTURE_PASS");QuitHygiene.Collect();GetTree().Quit();
    }
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/"+name+".png"));}
}
