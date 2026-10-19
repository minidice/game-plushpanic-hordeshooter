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
    private const int Rows=16,Cols=14;
    private const float Base=-.05f;

    /// <summary>Width, height, how square the shoulders are, how fast the sides close in, cup depth, forward fold (radians).</summary>
    private readonly record struct Shape(float Width,float Height,float Shoulder,float Taper,float Cup,float Fold);

    private static Shape For(int style)=>style switch
    {
        1=>new Shape(.37f,.27f,2.3f,.55f,.050f,0f),    // round
        2=>new Shape(.27f,.42f,1.0f,.95f,.040f,0f),    // pointed
        3=>new Shape(.31f,.35f,1.7f,.72f,.045f,1.35f), // folded tip
        _=>new Shape(.31f,.33f,1.7f,.72f,.048f,0f),    // default: rounded triangle
    };

    // Shared by every rig of a species (per-character colors are surface overrides). Cleared on quit.
    private static readonly Dictionary<(string,int),ArrayMesh> Cache=new();
    public static void ClearCache()=>Cache.Clear();

    public static ArrayMesh Get(string species,int style,Material[] materials)
    {
        if(Cache.TryGetValue((species,style),out var mesh))return mesh;
        return Cache[(species,style)]=Build(style,materials);
    }

    private static ArrayMesh Build(int style,Material[] materials)
    {
        var s=For(style);
        var vertices=new[]{new List<Vector3>(),new List<Vector3>()};
        var normals=new[]{new List<Vector3>(),new List<Vector3>()};
        for(int layer=0;layer<2;layer++)
        {
            float side=layer==0?1:-1; // 0 = front (hollow), 1 = back (fur)
            for(int r=0;r<Rows;r++)for(int c=0;c<Cols;c++)
            {
                float u0=(float)r/Rows,u1=(float)(r+1)/Rows,v0=-1+2f*c/Cols,v1=-1+2f*(c+1)/Cols;
                float um=(u0+u1)*.5f,vm=(v0+v1)*.5f;
                int surface=layer==0&&Mathf.Abs(vm)<.62f&&um<.80f?1:0;
                Quad(surface,side,(u0,v0),(u0,v1),(u1,v1),(u1,v0));
            }
        }
        // Close the base (hidden inside the head) so the shell has no open edge.
        for(int c=0;c<Cols;c++)
        {
            float v0=-1+2f*c/Cols,v1=-1+2f*(c+1)/Cols;
            var a=Point(s,0,v0,1);var b=Point(s,0,v1,1);var d=Point(s,0,v0,-1);var e=Point(s,0,v1,-1);
            Triangle(0,a,b,e,Vector3.Down,Vector3.Down,Vector3.Down);Triangle(0,a,e,d,Vector3.Down,Vector3.Down,Vector3.Down);
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
        u=Mathf.Clamp(u,0,1);v=Mathf.Clamp(v,-1,1);
        float half=s.Width*.5f*Mathf.Pow(Mathf.Max(0,1-Mathf.Pow(u,s.Shoulder)),s.Taper);
        float across=Mathf.Sqrt(Mathf.Max(0,1-v*v));
        // Thickness closes to zero at the rim, so front and back meet without a seam.
        float thick=.034f*across*Mathf.Sqrt(Mathf.Max(0,1-u));
        float cup=-s.Cup*across*Mathf.Sin(Mathf.Pi*Mathf.Min(1,u*1.1f+.05f));
        var p=new Vector3(v*half,Base+u*s.Height,cup+(side>0?thick*.35f:-thick*.65f)-.005f);
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
        u=Mathf.Clamp(u,.001f,.985f);v=Mathf.Clamp(v,-.985f,.985f);
        var du=Point(s,u+.002f,v,side)-Point(s,u-.002f,v,side);
        var dv=Point(s,u,v+.002f,side)-Point(s,u,v-.002f,side);
        var n=dv.Cross(du).Normalized();
        // dv × du points to +Z on the flat front; the back layer faces the other way.
        return side>0?n:-n;
    }

    private static float Step(float x){x=Mathf.Clamp(x,0,1);return x*x*(3-2*x);}
}
