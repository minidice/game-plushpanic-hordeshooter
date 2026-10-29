using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Independent real-scene acceptance: readable roster, walking pursuit/roll escape and render cost.</summary>
public partial class CommonRosterPlayCheck : Node
{
    private Stage _stage = null!;
    private Camera3D _camera = null!;
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private readonly string[] _species = {"eagle","parrot","lion","tiger","reindeer","rhino"};
    private sealed class Field : StageMap
    {
        public override Rect2 Area => new(-30,-25,130,50);
        protected override void BuildMap() { PlayerStart=new(0,0,0);Floor(-30,100,-25,25,"777857"); }
        public override void Light(Godot.Environment env,DirectionalLight3D key)
        {
            env.AmbientLightColor=new("d7d9b0");env.AmbientLightEnergy=.55f;
            key.LightColor=new("ffe4ae");key.LightEnergy=1.05f;key.RotationDegrees=new(-55,-35,0);
        }
    }
    private void Check(bool ok,string label) { if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label); }
    private async Task Frames(int n) {for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private async Task Shot(string name)
    {
        if(!Windowed)return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/roster-play-"+name+".png"))==Error.Ok,"capture "+name);
    }
    public override async void _Ready()
    {
        int exit=0;
        try {await Run();GD.Print("COMMON_ROSTER_PLAY_PASS "+_checks);}
        catch(Exception e) {GD.PushError(e.ToString());GD.Print("COMMON_ROSTER_PLAY_FAIL "+e.Message);exit=1;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.QueueFree();await Frames(3);QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private async Task Clear()
    {
        foreach(var e in _stage.AllEnemies.ToArray())if(IsInstanceValid(e))e.QueueFree();
        await Frames(3);
    }
    private Enemy Spawn(CommonIdentity identity,Vector3 at,bool active=false)
    {
        var enemy=_stage.SpawnCommonIdentity(identity,at,active);
        if(!active)enemy.SetPhysicsProcess(false);
        enemy.Rig.ShowAs(VisionSystem.Visibility.Visible);enemy.Rig.Face(new(.15f,0,1));return enemy;
    }
    private void Frame(Vector3 center,float size)
    {
        _camera.Projection=Camera3D.ProjectionType.Orthogonal;_camera.Size=size;
        _camera.Position=center+new Vector3(2,10,15);_camera.LookAt(center);_camera.MakeCurrent();
    }
    private async Task Run()
    {
        _stage=new Stage{MapOverride=new Field(),CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false,
            ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
        AddChild(_stage);await Frames(4);_stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;
        _stage.Cam.SetProcess(false);_stage.Cam.SetPhysicsProcess(false);_camera=new Camera3D();AddChild(_camera);
        await Clear();
        var player=_stage.Player;
        // Keep cosmetics separate from speed: a single unarmed human chases a normally walking survivor.
        player.TeleportTo(new(5,.05f,0));player.SetControls(Vector2.Right,new(50,1,0),false);
        var zombie=Spawn(new("zombie","human","ochre","unarmed",0),new(0,.05f,0),true);
        zombie.Reaction=Reaction.Hunter;float before=player.GlobalPosition.X-zombie.GlobalPosition.X;
        Frame(new(15,1,0),18);await Frames(240);
        float after=player.GlobalPosition.X-zombie.GlobalPosition.X;
        Check(zombie.Speed>Player.AutoRunSpeed&&zombie.Speed<Player.AutoRunSpeed*1.1f,"ordinary infected speed is subtly faster than automatic running");
        Check(after<before-.35f&&after>1,"walking pursuit gradually closes the gap without instant contact");await Shot("pursuit");
        float rollBefore=after;player.PressRoll();await Frames(30);
        Check(player.GlobalPosition.X-zombie.GlobalPosition.X>rollBefore+1,"roll still creates an escape opening");
        player.SetControls(Vector2.Zero,Vector3.Forward,false);player.TeleportTo(new(0,.05f,8));await Clear();
        _stage.Vision.SetPhysicsProcess(false);
        // Each individual uses the actual initialization, including weapons/grips, not a standalone mesh copy.
        Frame(new(0,1,0),16);
        for(int i=0;i<_species.Length;i++)Spawn(new("hunter",_species[i],"green",i%3==0?"club":i%3==1?"woodbow":"knife",i),new((i-2.5f)*2.7f,.05f,0));
        await Frames(35);await Shot("hunter-species");await Clear();
        for(int i=0;i<_species.Length;i++)Spawn(new("armed",_species[i],"red",i%3==0?"mp5":i%3==1?"ak47":"m4",i,CommonGear.Vest),new((i-2.5f)*2.7f,.05f,0));
        await Frames(35);await Shot("armed-species");await Clear();
        Frame(new(0,1,0),14);
        for(int i=0;i<10;i++)Spawn(new("zombie","human","ochre","unarmed",i),new((i%5-2)*2.6f,.05f,(i/5-.5f)*3.4f));
        await Frames(35);await Shot("zombie-ten");await Clear();
        foreach(string palette in new[]{"red","green"})
        {
            for(int i=0;i<10;i++)Spawn(new("armed","eagle",palette,i<4?"mp5":"ak47",i,i%2==0?CommonGear.Vest:CommonGear.None),new((i%5-2)*2.6f,.05f,(i/5-.5f)*3.4f));
            await Frames(35);await Shot("eagle-"+palette);await Clear();
        }
        var actor=Spawn(new("armed","rhino","green","ak47",2,CommonGear.Vest),Vector3.Zero);
        Frame(new(0,1,0),5);actor.Ignite(0,3,player);await Frames(8);actor.Rig.FlashHit();await Shot("status-hit");
        actor.Rig.ShowAs(VisionSystem.Visibility.Hidden);await Frames(2);
        var status=actor.Rig.GetNode<StatusVisualController>("StatusVisuals");Check(!status.Displayed,"new species status does not expose hidden enemy");
        actor.Rig.ShowAs(VisionSystem.Visibility.Visible);await Frames(30);actor.Die(Vector3.Back,0);await Frames(30);await Shot("corpse");await Clear();
        if(Windowed&&OS.GetCmdlineUserArgs().Contains("--roster-perf"))await Crowd();
        await ForestEncounter();
    }
    private async Task ForestEncounter()
    {
        _stage.Free();await Frames(3);
        _stage=new Stage {MapOverride=new LayoutMap(),ThemeOverride="animals",CharacterOverride="goose",
            ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
        AddChild(_stage);await Frames(6);
        var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;
        p.TeleportTo(new(54,.05f,-6));p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
        _stage.Cam.MakeCurrent();_stage.Cam.SnapTo(p.Position+Vector3.Up);await Frames(30);
        var forest=_stage.AllEnemies.Where(e=>!e.IsSpecial&&e.Position.X>=40&&e.Position.X<165).ToArray();
        Check(forest.Length>0&&forest.All(e=>e.Monster.CommonIdentity is {Faction:"hunter",Species:"eagle",Palette:"green"}),"real Stage2 forest ambient groups use coherent green eagle hunters");
        Check(_stage.CommonPackAt(new(480,0,-90))=="armed_rhino","real Stage2 guarded plateau is explicit armed exception");
        float start=p.Position.X;
        for(int i=0;i<120;i++)
        {
            var target=_stage.AllEnemies.Where(e=>!e.IsDead).OrderBy(e=>e.Position.DistanceSquaredTo(p.Position)).FirstOrDefault();
            p.SetControls(Vector2.Right,target?.Position+Vector3.Up??p.Position+Vector3.Right*10,true);
            if(i==45)p.PressRoll();await Frames(1);
        }
        Check(p.Position.X>start+4,"real forest walking and roll advance through integrated encounter");
        p.SetControls(Vector2.Zero,p.Position+Vector3.Right*10,false);await Shot("stage2-forest-play");
    }
    private async Task Crowd()
    {
        _stage.Player.TeleportTo(new(0,.05f,15));Frame(Vector3.Up,28);await Frames(30);
        async Task<(double ms,double draws)> Sample()
        {
            double draws=0;ulong start=Time.GetTicksUsec();
            for(int i=0;i<90;i++){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);draws+=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);}
            return((Time.GetTicksUsec()-start)/90000d,draws/90);
        }
        var baseline=await Sample();
        for(int i=0;i<60;i++)Spawn(new("armed",_species[i%6],"green",i%2==0?"mp5":"ak47",i%10,CommonGear.Vest),new((i%10-4.5f)*2.3f,.05f,(i/10-2.5f)*2.5f));
        await Frames(40);var crowded=await Sample();await Shot("crowd60");
        GD.Print($"COMMON_ROSTER_PERF actors=60 baseline_ms={baseline.ms:0.00} active_ms={crowded.ms:0.00} draws={crowded.draws:0}");
        await Clear();Check(!_stage.AllEnemies.Any(),"crowd scene cleanup removes all common actors");
    }
}
