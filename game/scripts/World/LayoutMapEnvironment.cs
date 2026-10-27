using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Environment pass of the layout maps. All terrain/link physics stays in LayoutMap. Every layout gets the generic parts
/// (route paths, cliff rock, the edge fill, container stack shells); Stage 3 (the size draft) adds its hand-placed lists
/// and forest study, the large forest Stage 2 a scattered forest, the urban Stage 1 buildings and fences beyond the edge.
/// </summary>
public sealed partial class LayoutMap
{
    private void BuildEnvironment()
    {
        foreach(var body in _stage.GetChildren().OfType<StaticBody3D>().Where(n=>n.HasMeta("smooth_terrain_ramp") || n.GetChildren().OfType<MeshInstance3D>().Any(m=>m.Mesh is PrismMesh)))
            foreach(var mesh in body.GetChildren().OfType<MeshInstance3D>())mesh.MaterialOverride=Urban?Mat.Solid("8d8c84",.95f):GroundMaterial;
        foreach (var region in Layout.Regions.Where(r=>r.Id.StartsWith("stack_")))
        {
            var box=RegionFootprint(region.Id)!.Value;
            var center=box.GetCenter();
            Landmark("container_stack_shell",new Vector3(center.X,(region.Tier-2)*TierMap.Height,center.Y));
        }
        if(Urban)
        {
            // A paved walkway along the routes so a run knows which way to go (log 014); on the roads the centre line
            // does that instead (A-30).
            BuildPaths(new ShaderMaterial{Shader=GD.Load<Shader>("res://shaders/urban_path.gdshader")},2.2f,UrbanRoadAt);
            BuildRoadMarkings();
            BuildUrbanBits();
            BuildUrbanBorders();
            return;
        }
        BuildPaths(ForestMaterial("res://shaders/stage2_path.gdshader"),3);
        BuildCliffFaces();
        BuildBoundaryGroves();
        if(Layout.Dressing=="stage3")BuildStage3Dressing();
        else BuildForestScatter();
    }

