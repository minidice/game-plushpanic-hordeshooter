using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
namespace HordeShooter.Tests;
public partial class DestructionCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-9,-11,35,22);
        protected override void BuildMap()
        {
            PlayerStart=new(0,0,-5); Floor(-8,25,-10,10,"6b735c"); BreachWall(Vector3.Zero);
            Solid(new(6,1.3f,0),new(4,2.6f,.45f),"71816c"); Prop("sandbags",new(18,0,0));
        }
    }
    private Stage _stage=null!; private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    public override async void _Ready()
    {
        int exit=0;
        try{Data();await Run();GD.Print("DESTRUCTION_CHECK_PASS "+_checks);}
        catch(Exception e){GD.PushError(e.ToString());GD.Print("DESTRUCTION_CHECK_FAIL "+e.Message);exit=1;}
        if(_stage!=null&&IsInstanceValid(_stage)){_stage.QueueFree();await Frames(3);}
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Data()
    {
        var data=ObjectInteractions.Current;
        Check(data.ForId(ExplosiveBarrel.ObjectId) is {Destruction.BreakableBy:BreakableBy.Gun,Destruction.MaxHealth:10,Explosion.Radius:4.6f,Explosion.Damage:125,Explosion.Impulse:140,Explosion.DelaySeconds:.12f},"actual barrel destruction and explosion data");
        Check(data.ForId("runtime:breach_wall") is {Destruction.BreakableBy:BreakableBy.Explosive,Explosion:null},"wall requires explosives but does not itself explode");
        Check(data.ForProp(PropLibrary.Find("sandbags")!) is {Destruction.MaxHealth:50,Explosion:null},"existing sandbag health and no self-explosion");
        string json=FileAccess.GetFileAsString(ObjectInteractions.DataPath);
        foreach(var key in new[]{"radius_m","damage","impulse","delay_s"})
        {
            var node=JsonNode.Parse(json)!;node["profiles"]!["explosive_barrel"]!["explosion"]![key]=-1;
            bool rejected=false;try{ObjectInteractions.Parse(node.ToJsonString());}catch(FormatException){rejected=true;}
            Check(rejected,"reject negative explosion "+key);
        }
        var changed=JsonNode.Parse(json)!;changed["profiles"]!["explosive_barrel"]!["explosion"]!["on_destroy"]=false;
        Check(ObjectInteractions.Parse(changed.ToJsonString()).ForId(ExplosiveBarrel.ObjectId)!.Explosion==null,"self-explosion is independent of breaking");
        changed["profiles"]!["explosive_barrel"]!["destruction"]!["breakable_by"]="explosive";
        Check(!ObjectInteractions.Parse(changed.ToJsonString()).ForId(ExplosiveBarrel.ObjectId)!.Destruction!.Accepts(ObjectDamage.Gun),"barrel profile can require explosives");
    }
    private async Task Shoot(Vector3 aim,int frames)
    {_stage.Player.SetControls(Vector2.Zero,aim,true);await Frames(frames);_stage.Player.SetControls(Vector2.Zero,aim,false);await Frames(2);}
    private Enemy Target(Vector3 at)
    {var e=_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,at,awake:false);e.SetPhysicsProcess(false);e.ScaleHealth(100);return e;}
    private Corpse Body(Vector3 at)
    {
        var rig=CharacterRig.Create(CharacterCatalog.Enemy("humanoid"));_stage.AddChild(rig);rig.GlobalPosition=at;
        var body=(Corpse)_stage.Effects.SpawnCorpse(rig,.4f,1.1f,Vector3.Zero,Vector3.Zero);
        body.GravityScale=0;body.Sleeping=true;return body;
    }
    private async Task ZeroBlast(float damage,float power,int index)
    {
        // Isolate each blast from the map and earlier targets; native rigid-body impulses still run.
        var at=new Vector3(100+index*30,0,100);var player=_stage.Player;
        player.SetPhysicsProcess(false);player.GlobalPosition=at+Vector3.Right*2;player.Velocity=Vector3.Zero;
        var enemy=Target(at+Vector3.Left*2);
        var placed=ObjectBody.Create(_stage,"ZeroBlastCover",ObjectInteractions.Current.ForProp(PropLibrary.Find("sandbags")!)!,null);
        _stage.AddChild(placed);placed.GlobalPosition=at+new Vector3(1,0,1);
        var barrel=ExplosiveBarrel.Create(_stage,at+new Vector3(0,0,3));barrel.Freeze=true;barrel.SetPhysicsProcess(false);
        var prop=new RigidBody3D{Position=at+Vector3.Back*2,GravityScale=0,CollisionLayer=0,CollisionMask=0};
        prop.AddChild(new CollisionShape3D{Shape=new SphereShape3D{Radius=.2f}});_stage.AddChild(prop);_stage.Props.Add(prop);
        var corpse=Body(at+Vector3.Forward*2);await Frames(2);
        prop.Sleeping=true;float enemyHp=enemy.Health,placedHp=placed.Health;
        // SpawnEnemy lifts the body .05 m above the floor; blast falloff uses full 3D distance.
        float enemyFalloff=1-enemy.GlobalPosition.DistanceTo(at)/4.6f;
        Blast.Explode(_stage,at,4.6f,damage,Colors.Orange,knockbackPower:power,by:player);
        if(damage==0)
            Check(enemy.Health==enemyHp&&placed.Health==placedHp&&barrel.Health==10&&!barrel.Ignited,
                $"zero damage with impulse {power} cannot hurt enemies or objects or chain barrels");
        else
        {
            float expected=(damage*enemyFalloff+15)*player.OutgoingDamage*enemy.DamageTakenScale;
            Check(Mathf.Abs(enemyHp-enemy.Health-expected)<.01f&&placed.Health<placedHp&&barrel.Ignited,
                $"positive damage {damage} retains falloff plus 15-point floor with impulse {power}");
        }
        if(power==0)
            Check(enemy.FlinchTime==0&&player.Velocity==Vector3.Zero,"zero impulse does not shove enemies or launch players");
        else
        {
            float edge=Mathf.Min(40,power),shove=edge+(power-edge)*enemyFalloff;
            float flinch=Mathf.Clamp(shove/enemy.Monster.Mass*.004f,.05f,.35f);
            Check(Mathf.IsEqualApprox(enemy.FlinchTime,flinch)&&player.Velocity.Y>0,
                $"positive impulse {power} uses capped edge shove and still launches players (flinch {enemy.FlinchTime:F6}/{flinch:F6}, player Y velocity {player.Velocity.Y:F3})");
        }
        await Frames(3);
        Check(power==0 ? prop.LinearVelocity.Length()<.01f&&corpse.LinearVelocity.Length()<.01f
            : prop.LinearVelocity.Length()>.1f&&corpse.LinearVelocity.Length()>.1f,
            $"impulse {power} controls native prop and corpse motion independently of damage {damage}");
        _stage.Props.Remove(prop);_stage.Props.Remove(barrel);_stage.Breakables.Remove(placed);_stage.Enemies.Remove(enemy);
        prop.QueueFree();barrel.QueueFree();if(IsInstanceValid(placed))placed.QueueFree();corpse.QueueFree();enemy.QueueFree();await Frames(2);
    }
    private async Task Run()
    {
        _stage=new Stage{CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false,MapOverride=new TestMap(),ModeOverride=GameMode.Scenario,SquadOverride=0};
        AddChild(_stage);await Frames(5);
        var player=_stage.Player;player.ManualControl=true;player.Invulnerable=true;
        var wall=_stage.Breakables.Single(b=>b.Interaction.Profile=="breach_wall");
        var enemy=Target(new(0,0,2));await Frames(2);player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
        float hp=wall.Health,enemyHp=enemy.Health;int shots=player.ShotsFired;
        await Shoot(new(0,1,2),36);
        Check(player.ShotsFired>shots&&wall.Health==hp&&enemy.Health==enemyHp,"actual M4 cannot damage wall or enemy behind it");
        Check(_stage.Flow.IsBlocked(Vector3.Zero),"intact wall blocks navigation");
        player.GlobalPosition=new(0,0,-2);player.Velocity=Vector3.Zero;player.SetControls(new(0,1),new(0,1,2),false);await Frames(50);
        Check(player.GlobalPosition.Z<-.6f,"intact wall stops actual walking");
        player.SetControls(Vector2.Zero,new(0,1,0),false);player.GlobalPosition=new(0,0,-5);player.Velocity=Vector3.Zero;
        var barrel=ExplosiveBarrel.Create(_stage,new(0,0,-1.5f));barrel.Freeze=true;await Frames(3);
        await Shoot(barrel.GlobalPosition+Vector3.Up,25);await Frames(60);
        Check(!IsInstanceValid(barrel)&&!IsInstanceValid(wall),"actual gunshot ignites barrel and its explosion breaks wall");
        Check(enemy.Health<enemyHp&&enemy.LastHitBy==player,"barrel blast retains shooter attribution");
        Check(!_stage.Flow.IsBlocked(Vector3.Zero),"destroyed middle releases navigation");
        Check(Rays.Clear(_stage,new(0,1,-1),new(0,1,1),Layers.MovementWorld|Layers.Props),"no hidden wall behind opening");
        Check(Rays.Cast(_stage,new(2.13f,1,-1),new(2.13f,1,1),Layers.MovementWorld)!=null,"side pillar retains collision");
        Check(_stage.GetChildren().OfType<BreakableWallVisual>().Count(v=>!v.IsProcessing())==1,"settled broken model remains");
        player.GlobalPosition=new(0,0,-1.5f);player.Velocity=Vector3.Zero;player.SetControls(new(0,1),new(0,1,3),false);await Frames(35);
        Check(player.GlobalPosition.Z>.5f,"actual player walks through destroyed opening");
        player.SetControls(Vector2.Zero,Vector3.Zero,false);player.GlobalPosition=new(-5,0,-6);
        Blast.Explode(_stage,new(6,0,-1),3,500,Colors.Orange);await Frames(3);
        Check(Rays.Cast(_stage,new(6,1,-1),new(6,1,1),Layers.BulletMask)!=null,"ordinary structure remains unbreakable after huge blast");
        var target=Target(new(11,0,1));await Frames(2);
        var bomb=ObjectBody.Create(_stage,"DataBomb",ExplosiveBarrel.Rule,null);_stage.AddChild(bomb);bomb.Position=new(11,0,0);float before=target.Health;
        bomb.TakeDamage(10,ObjectDamage.Gun,player);bomb.TakeDamage(10,ObjectDamage.Gun,player);
        Check(target.Health==before&&_stage.GetChildren().OfType<ObjectDetonation>().Count()==1,"generic explosion delayed and duplicate hits schedule once");
        await Frames(16);Check(target.Health<before&&target.LastHitBy==player,"generic object uses explosion data and shooter attribution");
        var first=ExplosiveBarrel.Create(_stage,new(19,0,-3));first.Freeze=true;var next=ExplosiveBarrel.Create(_stage,new(21,0,-3));next.Freeze=true;await Frames(2);
        first.TakeDamage(5,ObjectDamage.Gun,player);Check(first.Health==5&&!first.Ignited,"barrel accumulates damage until health depleted");
        first.TakeDamage(5,ObjectDamage.Gun,player);await Frames(40);Check(!IsInstanceValid(first)&&!IsInstanceValid(next),"barrels chain using data without collection mutation");
        await ZeroBlast(0,0,0);await ZeroBlast(0,140,1);await ZeroBlast(1,0,2);
        await ZeroBlast(0,10,3);await ZeroBlast(125,140,4);
        var clientBarrel=ExplosiveBarrel.Create(_stage,new(-4,0,-4));clientBarrel.Freeze=true;var clientTarget=Target(new(-3,0,-4));
        var clientCorpse=Body(new(-4,0,-2));await Frames(2);
        before=clientTarget.Health;player.GlobalPosition=new(-5,0,-4);player.Velocity=Vector3.Zero;_stage.Role=NetRole.Client;
        Blast.Explode(_stage,new(-4,0,-4),4.6f,125,Colors.Orange,clientBarrel,by:player);
        Check(clientTarget.Health==before&&clientTarget.FlinchTime==0&&player.Velocity==Vector3.Zero&&!clientBarrel.Ignited,"client blast is visual only without damage launch or chain");
        clientBarrel.TakeDamage(1000,ObjectDamage.Gun,player);Check(clientBarrel.Health==10&&!clientBarrel.Ignited,"client cannot destroy barrel locally");
        await Frames(3);Check(clientCorpse.LinearVelocity.Z>1&&clientCorpse.LinearVelocity.Y>1,
            "client blast still launches an existing local corpse outward and upward");
        _stage.Role=NetRole.Offline;
    }
}
