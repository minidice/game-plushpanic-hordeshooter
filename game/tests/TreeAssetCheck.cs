using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Checks the actual GLBs, common loader and Scatter, without a map-specific tree substitution.</summary>
public partial class TreeAssetCheck : Node3D
{
    private static readonly string[] Ids = { "pine_tall", "pine_small", "broadleaf", "round_oak", "round_linden" };
    private sealed class ScatterProbe : StageMap
    {
        public override Rect2 Area => new(-20,-20,40,40);
        protected override void BuildMap() { }
        public void Draw(Stage stage, string id) { _stage=stage; Scatter(id, new[] { Transform3D.Identity, new Transform3D(Basis.Identity,new Vector3(5,0,0)) }); }
    }
    private int _checks;
    private void Check(bool ok,string why) { if(!ok)throw new Exception(why); _checks++; GD.Print("PASS: "+why); }
    private static IEnumerable<MeshInstance3D> Parts(Node model) => model.FindChildren("*", nameof(MeshInstance3D), true, false).Cast<MeshInstance3D>();
    private static int Triangles(Mesh mesh) => Enumerable.Range(0,mesh.GetSurfaceCount()).Sum(s=>mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Index].AsInt32Array().Length/3);
    private static IEnumerable<Vector3> Positions(Node3D model)
    {
        foreach(var part in Parts(model))
        {
            var transform=Transform3D.Identity;
            for(Node? node=part;node!=model&&node!=null;node=node.GetParent()) if(node is Node3D n)transform=n.Transform*transform;
            for(int s=0;s<part.Mesh.GetSurfaceCount();s++)
                foreach(var p in part.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array()) yield return transform*p;
        }
    }
    // Imported mesh compression may move a vertex across a millimetre rounding boundary.
    private static bool SameShape(Vector3[] a,Vector3[] b) => a.All(p=>b.Any(q=>p.DistanceSquaredTo(q)<.002f*.002f))&&b.All(p=>a.Any(q=>p.DistanceSquaredTo(q)<.002f*.002f));
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            foreach(string id in Ids)
            {
                var info=PropLibrary.Find(id)!;
                var direct=GD.Load<PackedScene>(info.File).Instantiate<Node3D>();
                var loaded=PropLibrary.Instance(id)!;
                AddChild(direct);AddChild(loaded);
                var expected=FoliageArt.Mesh(id.StartsWith("pine")?FoliageArt.Kind.Pine:FoliageArt.Kind.Broadleaf,id=="round_oak"?2:0);
                float scale=id=="pine_small"?.68f:id=="round_linden"?.8f:1;
                var expectedPositions=expected.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                float minY=expectedPositions.Min(p=>p.Y);
                var wanted=expectedPositions.Select(p=>(p-Vector3.Up*minY)*scale).ToArray();
                Check(SameShape(Positions(direct).ToArray(),wanted),id+" direct GLB vertices match approved FoliageArt shape within 2mm import precision");
                Check(SameShape(Positions(loaded).ToArray(),wanted),id+" PropLibrary uses the same revised geometry");
                int triangles=Parts(direct).Sum(p=>Triangles(p.Mesh));
                Check(triangles==Triangles(expected)&&triangles<3000,id+" obsolete 8k/11k geometry is absent");
                Check(info.OcclusionParts.Length==4&&info.OcclusionParts.All(role=>direct.FindChild(role,true,false)!=null),id+" all four occlusion roles preserved");
                Check(direct.FindChild("Keep",true,false)!=null&&direct.FindChildren("*",nameof(CollisionObject3D),true,false).Count==0,id+" bark role and visual-only contract preserved");
                Check(Parts(direct).All(p=>Enumerable.Range(0,p.Mesh.GetSurfaceCount()).All(s=>p.Mesh.SurfaceGetMaterial(s) is StandardMaterial3D m&&m.Transparency==BaseMaterial3D.TransparencyEnum.Disabled&&m.VertexColorUseAsAlbedo)),id+" portable opaque vertex-color PBR materials");
                var stage=new Stage();
                new ScatterProbe().Draw(stage,id);
                var batches=stage.GetChildren().OfType<MultiMeshInstance3D>().ToArray();
                Check(batches.Length==Parts(direct).Count()&&batches.Sum(p=>Triangles(p.Multimesh.Mesh))==triangles&&batches.All(p=>p.Multimesh.InstanceCount==2),id+" Scatter draws revised meshes for every instance");
                stage.Free(); direct.Free(); loaded.Free();
            }
            if(DisplayServer.GetName()!="headless")
            {
                AddChild(new WorldEnvironment { Environment=new Godot.Environment { BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("25343c"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("b9c6d2"),AmbientLightEnergy=.42f } });
                AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-36,-48,0),LightColor=new Color("e5e8d5"),LightEnergy=1.2f,ShadowEnabled=true });
                var camera=new Camera3D { Projection=Camera3D.ProjectionType.Orthogonal,Size=20,Position=new Vector3(2,11,19),Current=true };AddChild(camera);camera.LookAt(new Vector3(0,1.5f,0));
                AddChild(new MeshInstance3D { Mesh=new PlaneMesh { Size=new Vector2(25,12) },MaterialOverride=Mat.Solid("69726b"),Position=new Vector3(0,-.02f,0) });
                for(int i=0;i<Ids.Length;i++)
                {
                    var model=PropLibrary.Instance(Ids[i])!;model.Position=new Vector3(-8+i*4,0,0);AddChild(model);
                    AddChild(new Label3D { Text=Ids[i],Position=model.Position+new Vector3(0,.1f,2),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=42,PixelSize=.006f });
                }
                for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/tree-assets-revised.png"));
                foreach(var label in GetChildren().OfType<Label3D>())label.Visible=false;
                var trees=GetChildren().OfType<Node3D>().Where(n=>Ids.Contains(n.Name.ToString())).ToArray();
                foreach(var tree in trees)tree.Visible=false;
                camera.Size=5.6f;
                foreach(var tree in trees)
                {
                    tree.Visible=true;camera.Position=tree.Position+new Vector3(4,4,6);camera.LookAt(tree.Position+new Vector3(0,1.8f,0));
                    for(int f=0;f<4;f++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/tree-{tree.Name}.png"));
                    tree.Visible=false;
                }
            }
            GD.Print("TREE_ASSET_CHECK_PASS "+_checks);
        }
        catch(Exception e) { GD.PushError(e.ToString()); failed=true; }
        foreach(Node child in GetChildren())child.Free();
        QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
