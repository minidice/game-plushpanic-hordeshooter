using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Which way a dressed edge region lies from the walkable ground (the side it faces).</summary>
public enum EdgeSide { Far, Near, West, East }

/// <summary>
/// Dressing the ground nobody can reach (decisions.md A-23, doc/model-briefs/outdoor-edge-kit.md): the walkable area is
/// closed by the map's own invisible walls. Independent foliage masses share the same meshes/material as nearby
/// trees, with no rectangular band bases. Spatial chunks and irregular spacing form layered groves.
/// Only the nearby far-side groves cast shadows. On the camera side and past the west end (the camera
/// looks from the south-west) only low pieces are used, so nothing covers a survivor at the edge.
/// </summary>
public abstract partial class StageMap
{
    /// <summary>Instances the edge dressing placed, by model id (checks, budgets).</summary>
    public Dictionary<string, int> EdgeCounts { get; } = new();
    /// <summary>Rows closer than this to the walkable edge cast shadows; the rest do not (cheaper, A-23).</summary>
    public const float EdgeShadowDepth = 10f;
    // Its own generator, so the dressing never moves what the map places with _rng after it (supplies, encounters).
    private readonly RandomNumberGenerator _edgeRng = new() { Seed = 23 };

    /// <summary>
    /// Packs <paramref name="region"/> (x, z on the ground) with independent foliage along its <paramref name="side"/>.
    /// The legacy <paramref name="rocky"/> argument is retained; rock banks are now dressed by OutdoorEdgeArt.
    /// </summary>
    protected void DressEdge(Rect2 region, EdgeSide side, bool rocky = false, bool ground = true)
    {
        if (region.Size.X <= 0 || region.Size.Y <= 0) return;
        bool alongX=side is EdgeSide.Far or EdgeSide.Near;
        float edge=side switch { EdgeSide.Far=>region.End.Y,EdgeSide.Near=>region.Position.Y,EdgeSide.West=>region.End.X,_=>region.Position.X };
        float away=side is EdgeSide.Far or EdgeSide.West?-1:1;
        float depth=alongX?region.Size.Y:region.Size.X;
        float from=alongX?region.Position.X:region.Position.Y,to=alongX?region.End.X:region.End.Y;
        bool near=side is EdgeSide.Near or EdgeSide.West;
        // A continuous surface at ground level, never a raised rectangular foliage base.
        if(ground)_stage.AddChild(new MeshInstance3D { Name="WoodlandGround",Mesh=new PlaneMesh { Size=region.Size },
            Position=new Vector3(region.GetCenter().X,-.025f,region.GetCenter().Y),
            MaterialOverride=new ShaderMaterial { Shader=GD.Load<Shader>("res://shaders/woodland_ground.gdshader") },
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
        // Spatial chunks bound culling. The positions form uneven groves rather than connected tiled masses.
        for(float chunk=from;chunk<to;chunk+=16)
        {
            var groups=new Dictionary<(FoliageArt.Kind,int,bool),List<Transform3D>>();
            float end=Mathf.Min(to,chunk+16);
            for(float d=1.2f;d<Mathf.Min(depth,38);d+=d<5?2.4f:4.3f)
            {
                bool low=near?d<16:d<3;
                float spacing=low?1.5f:2.6f;
                for(float t=chunk+_edgeRng.RandfRange(0,.8f);t<end;t+=spacing*_edgeRng.RandfRange(.8f,1.25f))
                {
                    // Leave a few natural pockets, but keep the wall's first shrub layer continuous enough to ground it.
                    if(d>3 && _edgeRng.Randf()<.12f)continue;
                    float run=Mathf.Clamp(t+_edgeRng.RandfRange(-.45f,.45f),from+.5f,to-.5f);
                    float offset=Mathf.Max(.85f,d+_edgeRng.RandfRange(-.7f,.7f)+.75f*Mathf.Sin(run*.43f+d));
                    var kind=low?FoliageArt.Kind.Shrub:(_edgeRng.Randf()<.15f?FoliageArt.Kind.Broadleaf:FoliageArt.Kind.Pine);
                    int variant=_edgeRng.RandiRange(0,3);
                    float scale=low?_edgeRng.RandfRange(.7f,1.15f):_edgeRng.RandfRange(.85f,1.35f);
                    var at=alongX?new Vector3(run,-.06f,edge+away*offset):new Vector3(edge+away*offset,-.06f,run);
                    var basis=new Basis(Vector3.Up,_edgeRng.RandfRange(0,Mathf.Tau)).Scaled(new Vector3(scale,scale*_edgeRng.RandfRange(.9f,1.15f),scale));
                    // The full foliage footprint must stay behind the edge. Includes irregular crown overhang.
                    var box=new Transform3D(basis,at)*FoliageArt.Mesh(kind,variant).GetAabb();
                    if(alongX)at.Z+=away<0?-Mathf.Max(0,box.End.Z-edge):Mathf.Max(0,edge-box.Position.Z);
                    else at.X+=away<0?-Mathf.Max(0,box.End.X-edge):Mathf.Max(0,edge-box.Position.X);
                    // Also contain the ends of a strip: neighbouring rooms can have wider walkable floors.
                    if(alongX)
                    {
                        if(box.Size.X>to-from)continue;
                        at.X+=Mathf.Max(0,from-box.Position.X)-Mathf.Max(0,box.End.X-to);
                    }
                    else
                    {
                        if(box.Size.Z>to-from)continue;
                        at.Z+=Mathf.Max(0,from-box.Position.Z)-Mathf.Max(0,box.End.Z-to);
                    }
                    bool shadows=!near && d<EdgeShadowDepth;
                    var key=(kind,variant,shadows);
                    if(!groups.TryGetValue(key,out var list))groups[key]=list=new();
                    list.Add(new Transform3D(basis,at));
                    string id=kind.ToString();EdgeCounts[id]=EdgeCounts.GetValueOrDefault(id)+1;
                }
            }
            foreach(var(key,items)in groups)
                FoliageArt.Instances(_stage,"Woodland"+key.Item1,key.Item1,key.Item2,items,key.Item3);
        }
    }
    /// <summary>A plain ground far past the dressed edges, so the end of the floor is never in view (A-23).</summary>
    protected void FarGround(string color, float size = 420f, float height = -.22f)
    {
        var plane = new MeshInstance3D
        {
            Name = "FarGround",
            Mesh = new PlaneMesh { Size = new Vector2(size, size) },
            MaterialOverride = Mat.Solid(color, .97f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(Area.GetCenter().X, height, Area.GetCenter().Y),
        };
        _stage.AddChild(plane);
    }
}
