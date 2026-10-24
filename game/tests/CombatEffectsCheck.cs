using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
namespace HordeShooter.Tests;
public partial class CombatEffectsCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok,string label) { if(!ok)throw new Exception(label); _checks++; GD.Print("PASS "+label); }
    private async Task Frames(int n) { for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private IEnumerable<T> Nodes<T>(Node parent) where T:Node
    {foreach(var child in parent.GetChildren()){if(child is T t)yield return t;foreach(var nested in Nodes<T>(child))yield return nested;}}
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-16,-16,32,32);
        protected override void BuildMap(){PlayerStart=new(0,0,5);Floor(-16,16,-16,16,"656e48");Wall(10,10.4f,-5,5,3,"78806d");}
    }
    public override async void _Ready()
    {
        int exit=0;
        try{Data();await Run();GD.Print("COMBAT_EFFECTS_PASS "+_checks);}
        catch(Exception e){GD.PushError(e.ToString());GD.Print("COMBAT_EFFECTS_FAIL "+e.Message);exit=1;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.QueueFree();await Frames(3);
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Data()
    {
        var catalog=CombatEffectCatalog.Current;
        Check(catalog.All.Count==11,"eleven shared recipes");
        foreach(var source in new[]{"blast","weapon","grenade","barrel","rocket","mortar","lure","fire_impact","fire_pool","poison_flight","poison_impact","poison_pool"})
            Check(catalog.ForSource(source).Sources.Contains(source),"registered actual source "+source);
        bool rejected=false;try{catalog.ForSource("unknown");}catch(ArgumentException){rejected=true;}
        Check(rejected,"unknown source cannot silently substitute another effect");
        string json=FileAccess.GetFileAsString(CombatEffectCatalog.DataPath);
        foreach(var field in new[]{"duration_s","particles","lobes","spread","rise","light_energy","preview_radius_m","preview_lifetime_s"})
        {
            var doc=JsonNode.Parse(json)!;
            var target=field=="preview_lifetime_s" ? doc["effects"]!.AsArray().First(e=>e!["id"]!.GetValue<string>()=="fire_pool") : doc["effects"]![0];
            target![field]=-1;
            rejected=false;try{CombatEffectCatalog.Parse(doc.ToJsonString());}catch(FormatException){rejected=true;}
            Check(rejected,"reject negative "+field);
        }
        foreach(var change in new[]{"duplicate_id","duplicate_source","color","kind","schema","irrelevant_field","missing_field"})
        {
            var doc=JsonNode.Parse(json)!;
            if(change=="duplicate_id")doc["effects"]![1]!["id"]="blast";
            if(change=="duplicate_source")doc["effects"]![1]!["sources"]![0]="blast";
            if(change=="color")doc["effects"]![0]!["color"]="bad-color";
            if(change=="kind")doc["effects"]![0]!["kind"]="unknown";
            if(change=="schema")doc["schema_version"]=2;
            if(change=="irrelevant_field")doc["effects"]![0]!["preview_lifetime_s"]=1;
            if(change=="missing_field")doc["effects"]![0]!.AsObject().Remove("duration_s");
            rejected=false;try{CombatEffectCatalog.Parse(doc.ToJsonString());}catch(FormatException){rejected=true;}
            Check(rejected,"reject "+change);
        }
        var edited=JsonNode.Parse(json)!;edited["effects"]![1]!["particles"]=7;
        Check(CombatEffectCatalog.Parse(edited.ToJsonString()).ForSource("grenade").Particles==7,"changed shared data actually changes recipe");
    }
    private bool Burst(string id)=>Nodes<CombatBurstEffect>(_stage).Any(b=>b.EffectId==id&&!b.IsQueuedForDeletion());
    private async Task Until(Func<bool> done,string label,int maxFrames=360)
    {for(int i=0;i<maxFrames&&!done();i++)await Frames(1);Check(done(),label);}
    private async Task Shot(string name)
    {
        if(!Windowed)return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/vfx-"+name+".png"))==Error.Ok,"render "+name);
    }
    private async Task Run()
    {
        _stage=new Stage{MapOverride=new TestMap(),CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false,
            ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
        AddChild(_stage);await Frames(5);
        var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;
        _stage.Cam.SetProcess(false);_stage.Cam.SetPhysicsProcess(false);
        var camera=new Camera3D{Position=new(10,15,20),Projection=Camera3D.ProjectionType.Orthogonal,Size=19};
        AddChild(camera);camera.LookAt(Vector3.Zero);camera.MakeCurrent();
        var catalog=CombatEffectCatalog.Current;
        // Analytic time and cleanup are the same in Studio and in the running game.
        foreach(var def in catalog.All.Values.Where(d=>d.Kind=="burst"))
        {
            var effect=CombatBurstEffect.Play(_stage.Effects,def,new(-3,0,0),def.PreviewRadius,Vector3.Right);
            effect.SetProcess(false);effect._Process(.12);
            var volume=Nodes<MeshInstance3D>(effect).First(m=>m.Visible&&m.MaterialOverride is ShaderMaterial sm&&sm.Shader.ResourcePath.EndsWith("combat_volume.gdshader"));
            float alpha=(float)volume.GetInstanceShaderParameter("visibility_alpha").AsDouble();
            Check(alpha>0&&alpha<1,"custom volume shader receives animated opacity "+def.Id);
            Check(effect.VisualParts>=def.Lobes+def.Particles,"actual layered recipe "+def.Id);
            Check(!Nodes<CollisionObject3D>(effect).Any(),"visual has no collision "+def.Id);
            await Shot(def.Id);
            effect._Process(def.Duration+.1);await Frames(2);
            Check(!IsInstanceValid(effect),"burst expires "+def.Id);
        }
        for(int i=0;i<40;i++)CombatBurstEffect.Play(_stage.Effects,catalog.ForSource("barrel"),new(-5,0,0),3);
        Check(Nodes<CombatBurstEffect>(_stage).Count(n=>!n.IsQueuedForDeletion())<=CombatBurstEffect.MaxBursts,"transient burst budget");
        Check(GetTree().GetNodesInGroup("combat_visual_light").Count<=CombatBurstEffect.MaxLights,"transient light budget");
        await Frames(100);
        Check(!Nodes<CombatBurstEffect>(_stage).Any(),"overlap leaves no burst nodes");
        var barrel=ExplosiveBarrel.Create(_stage,new(-4,0,0));barrel.Explode();
        Check(Burst("barrel"),"actual barrel selects fuel explosion");await Frames(100);
        ThrownItem.Throw(_stage,ArmoryCatalog.Throwable("frag"),new(-4,1,0),new(-4,0,-3),p);
        await Until(()=>Burst("grenade"),"actual thrown grenade selects fragment explosion");await Frames(65);
        Rocket.Launch(_stage,new(4,1,0),Vector3.Right,ArmoryCatalog.Weapon("alabong"),p.GetRid(),p);
        await Until(()=>Burst("rocket"),"actual rocket collision selects directed explosion");await Frames(70);
        MortarShell.Fire(_stage,new(0,1,0),new(-3,0,-3),ArmoryCatalog.Weapon("mortar"),p);
        await Until(()=>Burst("mortar"),"actual mortar landing selects upward explosion");await Frames(80);
        var burn=new BurnPoolEffect("test",3,1.1f,45,9);
        var fire=FireZone.Create(_stage,new(-4,0,-2),burn,p);
        var surface=Nodes<CombatSurfaceEffect>(fire).Single();
        Check(surface.EffectId=="fire_pool"&&surface.Radius==burn.RadiusM&&surface.Lifetime==burn.Duration,"fire visual gets actual gameplay radius and duration");
        Check(Burst("fire_impact"),"fire creates impact spread");await Frames(15);await Shot("fire-live");await Frames(60);
        Check(!IsInstanceValid(fire)&&!IsInstanceValid(surface),"fire hazard and visual end together");
        var spit=new EnemyProjectile();spit.Setup(_stage,new(-3,1,0),new(2,1,0),new RangedTrait(15,.5f,2,5,1.2f,1.5f),8);_stage.AddChild(spit);
        Check(Nodes<CombatProjectileEffect>(spit).Single().EffectId=="poison_flight","actual enemy projectile has liquid trail");
        await Frames(15);await Shot("poison-flight-live");
        await Until(()=>Burst("poison_impact"),"actual poison impact spawns splash");
        var hazard=Nodes<VomitHazard>(_stage).Single();var poison=Nodes<CombatSurfaceEffect>(hazard).Single();
        Check(poison.EffectId=="poison_pool"&&poison.Radius==1.5f&&poison.Lifetime==1.2f,"poison visual gets actual hazard contract");
        await Shot("poison-impact-live");await Frames(80);
        Check(!IsInstanceValid(hazard)&&!IsInstanceValid(poison),"poison hazard and visual removed together");
        // Real movement and fire while hazards and bursts overlap; damage balance is checked by Integration/Destruction.
        for(int i=0;i<12;i++)_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,new((i%4-1.5f)*1.2f,0,-5-i/4),awake:true);
        p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);int shots=p.ShotsFired;
        FireZone.Create(_stage,new(-3,0,-3),new BurnPoolEffect("overlap",2.5f,3,45,9),p);
        var pool=new VomitHazard();pool.Setup(_stage,new(3,.06f,-3),3,2,3);_stage.AddChild(pool);
        Vector3 before=p.GlobalPosition;
        for(int frame=0;frame<90;frame++)
        {
            p.SetControls(new Vector2(.35f,0),new(0,1,-6),true);
            if(frame%18==0)Blast.Explode(_stage,new(-2+frame/30f,0,-5),2,10,Colors.Orange,by:p,effectSource:"grenade");
            await Frames(1);if(frame==30)await Shot("overlap-combat");
        }
        p.SetControls(Vector2.Zero,Vector3.Zero,false);
        Check(p.ShotsFired>shots&&p.GlobalPosition.DistanceTo(before)>.3f,"real player moves and fires during overlapping effects");
        await Frames(150);Check(!Nodes<CombatSurfaceEffect>(_stage).Any()&&!Nodes<CombatBurstEffect>(_stage).Any(),"all finite visuals retire after combat");
    }
}
