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
        Check(catalog.All.Count==12,"twelve shared recipes including visual-only smoke");
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
    private async Task ImpactContracts()
    {
        var catalog=CombatEffectCatalog.Current;
        foreach(string id in new[]{"blast","grenade","rocket","mortar","lure"})
        {
            var def=catalog.All[id];
            var whole=CombatBurstEffect.Play(_stage.Effects,def,new(0,2,0),4,Vector3.Right,0);
            var split=CombatBurstEffect.Play(_stage.Effects,def,new(0,2,0),4,Vector3.Right,0);
            whole.SetProcess(false);split.SetProcess(false);
            whole._Process(def.Duration*.2);for(int i=0;i<2;i++)split._Process(def.Duration*.1);
            var a=Nodes<MeshInstance3D>(whole).ToArray();var b=Nodes<MeshInstance3D>(split).ToArray();
            Check(!a.Any(m=>m.Mesh is PlaneMesh and not QuadMesh),"impact has no ground ring "+id);
            Check(a.Zip(b).All(pair=>pair.First.Visible==pair.Second.Visible&&(!pair.First.Visible
                ||pair.First.Position.IsEqualApprox(pair.Second.Position)&&pair.First.Scale.IsEqualApprox(pair.Second.Scale)
                &&Mathf.IsEqualApprox(((StandardMaterial3D)pair.First.MaterialOverride).AlbedoColor.A,
                    ((StandardMaterial3D)pair.Second.MaterialOverride).AlbedoColor.A))),"impact timeline independent of step size "+id);
            var positions=a.Select(m=>m.Position).ToArray();await Frames(2);
            Check(a.Select((m,i)=>m.Position==positions[i]).All(s=>s),"impact freezes with parent clock "+id);
            if(id=="lure")
            {
                var tint=((StandardMaterial3D)a.First(m=>m.Visible&&m.Name.ToString().StartsWith("Impact")).MaterialOverride).AlbedoColor;
                Check(tint.R>tint.G&&tint.B>tint.G,"lure retains pink impact color");
            }
            whole._Process(def.Duration*.45);
            Check(a.Any(m=>m.Visible&&m.Name.ToString().StartsWith("Smoke"))
                &&!a.Any(m=>m.Visible&&m.Name.ToString().StartsWith("Impact")),"impact ends before translucent dust "+id);
            var fragments=a.Where(m=>m.Visible&&m.Name.ToString().StartsWith("Fragment")).ToArray();
            Check(fragments.Any(m=>m.Position.Y<0)&&fragments.All(m=>m.Position.Y>=-1.941f),"airburst fragments descend toward supplied floor "+id);
            whole._Process(def.Duration*.33);
            Check(a.All(m=>!m.Visible),"impact layers fade before owner expires "+id);
            whole.QueueFree();split.QueueFree();await Frames(2);
            Check(a.All(m=>!IsInstanceValid(m)),"impact children retire with parent "+id);
            foreach(int lobes in new[]{0,24})
            {
                var tiny=CombatBurstEffect.Play(_stage.Effects,def with {Duration=.1f,Lobes=lobes,Particles=0},Vector3.Zero,.05f);
                tiny.SetProcess(false);tiny._Process(.099);
                Check(Nodes<MeshInstance3D>(tiny).All(m=>!m.Visible),"short impact and zero particles finish "+id+"/"+lobes);
                tiny._Process(.01);await Frames(2);
                Check(!IsInstanceValid(tiny),"short impact cleanup "+id+"/"+lobes);
            }
        }
        var rocket=catalog.ForSource("rocket");
        var right=CombatBurstEffect.Play(_stage.Effects,rocket,Vector3.Zero,3,Vector3.Right);
        var fast=CombatBurstEffect.Play(_stage.Effects,rocket,Vector3.Zero,3,Vector3.Right*30);
        var left=CombatBurstEffect.Play(_stage.Effects,rocket,Vector3.Zero,3,Vector3.Left);
        var zero=CombatBurstEffect.Play(_stage.Effects,rocket,Vector3.Zero,3);
        foreach(var effect in new[]{right,fast,left,zero}){effect.SetProcess(false);effect._Process(.2);}
        MeshInstance3D[] Impacts(CombatBurstEffect effect)=>Nodes<MeshInstance3D>(effect).Where(m=>m.Name.ToString().StartsWith("Impact")).ToArray();
        Check(Impacts(right).Average(m=>m.Position.X)>Impacts(left).Average(m=>m.Position.X)+1,"rocket motion visibly follows supplied direction");
        Check(Impacts(right).Zip(Impacts(fast)).All(p=>p.First.Position.IsEqualApprox(p.Second.Position)),"rocket visual depends on direction not speed");
        Check(Impacts(zero).All(m=>m.Position.IsFinite())&&Mathf.Abs(Impacts(zero).Average(m=>m.Position.X))<.5f,"zero direction remains finite radial burst");
        foreach(var effect in new[]{right,fast,left,zero})effect.QueueFree();await Frames(2);
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
            if(def.Family=="blast")
                Check(Nodes<MeshInstance3D>(effect).Any(m=>m.Visible&&m.Mesh is QuadMesh
                    &&m.MaterialOverride is StandardMaterial3D mat&&mat.AlbedoTexture!=null),"blast family uses textured translucent cards "+def.Id);
            var volume=Nodes<MeshInstance3D>(effect).First(m=>m.Visible&&(def.Family=="blast"
                ? m.MaterialOverride is StandardMaterial3D
                : m.MaterialOverride is ShaderMaterial sm&&sm.Shader.ResourcePath.EndsWith("combat_volume.gdshader")));
            float alpha=volume.MaterialOverride is StandardMaterial3D card ? card.AlbedoColor.A
                : (float)volume.GetInstanceShaderParameter("visibility_alpha").AsDouble();
            Check(alpha>0&&alpha<1,"custom volume shader receives animated opacity "+def.Id);
            Check(effect.VisualParts>=def.Lobes+def.Particles,"actual layered recipe "+def.Id);
            Check(!Nodes<CollisionObject3D>(effect).Any(),"visual has no collision "+def.Id);
            await Shot(def.Id);
            if(def.Id=="barrel")
            {
                Check(!Nodes<MeshInstance3D>(effect).Any(m=>m.Mesh is PlaneMesh and not QuadMesh),"fuel burst has no ground paint");
                effect._Process(.8);
                Check(Nodes<MeshInstance3D>(effect).Any(m=>m.Visible&&m.Name.ToString().StartsWith("Smoke")),"fuel smoke outlives initial fireball");
                Check(!Nodes<MeshInstance3D>(effect).Any(m=>m.Visible&&m.Name.ToString().StartsWith("Fireball")),"fuel fireball retires before smoke");
                await Shot("barrel-tail");
                effect._Process(def.Duration-.92-.02);
                Check(Nodes<MeshInstance3D>(effect).All(m=>!m.Visible),"fuel cards fade before owner cleanup");
            }
            effect._Process(def.Duration+.1);await Frames(2);
            Check(!IsInstanceValid(effect),"burst expires "+def.Id);
        }
        await ImpactContracts();
        // Studio may step in one jump; live playback may take many smaller steps.
        var fuelDef=catalog.ForSource("barrel");
        var a=CombatBurstEffect.Play(_stage.Effects,fuelDef,Vector3.Zero,3);
        var b=CombatBurstEffect.Play(_stage.Effects,fuelDef,Vector3.Zero,3);
        a.SetProcess(false);b.SetProcess(false);a._Process(.4);
        for(int i=0;i<4;i++)b._Process(.1);
        var cardsA=Nodes<MeshInstance3D>(a).ToArray();var cardsB=Nodes<MeshInstance3D>(b).ToArray();
        Check(cardsA.Zip(cardsB).All(pair=>pair.First.Visible==pair.Second.Visible&&(!pair.First.Visible
            ||pair.First.Position.IsEqualApprox(pair.Second.Position)
            &&pair.First.Scale.IsEqualApprox(pair.Second.Scale)
            &&Mathf.IsEqualApprox(((StandardMaterial3D)pair.First.MaterialOverride).AlbedoColor.A,
                ((StandardMaterial3D)pair.Second.MaterialOverride).AlbedoColor.A))),"fuel timeline is independent of step size");
        var frozen=cardsA.Select(c=>c.Position).ToArray();await Frames(3);
        Check(cardsA.Select((c,i)=>c.Position==frozen[i]).All(v=>v),"fuel cards do not advance without parent clock");
        a.QueueFree();b.QueueFree();await Frames(2);
        Check(cardsA.All(c=>!IsInstanceValid(c)),"fuel child cards retire with parent");
        foreach(int lobes in new[]{0,24})
        {
            var shortBurst=CombatBurstEffect.Play(_stage.Effects,fuelDef with {Duration=.1f,Lobes=lobes,Particles=0},Vector3.Zero,.05f);
            shortBurst.SetProcess(false);shortBurst._Process(.099);
            Check(Nodes<MeshInstance3D>(shortBurst).All(m=>!m.Visible),"short fuel lifetime and lobe boundary "+lobes);
            shortBurst._Process(.01);await Frames(2);
            Check(!IsInstanceValid(shortBurst),"short fuel cleanup "+lobes);
        }
        for(int i=0;i<40;i++)CombatBurstEffect.Play(_stage.Effects,catalog.ForSource("barrel"),new(-5,0,0),3);
        Check(Nodes<CombatBurstEffect>(_stage).Count(n=>!n.IsQueuedForDeletion())<=CombatBurstEffect.MaxBursts,"transient burst budget");
        Check(GetTree().GetNodesInGroup("combat_visual_light").Count<=CombatBurstEffect.MaxLights,"transient light budget");
        await Frames((int)Math.Ceiling(catalog.ForSource("barrel").Duration*60)+5);
        Check(!Nodes<CombatBurstEffect>(_stage).Any(),"overlap leaves no burst nodes");
        var barrel=ExplosiveBarrel.Create(_stage,new(-4,0,0));barrel.Explode();
        Check(Burst("barrel"),"actual barrel selects fuel explosion");await Frames(100);
        Blast.Explode(_stage,new(-4,0,0),2,0,Colors.Orange,knockbackPower:0);
        Check(Burst("blast"),"default blast source selects generic impact");
        ThrownItem.Throw(_stage,ArmoryCatalog.Throwable("lure"),new(-4,1,0),new(-4,0,-3),p);
        await Until(()=>Burst("lure"),"actual lure selects pink impact",900);
        await Frames((int)Math.Ceiling(catalog.ForSource("lure").Duration*60)+5);
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
        var fireLight=Nodes<OmniLight3D>(fire).Single();
        Check(Burst("fire_impact"),"fire creates impact spread");await Frames(15);await Shot("fire-live");await Frames(60);
        Check(!IsInstanceValid(fire)&&IsInstanceValid(surface)&&surface.IsResidue,"fire hazard ends before harmless residue");
        Check(!GetTree().GetNodesInGroup(SquadGroups.Hazard).Contains(fire)&&!IsInstanceValid(fireLight)&&!_stage.FlickerLights.Contains(fireLight),"expired fire leaves no hazard or light registration");
        Check(surface.GetParent()==_stage.Effects&&!Nodes<CollisionObject3D>(surface).Any()&&!Nodes<Light3D>(surface).Any()&&!Nodes<AudioStreamPlayer3D>(surface).Any(),"detached residue subtree contains visual-only layers");
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
        await Frames(240);Check(!Nodes<CombatSurfaceEffect>(_stage).Any()&&!Nodes<CombatBurstEffect>(_stage).Any(),"all finite visuals retire after combat");
    }
}
