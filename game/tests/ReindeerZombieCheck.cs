using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual common-rig motion/outfit contracts plus an infected's movement and melee in Stage 2.</summary>
public partial class ReindeerZombieCheck : Node
{
    private Stage? _stage;
    private int _checks;
    private void Check(bool ok,string message) { if(!ok)throw new Exception(message);_checks++; }
    private async Task Frames(int count) { for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/reindeer-zombie/"+name+".png"))==Error.Ok,"saved "+name);
    }
    public override async void _Ready()
    {
        int exit=0;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output/reindeer-zombie"));
            foreach(string weapon in new[]{"club","knife","woodbow","mp5","ak47","m4"})
                RigCheck(new(weapon is "club" or "knife" or "woodbow"?"hunter":"armed","reindeer","ochre",weapon,3,
                    weapon is "mp5" or "ak47" or "m4"?CommonGear.Vest:CommonGear.None));
            for(int variant=0;variant<10;variant++)RigCheck(new("zombie","human","green","unarmed",variant));
            await InfectedStage();
            GD.Print("REINDEER_ZOMBIE_CHECK_PASS "+_checks);
        }
        catch(Exception e) { GD.PushError("REINDEER_ZOMBIE_CHECK_FAIL "+e);exit=1; }
        _stage?.QueueFree();await Frames(3);QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void RigCheck(CommonIdentity id)
    {
        var roster=CommonRoster.Current;var rig=CharacterRig.Create(roster.LookFor(id));AddChild(rig);roster.Apply(rig,id);
        try
        {
            Check(!rig.IsPlaceholder&&rig.Head!=null,"real authored rig "+id.Species);
            var head=rig.Head!;var home=head.Transform;
            var meshes=rig.Model.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
            Check(meshes.Where(m=>m.IsVisibleInTree()).All(m=>m.Mesh!=null&&m.GetAabb().Size.IsFinite()),"finite visible geometry");
            Check(rig.Model.FindChildren("Gear_*",nameof(Node3D),true,false).OfType<Node3D>()
                .Where(n=>n.Name.ToString().Contains("Glasses")||n.Name.ToString().Contains("Eyepatch")).All(n=>!n.IsVisibleInTree()),"no eyewear");
            if(id.Species=="human")
            {
                var expected=roster.Presets[id.Variant].Modules.Order().ToArray();
                var actual=rig.Model.FindChildren("Outfit_*",nameof(Node3D),true,false).OfType<Node3D>()
                    .Where(n=>n is not MeshInstance3D && n.IsVisibleInTree()).Select(n=>n.Name.ToString()).Order().ToArray();
                Check(expected.SequenceEqual(actual),"actual outfit modules match infected preset "+id.Variant+": "+string.Join(",",actual)+" expected "+string.Join(",",expected));
            }
            foreach(var direction in new[]{Vector3.Right,Vector3.Left,Vector3.Forward,Vector3.Back})
            {
                rig.ResetPose();rig.Jolt(direction*24);
                for(int i=0;i<90;i++)
                {
                    rig.Animate(1f/60,i<60?1:0,0,i<5?1:0,i<30?direction*4.7f:i<60?-direction*4.7f:Vector3.Zero);
                    Check(head.Transform.Origin.IsFinite()&&head.Transform.Basis.IsFinite(),"head motion finite");
                    if(id.Weapon!="unarmed") Check(rig.GripConnectionError<.002f,"motion keeps grip attached "+id.Weapon);
                }
            }
            rig.ResetPose();Check(head.Transform.IsEqualApprox(home),"head reset restores pivot");
            var status=StatusVisualController.Attach(rig);status.SetStates(new[]{new StatusVisualState("burn",2)});status._Process(0);
            rig.ShowAs(VisionSystem.Visibility.Hidden);status._Process(0);
            Check(meshes.All(m=>!m.IsVisibleInTree())&&!status.Displayed,"hidden model does not leak status");
        }
        finally { rig.Free(); }
    }
    private Vector3 Point(float x,float z=-2) => new(x,_stage!.Tiers.GroundY(new(x,0,z))+.05f,z);
    private async Task InfectedStage()
    {
        _stage=new Stage{MapOverride=new LayoutMap(),CharacterOverride="goose",ThemeOverride="animals",DirectorEnabled=false,
            ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
        AddChild(_stage);await Frames(5);
        var player=_stage.Player;player.ManualControl=true;player.TeleportTo(Point(16));
        player.SetControls(Vector2.Zero,Point(11)+Vector3.Up,false);
        var enemy=_stage.SpawnCommonIdentity(new("zombie","human","green","unarmed",0),Point(11),true);
        float distance=enemy.GlobalPosition.DistanceTo(player.GlobalPosition);
        await Frames(36);
        Check(enemy.GlobalPosition.DistanceTo(player.GlobalPosition)<distance-1,"infected approaches in actual Stage2 physics");
        await Shot("zombie-walk");
        int hp=player.Health;
        for(int i=0;i<240&&player.Health==hp;i++)await Frames(1);
        Check(player.Health<hp,"infected actual melee damages survivor");await Shot("zombie-attack");
        enemy.QueueFree();await Frames(3);player.Invulnerable=true;
        // Inspect all outfits in real map lighting; paused AI and camera are explicit inspection controls.
        _stage.Vision.SetPhysicsProcess(false);var camera=_stage.Cam;camera.SetProcess(false);
        for(int variant=0;variant<10;variant++)
        {
            var actor=_stage.SpawnCommonIdentity(new("zombie","human","ochre","unarmed",variant),Point(11),false);
            actor.SetPhysicsProcess(false);actor.Rig.ShowAs(VisionSystem.Visibility.Visible);
            var focus=actor.GlobalPosition+Vector3.Up;
            camera.GlobalPosition=focus+new Vector3(2.5f,2.3f,4);camera.LookAt(focus);
            actor.Rig.Face(camera.GlobalPosition-actor.GlobalPosition);await Frames(2);await Shot("zombie-outfit-"+variant);
            actor.QueueFree();await Frames(2);
        }
    }
}
