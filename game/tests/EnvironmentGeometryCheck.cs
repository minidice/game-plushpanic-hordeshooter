using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace HordeShooter.Tests;

public partial class EnvironmentGeometryCheck : Node3D
{
    private int _checks;
    private void Check(bool ok,string why){if(!ok)throw new Exception(why);_checks++;GD.Print("PASS: "+why);}
    private static IEnumerable<(Vector3 a,Vector3 b,Vector3 c)> Triangles(Mesh mesh)
    {
        for(int surface=0;surface<mesh.GetSurfaceCount();surface++)
        {
            var arrays=mesh.SurfaceGetArrays(surface);
            var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices=arrays[(int)Mesh.ArrayType.Index].VariantType==Variant.Type.Nil?Array.Empty<int>():arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            int n=indices.Length==0?vertices.Length:indices.Length;
            Vector3 At(int i)=>vertices[indices.Length==0?i:indices[i]];
            for(int i=0;i<n;i+=3)yield return(At(i),At(i+1),At(i+2));
        }
    }
    public override void _Ready()
    {
        bool failed=false;
        try
        {
            foreach(bool plainFirst in new[]{false,true})
            {
                var root=new Node3D();AddChild(root);
                foreach(bool bevel in new[]{!plainFirst,plainFirst})
                    StageArt.Box(root,new Vector3(bevel?-3:3,0,0),Vector3.One,"82918a",bevel);
                int expected=root.GetChildren().OfType<MeshInstance3D>().Sum(m=>Triangles(m.Mesh).Count());
                StaticArtBatch.Bake(root);
                var triangles=root.GetChildren().OfType<MeshInstance3D>().SelectMany(m=>Triangles(m.Mesh)).ToArray();
                Check(triangles.Length==expected,$"batch keeps indexed and nonindexed triangle counts in either order ({triangles.Length}/{expected})");
                Check(triangles.Any(t=>t.a.X< -2)&&triangles.Any(t=>t.a.X>2),"both separated boxes remain referenced by drawable triangles");
                root.Free();
            }
            var portal=new Node3D();AddChild(portal);
            ArchitectureArt.Portal(portal,0,6,3.85f,shutter:true);
            var sweep=new Aabb(new Vector3(-.2f,3.1f,-3),new Vector3(.4f,3.2f,6));
            foreach(var m in portal.FindChildren("*",nameof(MeshInstance3D),true,false).Cast<MeshInstance3D>())
                foreach(var(a,b,c)in Triangles(m.Mesh))
                {
                    var bounds=new Aabb(a,Vector3.Zero).Expand(b).Expand(c);
                    bool overlaps = bounds.Position.X < sweep.End.X-.001f && bounds.End.X > sweep.Position.X+.001f &&
                        bounds.Position.Y < sweep.End.Y-.001f && bounds.End.Y > sweep.Position.Y+.001f &&
                        bounds.Position.Z < sweep.End.Z-.001f && bounds.End.Z > sweep.Position.Z+.001f;
                    Check(!overlaps,
                        "shutter frame triangles stay out of the raised slab envelope");
                }
            portal.Free();
            GD.Print("ENVIRONMENT_GEOMETRY_CHECK_PASS "+_checks);
        }
        catch(Exception e){failed=true;GD.PushError(e.ToString());}
        QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
