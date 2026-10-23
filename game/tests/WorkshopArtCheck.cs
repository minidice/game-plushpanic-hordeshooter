using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

public partial class WorkshopArtCheck : Node
{
    private int _checks;
    private void Check(bool ok,string why){if(!ok)throw new Exception(why);_checks++;GD.Print("PASS: "+why);}
    private static Aabb Bounds(CollisionShape3D shape) => ((Node3D)shape.GetParent()).GlobalTransform*shape.Transform*
        new Aabb(-((BoxShape3D)shape.Shape).Size/2,((BoxShape3D)shape.Shape).Size);
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            foreach(int number in new[]{1,2})
            {
                var stage=new Stage { MapOverride=number==1?new StageOneLayout():new StageTwoMap(),
                    ModeOverride=GameMode.Scenario,DirectorEnabled=false };
                AddChild(stage);
                foreach(var door in stage.Map.Doors)door.SetOpen(true,0);
                stage.Map.ApplyFlow();
                Check(stage.Director.EncounterErrors.Count==0,"existing encounter entries retain clear landing space");
                foreach(var entry in stage.Map.Encounters!.Entries)
                    Check(stage.ExitFlow.Reachable(entry.Ground),$"stage {number} entry {entry.At} can reach the route");
                var names=number==1?new[]{"Workbench","ToolChest","PartsShelf","SafetyBoard"}
                    :new[]{"Generator","CableReel","PipeRack","SafetyBoard"};
                foreach(string name in names)
                {
                    var prop=stage.GetNodeOrNull<Node3D>("Workshop_"+name);
                    Check(prop!=null,$"stage {number} contains the {name} set piece");
                    var meshes=prop!.FindChildren("*",nameof(MeshInstance3D),true,false).Cast<MeshInstance3D>().ToArray();
                    Check(meshes.Length>0&&meshes.Length<=8,name+" has visible, material-batched geometry");
                    if(prop is not StaticBody3D body)continue;
                    var collider=body.GetChildren().OfType<CollisionShape3D>().Single();
                    var bounds=Bounds(collider);
                    Check(meshes.All(m=>bounds.Grow(.025f).Encloses(m.GlobalTransform*m.GetAabb())),name+" fits its blocking envelope");
                    Check(stage.Flow.IsBlocked(body.GlobalPosition),name+" cannot be walked through");
                    foreach(var other in stage.GetChildren().OfType<StaticBody3D>().Where(b=>b!=body&&(b.CollisionLayer&Layers.World)!=0))
                        foreach(var shape in other.GetChildren().OfType<CollisionShape3D>().Where(c=>c.Shape is BoxShape3D))
                        {
                            var b=Bounds(shape);
                            if(b.End.Y<.1f)continue;
                            Check(!bounds.Grow(-.025f).Intersects(b),name+" does not intersect another wall or prop");
                        }
                }
                for(float x=number==1?54:68;x<(number==1?83:99);x+=1)
                    Check(!stage.Flow.IsBlocked(new Vector3(x,0,.5f)),"the central route remains open at "+x);
                stage.Free();
            }
            var catalog=new Node3D();AddChild(catalog);
            int index=0;
            foreach(var kind in Enum.GetValues<WorkshopArt.Kind>())
            {
                var at=new Vector3((index%4)*3.9f-5.85f,0,(index/4)*4.2f-2.1f);
                var model=WorkshopArt.Create(catalog,kind,at);
                int triangles=0;
                foreach(var mesh in model.GetChildren().OfType<MeshInstance3D>())
                    for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                        triangles+=mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Index].AsInt32Array().Length/3;
                Check(triangles>0&&triangles<3500,kind+" stays within the static prop geometry budget: "+triangles);
                if(DisplayServer.GetName()!="headless")catalog.AddChild(new Label3D { Text=kind.ToString(),Position=at+new Vector3(0,.1f,1.25f),
                    Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=42,PixelSize=.005f,NoDepthTest=true });
                index++;
            }
            if(DisplayServer.GetName()!="headless")
            {
                OS.LowProcessorUsageMode=false;Engine.MaxFps=120;DisplayServer.WindowMoveToForeground();
                AddChild(new WorldEnvironment { Environment=new Godot.Environment {
                    BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("293840"),
                    AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("b9c6d2"),AmbientLightEnergy=.5f } });
                AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-48,-35,0),LightEnergy=1.2f,ShadowEnabled=true });
                Mat.Mesh(catalog,new PlaneMesh { Size=new Vector2(19,13) },new Vector3(0,-.025f,0),Mat.Solid("647273"));
                var camera=new Camera3D { Projection=Camera3D.ProjectionType.Orthogonal,Size=18.5f,Position=new Vector3(4,11,17),Current=true };
                AddChild(camera);camera.LookAt(new Vector3(0,.5f,0));
                for(int i=0;i<16;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/workshop-catalog.png"));
            }
            GD.Print("WORKSHOP_ART_CHECK_PASS "+_checks);
        }
        catch(Exception e){GD.PushError(e.ToString());failed=true;}
        foreach(Node child in GetChildren())child.Free();
        QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
