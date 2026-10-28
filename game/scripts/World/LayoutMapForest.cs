using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>A measured warm-forest art study, with the existing map's terrain and movement contract.</summary>
public sealed partial class LayoutMap
{
    public bool ForestStudyEnabled { get; init; } = true;
    /// <summary>False retains the original 20m prototype for reproducible comparisons.</summary>
    public bool ForestExpanded { get; init; } = true;
    public static readonly Rect2 ForestStudyBounds=new(84,-34,20,20);
    public int ForestGrassCount { get; private set; }
    public int ForestTreeCount { get; private set; }

    private ShaderMaterial ForestMaterial(string shader)
    {
        var material=new ShaderMaterial { Shader=GD.Load<Shader>(shader) };
        material.SetShaderParameter("forest_enabled",ForestStudyEnabled);
        material.SetShaderParameter("forest_expanded",ForestExpanded);
        material.SetShaderParameter("forest_surface_quality",ForestStudyEnabled);
        var segments=new List<Vector4>();
        foreach(var route in Layout.Routes.Values)for(int i=0;i+1<route.Length;i++)
            if(Mathf.Min(route[i].X,route[i+1].X)<282)
                segments.Add(new Vector4(route[i].X,route[i].Y,route[i+1].X,route[i+1].Y));
        foreach(var link in Layout.Links.Where(l=>l.Type=="ramp" && l.From.X<282))
            segments.Add(new Vector4(link.From.X,link.From.Y,link.To.X,link.To.Y));
        material.SetShaderParameter("forest_route_count",segments.Count);
        while(segments.Count<32)segments.Add(Vector4.Zero);
        material.SetShaderParameter("forest_routes",segments.ToArray());
        return material;
    }

    public static float ForestWeight(Vector2 point,bool expanded=false)
    {
        if(expanded)
        {
            float west=Mathf.SmoothStep(32,46,point.X)*(1-Mathf.SmoothStep(166,192,point.X));
            float east=Mathf.SmoothStep(208,224,point.X)*(1-Mathf.SmoothStep(248,282,point.X));
            return Mathf.Max(west,east);
        }
        var d=(point-ForestStudyBounds.GetCenter()).Abs();
        return Mathf.SmoothStep(0,3,10-Mathf.Max(d.X,d.Y));
    }

    private void BuildForestStudy()
    {
        if(ForestExpanded)BuildForestExpansion();else BuildForestPrototype();
    }

    private void BuildForestPrototype()
    {
        var trees=new List<Vector2>();
        foreach(var (x,z,scale,oak) in new (float,float,float,bool)[]{
            (88,-17,.88f,false),(94,-16,.82f,true),(101,-18,.95f,false),
            (101,-31,1.04f,false),(95,-32,.9f,false),(87,-29,.86f,true)})
        {
            var p=new Vector2(x,z);
            if(!Flat(p,1) || PathDistance(p)<3.8f)continue;
            var model=Prop(oak?"forest_broadleaf":"forest_pine",FloorAt(p),x*19,scale);
            if(model==null)continue;
            PropOcclusion.Attach(_stage,model);trees.Add(p);ForestTreeCount++;
        }
        var root=new Node3D{Name="ForestQualityStudy"};_stage.AddChild(root);
        var rng=new RandomNumberGenerator { Seed=241024 };
        var groups=new Dictionary<(int,int,int),List<Transform3D>>();
        for(float x=84.2f;x<103.9f;x+=.36f)for(float z=-33.8f;z<-14.1f;z+=.36f)
        {
            var p=new Vector2(x+rng.RandfRange(-.14f,.14f),z+rng.RandfRange(-.14f,.14f));
            float distance=PathDistance(p),weight=ForestWeight(p);
            float cluster=.72f+.22f*Mathf.Sin(p.X*1.5f+Mathf.Sin(p.Y*1.7f));
            if(distance<2.3f || rng.Randf()>weight*cluster*Mathf.SmoothStep(2.3f,3.7f,distance) || !Flat(p,.3f)
                || trees.Any(t=>t.DistanceTo(p)<.4f))continue;
            int kind=rng.Randf()<.65f?0:1;
            float size=rng.RandfRange(.7f,1.25f);
            var transform=new Transform3D(new Basis(Vector3.Up,rng.Randf()*Mathf.Tau).Scaled(new Vector3(size,size*rng.RandfRange(.7f,1.1f),size)),FloorAt(p)+Vector3.Up*.015f);
            var key=((int)((x-84)/5),(int)((z+34)/5),kind);
            if(!groups.TryGetValue(key,out var list)){list=new();groups.Add(key,list);}list.Add(transform);
        }
        AddForestGrass(root,groups,rng,false);
        root.AddChild(new ForestStudyLight { Name="ForestStudyLight",Stage=_stage });
    }

