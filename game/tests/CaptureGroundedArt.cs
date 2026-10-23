using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HordeShooter.Tests;
public partial class CaptureGroundedArt : Node
{
    private async Task Shot(string tag)
    {
        for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/grounded-"+tag+".png"));
    }
    public override async void _Ready()
    {
        try
        {
            if(DisplayServer.GetName()=="headless")throw new Exception("Rendered viewport required");
            var studio=new Node3D();AddChild(studio);
            studio.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("18242b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("d6e1e5"),AmbientLightEnergy=.7f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
            studio.AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-45,-35,0),LightEnergy=1.5f,LightColor=new Color("ffe7d3"),ShadowEnabled=true});
            studio.AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-20,150,0),LightEnergy=.55f,LightColor=new Color("c6dcff")});
            Mat.Box(studio,new Vector3(0,-.12f,0),new Vector3(12,.2f,6),Mat.Solid("415652"));
            var cam=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=5.7f,Position=new Vector3(0,3.3f,10)};studio.AddChild(cam);cam.LookAt(new Vector3(0,1.1f,0));
            var rigs=new List<CharacterRig>();
            int index=0;
            foreach(string s in new[]{"goose","duck","raccoon","redpanda"})
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player(s));studio.AddChild(rig);rigs.Add(rig);
                rig.Position=new Vector3(-2.7f+index++*1.8f,0,0);rig.RotationDegrees=new Vector3(0,-25,0);
                rig.ApplyAppearance(new AppearanceProfile{Hat=0,Pack=false,Goggles=false,Headset=false,Vest=true});rig.HideBuiltInWeapon();
                var gun=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),rig.HandColor);rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
            }
            await Shot("lineup");
            foreach(var rig in rigs){rig.RotationDegrees=new Vector3(0,150,0);rig.SetPreviewVisibility(true,false);}
            await Shot("rear");
            foreach(var (tag,w,h) in new[]{("slim-tall",.88f,1.1f),("wide-short",1.12f,.9f)})
            {
                foreach(var rig in rigs){rig.RotationDegrees=new Vector3(0,-25,0);rig.ApplyAppearance(new AppearanceProfile{Hat=0,Pack=false,Goggles=false,BodyWidth=w,BodyHeight=h});rig.SetPreviewVisibility(true,true);}
                await Shot(tag);
            }
            foreach(var rig in rigs){rig.ApplyAppearance(new AppearanceProfile{Hat=0,Pack=false,Goggles=false});rig.SetLocomotionPose(.3f,1,false);rig.Animate(.016f,1,0,0,Vector3.Zero);}
            await Shot("running");
            foreach(var rig in rigs){rig.SetLocomotionPose(.1f,0,true);rig.Animate(.016f,0,0,0,Vector3.Zero);}
            foreach(var rig in rigs){rig.FlashHit();rig.GetNode<Node>("HitFlash").SetProcess(false);}
            await Shot("hit-flash");
            foreach(var rig in rigs){rig.GetNode<Node>("HitFlash")._Process(.2);rig.ShowAs(VisionSystem.Visibility.Silhouette);}
            await Shot("silhouette");
            foreach(var rig in rigs)rig.ShowAs(VisionSystem.Visibility.Visible);
            for(int i=0;i<35;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            cam.Size=2.8f;cam.Position=rigs[0].Position+new Vector3(3,2.4f,5);cam.LookAt(rigs[0].Position+new Vector3(.1f,1.1f,.15f));await Shot("goose-close");
            studio.Free();
            var stage=new Stage{CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false};AddChild(stage);
            stage.Player.ManualControl=true;stage.Player.Invulnerable=true;
            stage.Player.Rig.ApplyAppearance(new AppearanceProfile{Hat=0,Pack=false,Goggles=false});
            stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
            stage.Player.SetControls(Vector2.Zero,stage.Player.GlobalPosition+new Vector3(4,1,6),false);
            await Shot("gameplay");
            stage.Free();QuitHygiene.Collect();GD.Print("GROUNDED_CAPTURE_PASS");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitHygiene.Collect();GetTree().Quit(1);}
    }
}
