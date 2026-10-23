using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class TerrainEdgeCheck : Node
{
    private Stage _stage=null!;
    private int _checks;
    private string _label="after";
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private void Check(bool ok,string text){if(!ok)throw new Exception(text);_checks++;GD.Print("PASS: "+text);}
    private float RayY(Vector3 at)
    {
        var hit=_stage.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*8,at-Vector3.Up*8,Layers.World));
        if(hit.Count==0)throw new Exception("no terrain under "+at);
        return hit["position"].AsVector3().Y;
    }
    private async Task Shot(string name,Vector3 at)
    {
        if(DisplayServer.GetName()=="headless")return;
        var cam=new Camera3D{Position=at+new Vector3(-8,9,11),Fov=43};AddChild(cam);cam.LookAt(at);cam.Current=true;
        await Frames(12);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/terrain-edge-{_label}-{name}.png"))==Error.Ok,"render "+name);
        cam.Free();_stage.Cam.Current=true;
    }
    public override async void _Ready()
    {
        bool failed=false;
        try{await Run();GD.Print("TERRAIN_EDGE_PASS "+_checks);}
        catch(Exception e){GD.PushError(e.ToString());GD.Print("TERRAIN_EDGE_FAIL "+e.Message);failed=true;}
        if(_stage!=null&&IsInstanceValid(_stage))_stage.Free();QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
    private async Task Run()
    {
        foreach(var up in new[]{Vector3.Right,Vector3.Left,Vector3.Back,Vector3.Forward})
        {
            var tiers=new TierMap(0,0,8,8,1);tiers.PaintRamp(1,7,1,7,0,up,smooth:true);
            bool valid=true;
            for(int i=1;i<24;i++)
            {float t=i/24f;var at=new Vector3(4,0,4)+up*((t-.5f)*6);valid&=Mathf.Abs(tiers.GroundY(at)-TierMap.TerrainRampProfile(t)*TierMap.Height)<.001f;}
            Check(valid,"smooth height query supports axis "+up);
            tiers.PaintTier(1,7,1,7,2);Check(Mathf.Abs(tiers.GroundY(new(3.2f,0,3.4f))-2.4f)<.001f,"repainting a flat tier removes smooth override "+up);
        }
        _label=OS.GetCmdlineUserArgs().FirstOrDefault()??"after";
        var map=new LayoutMap();_stage=new Stage{MapOverride=map,DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};AddChild(_stage);await Frames(4);
        var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();await Frames(2);
        var ramps=_stage.GetChildren().OfType<StaticBody3D>().Where(n=>n.HasMeta("smooth_terrain_ramp") || n.GetChildren().OfType<MeshInstance3D>().Any(m=>m.Mesh is PrismMesh)).ToArray();
        Check(ramps.Length==6,"six authored ramps retained");
        for(int i=0;i<3;i++)
        {
            var at=ramps[i].Position;p.TeleportTo(at+new Vector3(-1,2,0));_stage.Cam.SnapTo(at);await Frames(40);await Shot("ramp-"+i,at);
        }
        await Shot("wall",new Vector3(70,.5f,-24));
        foreach(var ramp in ramps)
        {
            var dir=ramp.Basis.X;var across=ramp.Basis.Z;float run=6;
            float halfWidth=ramp.GetChildren().OfType<MeshInstance3D>().Single().Mesh.GetAabb().Size.Z*.5f;
            float Sample(float t)=>RayY(ramp.Position+dir*(run*(t-.5f)));
            float low=Sample(.02f),nearLow=Sample(.06f),high=Sample(.98f),nearHigh=Sample(.94f);
            Check((nearLow-low)/(.04f*run)<.07f && (high-nearHigh)/(.04f*run)<.07f,"ramp smoothly joins flat floors at "+ramp.Position);
            bool aligned=true;
            foreach(float side in new[]{-halfWidth+.5f,0,halfWidth-.5f})for(int i=1;i<24;i++)
            {
                var at=ramp.Position+dir*(run*(i/24f-.5f))+across*side;float physical=RayY(at);
                aligned&=Mathf.Abs(physical-_stage.Tiers.GroundY(at))<.015f;
            }
            Check(aligned,"continuous height query agrees with collision at "+ramp.Position);
            var lowEnd=ramp.Position-dir*4;lowEnd.Y=RayY(lowEnd);var highEnd=ramp.Position+dir*4;highEnd.Y=RayY(highEnd);
            p.TeleportTo(lowEnd+Vector3.Up*.05f);await Frames(20);await Walk(highEnd,false);await Walk(lowEnd,true);
        }
        p.TeleportTo(new Vector3(68,.05f,-24));await Frames(20);
        for(int i=0;i<100;i++){p.SetControls(Vector2.Right,new Vector3(74,1,-24),false);await Frames(1);}
        p.SetControls(Vector2.Zero,p.Position+Vector3.Right,false);
        Check(p.Position.X<70 && p.Position.Y<.2f,"non-ramp cliff stays impassable to walking");
        Check(_stage.FindChildren("TerrainRock_*",nameof(MeshInstance3D),true,false).Count>0,"continuous irregular rock walls exist");
        var rocks=_stage.FindChildren("TerrainRock_*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
        Check(rocks.All(r=>r.GetParent()==_stage && r.GetChildren().Count==0 && r.MaterialOverride is ShaderMaterial),"rock dressing adds no collision or per-piece processing");
        GD.Print($"TERRAIN_EDGE_MEASURE batches={rocks.Length}; rock_triangles={rocks.Sum(r=>r.Mesh.GetFaces().Length/3)}; trees={map.ForestTreeCount}; grass={map.ForestGrassCount}");
        Check(_stage.Director.EncounterErrors.Count==0,"terrain update preserves enemy entry points");
    }
    private async Task Walk(Vector3 goal,bool roll)
    {
        var p=_stage.Player;int n=0;
        while(((goal-p.Position) with{Y=0}).Length()>.2f && n++<600)
        {var d=((goal-p.Position) with{Y=0}).Normalized();p.SetControls(new(d.X,d.Z),goal+Vector3.Up,false);if(roll&&n==15)p.PressRoll();await Frames(1);}
        p.SetControls(Vector2.Zero,goal+Vector3.Right,false);await Frames(20);
        Check(((goal-p.Position) with{Y=0}).Length()<.25f && Mathf.Abs(p.Position.Y-goal.Y)<.12f,"actual walk/roll reaches "+goal+" at "+p.Position);
    }
}
