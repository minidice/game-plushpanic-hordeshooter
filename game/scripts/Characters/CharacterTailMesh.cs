using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>A closed, continuous tail surface. Stripes own adjacent faces, never overlapping shells.</summary>
internal static class CharacterTailMesh
{
    private const int Rings=36, Sides=24;

    /// <summary>Length and thickness multipliers, and how far the tip curls up (0 = none).</summary>
    public readonly record struct TailShape(float Length,float Width,float Curl);

    /// <summary>Tail styles offered in the workshop: default, fluffy, long and thin, short and round, curled.</summary>
    public static TailShape For(int style)=>style switch
    {
        1=>new TailShape(.95f,1.35f,0),
        2=>new TailShape(1.30f,.72f,0),
        3=>new TailShape(.55f,1.20f,0),
        4=>new TailShape(1f,1f,1),
        _=>new TailShape(1,1,0),
    };

    public static ArrayMesh Build(string species,Material[] materials,int style=0)
    {
        var shape=For(style);
        bool bird=AppearanceCatalog.IsBird(species);
        int surfaces=bird?1:2;
        var vertices=new List<Vector3>[surfaces];
        var normals=new List<Vector3>[surfaces];
        for(int i=0;i<surfaces;i++){vertices[i]=new();normals[i]=new();}
        for(int ring=0;ring<Rings;ring++)
        {
            float t0=(float)ring/Rings,t1=(float)(ring+1)/Rings;
            int band=bird?0:(ring/6)%2;
            for(int side=0;side<Sides;side++)
            {
                float a0=Mathf.Tau*side/Sides,a1=Mathf.Tau*(side+1)/Sides;
                // Godot's front faces are clockwise. Normals stay outward at shared band edges.
                Triangle(band,t0,a0,t0,a1,t1,a0);
                if(ring<Rings-1)Triangle(band,t1,a0,t0,a1,t1,a1);
            }
        }
        // The small root cap is embedded in the rump, and keeps the mesh closed.
        for(int side=0;side<Sides;side++)
        {
            float a0=Mathf.Tau*side/Sides,a1=Mathf.Tau*(side+1)/Sides;
            var normal=-Tangent(species,shape,0);
            vertices[0].Add(Center(species,shape,0));vertices[0].Add(Point(species,shape,0,a1));vertices[0].Add(Point(species,shape,0,a0));
            normals[0].Add(normal);normals[0].Add(normal);normals[0].Add(normal);
        }
        var mesh=new ArrayMesh();
        for(int surface=0;surface<surfaces;surface++)
        {
            var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=vertices[surface].ToArray();
            arrays[(int)Mesh.ArrayType.Normal]=normals[surface].ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
            mesh.SurfaceSetMaterial(surface,materials[Math.Min(surface,materials.Length-1)]);
        }
        return mesh;

        void Triangle(int band,float t0,float a0,float t1,float a1,float t2,float a2)
        {
            foreach(var (t,a) in new[]{(t0,a0),(t1,a1),(t2,a2)})
            {vertices[band].Add(Point(species,shape,t,a));normals[band].Add(Normal(species,shape,t,a));}
        }
    }

    private static Vector3 Center(string species,TailShape shape,float t)
    {
        // Curl lifts the outer half of the tail and pulls the tip back over the rump.
        float curl=shape.Curl*Mathf.Pow(t,2.2f);
        if(AppearanceCatalog.IsBird(species))
            return new Vector3(0,.04f*t+.11f*t*t+curl*.30f,-(species=="goose"?.46f:.42f)*shape.Length*t*(1-curl*.35f));
        return new Vector3(.22f*t*t,-.10f*Mathf.Sin(Mathf.Pi*t)+.28f*t*t+curl*.62f,-(species=="redpanda"?1.15f:.95f)*shape.Length*t*(1-curl*.45f));
    }

    private static Vector3 Tangent(string species,TailShape shape,float t)
    {
        float before=Mathf.Max(0,t-.001f),after=Mathf.Min(1,t+.001f);
        return (Center(species,shape,after)-Center(species,shape,before)).Normalized();
    }

    private static Vector3 Point(string species,TailShape shape,float t,float angle)
    {
        var tangent=Tangent(species,shape,t);
        bool bird=AppearanceCatalog.IsBird(species);
        var across=Across(species,tangent);
        var up=across.Cross(tangent).Normalized();
        float taper=Mathf.Sqrt(Mathf.Max(0,1-t*t))*(.60f+.40f*Mathf.Sin(Mathf.Pi*t));
        float width=(bird?(species=="duck"?.25f:.22f):(species=="redpanda"?.26f:.21f))*shape.Width;
        float height=bird?.085f*Mathf.Sqrt(shape.Width):width;
        return Center(species,shape,t)+(across*Mathf.Cos(angle)*width+up*Mathf.Sin(angle)*height)*taper;
    }

    // Bird tails stay in the body's mid plane, so their side axis is always +X. Deriving it from the
    // tangent flips it when a curled tip turns past vertical, twisting the last ring inside out.
    private static Vector3 Across(string species,Vector3 tangent)=>
        AppearanceCatalog.IsBird(species)?Vector3.Right:tangent.Cross(Vector3.Up).Normalized();

    /// <summary>For checks: the side axis never reverses between neighbouring rings.</summary>
    internal static bool FrameIsContinuous(string species,int style)
    {
        var shape=For(style);
        var previous=Across(species,Tangent(species,shape,0));
        for(int ring=1;ring<=Rings;ring++)
        {
            var across=Across(species,Tangent(species,shape,(float)ring/Rings));
            if(!float.IsFinite(across.X)||across.Dot(previous)<=0)return false;
            previous=across;
        }
        return true;
    }

    private static Vector3 Normal(string species,TailShape shape,float t,float angle)
    {
        if(t>=1)return Tangent(species,shape,1);
        var along=Point(species,shape,Mathf.Min(1,t+.001f),angle)-Point(species,shape,Mathf.Max(0,t-.001f),angle);
        var around=Point(species,shape,t,angle+.001f)-Point(species,shape,t,angle-.001f);
        return along.Cross(around).Normalized();
    }
}
