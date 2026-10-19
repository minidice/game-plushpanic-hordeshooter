using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class CorpseRetentionCheck : Node3D
{
    private int _checks;
    private void Check(bool value,string message)
    {if(!value)throw new Exception(message);_checks++;GD.Print("PASS: "+message);}
    public override async void _Ready()
    {
        try
        {
            var stage=new Stage{CharacterOverride="test_capsule",DirectorEnabled=false};AddChild(stage);
            stage.Player.ManualControl=true;stage.Player.Invulnerable=true;
            stage.Player.SetPhysicsProcess(false);stage.Cam.SetProcess(false);stage.Effects.SetProcess(false);
            foreach(var enemy in stage.AllEnemies.ToArray())enemy.QueueFree();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var bodies=new List<Corpse>();
            for(int i=0;i<110;i++)
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player("test_capsule"));AddChild(rig);
                rig.Position=stage.Player.Position;
                var body=stage.Effects.SpawnCorpse(rig,.3f,1f,Vector3.Zero,Vector3.Zero);body.Freeze=true;
                bodies.Add((Corpse)body);
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(stage.Effects.CorpseCount==110,"visible corpses survive beyond the 100 target");
            Check(stage.Effects.CorpseTargetCount==100,"default is a tunable 100 corpse soft target");
            stage.Effects.AdvanceCorpseCleanup(12);
            Check(stage.Effects.CorpseCount==110,"age does not remove a visible nearby pile");
            Check(bodies.All(b=>b.CollisionMask==Corpse.SceneryCollisionMask),"corpses collide only with world and cosmetic corpses");
            Check(stage.Props.All(p=>(p.CollisionMask&Layers.Corpse)==0),"all gameplay props exclude corpse collisions on the other side too");
            Check((stage.Player.CollisionMask&Layers.Corpse)==0,"player movement ignores cosmetic corpse count");
            var remote=new Node3D{Position=bodies[1].Position};AddChild(remote);
            stage.Effects.RegisterCorpseObserver(remote);
            stage.Player.Position=new Vector3(100,0,0);stage.Cam.SnapTo(stage.Player.Position);
            stage.Effects.AdvanceCorpseCleanup(20);
            Check(stage.Effects.CorpseCount==110,"a second observer protects corpses far from the local player");
            stage.Effects.UnregisterCorpseObserver(remote);remote.QueueFree();
            stage.Cam.SnapTo(bodies[1].Position);
            stage.Effects.AdvanceCorpseCleanup(6);
            Check(stage.Effects.CorpseCount==110,"camera view protects distant corpses independently of anchor distance");
            stage.Cam.SnapTo(stage.Player.Position);
            bodies[0].Position=stage.Player.Position+Vector3.Up;
            stage.Effects.AdvanceCorpseCleanup(4);
            Check(stage.Effects.CorpseCount==110,"offscreen grace prevents cleanup immediately after camera movement");
            stage.Effects.AdvanceCorpseCleanup(1);
            Check(stage.Effects.CorpseCount==102,"cleanup is bounded to eight old eligible corpses per scan");
            Check(!bodies[0].IsQueuedForDeletion()&&bodies.Skip(1).Take(8).All(b=>b.IsQueuedForDeletion()),"oldest eligible deaths retire first and protected oldest is skipped");
            stage.Effects.AdvanceCorpseCleanup(1);
            Check(stage.Effects.CorpseCount==100&&bodies[9].IsQueuedForDeletion()&&bodies[10].IsQueuedForDeletion(),"cleanup stops at target in death order");
            var youngRig=CharacterRig.Create(CharacterCatalog.Player("test_capsule"));AddChild(youngRig);youngRig.Position=Vector3.Zero;
            var young=stage.Effects.SpawnCorpse(youngRig,.3f,1f,Vector3.Zero,Vector3.Zero);young.Freeze=true;
            stage.Effects.AdvanceCorpseCleanup(1);
            Check(!young.IsQueuedForDeletion(),"newly fallen bodies survive even when far and above target");
            stage.Effects.UnregisterCorpseObserver(stage.Player);
            stage.Effects.CorpseTargetCount=0;stage.Effects.AdvanceCorpseCleanup(30);
            Check(stage.Effects.CorpseCount==100,"missing observers retain corpses conservatively");
            stage.Effects.RegisterCorpseObserver(stage.Player,stage.Cam);
            young.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            stage.Effects.CorpseTargetCount=100;stage.Effects.AdvanceCorpseCleanup(1);
            Check(stage.Effects.CorpseCount==99,"externally freed corpses are pruned without stale handles");
            var prop=stage.Props.First();prop.Position=new Vector3(300,3,0);prop.GravityScale=0;
            prop.LinearVelocity=Vector3.Zero;prop.AngularVelocity=Vector3.Zero;
            bodies[0].Position=prop.Position;
            for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(prop.Position.DistanceTo(new Vector3(300,3,0))<.001f,"overlapping cosmetic corpse does not push a gameplay prop in Jolt");
            stage.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("CORPSE_RETENTION_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
