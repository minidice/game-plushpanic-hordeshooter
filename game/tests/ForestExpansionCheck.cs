using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class ForestExpansionCheck : Node
{
    private Stage _stage=null!;
    private int _checks;
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private void Check(bool ok,string message){if(!ok)throw new Exception(message);_checks++;GD.Print("PASS: "+message);}
    private async Task Shot(string label)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/forest-expansion-{label}.png"))==Error.Ok,"render "+label);
    }
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            await Run();GD.Print("FOREST_EXPANSION_PASS "+_checks);
        }
        catch(Exception e){GD.PushError(e.ToString());GD.Print("FOREST_EXPANSION_FAIL "+e.Message);failed=true;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.Free();QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
    private async Task Run()
    {
        var map=new LayoutMap();_stage=new Stage {MapOverride=map,DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};AddChild(_stage);await Frames(5);
        var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;
        foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();
        Check(_stage.Director.EncounterErrors.Count==0,"all authored enemy entry points and special positions remain open: "+string.Join("; ",_stage.Director.EncounterErrors));
        Check(map.ForestTreeCount>=40 && map.ForestTreeCount<200,$"forest-wide tree budget {map.ForestTreeCount}");
        Check(map.ForestGrassCount>5000 && map.ForestGrassCount<30000,$"forest-wide grass budget {map.ForestGrassCount}");
        string Asset(Node3D holder)=>holder.GetChildren().OfType<Node3D>().First(n=>n.SceneFilePath.EndsWith(".glb")).SceneFilePath;
        var trees=map.KitProps.Where(t=>Asset(t).Contains("/forest_")).ToArray();
        foreach(string id in new[]{"forest_broadleaf","forest_oval","forest_fan"})
        {
            var info=PropLibrary.Find(id);
            Check(info!=null && trees.Any(t=>Asset(t).EndsWith("/"+id+".glb")),"revised tree shape is loaded and planted: "+id);
            Check(info!.Collision.Kind=="cylinder" && info.Collision.Radius==.25f && info.Collision.Height==1.4f
                && info.Dimensions.X<=4 && info.Dimensions.Z<=4 && info.Dimensions.Y<=4.6f,"tree shape preserves physical and canopy budget: "+id);
            var model=PropLibrary.Instance(id)!;
            Check(model.FindChildren("Keep","",true,false).Count==1 && model.FindChildren("Occlude_Canopy","",true,false).Count==1
                && model.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
                    .All(m=>m.Mesh.SurfaceGetMaterial(0) is StandardMaterial3D mat && mat.VertexColorUseAsAlbedo),"tree shape preserves occlusion roles and imported vertex colours: "+id);
            model.Free();
        }
        foreach(var (name,x0,x1) in new[]{("low",40f,70f),("mid",70f,115f),("high",115f,165f),("west",165f,185f),("east",215f,255f)})
            Check(trees.Count(t=>t.Position.X>=x0&&t.Position.X<x1)>=2,$"new tree style covers {name}");
        float PathDistance(Vector2 point)=>map.Layout.Routes.Values.SelectMany(route=>route.Zip(route.Skip(1))).Min(pair=>point.DistanceTo(Geometry2D.GetClosestPointToSegment(point,pair.First,pair.Second)));
        Check(trees.All(t=>t is StaticBody3D&&_stage.Flow.IsBlocked(t.GlobalPosition)&&PathDistance(new Vector2(t.Position.X,t.Position.Z))>=4.8f),"every new trunk has matching blockers and stays away from routes");
        Check(trees.All(t=>!map.Layout.Links.Any(l=>new Vector2(t.Position.X,t.Position.Z).DistanceTo(Geometry2D.GetClosestPointToSegment(new(t.Position.X,t.Position.Z),l.From,l.To))<5)),"all movement links remain clear of new trees");
        var root=_stage.GetNode<Node3D>("ForestQualityStudy");var batches=root.GetChildren().OfType<MultiMeshInstance3D>().ToArray();
        Check(batches.Length<500 && batches.Sum(b=>b.Multimesh.InstanceCount)==map.ForestGrassCount,"grass uses bounded spatial batches");
        Check(root.FindChildren("*",nameof(CollisionObject3D),true,false).Count==0,"grass adds no physical blockers");
        if(DisplayServer.GetName()!="headless")
        {
            bool valid=true,low=false,high=false,east=false,yard=false;
            bool noRoofGrass=true;
            foreach(var b in batches)for(int i=0;i<b.Multimesh.InstanceCount;i++)
            {
                var t=b.Multimesh.GetInstanceTransform(i);var at=t.Origin;var point=new Vector2(at.X,at.Z);
                valid&=PathDistance(point)>=2.3f && LayoutMap.ForestWeight(point,true)>0 && map.Layout.TierAt(point)>=0
                    && Mathf.Abs(at.Y-_stage.Tiers.GroundY(at)-.015f)<.02f && !(at.X>=185&&at.X<215)
                    && !new Rect2(224,-59,16,14).HasPoint(point) && !(at.X>=113&&at.X<=127&&at.Z>=-80&&at.Z<=-70)
                    && b.Multimesh.Mesh.GetAabb().Size.Y*t.Basis.Scale.Y<.6f;
                low|=at.X<70;high|=at.X>120&&at.X<160;east|=at.X>215&&at.X<250;yard|=at.X>260;
                noRoofGrass &= !map.Layout.Regions.Where(r=>r.Id.StartsWith("stack_")).Any(r=>Geometry2D.IsPointInPolygon(point,r.Poly));
            }
            Check(noRoofGrass,"container roofs remain free of ground vegetation");
            Check(valid,"every grass tuft: clear route, correct floor, below knee height, outside canyon/buildings");
            Check(low&&high&&east&&yard,"grass reaches low/high forest, east rim and yard transition");
        }
        GD.Print($"FOREST_EXPANSION_MEASURE trees={trees.Length}; tufts={map.ForestGrassCount}; batches={batches.Length}");
        foreach(var group in trees.GroupBy(t=>System.IO.Path.GetFileNameWithoutExtension(Asset(t))))
            GD.Print($"TREE_SHAPE_COUNT {group.Key}={group.Count()}");
        foreach(string id in new[]{"forest_broadleaf","forest_oval","forest_fan"})
        {
            var tree=trees.First(t=>Asset(t).EndsWith("/"+id+".glb"));var at=tree.GlobalPosition;
            p.TeleportTo(at+new Vector3(3,0,-3));p.SetControls(Vector2.Zero,at,false);
            _stage.Cam.SnapTo(at+Vector3.Up);
            var close=new Camera3D{Position=at+new Vector3(6,7,8),Fov=40};AddChild(close);close.LookAt(at+Vector3.Up*2);close.Current=true;
            await Frames(40);await Shot(id+"-detail");
            p.TeleportTo(at+new Vector3(0,0,-2.5f));close.Position=at+new Vector3(0,4,5);close.LookAt(p.GlobalPosition+Vector3.Up);
            await Frames(40);
            var canopy=tree.FindChildren("Occlude_*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
            var trunk=tree.FindChildren("Keep*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
            Check(canopy.Length>0 && canopy.All(m=>m.Transparency>.7f) && trunk.Length>0 && trunk.All(m=>m.Transparency==0),"only canopy fades for "+id);
            await Shot(id+"-fade");
            p.TeleportTo(at+new Vector3(5,0,3));await Frames(40);
            Check(canopy.All(m=>m.Transparency<.05f),"canopy restores for "+id);close.Free();_stage.Cam.Current=true;
        }
        Check(LayoutMap.ForestWeight(new(94,-24),true)==1 && LayoutMap.ForestWeight(new(146,-38),true)==1
            && LayoutMap.ForestWeight(new(200,-30),true)==0 && LayoutMap.ForestWeight(new(300,-40),true)==0,"forest and exposed canyon/yard material weights");
        foreach(float x in new[]{32f,166,192,208,224,248,282})
            Check(Mathf.Abs(LayoutMap.ForestWeight(new(x-.05f,0),true)-LayoutMap.ForestWeight(new(x+.05f,0),true))<.015f,$"smooth biome boundary at x={x}");
        foreach(var (name,at) in new (string,Vector3)[]{("low",new(54,.05f,-6)),("mid",new(94,1.25f,-24)),("high",new(146,2.45f,-38)),("west",new(176,2.45f,-43)),("canyon",new(200,.05f,-30)),("east",new(242,2.45f,-51)),("yard",new(272,1.25f,-47))})
        {p.TeleportTo(at);p.SetControls(Vector2.Zero,at+Vector3.Right*5,false);_stage.Cam.SnapTo(at+Vector3.Up);await Frames(45);await Shot(name);}
        p.TeleportTo(new(300,1.25f,-40));await Frames(50);
        Check(root.GetNode<ForestStudyLight>("ForestStudyLight").Weight==0 && _stage.GetChildren().OfType<DirectionalLight3D>().Single().RotationDegrees.DistanceTo(new(-36,-48,0))<.01f,"yard restores original lighting");
        p.TeleportTo(new(279,3.65f,-78));p.SetControls(Vector2.Zero,p.Position+Vector3.Right*5,false);_stage.Cam.SnapTo(p.Position+Vector3.Up);await Frames(45);await Shot("container-roof");
        p.TeleportTo(new(54,.05f,-6));await Walk(new(62,0,-7),true);await Shot("low-walk-roll");
        p.TeleportTo(new(244,2.45f,-50));foreach(var point in new[]{new Vector3(250,2.4f,-48),new(252,2.4f,-48),new(262,1.2f,-46),new(273,1.2f,-47)})await Walk(point,false);
        await Shot("yard-ramp-walk");
        p.TeleportTo(new(146,2.45f,-38));p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);_stage.Cam.SnapTo(p.Position+Vector3.Up);await Frames(45);
        int before=_stage.Kills;
        for(int i=0;i<6;i++)_stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,new Vector3(149+i%3*1.2f,2.4f,-39+i/3*1.2f),awake:true);
        await Frames(10);await Shot("high-enemies");
        for(int i=0;i<1800 && _stage.Kills-before<6;i++)
        {
            var enemy=_stage.AllEnemies.Where(e=>!e.IsDead).OrderBy(e=>e.Position.DistanceSquaredTo(p.Position)).FirstOrDefault();
            if(enemy==null)break;if(p.Gun.Mag==0)p.PressReload();p.SetControls(Vector2.Zero,enemy.Position+Vector3.Up*Player.AimHeight,true);await Frames(1);
        }
        p.SetControls(Vector2.Zero,p.Position+Vector3.Right*5,false);Check(_stage.Kills-before==6,"actual gunfire clears six moving enemies in high forest");await Shot("high-combat-cleared");
        var outside=map.KitProps.Where(t=>t.Position.X<32||t.Position.X>=282).Select(t=>$"{Asset(t)}:{t.Position}:{t.Rotation}").OrderBy(s=>s).ToArray();
        int floors=map.FloorRects,voids=map.VoidRects;_stage.Free();await Frames(2);
        var baseline=new LayoutMap{ForestExpanded=false};_stage=new Stage{MapOverride=baseline,DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};AddChild(_stage);await Frames(3);
        var baselineOutside=baseline.KitProps.Where(t=>t.Position.X<32||t.Position.X>=282).Select(t=>$"{Asset(t)}:{t.Position}:{t.Rotation}").OrderBy(s=>s).ToArray();
        Check(floors==baseline.FloorRects && voids==baseline.VoidRects && outside.SequenceEqual(baselineOutside),"terrain and props beyond expanded forest retain original placement");
        Check(PropLibrary.Errors.Count==0,"all reused Blender models load");
    }
    private async Task Walk(Vector3 goal,bool roll)
    {
        var p=_stage.Player;int n=0;
        while(((goal-p.Position) with {Y=0}).Length()>.2f && n++<600)
        {var d=((goal-p.Position) with {Y=0}).Normalized();p.SetControls(new(d.X,d.Z),goal+Vector3.Up,false);if(roll&&n==10)p.PressRoll();await Frames(1);}
        p.SetControls(Vector2.Zero,goal+Vector3.Right*5,false);
        // Let gravity settle the character after descending off the ramp's end.
        await Frames(15);
        Check(((goal-p.Position) with {Y=0}).Length()<=.2f && Mathf.Abs(p.Position.Y-goal.Y)<.15f,$"actual walk/roll reaches {goal}; actual={p.Position}");
    }
}
