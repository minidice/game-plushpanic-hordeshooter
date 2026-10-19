using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>A closed, continuous tail surface. Stripes own adjacent faces, never overlapping shells.</summary>
internal static class CharacterTailMesh
{
    private const int Rings=36, Sides=24;

    public static ArrayMesh Build(string species,Material[] materials)
    {
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
            var normal=-Tangent(species,0);
            vertices[0].Add(Center(species,0));vertices[0].Add(Point(species,0,a1));vertices[0].Add(Point(species,0,a0));
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
            {vertices[band].Add(Point(species,t,a));normals[band].Add(Normal(species,t,a));}
        }
    }

    private static Vector3 Center(string species,float t)
    {
        if(AppearanceCatalog.IsBird(species))
            return new Vector3(0,.04f*t+.11f*t*t,-(species=="goose"?.46f:.42f)*t);
        return new Vector3(.22f*t*t,-.10f*Mathf.Sin(Mathf.Pi*t)+.28f*t*t,-(species=="redpanda"?1.15f:.95f)*t);
    }

    private static Vector3 Tangent(string species,float t)
    {
        float before=Mathf.Max(0,t-.001f),after=Mathf.Min(1,t+.001f);
        return (Center(species,after)-Center(species,before)).Normalized();
    }

    private static Vector3 Point(string species,float t,float angle)
    {
        var tangent=Tangent(species,t);
        var across=tangent.Cross(Vector3.Up).Normalized();
        var up=across.Cross(tangent).Normalized();
        bool bird=AppearanceCatalog.IsBird(species);
        float taper=Mathf.Sqrt(Mathf.Max(0,1-t*t))*(.60f+.40f*Mathf.Sin(Mathf.Pi*t));
        float width=bird?(species=="duck"?.25f:.22f):(species=="redpanda"?.26f:.21f);
        float height=bird?.085f:width;
        return Center(species,t)+(across*Mathf.Cos(angle)*width+up*Mathf.Sin(angle)*height)*taper;
    }

    private static Vector3 Normal(string species,float t,float angle)
    {
        if(t>=1)return Tangent(species,1);
        var along=Point(species,Mathf.Min(1,t+.001f),angle)-Point(species,Mathf.Max(0,t-.001f),angle);
        var around=Point(species,t,angle+.001f)-Point(species,t,angle-.001f);
        return along.Cross(around).Normalized();
    }
}
