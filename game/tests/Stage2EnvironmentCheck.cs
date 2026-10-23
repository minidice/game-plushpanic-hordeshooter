using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Composition captures use explicit teleports; Stage2RouteCheck separately verifies continuous actual play.</summary>
public partial class Stage2EnvironmentCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    private async Task Frames(int n) { for (int i=0;i<n;i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            var map=new LayoutMap();
            _stage=new Stage { MapOverride=map, ModeOverride=GameMode.Scenario, CharacterOverride="goose", DirectorEnabled=false, SquadOverride=0 };
            AddChild(_stage); await Frames(4);
            _stage.Player.ManualControl=true; _stage.Player.Invulnerable=true;
            Check(_stage.FindChild("EnvironmentPaths",true,false)!=null,"connected ground path art exists");
            var path=(MeshInstance3D)_stage.FindChild("EnvironmentPaths",true,false);
            var vertices=path.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Check(vertices.All(v=>Math.Abs(v.Y-_stage.Tiers.GroundY(v)-.025f)<.002f),"every path vertex follows actual ground including slopes");
            var triangles=new HashSet<string>();
            for(int i=0;i<vertices.Length;i+=3)
                CheckTriangle(vertices,i,triangles);
            Check(triangles.Count==vertices.Length/3,"route junctions never emit duplicate triangles");
            Check(_stage.FindChild("EnvironmentGroves",true,false)!=null,"outer woodland is instanced");
            var shell=PropLibrary.Find("container_stack_shell");
            Check(shell!=null && shell.Dimensions.DistanceTo(new Vector3(14,2.4f,12))<.02f,"container shell matches existing deck footprint and height");
            Check(map.KitProps.Count(p=>p.GetChildren().Any(c=>c.Name=="container_stack_shell"))==3,"all three climbable container decks dressed");
            Check(map.KitProps.Count(p=>p.GetChildren().Any(c=>c.Name=="route_sign"))>=8,"route junctions have direction markers");
            Check(_stage.FindChildren("TerrainRock_*",nameof(MeshInstance3D),true,false).Count>10,"natural tier walls have continuous rock relief");
            var cliff=PropLibrary.Find("cliff_face");
            Check(cliff!=null && Math.Abs(cliff.Dimensions.X-6)<.01 && Math.Abs(cliff.Dimensions.Y-2.4f)<.01,"rock module height and length match tier grid");
            GD.Print($"ENVIRONMENT_MEASURE kit={map.KitProps.Count}; cliffs={map.Scattered.GetValueOrDefault("cliff_face")}; rocks={map.Scattered.GetValueOrDefault("rock_cluster")}; grass={map.Scattered.GetValueOrDefault("bit_grass_clump")}; path_triangles={triangles.Count}");
            var station=new Vector3(366,2.2f,-18);
            bool stationSolid=!Rays.Clear(_stage.Player,station+new Vector3(0,0,3),station,Layers.World);
            var fixtures=map.KitProps.Where(p=>p.GetChildren().Any(c=>c.Name=="streetlamp")).ToArray();
            int unmounted=_stage.GetChildren().OfType<OmniLight3D>()
                .Where(l=>Math.Abs(l.Position.Y-_stage.Tiers.GroundY(l.Position)-3.6f)<.02f)
                .Count(l=>!fixtures.Any(p=>(p.GlobalPosition+Vector3.Up*3.6f).DistanceTo(l.GlobalPosition)<.05f));
            Check(stationSolid && unmounted==0,$"workshop ray is blocked ({stationSolid}); every environment light has a lamp ({unmounted} unmounted)");
            foreach(var at in new[]{new Vector3(279,0,-78),new Vector3(329,0,-84),new Vector3(375,0,-89)})
                Check(Math.Abs(_stage.Tiers.GroundY(at)-3.6f)<.01f,"container roof keeps original walk height "+at.X);
            var views=new (string,float,float)[]{("01-start",18,-2),("02-forest-ramp",70,-10),("03-forest",100,-27),("04-tower",118,-64),("05-rim",174,-43),("06-pump",200,-8),("07-return",183,-22),("08-bridge",196,-45),("09-hut",232,-52),("10-yard-entry",260,-46),("11-container",279,-75),("12-truck",318,-40),("13-workshop",360,-14),("14-crane",350,-87),("15-yard-exit",437,-68),("16-plateau",484,-99),("17-gondola",526,-128),("18-arrival",610,-143)};
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
            foreach(var (name,x,z) in views)
            {
                var p=new Vector3(x,0,z);p.Y=_stage.Tiers.GroundY(p)+.05f;
                _stage.Player.GlobalPosition=p; _stage.Player.Velocity=Vector3.Zero;
                _stage.Player.SetControls(Vector2.Zero,p+Vector3.Right*5,false);
                await Frames(45);
                if(DisplayServer.GetName()=="headless")continue;
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/environment-"+name+".png"))==Error.Ok,"render "+name);
            }
            GD.Print("STAGE2_ENVIRONMENT_PASS "+_checks);
        }
        catch(Exception e){GD.PushError(e.ToString());failed=true;}
        if(_stage!=null && IsInstanceValid(_stage))_stage.Free();
        QuitHygiene.Collect(); GetTree().Quit(failed?1:0);
    }
    private static void CheckTriangle(Vector3[] vertices,int i,HashSet<string> triangles)
    {
        var key=string.Join(";",vertices.Skip(i).Take(3).Select(v=>v.ToString()).OrderBy(v=>v));
        if(!triangles.Add(key))throw new Exception("overlapping duplicate path triangle "+key);
    }
}
