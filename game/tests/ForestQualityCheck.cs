using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class ForestQualityCheck : Node
{
    private Stage _stage=null!;
    private int _checks;
    private async Task Frames(int count) { for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private void Check(bool ok,string text) { if(!ok)throw new Exception(text);_checks++;GD.Print("PASS: "+text); }
    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/forest-quality-"+name+".png"))==Error.Ok,"render "+name);
    }
    public override async void _Ready()
    {
        bool failed=false;
        try { await Run();GD.Print("FOREST_QUALITY_PASS "+_checks); }
        catch(Exception e) { GD.PushError(e.ToString());GD.Print("FOREST_QUALITY_FAIL "+e.Message);failed=true; }
        if(_stage!=null&&IsInstanceValid(_stage))_stage.Free();QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
    private async Task Run()
    {
        var map=new LayoutMap {ForestExpanded=false};
        _stage=new Stage { MapOverride=map,ModeOverride=GameMode.Scenario,CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false,SquadOverride=0 };
        AddChild(_stage);await Frames(5);
        var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;
        foreach(var e in _stage.AllEnemies.ToList())e.QueueFree();
        p.TeleportTo(new Vector3(94,1.25f,-24));p.SetControls(Vector2.Zero,p.GlobalPosition+Vector3.Right*5,false);
        _stage.Cam.SnapTo(p.GlobalPosition+Vector3.Up);await Frames(45);
        await Shot(PropLibrary.Find("forest_pine")==null?"before-wide":"after-wide");
        _stage.Cam.Close=true;await Frames(45);
        await Shot(PropLibrary.Find("forest_pine")==null?"before-close":"after-close");
        Check(PropLibrary.Find("forest_pine")!=null,"Blender forest study models exist");
        var root=_stage.GetNode<Node3D>("ForestQualityStudy");
        var light=root.GetNode<ForestStudyLight>("ForestStudyLight");
        var sun=_stage.GetChildren().OfType<DirectionalLight3D>().Single();
        var env=_stage.GetChildren().OfType<WorldEnvironment>().Single().Environment;
        Check(map.ForestTreeCount>=4 && map.ForestTreeCount<=6 && map.ForestGrassCount>600 && map.ForestGrassCount<2000,$"study planting stays within budget: {map.ForestTreeCount} trees, {map.ForestGrassCount} tufts");
        Check(light.Weight>.99f && env.AmbientLightEnergy<.4f && sun.LightColor.R>sun.LightColor.B,"warm light and deeper ambient shading at the patch centre");
        string Asset(Node3D holder)=>holder.GetChildren().OfType<Node3D>().First(n=>n.SceneFilePath.EndsWith(".glb")).SceneFilePath;
        var trees=map.KitProps.Where(t=>Asset(t).Contains("/forest_")).ToArray();
        Check(trees.Length==map.ForestTreeCount && trees.All(t=>t is StaticBody3D && _stage.Flow.IsBlocked(t.GlobalPosition)),"all trunks have matching physical and flow blockers");
        foreach(var tree in trees)
            Check(!Rays.Clear(p,tree.GlobalPosition+Vector3.Left*1.2f+Vector3.Up*.5f,tree.GlobalPosition+Vector3.Up*.5f,Layers.World),"tree trunk blocks a real world ray");
        var batches=root.GetChildren().OfType<MultiMeshInstance3D>().ToArray();
        Check(batches.Length<=32 && batches.Sum(b=>b.Multimesh.InstanceCount)==map.ForestGrassCount,"grass is batched in spatial cells");
        bool grassValid=true;
        if(DisplayServer.GetName()!="headless")
        foreach(var batch in batches)for(int i=0;i<batch.Multimesh.InstanceCount;i++)
        {
            var at=batch.Multimesh.GetInstanceTransform(i).Origin;var point=new Vector2(at.X,at.Z);
            float distance=point.DistanceTo(Geometry2D.GetClosestPointToSegment(point,new Vector2(60,-6),new Vector2(110,-30)));
            bool valid=LayoutMap.ForestStudyBounds.HasPoint(point) && distance>=2.3f && Mathf.Abs(at.Y-1.215f)<.01f;
            if(!valid&&grassValid)GD.Print($"GRASS_INVALID at={at}; distance={distance}");
            grassValid &= valid;
        }
        if(DisplayServer.GetName()!="headless")Check(grassValid,"every grass tuft stays inside the study, on the floor and outside the clear path");
        else GD.Print("SKIP: dummy renderer cannot read MultiMesh transforms; placement inspection runs windowed");
        Check(root.FindChildren("*",nameof(CollisionObject3D),true,false).Count==0,"grass and lighting add no collision");
        Check(LayoutMap.ForestWeight(new Vector2(84,-24))==0 && LayoutMap.ForestWeight(new Vector2(85.5f,-24))>.4f && LayoutMap.ForestWeight(new Vector2(94,-24))==1,"study colour mask feathers at the edge");
        GD.Print($"FOREST_MEASURE trees={map.ForestTreeCount}; tufts={map.ForestGrassCount}; batches={batches.Length}; triangles={batches.Sum(b=>(long)b.Multimesh.Mesh.GetFaces().Length/3*b.Multimesh.InstanceCount)}");
        // Show crown surfaces and grass at a comparable scale to the supplied close reference.
        p.TeleportTo(new Vector3(98,1.25f,-26));p.SetControls(Vector2.Zero,p.GlobalPosition+Vector3.Right*4,false);
        _stage.Cam.SnapTo(new Vector3(99,2,-29));await Frames(30);
        var detail=new Camera3D {Position=new Vector3(108,10,-21),Fov=40};AddChild(detail);detail.LookAt(new Vector3(99,2.4f,-29));detail.Current=true;
        await Frames(8);await Shot("tree-ground-detail");detail.Free();_stage.Cam.Current=true;
        // Walk and roll through the real movement/collision pipeline, in both directions.
        p.TeleportTo(new Vector3(80,1.25f,-15.6f));_stage.Cam.Close=false;
        await Walk(new Vector3(94,1.2f,-22.32f),true);await Shot("walk-centre");
        await Walk(new Vector3(108,1.2f,-29.04f),false);await Shot("walk-exit");await Frames(55);
        Check(light.Weight<.001f && Mathf.Abs(env.AmbientLightEnergy-.55f)<.001f && sun.RotationDegrees.DistanceTo(new Vector3(-36,-48,0))<.001f,"leaving restores the original light colour, energy and direction");
        await Walk(new Vector3(94,1.2f,-22.32f),false);
        p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
        int kills=_stage.Kills,shots=p.ShotsFired;
        foreach(var at in new[]{new Vector3(98,1.2f,-25),new Vector3(99,1.2f,-24),new Vector3(97,1.2f,-26),new Vector3(96,1.2f,-27),new Vector3(92,1.2f,-27),new Vector3(91,1.2f,-26)})
            _stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,at,awake:true);
        await Frames(12);await Shot("combat-readable");
        for(int i=0;i<1800 && _stage.Kills-kills<6;i++)
        {
            var enemy=_stage.AllEnemies.Where(e=>!e.IsDead).OrderBy(e=>e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition)).FirstOrDefault();
            if(enemy==null)break;
            if(p.Gun.Mag==0)p.PressReload();
            p.SetControls(Vector2.Zero,enemy.GlobalPosition+Vector3.Up*Player.AimHeight,true);await Frames(1);
        }
        p.SetControls(Vector2.Zero,p.GlobalPosition+Vector3.Right*5,false);
        Check(_stage.Kills-kills>=6 && p.ShotsFired>shots,"six moving enemies are killed with actual gunfire in the dressed forest");await Shot("combat-cleared");
        // Force a crown between camera and player, then leave it: only canopy fades, never the trunk.
        var occlusionTree=trees.First(t=>Asset(t).EndsWith("forest_pine.glb"));
        p.TeleportTo(occlusionTree.GlobalPosition+new Vector3(0,0,-2.5f));
        var crownCam=new Camera3D {Position=occlusionTree.GlobalPosition+new Vector3(0,4,5),Fov=45};AddChild(crownCam);crownCam.LookAt(p.GlobalPosition+Vector3.Up);crownCam.Current=true;
        await Frames(40);
        var canopy=occlusionTree.FindChildren("Occlude_*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
        Check(canopy.Length>0 && canopy.Any(m=>m.Transparency>.7f),"crown fades when it covers the survivor");await Shot("canopy-fade");
        crownCam.Free();_stage.Cam.Current=true;p.TeleportTo(new Vector3(108,1.25f,-29));await Frames(50);
        Check(canopy.All(m=>m.Transparency<.05f),"crown opacity restores after leaving");
        var outside=map.KitProps.Where(t=>!LayoutMap.ForestStudyBounds.HasPoint(new Vector2(t.GlobalPosition.X,t.GlobalPosition.Z))).Select(t=>$"{Asset(t)}:{t.GlobalPosition}:{t.Rotation}").OrderBy(s=>s).ToArray();
        int floors=map.FloorRects,voids=map.VoidRects;
        _stage.Free();await Frames(2);
        var baseline=new LayoutMap {ForestStudyEnabled=false,ForestExpanded=false};
        _stage=new Stage {MapOverride=baseline,ModeOverride=GameMode.Scenario,CharacterOverride="goose",DirectorEnabled=false,SquadOverride=0};AddChild(_stage);await Frames(5);
        var baselineOutside=baseline.KitProps.Where(t=>!LayoutMap.ForestStudyBounds.HasPoint(new Vector2(t.GlobalPosition.X,t.GlobalPosition.Z))).Select(t=>$"{Asset(t)}:{t.GlobalPosition}:{t.Rotation}").OrderBy(s=>s).ToArray();
        Check(floors==baseline.FloorRects && voids==baseline.VoidRects && outside.SequenceEqual(baselineOutside),"terrain topology and every prop outside the study match disabled baseline");
        Check(!_stage.HasNode("ForestQualityStudy") && PropLibrary.Errors.Count==0,"disabled study adds no grass/light and all resources load");
    }

    private async Task Walk(Vector3 goal,bool roll)
    {
        var p=_stage.Player;int frames=0;
        while(((goal-p.GlobalPosition) with {Y=0}).Length()>.18f && frames++<600)
        {
            var direction=((goal-p.GlobalPosition) with {Y=0}).Normalized();
            p.SetControls(new Vector2(direction.X,direction.Z),goal+Vector3.Up,false);
            if(roll&&frames==15)p.PressRoll();
            await Frames(1);
        }
        p.SetControls(Vector2.Zero,goal+Vector3.Right*4,false);
        Check(((goal-p.GlobalPosition) with {Y=0}).Length()<=.18f && Mathf.Abs(p.GlobalPosition.Y-1.2f)<.1f,"actual "+(roll?"walk/roll":"walk")+" reaches "+goal);
    }
}
