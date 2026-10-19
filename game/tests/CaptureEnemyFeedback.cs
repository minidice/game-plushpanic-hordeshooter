using Godot;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class CaptureEnemyFeedback : Node
{
    public override async void _Ready()
    {
        AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("18242b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("cbdde4"),AmbientLightEnergy=.6f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-45,-35,0),LightEnergy=1.5f,ShadowEnabled=true});
        Mat.Box(this,new Vector3(0,-.12f,0),new Vector3(16,.2f,10),Mat.Solid("415652"));
        var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=14,Position=new Vector3(0,9,12)};AddChild(camera);camera.LookAt(new Vector3(0,1,0));
        var times=new[]{0f,.15f,.28f,.50f};
        var names=new[]{"READY","WINDUP","IMPACT","RECOVERY"};
        for(int row=0;row<2;row++)
        for(int i=0;i<4;i++)
        {
            var def=row==0?CharacterCatalog.Enemy("humanoid"):CharacterDefinition.MakeZombie(CharacterCatalog.Player("goose"),CharacterCatalog.Enemy("humanoid"));
            var rig=CharacterRig.Create(def);AddChild(rig);rig.Position=new Vector3(-4.5f+i*3,0,row==0?-2.5f:2.5f);
            rig.InitializeEnemyAttackPose();rig.Animate(.016f,0,0,0,Vector3.Zero);rig.ApplyEnemyAttackPose(times[i],.28f,.32f);
            AddChild(new Label3D{Text=names[i],FontSize=32,PixelSize=.005f,Position=rig.Position+Vector3.Up*2.5f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true});
        }
        await Frames(3);await Shot("enemy-attack-sequence");
        foreach(var child in GetChildren())if(child is CharacterRig rig)rig.QueueFree();
        foreach(var child in GetChildren())if(child is Label3D label)label.QueueFree();
        await Frames(2);
        camera.Size=12;camera.Position=new Vector3(0,6,12);camera.LookAt(new Vector3(0,1,0));
        for(int i=0;i<4;i++)
        {
            var rig=CharacterRig.Create(i<2?CharacterCatalog.Player("goose"):CharacterCatalog.Enemy("humanoid"));AddChild(rig);rig.Position=new Vector3(-4.2f+i*2.8f,0,0);rig.HideBuiltInWeapon();
            if(i%2==1){rig.FlashHit();rig.GetNode<Node>("HitFlash").SetProcess(false);}
            AddChild(new Label3D{Text=i%2==0?"NORMAL":"HIT FLASH / 0.10s",FontSize=32,PixelSize=.005f,Position=rig.Position+Vector3.Up*2.5f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true});
        }
        await Frames(3);await Shot("hit-feedback-comparison");
        GD.Print("ENEMY_FEEDBACK_CAPTURE_PASS");QuitHygiene.Collect();GetTree().Quit();
    }
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/"+name+".png"));}
}