    /// <summary>Stage 3's hand-placed dressing (written for its coordinates; other layouts place props by data).</summary>
    private void BuildStage3Dressing()
    {
        // Deliberate working groups, with a continuous open corridor between them.
        foreach (var (x,z,yaw) in new (float,float,float)[]{(266,-60,0),(302,-78,90),(345,-65,0),(398,-82,90),(420,-28,0)})
        {
            EnvironmentProp("container_closed",new(x,z),yaw);
            EnvironmentProp("pallet",new(x+4,z+2),12);
            EnvironmentProp("wooden_crate",new(x+4,z+3.5f),-8);
            EnvironmentProp("barrel",new(x-4,z+2),0);
        }
        foreach(var p in new[]{new Vector2(350,-8),new Vector2(370,-8),new Vector2(360,-23)})
            EnvironmentProp("wooden_crate",p,15);
        // These existing workshop models include matching world/flow colliders through WorkshopProp.
        WorkshopProp(WorkshopArt.Kind.Workbench,FloorAt(new(366,-18)));
        WorkshopProp(WorkshopArt.Kind.ToolChest,FloorAt(new(364,-19)));
        WorkshopProp(WorkshopArt.Kind.Generator,FloorAt(new(369,-21)));
        foreach(var p in new[]{new Vector2(28,10),new Vector2(239,-63),new Vector2(454,-102),new Vector2(495,-119),new Vector2(613,-155)})
        {
            EnvironmentProp("sandbags",p,0);
            EnvironmentProp("barrel",p+new Vector2(3,1),0);
        }
        foreach(var p in new[]{new Vector2(51,9),new Vector2(84,-32),new Vector2(138,-60),new Vector2(156,-20),new Vector2(174,-62),new Vector2(221,-65)})
        {
            EnvironmentProp("boulder",p,27);
            EnvironmentProp("fallen_log",p+new Vector2(4,1),35);
            EnvironmentProp("shrub",p+new Vector2(2,-2),0);
        }

        // Sign position and next destination are explicit: arrows cannot accidentally point into a cliff.
        foreach(var (at,to) in new (Vector2,Vector2)[]{
            (new(34,1),new(60,-6)),(new(62,-4),new(74,-12)),(new(107,-24),new(118,-31)),
            (new(112,-47),new(118,-64)),(new(178,-38),new(184,-30)),(new(194,-8),new(188,-16)),
            (new(181,-27),new(183,-40)),(new(220,-40),new(232,-52)),(new(247,-42),new(260,-46)),
            (new(341,-40),new(360,-14)),(new(431,-61),new(450,-78)),(new(468,-89),new(484,-99)),
            (new(494,-106),new(508,-118)),(new(523,-119),new(530,-128)),(new(603,-136),new(626,-145))})
        {
            var direction=to-at;
            var prop=Prop("route_sign",FloorAt(at),Mathf.RadToDeg(-Mathf.Atan2(direction.Y,direction.X)),solid:false);
            if(prop!=null) Solid(FloorAt(at)+Vector3.Up, new Vector3(.2f,2,.2f),"",drawCargo:false,surfaceObject:"route_sign");
        }
        foreach(var p in new[]{new Vector2(65,-16),new Vector2(110,-36),new Vector2(179,-18),new Vector2(247,-55),new Vector2(438,-80),new Vector2(472,-103),new Vector2(496,-120),new Vector2(522,-134),new Vector2(612,-149)})
        {
            if(EnvironmentProp("streetlamp",p,90)!=null)
                Lamp(FloorAt(p)+Vector3.Up*3.6f,"ffe1ab",1.1f,9,false).Name="EnvironmentLamp";
        }
        // Low debris breaks the empty lawn, kept away from paths and industrial floors.
        var grass=new List<Transform3D>();
        var rng=new RandomNumberGenerator{Seed=24024};
        for(float x=48;x<176;x+=9)for(float z=-64;z<16;z+=8)
        {
            var p=new Vector2(x+rng.RandfRange(-2,2),z+rng.RandfRange(-2,2));
            if(rng.Randf()<.55f || PathDistance(p)<9)continue;
            string treeId=rng.Randf()<.75f?"pine_tall":"broadleaf";float treeYaw=rng.Randf()*360;
            if(ForestStudyEnabled && (ForestExpanded?ForestWeight(p,true)>0:ForestStudyBounds.HasPoint(p)))continue;
            EnvironmentProp(treeId,p,treeYaw);
        }
        for(float x=44;x<180;x+=3.6f) for(float z=-72;z<18;z+=3.8f)
        {
            var p=new Vector2(x+rng.RandfRange(-1,1),z+rng.RandfRange(-1,1));
            if(Layout.TierAt(p)<0 || PathDistance(p)<4 || !Flat(p,.7f) || rng.Randf()<.35f)continue;
            var grassBasis=new Basis(Vector3.Up,rng.Randf()*Mathf.Tau);
            if(ForestStudyEnabled && (ForestExpanded?ForestWeight(p,true)>0:ForestStudyBounds.HasPoint(p)))continue;
            grass.Add(new Transform3D(grassBasis,FloorAt(p)+Vector3.Up*.015f));
        }
        Scatter("bit_grass_clump",grass,false);
        if(ForestStudyEnabled)BuildForestStudy();
    }

    /// <summary>A forest over the whole walkable layout, kept off the routes, links, markers and supply spots.</summary>
    private void BuildForestScatter()
    {
        var grass=new List<Transform3D>();
        var rng=new RandomNumberGenerator{Seed=(ulong)(24024+Layout.Stage)};
        for(float x=Layout.Bounds.Position.X;x<Layout.Bounds.End.X;x+=9)for(float z=Layout.Bounds.Position.Y;z<Layout.Bounds.End.Y;z+=8)
        {
            var p=new Vector2(x+rng.RandfRange(-2,2),z+rng.RandfRange(-2,2));
            if(Layout.TierAt(p)<0 || rng.Randf()<.55f || PathDistance(p)<9)continue;
            EnvironmentProp(rng.Randf()<.75f?"pine_tall":"broadleaf",p,rng.Randf()*360);
        }
        for(float x=Layout.Bounds.Position.X;x<Layout.Bounds.End.X;x+=3.6f) for(float z=Layout.Bounds.Position.Y;z<Layout.Bounds.End.Y;z+=3.8f)
        {
            var p=new Vector2(x+rng.RandfRange(-1,1),z+rng.RandfRange(-1,1));
            if(Layout.TierAt(p)<0 || PathDistance(p)<4 || !Flat(p,.7f) || rng.Randf()<.35f)continue;
            grass.Add(new Transform3D(new Basis(Vector3.Up,rng.Randf()*Mathf.Tau),FloorAt(p)+Vector3.Up*.015f));
        }
        Scatter("bit_grass_clump",grass,false);
    }

