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
            // Junction coping is a union: each point on a shared L/T/straight top is covered exactly once.
            foreach(string junction in new[]{"L","T","straight","height"})
            {
                var root=new Node3D();AddChild(root);
                var walls=new List<(Node3D Body,Vector3 Size)>();
                void Wall(Vector3 at,Vector3 size){var node=new Node3D { Position=at };root.AddChild(node);walls.Add((node,size));}
                Wall(new Vector3(2,1.5f,0),new Vector3(4,3,.4f));
                if(junction=="straight")Wall(new Vector3(6,1.5f,0),new Vector3(4,3,.4f));
                else Wall(new Vector3(junction=="T"?2:4,junction=="height"?2:1.5f,1.8f),new Vector3(.4f,junction=="height"?4:3,4));
                ArchitectureArt.JoinWalls(root,walls);
                var tris=root.FindChildren("*",nameof(MeshInstance3D),true,false).Cast<MeshInstance3D>().SelectMany(m=>Triangles(m.Mesh)).ToArray();
                foreach(var w in walls)
                {
                    float h=w.Body.Position.Y+w.Size.Y/2;
                    foreach(float fraction in new[]{-.47f,-.13f,.11f,.47f})
                    {
                        var p=new Vector2(w.Body.Position.X+w.Size.X*fraction,w.Body.Position.Z+w.Size.Z*.13f);
                        if(walls.Any(v=>v.Size.Y>h+.01f&&new Rect2(v.Body.Position.X-v.Size.X/2,v.Body.Position.Z-v.Size.Z/2,v.Size.X,v.Size.Z).HasPoint(p)))continue;
                        int covers=tris.Count(t=>Mathf.Abs(t.a.Y-h)<.001f&&Mathf.Abs(t.b.Y-h)<.001f&&Mathf.Abs(t.c.Y-h)<.001f&&
                            Geometry2D.PointIsInsideTriangle(p,new Vector2(t.a.X,t.a.Z),new Vector2(t.b.X,t.b.Z),new Vector2(t.c.X,t.c.Z)));
                        Check(covers==1,junction+" coping has one surface at "+p+" ("+covers+")");
                    }
                }
                root.Free();
            }
            GD.Print("ENVIRONMENT_GEOMETRY_CHECK_PASS "+_checks);
        }
        catch(Exception e){failed=true;GD.PushError(e.ToString());}
        QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
