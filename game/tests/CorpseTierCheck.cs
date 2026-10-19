using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class CorpseTierCheck : Node3D
{
    private int _checks;
    private void Check(bool value,string message){if(!value)throw new Exception(message);_checks++;GD.Print("PASS: "+message);}
    public override async void _Ready()
    {
        try
        {
            var settings=CorpseRetentionSettings.Load();
            Check(settings.Errors.Count==0&&settings.NamedLifetime==300&&settings.CommonTargetCount==100,"production JSON sets 300 seconds and common target 100");
            var custom=CorpseRetentionSettings.FromText("{\"cleanup_interval_seconds\":0.25,\"named\":{\"lifetime_seconds\":2},\"common\":{\"target_count\":1,\"keep_distance_m\":8,\"minimum_age_seconds\":0,\"unseen_grace_seconds\":0,\"removals_per_pass\":2}}");
            Check(custom.Errors.Count==0&&custom.NamedLifetime==2&&custom.CommonTargetCount==1&&custom.KeepDistance==8&&custom.RemovalsPerPass==2,"JSON custom values are consumed");
            foreach(var malformed in new[]{"{", "[]", "{\"named\":false}", "{\"named\":{\"lifetime_seconds\":-1}}", "{\"named\":{\"lifetime_seconds\":\"300\"}}", "{\"named\":{\"lifetime_seconds\":1e999}}"})
            {
                var bad=CorpseRetentionSettings.FromText(malformed);
                Check(bad.Errors.Count>0&&bad.NamedLifetime==300,"invalid JSON/field keeps safe named lifetime with diagnostic");
            }
            var invalid=CorpseRetentionSettings.FromText("{\"cleanup_interval_seconds\":0,\"common\":{\"target_count\":1.5,\"removals_per_pass\":0},\"named\":{\"lifetim_seconds\":12}}");
            Check(invalid.Errors.Count==4&&invalid.CommonTargetCount==100&&invalid.CleanupInterval==1&&invalid.RemovalsPerPass==8,"unknown and invalid cleanup fields are diagnosed and defaulted");
            Check(CorpseRetentionSettings.FromText("{}").NamedLifetime==300,"partial configuration keeps defaults");
            Check(MonsterDatabase.Current.Errors.Count==0,"monster data accepts explicit tiers");
            foreach(var id in new[]{"bull","balloon","tether","leaper","spitter","armed_veteran"})
                Check(MonsterDatabase.Get(id).Tier==MonsterTier.Named,id+" uses named tier from CSV");
            foreach(var id in new[]{"humanoid","crawler","wild_animal","armed_animal","plush"})
                Check(MonsterDatabase.Get(id).Tier==MonsterTier.Common,id+" stays common");
            var legacy="id,name,behavior,look\nlegacy,name_monster_plush,common,plush\n";
            Check(MonsterDatabase.FromText(legacy,new System.Collections.Generic.Dictionary<string,string>()).Errors.Count==0,"legacy CSV without tier still loads as common");
            var badTier=MonsterDatabase.FromText("id,name,behavior,look,tier\nbad,name_monster_plush,common,plush,99\n",new System.Collections.Generic.Dictionary<string,string>());
            Check(badTier.Errors.Any(e=>e.Contains("tier"))&&badTier.Monsters["bad"].Tier==MonsterTier.Common,"invalid numeric tier cannot silently create an undefined grade");
            var stage=new Stage{CharacterOverride="test_capsule",DirectorEnabled=false};AddChild(stage);
            stage.Player.ManualControl=true;stage.Player.Invulnerable=true;stage.Player.SetPhysicsProcess(false);stage.Cam.SetProcess(false);stage.Effects.SetProcess(false);
            foreach(var e in stage.AllEnemies.ToArray())e.QueueFree();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            stage.Spawn(MonsterDatabase.Get("humanoid"),stage.Player.Position+Vector3.Right,true).Die(Vector3.Zero,0);
            foreach(var id in new[]{"bull","balloon","tether","leaper","spitter","armed_veteran"})
                stage.Spawn(MonsterDatabase.Get(id),new Vector3(60,0,0),true).Die(Vector3.Zero,0);
            Check(stage.Effects.CommonCorpseCount==1&&stage.Effects.NamedCorpseCount==6,"real death paths including balloon create separate corpse pools");
            Check(stage.Effects.NamedCorpses.Select(r=>r.CorpseId).Distinct().Count()==6,"each named death receives a unique stage-local ID");
            var record=stage.Effects.NamedCorpses.First(r=>r.MonsterId=="bull");
            Check(record.ExpiresAt-record.CreatedAt==300,"named deadline is captured at death");
            var anchor=record.GlobalPosition;record.Visual!.GlobalPosition+=Vector3.Right*8;
            Check(record.GlobalPosition==anchor&&record.GetChildren().OfType<CollisionObject3D>().All(c=>c.CollisionMask==Corpse.SceneryCollisionMask),"death anchor stays fixed while cosmetic body moves and cannot affect gameplay");
            record.Visual.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(record.Visual==null&&!record.IsQueuedForDeletion(),"named metadata survives removal of its cosmetic body");
            stage.Effects.CorpseTargetCount=1;stage.Player.Position=new Vector3(120,0,0);stage.Cam.SnapTo(stage.Player.Position);
            stage.Effects.AdvanceCorpseCleanup(11);
            Check(stage.Effects.CommonCorpseCount==1&&stage.Effects.NamedCorpseCount==6,"named records do not consume the common retention budget");
            stage.Effects.CorpseTargetCount=0;stage.Effects.AdvanceCorpseCleanup(1);
            Check(stage.Effects.CommonCorpseCount==0&&stage.Effects.NamedCorpseCount==6,$"common cleanup never evicts a named corpse (common={stage.Effects.CommonCorpseCount}, named={stage.Effects.NamedCorpseCount})");
            stage.Effects.AdvanceCorpseCleanup(287);
            Check(stage.Effects.NamedCorpseCount==6,"named corpses survive until 299 seconds without observers nearby");
            stage.Cam.SnapTo(anchor);stage.Player.Position=anchor;
            stage.Effects.AdvanceCorpseCleanup(1);
            Check(stage.Effects.NamedCorpseCount==0&&record.IsQueuedForDeletion(),"named corpse expires at five minutes even when visible");
            stage.Effects.RetentionSettings=custom;
            stage.Spawn(MonsterDatabase.Get("bull"),anchor,true).Die(Vector3.Zero,0);
            stage.Effects.AdvanceCorpseCleanup(1.75);
            Check(stage.Effects.NamedCorpseCount==1,"custom lifetime does not expire early");
            stage.Effects.AdvanceCorpseCleanup(.25);
            Check(stage.Effects.NamedCorpseCount==0,"custom JSON lifetime and scan interval control actual expiry");
            var probeRig=CharacterRig.Create(CharacterCatalog.Player("test_capsule"));AddChild(probeRig);probeRig.Position=new Vector3(250,4,0);
            var probe=(Corpse)stage.Effects.SpawnCorpse(probeRig,.3f,1,Vector3.Right*4,Vector3.Zero,new MonsterDefinition{Id="named_common_behavior",Tier=MonsterTier.Named});
            probe.GravityScale=0;var start=probe.GlobalPosition;var fixedAnchor=probe.NamedRecord!.GlobalPosition;
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(probe.GlobalPosition.X>start.X+.05f&&probe.NamedRecord.GlobalPosition==fixedAnchor,"named visual keeps its impulse after reparenting while death anchor stays fixed");
            stage.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("CORPSE_TIER_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
