using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Which way a dressed edge region lies from the walkable ground (the side it faces).</summary>
public enum EdgeSide { Far, Near, West, East }

/// <summary>
/// Dressing the ground nobody can reach (decisions.md A-23, doc/model-briefs/outdoor-edge-kit.md): the walkable area is
/// closed by the map's own invisible walls, and everything outside is packed with the edge kit — light front-only
/// clumps with no collider, no back faces and no fading parts — drawn with instancing. Rows step away from the walkable
/// edge: a low band hides tree feet and the ground's end, then forest clumps, then cheap backdrop silhouettes into
/// the haze. Only the rows next to the play area cast shadows. On the camera side and past the west end (the camera
/// looks from the south-west) only low pieces are used, so nothing covers a survivor at the edge.
/// </summary>
public abstract partial class StageMap
{
    /// <summary>Instances the edge dressing placed, by model id (checks, budgets).</summary>
    public Dictionary<string, int> EdgeCounts { get; } = new();
    /// <summary>Rows closer than this to the walkable edge cast shadows; the rest do not (cheaper, A-23).</summary>
    public const float EdgeShadowDepth = 6f;
    // Its own generator, so the dressing never moves what the map places with _rng after it (supplies, encounters).
    private readonly RandomNumberGenerator _edgeRng = new() { Seed = 23 };

    /// <summary>
    /// Packs <paramref name="region"/> (x, z on the ground) with edge-kit rows that run parallel to the walkable edge on
    /// its <paramref name="side"/>. <paramref name="rocky"/> uses the rock wall instead of the shrub band next to the edge.
    /// </summary>
    protected void DressEdge(Rect2 region, EdgeSide side, bool rocky = false)
    {
        if (region.Size.X <= 0 || region.Size.Y <= 0) return;
        bool alongX = side is EdgeSide.Far or EdgeSide.Near;
        // The walkable edge and the direction away from it.
        float edge = side switch { EdgeSide.Far => region.End.Y, EdgeSide.Near => region.Position.Y, EdgeSide.West => region.End.X, _ => region.Position.X };
        float away = side is EdgeSide.Far or EdgeSide.West ? -1 : 1;
        float depth = alongX ? region.Size.Y : region.Size.X;
        float runFrom = alongX ? region.Position.X : region.Position.Y, runTo = alongX ? region.End.X : region.End.Y;
        // The camera sits south-west of what it looks at (+Z, and −X from its yaw of −30°): past the west end, as on the
        // camera side, tall clumps would stand between it and the survivors, so both get the low rows.
        bool near = side is EdgeSide.Near or EdgeSide.West;

        var lit = new Dictionary<string, List<Transform3D>>();
        var unlit = new Dictionary<string, List<Transform3D>>();
        int row = 0;
        foreach (var (d, id, spacing) in Rows(depth, near, rocky))
        {
            // The first row hugs the edge; the rows behind it wander in depth and angle so they never read as stripes.
            float wander = row++ == 0 ? .3f : 1.3f;
            for (float t = runFrom + _edgeRng.RandfRange(0, spacing); t < runTo + spacing * .5f; t += spacing * _edgeRng.RandfRange(.75f, 1.05f))
            {
                string model = id switch
                {
                    "mix" => _edgeRng.Randf() < .6f ? "edge_forest_pine" : "edge_forest_round",
                    "low" => _edgeRng.Randf() < .55f ? "edge_shrub_band" : "edge_forest_low",
                    _ => id,
                };
                float off = edge + away * Mathf.Max(.6f, d + _edgeRng.RandfRange(-wander, wander));
                float run = Mathf.Clamp(t + _edgeRng.RandfRange(-.8f, .8f), runFrom, runTo);
                var at = alongX ? new Vector3(run, 0, off) : new Vector3(off, 0, run);
                // Front faces the camera (+Z) whatever the side; turn and size jitter hide the repeats.
                float turn = row == 1 ? 10 : 25;
                var basis = new Basis(Vector3.Up, Mathf.DegToRad(_edgeRng.RandfRange(-turn, turn))).Scaled(Vector3.One * _edgeRng.RandfRange(.85f, 1.2f));
                var into = d < EdgeShadowDepth ? lit : unlit;
                if (!into.TryGetValue(model, out var list)) into[model] = list = new List<Transform3D>();
                list.Add(new Transform3D(basis, at));
            }
        }
        foreach (var (id, list) in lit) { Scatter(id, list, shadows: true); EdgeCounts[id] = EdgeCounts.GetValueOrDefault(id) + list.Count; }
        foreach (var (id, list) in unlit) { Scatter(id, list, shadows: false); EdgeCounts[id] = EdgeCounts.GetValueOrDefault(id) + list.Count; }
    }

    /// <summary>
    /// Rows by distance from the walkable edge (metres), with the model and its spacing along the row. The camera side
    /// keeps everything low near the edge: seen from the survivor at the edge the camera is ~41° up, so a 1 m band needs
    /// ~1.2 m and a 3.6 m clump ~4.2 m of room.
    /// </summary>
    private static IEnumerable<(float Depth, string Id, float Spacing)> Rows(float depth, bool near, bool rocky)
    {
        if (near)
        {
            yield return (1.4f, "edge_shrub_band", 4.4f);
            yield return (3.6f, "low", 4.8f);
            for (float d = 7f; d < depth; d += 3.6f) yield return (d, d < 15 ? "low" : "edge_forest_pine", 5.4f);
            yield break;
        }
        yield return (1.2f, rocky ? "edge_rock_wall" : "edge_shrub_band", rocky ? 5.4f : 4.4f);
        yield return (3.8f, "mix", 5.4f);
        yield return (7.6f, "edge_forest_pine", 6.4f);
        for (float d = 11.5f; d < depth; d += 4.2f) yield return (d, "edge_backdrop", 10.5f);
    }

    /// <summary>A plain ground far past the dressed edges, so the end of the floor is never in view (A-23).</summary>
    protected void FarGround(string color, float size = 420f)
    {
        var plane = new MeshInstance3D
        {
            Name = "FarGround",
            Mesh = new PlaneMesh { Size = new Vector2(size, size) },
            MaterialOverride = Mat.Solid(color, .97f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(Area.GetCenter().X, -.22f, Area.GetCenter().Y),
        };
        _stage.AddChild(plane);
    }
}
