using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class CaptureReloadPose : Node
{
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var error=GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/"+name+".png"));
        if(error!=Error.Ok)throw new Exception("Capture failed: "+error);
    }
    public override async void _Ready()
    {
        try
        {
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output/reload-frames"));
            AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("18242b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("cbdde4"),AmbientLightEnergy=.7f,TonemapMode=Godot.Environment.ToneMapper.Agx}});
            AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-45,-35,0),LightEnergy=1.5f,LightColor=new Color("ffe7c6"),ShadowEnabled=true});
            AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-20,150,0),LightEnergy=.5f});
            Mat.Box(this,new Vector3(0,-.13f,0),new Vector3(16,.2f,8),Mat.Solid("415652"));
            var cam=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=8.8f,Position=new Vector3(0,5.2f,10)};AddChild(cam);cam.LookAt(new Vector3(0,1,0));
            var ui=new CanvasLayer();AddChild(ui);
            var title=new Label{Position=new Vector2(50,40)};title.AddThemeFontSizeOverride("font_size",30);ui.AddChild(title);
            var ids=new[]{"goose","duck","raccoon","redpanda"};
            var rigs=new List<CharacterRig>();
            for(int i=0;i<4;i++)
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player(ids[i]));AddChild(rig);rigs.Add(rig);
                rig.Position=new Vector3(-3.15f+i*2.1f,0,0);rig.RotationDegrees=new Vector3(0,-25,0);
                var label=new Label3D{Text=ids[i].ToUpperInvariant(),FontSize=32,PixelSize=.006f,Position=rig.Position+new Vector3(0,2.55f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled};AddChild(label);
            }
            int movie=0;
            foreach(bool gear in new[]{false,true})
            foreach(string id in new[]{"m4","r870"})
            {
                var states=new List<WeaponState>();var guns=new List<Node3D>();
                foreach(var rig in rigs)
                {
                    rig.ApplyAppearance(new AppearanceProfile{Hat=gear?1:0,Vest=gear,Pack=gear,Goggles=false,VestStyle=gear?1:0});rig.HideBuiltInWeapon();
                    var def=ArmoryCatalog.Weapon(id);var gun=WeaponVisual.Build(def,rig.HandColor);rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);guns.Add(gun.Root);
                    var state=new WeaponState(def,0,id=="m4"?def.MagSize:3);states.Add(state);
                }
                await Frames(3);
                for(int frame=0;frame<96;frame++)
                {
                    float dt=1f/30;
                    title.Text=$"{id.ToUpperInvariant()}  /  {(gear?"EQUIPPED":"BASE")}  /  {(frame<10?"READY":states[0].Reloading?"RELOADING":"RETURN")}";
                    for(int i=0;i<rigs.Count;i++)
                    {
                        if(frame==10)states[i].StartReload();
                        states[i].Tick(dt,false);
                        rigs[i].SetLocomotionPose(dt,0,false);rigs[i].SetReloadPose(dt,states[i]);rigs[i].Animate(dt,0,0,0,Vector3.Zero);
                    }
                    if(frame is 10 or 24 or 38 or 56 or 83)await Shot($"reload-{id}-{(gear?"gear":"base")}-{frame}");
                    if(gear)await Shot($"reload-frames/{movie++:D4}");
                    else await Frames(1);
                }
                foreach(var rig in rigs)rig.ResetPose();foreach(var gun in guns)gun.Free();
            }
            // High quarter-view at gameplay scale: both reload types with helmets and vests.
            cam.Size=17;cam.Position=new Vector3(7,13,10);cam.LookAt(new Vector3(0,1,0));
            for(int i=0;i<4;i++)
            {
                var def=ArmoryCatalog.Weapon(i%2==0?"m4":"r870");var gun=WeaponVisual.Build(def,rigs[i].HandColor);rigs[i].Weapon.AddChild(gun.Root);rigs[i].BindWeaponGrip(gun);
                var state=new WeaponState(def,0,4);state.StartReload();state.Tick(def.ReloadTime*.75f,false);rigs[i].SetReloadPose(.2f,state);rigs[i].Animate(0,0,0,0,Vector3.Zero);
            }
            title.Text="GAMEPLAY SCALE / MAGAZINE + SHELL";await Frames(3);await Shot("reload-gameplay-scale");
            foreach(var rig in rigs)rig.Free();GD.Print("RELOAD_CAPTURE_PASS");QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
