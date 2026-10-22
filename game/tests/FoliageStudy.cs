using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class FoliageStudy : Node3D
{
    private int _checks;
    private void Check(bool ok,string why){if(!ok)throw new Exception(why);_checks++;GD.Print("PASS: "+why);}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            foreach(var kind in Enum.GetValues<FoliageArt.Kind>())for(int v=0;v<4;v++)
            {
                var mesh=FoliageArt.Mesh(kind,v);var arrays=mesh.SurfaceGetArrays(0);
                var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();var normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                int triangles=arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Length/3;
                Check(triangles<3000,kind+" variant "+v+" budget "+triangles+" triangles");
                Check(vertices.All(p=>p.IsFinite())&&normals.All(n=>n.IsFinite()&&n.Length()>.99f),"finite geometry and unit normals");
                Check(mesh.SurfaceGetMaterial(0) is ShaderMaterial,"common foliage shading");
            }
            if(DisplayServer.GetName()!="headless")
            {
                OS.LowProcessorUsageMode=false;Engine.MaxFps=120;DisplayServer.WindowMoveToForeground();
                AddChild(new WorldEnvironment { Environment=new Godot.Environment {
                    BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("25343c"),
                    AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("b9c6d2"),AmbientLightEnergy=.42f } });
                AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-36,-48,0),LightColor=new Color("e5e8d5"),LightEnergy=1.2f,ShadowEnabled=true });
                AddChild(new MeshInstance3D { Mesh=new PlaneMesh{Size=new Vector2(24,14)},MaterialOverride=Mat.Solid("69726b"),Position=new Vector3(0,-.08f,0) });
                var camera=new Camera3D { Projection=Camera3D.ProjectionType.Orthogonal,Size=16,Position=new Vector3(2,11,17),Current=true };
                AddChild(camera);camera.LookAt(new Vector3(0,1.4f,0));
                var models=new System.Collections.Generic.List<Node3D>();
                for(int i=0;i<4;i++)
                {
                    bool pine=i>=2;bool revised=i%2==1;
                    var model=PropLibrary.Instance(pine?"pine_tall":"broadleaf")!;
                    if(revised)FoliageArt.ReplaceTree(model,pine?"pine_tall":"broadleaf");
                    model.Position=new Vector3(-6+i*4,0,0);AddChild(model);models.Add(model);
                    AddChild(new Label3D { Text=(revised?"NEW ":"OLD ")+(pine?"PINE":"CANOPY"),Position=model.Position+new Vector3(0,.1f,2),
                        Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=48,PixelSize=.006f,NoDepthTest=true });
                }
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
                await Frames(20);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/foliage-study-lit.png"));
                var silhouette=new StandardMaterial3D { AlbedoColor=new Color("152028"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded };
                foreach(var model in models)foreach(var node in model.FindChildren("*",nameof(MeshInstance3D),true,false).Cast<MeshInstance3D>())node.MaterialOverride=silhouette;
                await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/foliage-study-silhouette.png"));
            }
            GD.Print("FOLIAGE_STUDY_PASS "+_checks);
        }
        catch(Exception e){GD.PushError(e.ToString());failed=true;}
        foreach(Node child in GetChildren())child.Free();
        QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
