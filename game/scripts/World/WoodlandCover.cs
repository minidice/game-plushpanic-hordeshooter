using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Low, opaque grass blades, without a connecting base. Same world-space density as woodland_surface.</summary>
internal static class WoodlandCover
{
    private static readonly Dictionary<int,ArrayMesh> Meshes=new();
    private static ShaderMaterial? _material;

    public static float Density(Vector2 p)
    {
        float wave=.5f+.25f*Mathf.Sin(p.X*.53f+p.Y*.17f+.8f*Mathf.Sin(p.Y*.31f))
            +.25f*Mathf.Sin(p.Y*.29f-p.X*.24f);
        return Mathf.SmoothStep(.25f,.78f,wave);
    }

    public static ArrayMesh Mesh(int variant)
    {
        if(Meshes.TryGetValue(variant,out var mesh))return mesh;
        var rng=new RandomNumberGenerator { Seed=(ulong)(810+variant*71) };
        using var surface=new SurfaceTool();surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        void Vertex(Vector3 p,Vector3 normal,Color color){surface.SetNormal(normal);surface.SetColor(color.SrgbToLinear());surface.AddVertex(p);}
        for(int i=0;i<14;i++)
        {
            float a=rng.RandfRange(0,Mathf.Tau),r=Mathf.Sqrt(rng.Randf())*.38f;
            var at=new Vector3(Mathf.Cos(a)*r,-.015f,Mathf.Sin(a)*r);
            float yaw=rng.RandfRange(0,Mathf.Tau),height=rng.RandfRange(.30f,.68f),width=rng.RandfRange(.038f,.068f);
            var across=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw));
            var lean=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*height*.35f;
            var mid=at+Vector3.Up*height*.58f+lean*.25f;
            var tip=at+Vector3.Up*height+lean;
            var normal=(Vector3.Up*.8f+across.Cross(Vector3.Up)*.35f).Normalized();
            var root=new Color("354c2e");var middle=new Color(variant==0?"627843":"526d3e");var top=new Color("87994f");
            Vertex(at-across*width,normal,root);Vertex(at+across*width,normal,root);Vertex(mid+across*width*.55f,normal,middle);
            Vertex(at-across*width,normal,root);Vertex(mid+across*width*.55f,normal,middle);Vertex(mid-across*width*.55f,normal,middle);
            Vertex(mid-across*width*.55f,normal,middle);Vertex(mid+across*width*.55f,normal,middle);Vertex(tip,normal,top);
        }
        _material ??= new ShaderMaterial { Shader=GD.Load<Shader>("res://shaders/woodland_grass.gdshader") };
        surface.SetMaterial(_material);surface.Index();mesh=surface.Commit();Meshes[variant]=mesh;return mesh;
    }

    public static void Populate(Node parent,Rect2 region,EdgeSide side)
    {
        bool along=side is EdgeSide.Far or EdgeSide.Near;
        bool negative=side is EdgeSide.Far or EdgeSide.West;
        float edge=side switch { EdgeSide.Far=>region.End.Y,EdgeSide.Near=>region.Position.Y,EdgeSide.West=>region.End.X,_=>region.Position.X };
        float from=along?region.Position.X:region.Position.Y,to=along?region.End.X:region.End.Y;
        float depth=Mathf.Min(14,along?region.Size.Y:region.Size.X);
        var rng=new RandomNumberGenerator { Seed=(ulong)(int)(910+Mathf.Abs(region.Position.X*19+region.Position.Y*43)) };
        for(float start=from;start<to;start+=12)
        {
            var groups=new[]{new List<Transform3D>(),new List<Transform3D>()};
            for(float t=start+.4f;t<Mathf.Min(to,start+12)-.35f;t+=.65f)
                for(float d=.42f;d<depth;d+=.65f)
                {
                    float run=t+rng.RandfRange(-.22f,.22f),offset=d+rng.RandfRange(-.2f,.2f);
                    var p=along?new Vector2(run,edge+(negative?-offset:offset)):new Vector2(edge+(negative?-offset:offset),run);
                    float density=Density(p);
                    if(rng.Randf()>.18f+.77f*density)continue;
                    float scale=rng.RandfRange(.7f,1.15f)*Mathf.Lerp(.45f,1,density);
                    int variant=rng.RandiRange(0,1);
                    var basis=new Basis(Vector3.Up,rng.RandfRange(0,Mathf.Tau)).Scaled(new Vector3(scale,scale*.95f,scale));
                    var at=new Vector3(p.X,.015f,p.Y);
                    var bounds=new Transform3D(basis,at)*Mesh(variant).GetAabb();
                    // All blade tips must remain in this inaccessible strip, including zone corners.
                    if(!region.Encloses(new Rect2(bounds.Position.X,bounds.Position.Z,bounds.Size.X,bounds.Size.Z)))continue;
                    groups[variant].Add(new Transform3D(basis,at));
                }
            for(int variant=0;variant<2;variant++)
            {
                var items=groups[variant];if(items.Count==0)continue;
                var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=Mesh(variant),InstanceCount=items.Count };
                for(int i=0;i<items.Count;i++)multi.SetInstanceTransform(i,items[i]);
                parent.AddChild(new MultiMeshInstance3D { Name="WoodlandGrass",Multimesh=multi,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off },true);
            }
        }
    }
}
