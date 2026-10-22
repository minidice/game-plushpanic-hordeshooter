using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Close-range, non-colliding rock outcrops. Broad uneven faces and moss shelves replace repeated pillars.
/// Six shared meshes are instanced per short zone so offscreen zones can be culled.</summary>
internal static class OutdoorEdgeArt
{
    private static readonly Dictionary<int,ArrayMesh> Rocks = new();
    private static readonly Dictionary<int,ArrayMesh> Pines = new();
    private static readonly StandardMaterial3D Stone = new() { VertexColorUseAsAlbedo=true, VertexColorIsSrgb=true, Roughness=.96f };

    public static ArrayMesh Rock(int variant)
    {
        if(Rocks.TryGetValue(variant,out var cached)) return cached;
        var rng=new RandomNumberGenerator { Seed=(ulong)(346+variant*171) };
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        const int n=7;
        var rings=new Vector3[4,n];
        float tiltX=rng.RandfRange(-.14f,.14f),tiltZ=rng.RandfRange(-.12f,.12f);
        for(int i=0;i<n;i++)
        {
            float angle=Mathf.Tau*i/n+rng.RandfRange(-.19f,.19f);
            float radius=rng.RandfRange(.75f,1.16f);
            float x=Mathf.Cos(angle)*radius*.5f,z=Mathf.Sin(angle)*radius*.5f;
            rings[0,i]=new(x*.92f,-.10f,z*.92f);
            rings[1,i]=new(x,.18f+rng.RandfRange(-.08f,.08f),z);
            float shoulder=rng.RandfRange(.66f,.86f);
            rings[2,i]=new(x*rng.RandfRange(.84f,1.04f)+.035f,shoulder+x*tiltX+z*tiltZ,z*rng.RandfRange(.82f,1.03f));
            rings[3,i]=new(x*.78f-.07f,.91f+x*tiltX+z*tiltZ,z*.82f+.065f);
        }
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color,Vector3 normal)
        {
            if((b-a).Cross(c-a).Dot(normal)>0)(b,c)=(c,b);
            foreach(var p in new[]{a,b,c}){s.SetNormal(normal);s.SetColor(color);s.AddVertex(p);}
        }
        for(int r=0;r<3;r++)for(int i=0;i<n;i++)
        {
            int j=(i+1)%n;var a=rings[r,i];var b=rings[r,j];var c=rings[r+1,j];var d=rings[r+1,i];
            var normal=(b-a).Cross(d-a).Normalized();if(normal.Dot(new Vector3(a.X,0,a.Z))<0)normal=-normal;
            var col=new Color(r==2?"75827e":r==0?"4d6068":"617580");
            col=col.Darkened(rng.RandfRange(0,.13f));
            Triangle(a,b,c,col,normal);Triangle(a,c,d,col,normal);
        }
        // Inset moss cap follows the top plane, with an uneven edge exposing a stone lip.
        Vector3 centre=new(-.07f,.91f,.065f);
        for(int i=0;i<n;i++)
        {
            int j=(i+1)%n;var a=rings[3,i];var b=rings[3,j];
            var normal=(a-centre).Cross(b-centre).Normalized();if(normal.Y<0)normal=-normal;
            Triangle(centre,a,b,new Color("748378"),normal);
            float insetA=.87f+.07f*Mathf.Sin(i*2.7f+variant),insetB=.87f+.07f*Mathf.Sin(j*2.7f+variant);
            Triangle(centre+Vector3.Up*.006f,centre.Lerp(a,insetA)+Vector3.Up*.006f,centre.Lerp(b,insetB)+Vector3.Up*.006f,
                new Color(variant%2==0?"5d7c48":"527246"),normal);
        }
        s.SetMaterial(Stone);var mesh=s.Commit();Rocks.Add(variant,mesh);return mesh;
    }

    private static ArrayMesh Pine(int variant)
    {
        if(Pines.TryGetValue(variant,out var mesh))return mesh;
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        var rng=new RandomNumberGenerator{Seed=(ulong)(920+variant)};
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            var normal=(b-a).Cross(c-a).Normalized();
            if(normal.Dot(new Vector3((a.X+b.X+c.X)/3,.12f,(a.Z+b.Z+c.Z)/3))<0)normal=-normal;
            if((b-a).Cross(c-a).Dot(normal)>0)(b,c)=(c,b);
            foreach(var p in new[]{a,b,c}){s.SetNormal(normal);s.SetColor(color);s.AddVertex(p);}
        }
        // A visible short trunk under overlapping, scalloped branch skirts.
        for(int i=0;i<7;i++)
        {
            float a=Mathf.Tau*i/7,b=Mathf.Tau*(i+1)/7;
            var p=new Vector3(Mathf.Cos(a)*.06f,0,Mathf.Sin(a)*.06f);
            var q=new Vector3(Mathf.Cos(b)*.06f,0,Mathf.Sin(b)*.06f);
            Tri(p,q,q+Vector3.Up*.7f,new Color("544f3c"));Tri(p,q+Vector3.Up*.7f,p+Vector3.Up*.7f,new Color("544f3c"));
        }
        for(int layer=0;layer<4;layer++)
        {
            float y=.4f+layer*.62f,r=.76f-layer*.14f;
            var rim=new Vector3[12];var shoulder=new Vector3[12];
            for(int i=0;i<12;i++)
            {
                float angle=Mathf.Tau*i/12+layer*.37f;
                float w=r*rng.RandfRange(.85f,1.10f)*(i%2==0?1:.87f);
                rim[i]=new Vector3(Mathf.Cos(angle)*w,y+rng.RandfRange(-.09f,.07f),Mathf.Sin(angle)*w);
                shoulder[i]=new Vector3(Mathf.Cos(angle)*r*.7f+.04f,y+.31f,Mathf.Sin(angle)*r*.7f);
            }
            var top=new Vector3(.07f,y+1.04f,-.025f);
            for(int i=0;i<12;i++)
            {
                int j=(i+1)%12;
                var col=new Color(layer==3?"638746":layer==2?"4b7548":"355e46").Darkened(rng.RandfRange(0,.1f));
                Tri(rim[i],rim[j],shoulder[j],col.Darkened(.12f));Tri(rim[i],shoulder[j],shoulder[i],col.Darkened(.12f));
                Tri(shoulder[i],shoulder[j],top,col);
                Tri(new Vector3(0,y+.07f,0),rim[j],rim[i],new Color("294e3c"));
            }
        }
        s.SetMaterial(Stone);mesh=s.Commit();Pines.Add(variant,mesh);return mesh;
    }

    public static void Grove(Node parent,float from,float to,float edge,int seed)
    {
        var rng=new RandomNumberGenerator{Seed=(ulong)seed};
        for(int variant=0;variant<4;variant++)
        {
            var transforms=new List<Transform3D>();
            for(float x=from+variant*.85f;x<to;x+=rng.RandfRange(3.7f,5.2f))
            {
                float size=rng.RandfRange(.88f,1.3f);
                float z=edge-3.8f-variant*.65f-Mathf.Sin(x*.39f)*1.1f;
                var basis=new Basis(Vector3.Up,rng.RandfRange(0,Mathf.Tau)).Scaled(new Vector3(size,size*rng.RandfRange(.88f,1.2f),size));
                transforms.Add(new Transform3D(basis,new Vector3(x,-.08f,z)));
            }
            var multi=new MultiMesh{TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=Pine(variant),InstanceCount=transforms.Count};
            for(int i=0;i<transforms.Count;i++)multi.SetInstanceTransform(i,transforms[i]);
            parent.AddChild(new MultiMeshInstance3D{Name="ClosePineGrove",Multimesh=multi},true);
        }
    }

    /// <summary>at is at the foot of the inaccessible bank, away is +/- Z; low banks never exceed 0.8m.</summary>
    public static void Bank(Node parent,float from,float to,float edge,float away,bool low=false,int seed=31)
    {
        var rng=new RandomNumberGenerator { Seed=(ulong)seed };
        var groups=new Dictionary<int,List<Transform3D>>();
        void Add(Vector3 at,Vector3 size)
        {
            int id=rng.RandiRange(0,5);
            var basis=new Basis(Vector3.Up,rng.RandfRange(-.7f,.7f)).Scaled(size);
            // Rotation can project a wide rock past the nominal bank or into the neighbouring wider zone.
            // Clamp the actual mesh envelope, not just its pivot, to this inaccessible strip.
            var bounds=new Transform3D(basis,at)*Rock(id).GetAabb();
            if(bounds.Size.X>to-from-.1f)return;
            at.X+=Mathf.Clamp(bounds.Position.X,from+.05f,to-.05f-bounds.Size.X)-bounds.Position.X;
            if(away<0)at.Z-=Mathf.Max(0,bounds.End.Z-edge);
            else at.Z+=Mathf.Max(0,edge-bounds.Position.Z);
            if(!groups.TryGetValue(id,out var list))groups[id]=list=new();
            list.Add(new Transform3D(basis,at));
        }
        for(float x=from+1.6f;x<to;x+=rng.RandfRange(4.5f,6.7f))
        {
            float h=low?rng.RandfRange(.42f,.74f):rng.RandfRange(1.7f,2.8f);
            float z=edge+away*(low?1.1f:1.9f);
            Add(new Vector3(x,0,z),new Vector3(rng.RandfRange(3.4f,5.7f),h,low?2.0f:rng.RandfRange(2.7f,3.8f)));
            Add(new Vector3(x-1.8f,-.08f,z-away*.5f),new Vector3(2.7f,h*.56f,2.1f));
            Add(new Vector3(x+1.4f,-.06f,z-away*.7f),new Vector3(2.3f,h*.42f,1.8f));
            if(!low)Add(new Vector3(x+.7f,h*.55f,z+away*.7f),new Vector3(2.8f,h*.61f,2.0f));
            // Loose stones partly buried in the foot, all beyond the movement boundary.
            for(int i=0;i<3;i++)Add(new Vector3(x+rng.RandfRange(-2.5f,2.5f),-.07f,edge+away*rng.RandfRange(.45f,1.1f)),
                new Vector3(rng.RandfRange(.35f,.7f),rng.RandfRange(.19f,.35f),rng.RandfRange(.3f,.65f)));
        }
        foreach(var(id,transforms)in groups)
        {
            var multi=new MultiMesh{TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=Rock(id),InstanceCount=transforms.Count};
            for(int i=0;i<transforms.Count;i++)multi.SetInstanceTransform(i,transforms[i]);
            parent.AddChild(new MultiMeshInstance3D{Name="CloseRockBank",Multimesh=multi,
                CastShadow=low?GeometryInstance3D.ShadowCastingSetting.Off:GeometryInstance3D.ShadowCastingSetting.On},true);
        }
    }
}
