using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>One silhouette language for individual trees and instanced woodland. No base slabs or colliders.</summary>
internal static class FoliageArt
{
    public enum Kind { Pine, Broadleaf, Shrub }
    private static readonly Dictionary<(Kind,int),ArrayMesh> Cache = new();
    private static ShaderMaterial? _material;
    private static ShaderMaterial Material => _material ??= new ShaderMaterial { Shader=GD.Load<Shader>("res://shaders/foliage.gdshader") };

    public static ArrayMesh Mesh(Kind kind,int variant=0)
    {
        variant = Mathf.Abs(variant)%4;
        if(Cache.TryGetValue((kind,variant),out var cached))return cached;
        using var surface=new SurfaceTool();surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var rng=new RandomNumberGenerator { Seed=(ulong)(1827+variant*53+(int)kind*371) };
        // Vertex alpha is a material mask: leaves=1, bark=0. RGB is linear for shader use.
        void Vertex(Vector3 p,Vector3 normal,Color color,float leaf)
        {
            var linear=color.SrgbToLinear();linear.A=leaf;
            surface.SetColor(linear);surface.SetNormal(normal.Normalized());surface.AddVertex(p);
        }
        void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 na,Vector3 nb,Vector3 nc,Color col,float leaf=1)
        {
            if((b-a).Cross(c-a).Dot(na+nb+nc)>0){(b,c)=(c,b);(nb,nc)=(nc,nb);}
            Vertex(a,na,col,leaf);Vertex(b,nb,col,leaf);Vertex(c,nc,col,leaf);
        }
        void Stem(Vector3 a,Vector3 b,float r)
        {
            var up=(b-a).Normalized();var right=up.Cross(Vector3.Forward).Normalized();var forward=right.Cross(up);
            for(int i=0;i<8;i++)
            {
                float t=Mathf.Tau*i/8,u=Mathf.Tau*(i+1)/8;
                var n=right*Mathf.Cos(t)+forward*Mathf.Sin(t);var m=right*Mathf.Cos(u)+forward*Mathf.Sin(u);
                Tri(a+n*r,a+m*r,b+m*r*.65f,n,m,m,new Color("695447"),0);
                Tri(a+n*r,b+m*r*.65f,b+n*r*.65f,n,m,n,new Color("695447"),0);
            }
        }
        // Uneven, broad foliage masses; no leaf-pad carpet and no spherical primitive silhouette.
        void Crown(Vector3 at,Vector3 size,int seed)
        {
            const int n=24; const int rows=7;
            var pts=new Vector3[rows,n];var normals=new Vector3[rows,n];
            float[] radius={.12f,.63f,.92f,1f,.86f,.52f,.015f};
            float[] heights={-.72f,-.54f,-.26f,.08f,.44f,.73f,.88f};
            for(int row=0;row<rows;row++)for(int i=0;i<n;i++)
            {
                float angle=Mathf.Tau*i/n;
                float edge=1+.105f*Mathf.Sin(angle*5+seed+row*.18f)+.075f*Mathf.Sin(angle*3+seed*1.7f);
                var p=new Vector3(Mathf.Cos(angle)*radius[row]*edge,heights[row]+.045f*Mathf.Sin(angle*3+seed),Mathf.Sin(angle)*radius[row]*edge);
                pts[row,i]=at+p*size;
                normals[row,i]=new Vector3(p.X/size.X,p.Y/size.Y,p.Z/size.Z).Normalized();
            }
            for(int row=0;row<rows-1;row++)for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;var col=new Color("60834e");
                Tri(pts[row,i],pts[row,j],pts[row+1,j],normals[row,i],normals[row,j],normals[row+1,j],col);
                Tri(pts[row,i],pts[row+1,j],pts[row+1,i],normals[row,i],normals[row+1,j],normals[row+1,i],col);
            }
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                Tri(at+Vector3.Down*size.Y*.65f,pts[0,j],pts[0,i],Vector3.Down,normals[0,j],normals[0,i],new Color("34553f"));
                Tri(at+Vector3.Up*size.Y*.90f,pts[rows-1,i],pts[rows-1,j],Vector3.Up,normals[rows-1,i],normals[rows-1,j],new Color("60834e"));
            }
        }
        if(kind==Kind.Pine)
        {
            Stem(new Vector3(0,-.08f,0),new Vector3(.06f,3.7f,0),.15f);
            for(int layer=0;layer<4;layer++)
            {
                const int n=24;
                float radius=1.12f-layer*.225f,y=.70f+layer*.73f;
                var center=new Vector3(.07f*Mathf.Sin(layer+variant),y,.06f*Mathf.Cos(layer*2+variant));
                var rings=new Vector3[4,n];var norms=new Vector3[4,n];
                float[] widths={.89f,1,.72f,.025f};float[] levels={.025f,.14f,.48f,1.20f};
                for(int row=0;row<4;row++)for(int i=0;i<n;i++)
                {
                    float a=Mathf.Tau*i/n+layer*.43f;
                    float scallop=1+.18f*Mathf.Cos(a*6+variant)+.065f*Mathf.Sin(a*3+layer);
                    float h=levels[row]+(row<2?-.095f*Mathf.Cos(a*6+variant):.025f*Mathf.Sin(a*3));
                    rings[row,i]=center+new Vector3(Mathf.Cos(a)*radius*widths[row]*scallop,h,Mathf.Sin(a)*radius*widths[row]*scallop);
                    norms[row,i]=new Vector3(Mathf.Cos(a),row==0?-.55f:row==1?.35f:row==2?.80f:.45f,Mathf.Sin(a)).Normalized();
                }
                for(int row=0;row<3;row++)for(int i=0;i<n;i++)
                {
                    int j=(i+1)%n;var col=new Color("335c45").Lerp(new Color("719348"),.34f+layer*.14f);
                    Tri(rings[row,i],rings[row,j],rings[row+1,j],norms[row,i],norms[row,j],norms[row+1,j],col);
                    Tri(rings[row,i],rings[row+1,j],rings[row+1,i],norms[row,i],norms[row+1,j],norms[row+1,i],col);
                }
                for(int i=0;i<n;i++)
                {
                    int j=(i+1)%n;
                    Tri(center,rings[0,j],rings[0,i],Vector3.Down,Vector3.Down,Vector3.Down,new Color("2d513e"));
                    Tri(center+Vector3.Up*1.23f,rings[3,i],rings[3,j],Vector3.Up,norms[3,i],norms[3,j],new Color("70934c"));
                }
            }
        }
        else if(kind==Kind.Broadleaf)
        {
            Stem(new Vector3(0,-.08f,0),new Vector3(.12f,2.5f,0),.23f);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.Tau/4+variant*.53f;
                var at=new Vector3(Mathf.Cos(a)*.67f,2.20f+rng.RandfRange(-.15f,.3f),Mathf.Sin(a)*.67f);
                Stem(new Vector3(.03f,1.15f,0),at,.09f);
                Crown(at,new Vector3(.91f,.86f,.87f),variant+i*7);
            }
            Crown(new Vector3(.10f,2.97f,-.12f),new Vector3(.85f,.64f,.83f),variant+21);
        }
        else
        {
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.Tau/3+variant*.8f;
                Crown(new Vector3(Mathf.Cos(a)*.32f,.32f+i*.05f,Mathf.Sin(a)*.27f),new Vector3(.5f,.46f,.46f),i+variant*7);
            }
        }
        surface.SetMaterial(Material);surface.Index();var mesh=surface.Commit();Cache[(kind,variant)]=mesh;return mesh;
    }

    public static void Instances(Node parent,string name,Kind kind,int variant,IReadOnlyList<Transform3D> transforms,bool shadows)
    {
        if(transforms.Count==0)return;
        var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=Mesh(kind,variant),InstanceCount=transforms.Count };
        for(int i=0;i<transforms.Count;i++)multi.SetInstanceTransform(i,transforms[i]);
        parent.AddChild(new MultiMeshInstance3D { Name=name,Multimesh=multi,CastShadow=shadows?GeometryInstance3D.ShadowCastingSetting.On:GeometryInstance3D.ShadowCastingSetting.Off },true);
    }

    public static bool ReplaceTree(Node3D model,string id)
    {
        if(id is not ("pine_tall" or "pine_small" or "broadleaf" or "round_oak" or "round_linden"))return false;
        foreach(Node child in model.GetChildren())child.Free();
        bool pine=id.StartsWith("pine");float scale=id=="pine_small"?.68f:id=="round_linden"?.8f:1;
        model.AddChild(new MeshInstance3D { Name="UnifiedCanopy",Mesh=Mesh(pine?Kind.Pine:Kind.Broadleaf,id=="round_oak"?2:0),Scale=Vector3.One*scale });
        return true;
    }
}