    /// <summary>
    /// The urban edge (L-30 Stage 1): beyond the far (north) edge a row of buildings, and every edge no building covers
    /// fenced one 3 m fence after another (solid sheet on the far side, see-through wire on the camera side), so the lanes
    /// read as walled streets (L-36, level log 016). No collision (the edge walls already block).
    /// </summary>
    private void BuildUrbanBorders()
    {
        var buildings=new Dictionary<string,List<Transform3D>>();
        var fences=new List<Transform3D>();var sheets=new List<Transform3D>();
        var rng=new RandomNumberGenerator{Seed=(ulong)(31031+Layout.Stage)};
        var taken=new List<Rect2>();
        Vector2? Nearest(Vector2 p,float reach)
        {
            Vector2? best=null;float distance=float.MaxValue;
            for(int i=0;i<16;i++)for(float r=2;r<=reach;r+=2)
            {
                var q=p+Vector2.FromAngle(i*Mathf.Tau/16)*r;
                if(Layout.TierAt(q)>=0 && r<distance){best=q;distance=r;}
            }
            return best;
        }
        foreach(var spot in Layout.Props)
            if(PropLibrary.Find(spot.Id) is { } placed)taken.Add(new Rect2(spot.Pos-new Vector2(placed.Dimensions.X,placed.Dimensions.Z)/2,new Vector2(placed.Dimensions.X,placed.Dimensions.Z)));
        string[] kinds={"urban_rowhouse","urban_rowhouse","urban_shop","urban_warehouse"};
        for(float x=Layout.Bounds.Position.X+6;x<Layout.Bounds.End.X-6;x+=7)
        for(float z=Layout.Bounds.Position.Y+6;z<Layout.Bounds.End.Y-6;z+=7)
        {
            var p=new Vector2(x,z);
            if(Layout.TierAt(p)>=0 || Nearest(p,16) is not { } near)continue;
            if(p.Y<near.Y-3)
            {
                string id=kinds[rng.RandiRange(0,kinds.Length-1)];
                if(PropLibrary.Find(id) is not { } info)continue;
                var size=new Vector2(info.Dimensions.X,info.Dimensions.Z);
                var rect=new Rect2(p-size/2,size).Grow(1.5f);
                bool clear=!taken.Any(r=>r.Intersects(rect));
                // The whole footprint stays off the walkable ground.
                for(int i=0;i<9 && clear;i++)clear=Layout.TierAt(rect.Position+rect.Size*new Vector2(i%3*.5f,i/3*.5f))<0;
                if(!clear)continue;
                taken.Add(rect);
                if(!buildings.TryGetValue(id,out var list)){list=new();buildings.Add(id,list);}
                list.Add(new Transform3D(Basis.Identity,new Vector3(p.X,FloorAt(near).Y-.05f,p.Y)));
            }
        }
        // The fence line: just outside the floor's edge, one per 3 m. The floor is sampled once on a 1.5 m lattice; a void
        // lattice point's outward normal comes from the floor around it, the edge from a short search back along it.
        const float step=1.5f;
        var origin=Layout.Bounds.Position;
        int nx=(int)(Layout.Bounds.Size.X/step)+1,nz=(int)(Layout.Bounds.Size.Y/step)+1;
        var floor=new bool[nx,nz];
        for(int i=0;i<nx;i++)for(int k=0;k<nz;k++)floor[i,k]=Layout.TierAt(origin+new Vector2(i,k)*step)>=0;
        var fenceAt=new Dictionary<(int,int),List<Vector2>>();
        bool Crowded(Vector2 q)
        {
            for(int i=-1;i<=1;i++)for(int k=-1;k<=1;k++)
                if(fenceAt.TryGetValue(((int)Mathf.Floor(q.X/3)+i,(int)Mathf.Floor(q.Y/3)+k),out var cell) && cell.Any(o=>o.DistanceTo(q)<2.4f))return true;
            return false;
        }
        for(int i=0;i<nx;i++)for(int k=0;k<nz;k++)
        {
            if(floor[i,k])continue;
            var toFloor=Vector2.Zero;
            for(int di=-2;di<=2;di++)for(int dk=-2;dk<=2;dk++)
                if(i+di>=0 && i+di<nx && k+dk>=0 && k+dk<nz && floor[i+di,k+dk])toFloor+=new Vector2(di,dk);
            if(toFloor.LengthSquared()<.01f)continue;
            var p=origin+new Vector2(i,k)*step;var normal=-toFloor.Normalized();
            Vector2? hit=null;var outside=p;
            for(float r=.75f;r<=3.75f && hit==null;r+=.75f){var q=p-normal*r;if(Layout.TierAt(q)>=0)hit=q;else outside=q;}
            if(hit is not { } inside)continue;
            for(int n=0;n<6;n++){var mid=(outside+inside)/2;if(Layout.TierAt(mid)>=0)inside=mid;else outside=mid;}
            var at=outside+normal*.6f;
            if(Layout.TierAt(at)>=0 || Crowded(at) || taken.Any(r=>r.HasPoint(at)))continue;
            var key=((int)Mathf.Floor(at.X/3),(int)Mathf.Floor(at.Y/3));
            if(!fenceAt.TryGetValue(key,out var cell)){cell=new();fenceAt.Add(key,cell);}
            cell.Add(at);
            var along=normal.Orthogonal();
            (normal.Y<-.5f?sheets:fences).Add(new Transform3D(new Basis(Vector3.Up,-Mathf.Atan2(along.Y,along.X)),new Vector3(at.X,FloorAt(inside).Y-.05f,at.Y)));
        }
        foreach(var (id,list) in buildings)Scatter(id,list);
        Scatter("wire_fence",fences);
        Scatter("corrugated_fence",sheets);
    }