    private void AddForestGrass(Node3D root,Dictionary<(int,int,int),List<Transform3D>> groups,RandomNumberGenerator rng,bool expanded)
    {
        var sources=new[]{PropLibrary.Instance("forest_grass_a")!,PropLibrary.Instance("forest_grass_b")!};
        var meshes=sources.Select(s=>s.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().Single().Mesh).ToArray();
        var material=new ShaderMaterial {Shader=GD.Load<Shader>("res://shaders/forest_grass.gdshader")};
        material.SetShaderParameter("forest_surface_quality",ForestStudyEnabled);
        foreach(var ((cx,cz,kind),instances) in groups)
        {
            var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseColors=true,Mesh=meshes[kind],InstanceCount=instances.Count };
            for(int i=0;i<instances.Count;i++)
            {
                var transform=instances[i];
                var point=new Vector2(transform.Origin.X,transform.Origin.Z);
                float quality=ForestSurfaceQuality.Weight(point);
                if(quality>0)
                {
                    // Keep roots, exclusions and RNG consumption fixed; broad size groups break up the even sprinkle.
                    float clump=.5f+.5f*Mathf.Sin(point.X*.83f+Mathf.Sin(point.Y*.61f)*1.8f)*Mathf.Sin(point.Y*.72f-point.X*.19f);
                    clump=Mathf.SmoothStep(.26f,.78f,clump);
                    float shoulder=Mathf.SmoothStep(2.3f,4.3f,PathDistance(point));
                    float width=Mathf.Lerp(1,Mathf.Lerp(.52f,1.14f,clump),quality);
                    float height=Mathf.Lerp(1,Mathf.Lerp(.62f,1.12f,clump)*Mathf.Lerp(.78f,1,shoulder),quality);
                    transform.Basis=transform.Basis.Scaled(new Vector3(width,height,width));
                }
                multi.SetInstanceTransform(i,transform);
                float v=rng.RandfRange(.88f,1.12f);multi.SetInstanceColor(i,new Color(v,v,1,1));
            }
            root.AddChild(new MultiMeshInstance3D {Name=$"Grass_{cx}_{cz}_{kind}",Multimesh=multi,
                MaterialOverride=material,
                VisibilityRangeEnd=expanded?58:42,VisibilityRangeEndMargin=expanded?8:6,VisibilityRangeFadeMode=GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.On});
            ForestGrassCount+=instances.Count;
        }
        foreach(var source in sources)source.Free();
    }

    private Rect2[]? _forestStructures;
    private bool ForestSurface(Vector2 p)
    {
        if(ForestWeight(p,true)<=0 || Layout.TierAt(p)<0)return false;
        // A radial prop exclusion cannot cover the corners of rectangular roofs.
        _forestStructures??=new[]{"watchtower","stack_1","stack_2","stack_3"}.Select(RegionFootprint)
            .Where(r=>r.HasValue).Select(r=>r!.Value.Grow(1)).ToArray();
        return !_forestStructures.Any(r=>r.HasPoint(p))
            && !(p.X>=185 && p.X<215)
            && !(new Rect2(224,-59,16,14).HasPoint(p));
    }

    private void BuildForestExpansion()
    {
        var root=new Node3D{Name="ForestQualityStudy"};_stage.AddChild(root);
        var rng=new RandomNumberGenerator{Seed=241025};var trees=new List<Vector2>();
        var encounters=Encounters;
        var spawnLandings=(encounters?.Entries.Select(e=>new Vector2(e.Ground.X,e.Ground.Z))??Enumerable.Empty<Vector2>())
            .Concat(encounters?.Groups.Where(g=>g.Special>=0).Select(g=>new Vector2(g.At.X,g.At.Z))??Enumerable.Empty<Vector2>()).ToArray();
        // Existing scenery and buildings are exclusions, not random obstacles planted over each other.
        var exclusions=KitProps.Select(holder=>
        {
            var asset=holder.GetChildren().OfType<Node3D>().First(n=>n.SceneFilePath.EndsWith(".glb"));
            var info=PropLibrary.Find(System.IO.Path.GetFileNameWithoutExtension(asset.SceneFilePath))!;
            return (p:new Vector2(holder.Position.X,holder.Position.Z),radius:new Vector2(info.Dimensions.X,info.Dimensions.Z).Length()*.5f*asset.Scale.X);
        }).Where(e=>e.p.X<290).ToArray();
        for(float x=42;x<278;x+=7)for(float z=-81;z<20;z+=7)
        {
            var p=new Vector2(x+rng.RandfRange(-1.7f,1.7f),z+rng.RandfRange(-1.7f,1.7f));
            if(!ForestSurface(p) || !Flat(p,2) || PathDistance(p)<4.8f || rng.Randf()>ForestWeight(p,true)*.88f
                || Layout.Links.Any(l=>p.DistanceTo(Geometry2D.GetClosestPointToSegment(p,l.From,l.To))<5)
                || Layout.Markers.Any(m=>p.DistanceTo(m.Pos)<9) || Layout.Supplies.Any(s=>p.DistanceTo(s.Pos)<5)
                || spawnLandings.Any(s=>p.DistanceTo(s)<3)
                || exclusions.Any(e=>e.p.DistanceTo(p)<e.radius+1.8f))continue;
            float scale=rng.RandfRange(.76f,1.12f);
            // Reuse the existing one random draw: shape variety must not move any trees or grass.
            float shape=rng.Randf();
            string treeId=shape<.73f?"forest_pine":shape<.87f?"forest_broadleaf":shape<.96f?"forest_oval":"forest_fan";
            var tree=Prop(treeId,FloorAt(p),rng.Randf()*360,scale);
            if(tree==null)continue;
            PropOcclusion.Attach(_stage,tree);trees.Add(p);ForestTreeCount++;
        }
        var groups=new Dictionary<(int,int,int),List<Transform3D>>();
        for(float x=32.3f;x<281.8f;x+=.48f)for(float z=-83.7f;z<23.8f;z+=.48f)
        {
            var p=new Vector2(x+rng.RandfRange(-.17f,.17f),z+rng.RandfRange(-.17f,.17f));
            if(!ForestSurface(p))continue;
            float distance=PathDistance(p),weight=ForestWeight(p,true);
            float cluster=.65f+.25f*Mathf.Sin(p.X*.9f+Mathf.Sin(p.Y*1.3f));
            if(distance<2.3f || rng.Randf()>weight*cluster*Mathf.SmoothStep(2.3f,3.7f,distance) || !Flat(p,.32f)
                || trees.Any(t=>t.DistanceTo(p)<.42f)
                || Layout.Links.Any(l=>p.DistanceTo(l.From)<2.4f || p.DistanceTo(l.To)<2.4f)
                || Layout.Markers.Any(m=>p.DistanceTo(m.Pos)<3)
                || Layout.Supplies.Any(s=>p.DistanceTo(s.Pos)<2.5f)
                || exclusions.Any(e=>e.p.DistanceTo(p)<Mathf.Min(e.radius,5)+.2f))continue;
            int kind=rng.Randf()<.65f?0:1;float size=rng.RandfRange(.7f,1.18f);
            var transform=new Transform3D(new Basis(Vector3.Up,rng.Randf()*Mathf.Tau).Scaled(new Vector3(size,size*rng.RandfRange(.7f,1),size)),FloorAt(p)+Vector3.Up*.015f);
            var key=((int)((x-32)/8),(int)((z+84)/8),kind);
            if(!groups.TryGetValue(key,out var list)){list=new();groups.Add(key,list);}list.Add(transform);
        }
        AddForestGrass(root,groups,rng,true);
        root.AddChild(new ForestStudyLight{Name="ForestStudyLight",Stage=_stage,Expanded=true});
    }

    private readonly Dictionary<int,ArrayMesh> _forestBackdrops=new();
    private ArrayMesh ForestBackdrop(int kind)
    {
        if(_forestBackdrops.TryGetValue(kind,out var cached))return cached;
        var source=PropLibrary.Instance(kind==0?"forest_pine":"forest_broadleaf")!;
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach(var mesh in source.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>())
        {
            var transform=mesh.Transform;
            for(var parent=mesh.GetParent() as Node3D;parent!=null && parent!=source;parent=parent.GetParent() as Node3D)transform=parent.Transform*transform;
            surface.AppendFrom(mesh.Mesh,0,transform);
            surface.SetMaterial(mesh.Mesh.SurfaceGetMaterial(0));
        }
        var combined=surface.Commit();source.Free();_forestBackdrops.Add(kind,combined);return combined;
    }
}
