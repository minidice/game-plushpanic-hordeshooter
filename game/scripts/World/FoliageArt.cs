using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>One silhouette language for individual trees and instanced woodland. No base slabs or colliders.</summary>
internal static class FoliageArt
{
    public enum Kind { Pine, Broadleaf, Shrub }
    private static readonly Dictionary<(Kind,int,int),ArrayMesh> Cache = new();
    private static ShaderMaterial? _material;
    private static ShaderMaterial Material => _material ??= new ShaderMaterial { Shader=GD.Load<Shader>("res://shaders/foliage.gdshader") };

    public static ArrayMesh Mesh(Kind kind,int variant=0,int detail=0)
    {
        variant = Mathf.Abs(variant)%4;
        detail=Mathf.Clamp(detail,0,2);
        if(Cache.TryGetValue((kind,variant,detail),out var cached))return cached;
        using var surface=new SurfaceTool();surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var rng=new RandomNumberGenerator { Seed=(ulong)(1827+variant*53+(int)kind*371) };
        var form=variant switch { 1=>new Vector3(1.08f,.90f,1.02f),2=>new Vector3(.90f,1.10f,.94f),3=>new Vector3(1.02f,.97f,1.10f),_=>Vector3.One };
        // Vertex alpha is a material mask: leaves=1, bark=0. RGB is linear for shader use.
        void Vertex(Vector3 p,Vector3 normal,Color color,float leaf)
        {
            var linear=color.SrgbToLinear();linear.A=leaf;
            surface.SetColor(linear);surface.SetNormal((normal/form).Normalized());surface.AddVertex(p*form);
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
            int n=detail==0?36:detail==1?24:12; const int rows=7;
            var pts=new Vector3[rows,n];var normals=new Vector3[rows,n];
            float[] radius={.18f,.85f,1f,.94f,.78f,.43f,.015f};
            float[] heights={-.56f,-.37f,-.14f,.22f,.53f,.78f,.92f};
            for(int row=0;row<rows;row++)for(int i=0;i<n;i++)
            {
                float angle=Mathf.Tau*i/n;
                float lobe=.09f*Mathf.Sin(angle*5+seed)+.06f*Mathf.Sin(angle*3+seed*1.7f);
                float tooth=detail<2&&row is 1 or 2?(i%2==0?(.045f+.025f*Mathf.Sin(i*1.7f+seed)):-.025f)*(detail==0?1:.55f):0;
                float edge=1+lobe+tooth;
                var p=new Vector3(Mathf.Cos(angle)*radius[row]*edge,heights[row]+.045f*Mathf.Sin(angle*3+seed)-tooth*1.1f,Mathf.Sin(angle)*radius[row]*edge);
                pts[row,i]=at+p*size;
                normals[row,i]=new Vector3(p.X/size.X,p.Y/size.Y,p.Z/size.Z).Normalized();
            }
            for(int row=0;row<rows-1;row++)for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                var col=new Color("6c8748");
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
            // Stop inside the broad part of the final crown; the rounded tip has no exposed trunk.
            Stem(new Vector3(0,-.08f,0),new Vector3(.06f,3.18f,0),.16f);
            for(int r=0;r<4;r++)
            {
                float a=r*Mathf.Tau/4+variant;
                Stem(new Vector3(Mathf.Cos(a)*.40f,-.035f,Mathf.Sin(a)*.40f),new Vector3(0,.3f,0),.08f);
            }
            for(int layer=0;layer<4;layer++)
            {
                int n=detail==0?48:detail==1?24:16;
                float radius=new[]{1.22f,1.06f,.80f,.49f}[layer]*(1+.04f*Mathf.Sin(variant*3+layer));
                float y=new[]{.62f,1.43f,2.20f,2.91f}[layer];
                float height=new[]{1.30f,1.18f,1.05f,.88f}[layer];
                var center=new Vector3(.10f*Mathf.Sin(layer+variant),y,.08f*Mathf.Cos(layer*2+variant));
                var rings=new Vector3[6,n];var norms=new Vector3[6,n];
                float[] widths={.57f,1,1.01f,.88f,.54f,.018f};float[] levels={-.055f,0,.27f,.56f,.81f,1};
                for(int row=0;row<6;row++)for(int i=0;i<n;i++)
                {
                    float a=Mathf.Tau*i/n+layer*.43f;
                    float scallop=1+.065f*Mathf.Sin(a*7+variant)+.045f*Mathf.Sin(a*3+layer);
                    float tooth=detail<2&&row==1?(i%2==0?(.055f+.025f*Mathf.Sin(i*1.7f+variant+layer)):-.025f)*(detail==0?1:.55f):0;
                    float h=levels[row]*height+(row<3?-.05f*Mathf.Sin(a*7+variant):.025f*Mathf.Sin(a*3))-tooth*1.3f;
                    rings[row,i]=center+new Vector3(Mathf.Cos(a)*radius*(widths[row]*scallop+tooth),h,Mathf.Sin(a)*radius*(widths[row]*scallop+tooth));
                    float up=new[]{-.85f,-.3f,.08f,.60f,1.15f,.55f}[row];
                    norms[row,i]=new Vector3(Mathf.Cos(a),up,Mathf.Sin(a)).Normalized();
                }
                for(int row=0;row<5;row++)for(int i=0;i<n;i++)
                {
                    int j=(i+1)%n;
                    var col=new Color("78934b");
                    Tri(rings[row,i],rings[row,j],rings[row+1,j],norms[row,i],norms[row,j],norms[row+1,j],col);
                    Tri(rings[row,i],rings[row+1,j],rings[row+1,i],norms[row,i],norms[row+1,j],norms[row+1,i],col);
                }
                for(int i=0;i<n;i++)
                {
                    int j=(i+1)%n;
                    Tri(center,rings[0,j],rings[0,i],Vector3.Down,Vector3.Down,Vector3.Down,new Color("2d513e"));
                    Tri(center+Vector3.Up*(height+.02f),rings[5,i],rings[5,j],Vector3.Up,norms[5,i],norms[5,j],new Color("8b9e50"));
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
        surface.SetMaterial(Material);surface.Index();var mesh=surface.Commit();Cache[(kind,variant,detail)]=mesh;return mesh;
    }

    public static void Instances(Node parent,string name,Kind kind,int variant,IReadOnlyList<Transform3D> transforms,bool shadows,int detail=0)
    {
        if(transforms.Count==0)return;
        var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=Mesh(kind,variant,detail),InstanceCount=transforms.Count };
        for(int i=0;i<transforms.Count;i++)multi.SetInstanceTransform(i,transforms[i]);
        parent.AddChild(new MultiMeshInstance3D { Name=name,Multimesh=multi,CastShadow=shadows?GeometryInstance3D.ShadowCastingSetting.On:GeometryInstance3D.ShadowCastingSetting.Off },true);
    }

}