    private float PathDistance(Vector2 p)
    {
        float best=float.MaxValue;
        foreach(var route in Layout.Routes.Values) for(int i=0;i+1<route.Length;i++)
            best=Mathf.Min(best,p.DistanceTo(Geometry2D.GetClosestPointToSegment(p,route[i],route[i+1])));
        return best;
    }
    private bool Flat(Vector2 p,float radius)
    {
        var center=FloorAt(p);
        foreach(var d in new[]{new Vector2(-radius,-radius),new Vector2(radius,-radius),new Vector2(-radius,radius),new Vector2(radius,radius)})
            if(Layout.TierAt(p+d)<0 || Mathf.Abs(FloorAt(p+d).Y-center.Y)>.1f)return false;
        return true;
    }
    private Node3D? EnvironmentProp(string id,Vector2 p,float yaw)
    {
        var info=PropLibrary.Find(id)!;
        if(info.Collision.Kind=="compound")throw new InvalidOperationException($"Environment prop {id} needs explicit wall collision; use a dedicated placement helper.");
        float radius=new Vector2(info.Dimensions.X,info.Dimensions.Z).Length()/2;
        if(!Flat(p,radius+.4f) || PathDistance(p)<3.8f+radius || Layout.Links.Any(l=>p.DistanceTo(l.From)<radius+3 || p.DistanceTo(l.To)<radius+3)
            || Layout.Markers.Any(m=>p.DistanceTo(m.Pos)<radius+7) || Layout.Supplies.Any(s=>p.DistanceTo(s.Pos)<radius+3))return null;
        var prop=Prop(id,FloorAt(p),yaw);
        if(prop!=null)PropOcclusion.Attach(_stage,prop);
        return prop;
    }

