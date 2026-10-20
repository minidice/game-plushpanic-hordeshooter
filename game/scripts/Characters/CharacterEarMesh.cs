using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Mammal ears rebuilt as a closed, cupped shell: a furred back, a hollow front with a lighter inner
/// patch, and a rim where the two layers meet. Replaces the flat extruded plates from the generator.
/// Built in the ear pivot's space: base slightly inside the head at the origin, tip up (+Y), facing +Z.
/// Surface 0 = outer fur (custom_body), surface 1 = inner ear (custom_beak), matching the imported mesh.
/// </summary>
internal static class CharacterEarMesh
{
    private const int Cols=48;
    private static readonly float[] Rings={0,.16f,.32f,.48f,.60f,.72f,.82f,.90f,.95f,.985f,1};
    private const float Base=-.05f;

    /// <summary>Width, height, narrowing toward the tip, cup depth, forward fold (radians).</summary>
    private readonly record struct Shape(float Width,float Height,float Taper,float Cup,float Fold);

    private static Shape For(string species,int style)=>style switch
    {
        1=>new Shape(.37f,.27f,.06f,.050f,0f),    // round
        2=>new Shape(.27f,.42f,.48f,.040f,0f),    // pointed
        3=>new Shape(.31f,.35f,.30f,.045f,1.35f), // folded tip
        _=>species=="redpanda"?new Shape(.35f,.35f,.40f,.048f,0f):new Shape(.32f,.28f,.10f,.048f,0f),    // default: rounded triangle
    };

    // Shared by every rig of a species (per-character colors are surface overrides). Cleared on quit.
    private static readonly Dictionary<(string,int),ArrayMesh> Cache=new();
    public static void ClearCache()=>Cache.Clear();

    public static ArrayMesh Get(string species,int style,Material[] materials)
    {
        if(Cache.TryGetValue((species,style),out var mesh))return mesh;
        return Cache[(species,style)]=Build(species,style,materials);
    }

    private static ArrayMesh Build(string species,int style,Material[] materials)
    {
        var s=For(species,style);
        var vertices=new[]{new List<Vector3>(),new List<Vector3>()};
        var normals=new[]{new List<Vector3>(),new List<Vector3>()};
        for(int layer=0;layer<2;layer++)
        {
            float side=layer==0?1:-1; // 0 = front (hollow), 1 = back (fur)
            for(int r=0;r<Rings.Length-1;r++)for(int c=0;c<Cols;c++)
            {
                float u0=Rings[r],u1=Rings[r+1],v0=Mathf.Tau*c/Cols,v1=Mathf.Tau*(c+1)/Cols;
                // Concentric rings put the cream/fur boundary on a rounded contour, not a grid staircase.
                int surface=layer==0&&u1<=.72f?1:0;
                Quad(surface,side,(u0,v0),(u0,v1),(u1,v1),(u1,v0));
            }
        }
        var mesh=new ArrayMesh();
        for(int surface=0;surface<2;surface++)
        {
            var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=vertices[surface].ToArray();
            arrays[(int)Mesh.ArrayType.Normal]=normals[surface].ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
            mesh.SurfaceSetMaterial(surface,materials[Math.Min(surface,materials.Length-1)]);
        }
        return mesh;

        void Quad(int surface,float side,(float u,float v) a,(float u,float v) b,(float u,float v) c,(float u,float v) d)
        {
            Tri(surface,side,a,b,c);Tri(surface,side,a,c,d);
        }
        void Tri(int surface,float side,(float u,float v) a,(float u,float v) b,(float u,float v) c)
        {
            var pa=Point(s,a.u,a.v,side);var pb=Point(s,b.u,b.v,side);var pc=Point(s,c.u,c.v,side);
            if((pb-pa).Cross(pc-pa).LengthSquared()<1e-12f)return; // collapsed at the tip
            var na=Normal(s,a.u,a.v,side);var nb=Normal(s,b.u,b.v,side);var nc=Normal(s,c.u,c.v,side);
            Triangle(surface,pa,pb,pc,na,nb,nc);
        }
        void Triangle(int surface,Vector3 a,Vector3 b,Vector3 c,Vector3 na,Vector3 nb,Vector3 nc)
        {
            // Godot's front faces wind clockwise when seen from outside.
            if((b-a).Cross(c-a).Dot(na+nb+nc)>0){(b,c)=(c,b);(nb,nc)=(nc,nb);}
            vertices[surface].Add(a);vertices[surface].Add(b);vertices[surface].Add(c);
            normals[surface].Add(na);normals[surface].Add(nb);normals[surface].Add(nc);
        }
    }

    private static Vector3 Point(Shape s,float u,float v,float side)
    {
        float r=Mathf.Clamp(u,0,1),cs=Mathf.Cos(v),sn=Mathf.Sin(v);
        float taper=s.Taper;
        var center=new Vector2(0,Base+s.Height*.43f);
        var outline=new Vector2(sn*s.Width*.5f*(1-taper*cs),Base+s.Height*(cs+1)*.5f);
        var xy=center.Lerp(outline,r);
        float round=Mathf.Sqrt(Mathf.Max(0,1-r*r));
        // The plump rolled rim and cupped center meet the back on the same continuous outline.
        float z=side>0?.070f*round-(.070f+s.Cup*.42f)*round*round:-.060f*round-.035f*round*round;
        var p=new Vector3(xy.X,xy.Y,z);
        u=Mathf.Clamp((p.Y-Base)/s.Height,0,1);
        if(s.Fold>0&&u>.5f)
        {
            // Bend the upper half forward around a hinge at mid height.
            float bend=s.Fold*Step((u-.5f)/.5f);
            var hinge=new Vector3(0,Base+.5f*s.Height,0);
            var rel=p-hinge;
            p=hinge+new Vector3(rel.X,rel.Y*Mathf.Cos(bend)-rel.Z*Mathf.Sin(bend),rel.Y*Mathf.Sin(bend)+rel.Z*Mathf.Cos(bend));
        }
        return p;
    }

    private static Vector3 Normal(Shape s,float u,float v,float side)
    {
        if(u<.001f)return side>0?Vector3.Back:Vector3.Forward;
        u=Mathf.Clamp(u,.001f,.999f);
        var du=Point(s,u+.0005f,v,side)-Point(s,u-.0005f,v,side);
        var dv=Point(s,u,v+.001f,side)-Point(s,u,v-.001f,side);
        var n=dv.Cross(du).Normalized();
        return side>0?n:-n;
    }

    private static float Step(float x){x=Mathf.Clamp(x,0,1);return x*x*(3-2*x);}
}