    private void BuildPaths(Material material,float mainHalf,Func<Vector2,bool>? skip=null)
    {
        using var surface=new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        var segments=new List<(Vector2 a,Vector2 b,float half)>();
        foreach(var (id,points) in Layout.Routes)for(int i=0;i+1<points.Length;i++)
            segments.Add((points[i],points[i+1],id.StartsWith("main")?mainHalf:1.6f));
        foreach(var link in Layout.Links.Where(l=>l.Type=="ramp"))segments.Add((link.From,link.To,Mathf.Min(2.5f,(link.Width-1)/2)));
        float Distance(Vector2 p)
        {
            float best=10;
            foreach(var (a,b,half) in segments)best=Mathf.Min(best,p.DistanceTo(Geometry2D.GetClosestPointToSegment(p,a,b))/half);
            return best;
        }
        var bridge=BridgeFootprint();var tower=RegionFootprint("watchtower");
        // Each ground cell is emitted once, even where routes merge. Shared corners prevent transparent Z fighting.
        for(float x=Layout.Bounds.Position.X;x<Layout.Bounds.End.X;x++)for(float z=Layout.Bounds.Position.Y;z<Layout.Bounds.End.Y;z++)
        {
            if(Distance(new Vector2(x+.5f,z+.5f))>1.4f || (skip?.Invoke(new Vector2(x+.5f,z+.5f))??false))continue;
            var points=new[]{new Vector2(x,z),new Vector2(x+1,z),new Vector2(x+1,z+1),new Vector2(x,z+1)};
            if(points.Any(p=>Layout.TierAt(p)<0 || (bridge?.HasPoint(p)??false) || (tower?.HasPoint(p)??false)))continue;
            var verts=points.Select(FloorAt).ToArray();
            if(verts.Max(p=>p.Y)-verts.Min(p=>p.Y)>.45f)continue;
            foreach(int k in new[]{0,1,2,0,2,3})
            {
                surface.SetNormal(Vector3.Up);surface.SetUV(new Vector2(Mathf.Max(0,(1-Distance(points[k]))*.5f),0));
                surface.AddVertex(verts[k]+Vector3.Up*.025f);
            }
        }
        surface.SetMaterial(material);
        _stage.AddChild(new MeshInstance3D{Name="EnvironmentPaths",Mesh=surface.Commit(),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }

    private void BuildBoundaryGroves()
    {
        var root=new Node3D{Name="EnvironmentGroves"};_stage.AddChild(root);
        var groups=new Dictionary<(int,int,int,int,bool),List<Transform3D>>();
        var rocks=new List<Transform3D>();
        var rng=new RandomNumberGenerator{Seed=24025};
        // Search only a narrow band outside playable polygons; silhouettes agree with the invisible outer wall.
        for(float x=Layout.Bounds.Position.X+2;x<Layout.Bounds.End.X;x+=4.5f)
        for(float z=Layout.Bounds.Position.Y+2;z<Layout.Bounds.End.Y;z+=4.5f)
        {
            var p=new Vector2(x+rng.RandfRange(-1,1),z+rng.RandfRange(-1,1));
            if(Layout.TierAt(p)>=0)continue;
            Vector2? closest=null;float distance=float.MaxValue;
            for(int i=0;i<16;i++)for(float r=3;r<=12;r+=3)
            {
                var q=p+Vector2.FromAngle(i*Mathf.Tau/16)*r;
                if(Layout.TierAt(q)>=0 && r<distance){closest=q;distance=r;}
            }
            if(closest is not { } near || distance<3)continue;
            bool foreground=p.Y>near.Y+.5f || p.X<near.X-1;
            int kind=foreground?2:(rng.Randf()<.7f?0:1),variant=rng.RandiRange(0,3);
            float scale=foreground?rng.RandfRange(.65f,1):rng.RandfRange(.9f,1.5f);
            var transform=new Transform3D(new Basis(Vector3.Up,rng.Randf()*Mathf.Tau).Scaled(Vector3.One*scale),new Vector3(p.X,FloorAt(near).Y-.12f,p.Y));
            bool forest=ForestStudyEnabled && ForestExpanded && ForestWeight(p,true)>.15f && kind!=2;
            var key=((int)(x/32),(int)(z/32),kind,forest?0:variant,forest);
            if(!groups.TryGetValue(key,out var list)){list=new();groups.Add(key,list);}list.Add(transform);
            if(distance<=3 && rng.Randf()<.6f) rocks.Add(new Transform3D(new Basis(Vector3.Up,rng.Randf()*Mathf.Tau).Scaled(new Vector3(1.2f,1.1f,1.1f)),transform.Origin));
        }
        foreach(var ((cx,cz,kind,variant,forest),list) in groups)
        {
            var multi=new MultiMesh{TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=forest?ForestBackdrop(kind):FoliageArt.Mesh((FoliageArt.Kind)kind,variant,1),InstanceCount=list.Count};
            for(int i=0;i<list.Count;i++)multi.SetInstanceTransform(i,list[i]);
            root.AddChild(new MultiMeshInstance3D{Name=$"Grove_{cx}_{cz}_{kind}_{variant}",Multimesh=multi,CastShadow=kind==2?GeometryInstance3D.ShadowCastingSetting.Off:GeometryInstance3D.ShadowCastingSetting.On});
        }
        Scatter("rock_cluster",rocks);
    }

    private void BuildCliffFaces()
    {
        var batches=new Dictionary<(int,int),SurfaceTool>();
        var special=new[]{"bridge","watchtower","stack_1","stack_2","stack_3"}.Select(RegionFootprint).Where(r=>r.HasValue).Select(r=>r!.Value).ToArray();
        SurfaceTool Batch(Vector3 p)
        {
            var key=((int)Mathf.Floor(p.X/32),(int)Mathf.Floor(p.Z/32));
            if(!batches.TryGetValue(key,out var surface)){surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);batches.Add(key,surface);}return surface;
        }
        (int low,int high,bool positive)? Edge(float axis,float along,bool vertical)
        {
            var center=vertical?new Vector2(axis,along+.5f):new Vector2(along+.5f,axis);
            var delta=vertical?new Vector2(.5f,0):new Vector2(0,.5f);
            int minus=Layout.TierAt(center-delta),plus=Layout.TierAt(center+delta);
            if(minus<0 || plus<0 || minus==plus)return null;
            var highPoint=center+(plus>minus?delta:-delta);
            if(special.Any(r=>r.HasPoint(highPoint)))return null;
            // Follow actual ramp height instead of leaving an 8m-wide bare hole around every ramp.
            // Traversal apparatus still needs its own clear opening; keep manufactured decks unchanged.
            if(Layout.Links.Any(l=>l.Type!="ramp" && center.DistanceTo(Geometry2D.GetClosestPointToSegment(center,l.From,l.To))<1.2f))return null;
            return (Math.Min(minus,plus),Math.Max(minus,plus),plus>minus);
        }
        foreach(bool vertical in new[]{true,false})
        {
            float axis0=vertical?Layout.Bounds.Position.X:Layout.Bounds.Position.Y,axis1=vertical?Layout.Bounds.End.X:Layout.Bounds.End.Y;
            float along0=vertical?Layout.Bounds.Position.Y:Layout.Bounds.Position.X,along1=vertical?Layout.Bounds.End.Y:Layout.Bounds.End.X;
            for(float axis=axis0+1;axis<axis1;axis++)for(float along=along0;along<along1;along++)
            {
                var edge=Edge(axis,along,vertical);
                if(edge==null)continue;
                var (low,high,positive)=edge.Value;
                var outward=(vertical?Vector3.Right:Vector3.Back)*(positive?-1:1);
                for(int half=0;half<2;half++)
                {
                    float begin=along+half*.5f,end=begin+.5f;
                    var a=vertical?new Vector3(axis,0,begin):new Vector3(begin,0,axis);
                    var b=vertical?new Vector3(axis,0,end):new Vector3(end,0,axis);
                    var tangent=(b-a).Normalized();
                    float la=_stage.Tiers.GroundY(a+outward*.02f+tangent*.001f),lb=_stage.Tiers.GroundY(b+outward*.02f-tangent*.001f);
                    float ha=_stage.Tiers.GroundY(a-outward*.02f+tangent*.001f),hb=_stage.Tiers.GroundY(b-outward*.02f-tangent*.001f);
                    if(Mathf.Max(ha-la,hb-lb)>.035f)TerrainEdgeArt.Rock(Batch(a),a,b,la,lb,ha,hb,outward);
                }
            }
        }
        // Exposed sides of the smooth ramps have the same profile, covered by the same rock surface.
        foreach(var ramp in _stage.GetChildren().OfType<StaticBody3D>().Where(n=>n.HasMeta("smooth_terrain_ramp")))
        {
            var bounds=ramp.GetChildren().OfType<MeshInstance3D>().Single().Mesh.GetAabb();
            float run=bounds.Size.X,width=bounds.Size.Z,low=ramp.Position.Y-TierMap.Height*.5f;
            foreach(float side in new[]{-1f,1f})for(int i=0;i<24;i++)
            {
                float t0=i/24f,t1=(i+1)/24f;
                var a=ramp.ToGlobal(new Vector3((t0-.5f)*run,-TierMap.Height*.5f,side*width*.5f));
                var b=ramp.ToGlobal(new Vector3((t1-.5f)*run,-TierMap.Height*.5f,side*width*.5f));
                TerrainEdgeArt.Rock(Batch(a),a,b,low,low,low+TierMap.TerrainRampProfile(t0)*TierMap.Height,low+TierMap.TerrainRampProfile(t1)*TierMap.Height,ramp.Basis.Z*side);
            }
        }
        var rockMaterial=ForestMaterial("res://shaders/stage2_ground.gdshader");rockMaterial.SetShaderParameter("terrain_rock",true);
        foreach(var (key,surface) in batches)
        {
            surface.Index();var mesh=surface.Commit();surface.Dispose();
            _stage.AddChild(new MeshInstance3D{Name=$"TerrainRock_{key.Item1}_{key.Item2}",Mesh=mesh,MaterialOverride=rockMaterial});
        }
    }
}
