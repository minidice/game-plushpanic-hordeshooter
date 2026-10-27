using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// The urban Stage 1's ground (decisions.md A-30): roads, pavements and yards instead of one grey slab, and no grass.
/// The floor grid is baked once into a map the ground shader reads (<c>shaders/urban_ground.gdshader</c>): R the
/// distance to the nearest wall or map edge, G whether the cell is a road lane (the B · D · F lanes, L-38), B how wide
/// that lane's pavement is. Roads get asphalt with a pavement and kerb along their walls and a dashed centre line along
/// the route; the small zones, pockets and bases get concrete slabs; the ground past the walls is a gravel lot.
/// The urban bits (Codex, <c>assets/urban-bits</c>) are scattered over it: decoration only, no colliders, no shadows.
/// </summary>
public sealed partial class LayoutMap
{
    /// <summary>Distance to a wall the map stores, metres (R = 1 at and beyond it).</summary>
    public const float UrbanEdgeRange = 6f;
    /// <summary>Pavement width along a main lane's walls and along a side lane's, metres (`[임시]`).</summary>
    public const float MainPavement = 2.4f, SidePavement = 1.4f;
    private Image? _urbanMap;
    private Rect2 _urbanArea;
    private ShaderMaterial? _urbanFloor, _urbanLot;
    /// <summary>Urban bits placed on the ground, by model id (checks, budgets).</summary>
    public Dictionary<string, int> UrbanBits { get; } = new();

    // Lane regions are written by tools/level-guide/apply_narrow.py as "<lane>_<n>": b_main_0, d_yard_1, f_cross2_0 …
    private static bool RoadLane(string id) => id.Length > 2 && id[1] == '_' && "bdf".Contains(id[0]) && !id.Contains("pocket") && !id.StartsWith("c1_");
    private static float PavementOf(string id) =>
        id.StartsWith("b_main") || id.StartsWith("d_main") || id.StartsWith("f_main") ? MainPavement
        : id.Contains("cross") || id.Contains("stack") || id.Contains("pharmacy") ? 0 : SidePavement;

    /// <summary>Bakes the urban ground map from the walkable grid (TierAt per flow cell, as BuildMap reads it).</summary>
    private void BakeUrbanGround(int[] grid, int width, int depth)
    {
        var tiers = _stage.Tiers;
        float cell = tiers.Cell;
        _urbanArea = new Rect2(tiers.MinX, tiers.MinZ, width * cell, depth * cell);
        // Chamfer distance (3-4) from every walkable cell to the nearest unwalkable one.
        var dist = new int[width * depth];
        const int Far = 1 << 20;
        for (int i = 0; i < dist.Length; i++) dist[i] = grid[i] >= 0 ? Far : 0;
        int At(int x, int z) => x < 0 || z < 0 || x >= width || z >= depth ? 0 : dist[z * width + x];
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            int i = z * width + x;
            if (dist[i] == 0) continue;
            dist[i] = Math.Min(dist[i], Math.Min(Math.Min(At(x - 1, z) + 3, At(x, z - 1) + 3), Math.Min(At(x - 1, z - 1) + 4, At(x + 1, z - 1) + 4)));
        }
        for (int z = depth - 1; z >= 0; z--)
        for (int x = width - 1; x >= 0; x--)
        {
            int i = z * width + x;
            if (dist[i] == 0) continue;
            dist[i] = Math.Min(dist[i], Math.Min(Math.Min(At(x + 1, z) + 3, At(x, z + 1) + 3), Math.Min(At(x + 1, z + 1) + 4, At(x - 1, z + 1) + 4)));
        }
        // Road lanes and their pavement width, rastered from the lane polygons (cheap: each lane's own box only).
        var road = new float[width * depth];
        Array.Fill(road, -1f);
        foreach (var region in Layout.Regions.Where(r => r.Tier == 0 && RoadLane(r.Id)))
        {
            float pave = PavementOf(region.Id);
            float minX = region.Poly.Min(p => p.X), maxX = region.Poly.Max(p => p.X), minZ = region.Poly.Min(p => p.Y), maxZ = region.Poly.Max(p => p.Y);
            int x0 = Math.Max(0, (int)((minX - tiers.MinX) / cell)), x1 = Math.Min(width - 1, (int)((maxX - tiers.MinX) / cell));
            int z0 = Math.Max(0, (int)((minZ - tiers.MinZ) / cell)), z1 = Math.Min(depth - 1, (int)((maxZ - tiers.MinZ) / cell));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(tiers.MinX + (x + .5f) * cell, tiers.MinZ + (z + .5f) * cell);
                if (Geometry2D.IsPointInPolygon(p, region.Poly)) road[z * width + x] = Mathf.Max(road[z * width + x], pave);
            }
        }
        // Rooms stay concrete even where a lane's band reaches under them (the street's end runs into base C1).
        var rooms = Layout.Markers.Select(m => m.Type switch
        {
            "base" => new Rect2(m.Pos - BaseSize / 2, BaseSize),
            "start_room" => new Rect2(m.Pos - StartRoomSize / 2, StartRoomSize),
            "safehouse" => new Rect2(m.Pos - SafehouseSize / 2, SafehouseSize),
            _ => new Rect2(),
        }).Where(r => r.Size.X > 0).Select(r => r.Grow(.5f)).ToList();
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            var p = new Vector2(tiers.MinX + (x + .5f) * cell, tiers.MinZ + (z + .5f) * cell);
            if (road[z * width + x] >= 0 && rooms.Any(r => r.HasPoint(p))) road[z * width + x] = -1;
        }
        _urbanMap = Image.CreateEmpty(width, depth, false, Image.Format.Rgba8);
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            int i = z * width + x;
            float metres = dist[i] / 3f * cell;
            float pave = road[i];
            _urbanMap.SetPixel(x, z, new Color(Mathf.Min(1, metres / UrbanEdgeRange), pave >= 0 ? 1 : 0, Mathf.Max(0, pave) / 3f, 1));
        }
        var texture = ImageTexture.CreateFromImage(_urbanMap);
        var area = new Vector4(_urbanArea.Position.X, _urbanArea.Position.Y, _urbanArea.Size.X, _urbanArea.Size.Y);
        var shader = GD.Load<Shader>("res://shaders/urban_ground.gdshader");
        _urbanFloor = new ShaderMaterial { Shader = shader };
        _urbanFloor.SetShaderParameter("map", texture);
        _urbanFloor.SetShaderParameter("area", area);
        _urbanFloor.SetShaderParameter("edge_range", UrbanEdgeRange);
        _urbanLot = (ShaderMaterial)_urbanFloor.Duplicate();
        _urbanLot.SetShaderParameter("lot", true);
    }

    private Color UrbanSample(Vector2 p)
    {
        if (_urbanMap == null || !_urbanArea.HasPoint(p)) return new Color(0, 0, 0, 0);
        var uv = (p - _urbanArea.Position) / _urbanArea.Size;
        return _urbanMap.GetPixel(Math.Clamp((int)(uv.X * _urbanMap.GetWidth()), 0, _urbanMap.GetWidth() - 1), Math.Clamp((int)(uv.Y * _urbanMap.GetHeight()), 0, _urbanMap.GetHeight() - 1));
    }

    /// <summary>Whether the urban ground at <paramref name="p"/> is a road lane (A-30).</summary>
    public bool UrbanRoadAt(Vector2 p) => UrbanSample(p).G > .5f;
    /// <summary>Metres from <paramref name="p"/> to the nearest wall on the urban ground (capped at <see cref="UrbanEdgeRange"/>).</summary>
    public float UrbanEdgeDistance(Vector2 p) => UrbanSample(p).R * UrbanEdgeRange;
    private float UrbanPavement(Vector2 p) => UrbanSample(p).B * 3;

    /// <summary>
    /// The dashed centre line along the main route where it runs on a road: 3 m dashes, 3 m gaps, kept off the
    /// pavements and away from links (ramps, ladders).
    /// </summary>
    private void BuildRoadMarkings()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        int dashes = 0;
        foreach (var (id, points) in Layout.Routes.Where(r => r.Key.StartsWith("main")))
        {
            for (int i = 0; i + 1 < points.Length; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                float length = a.DistanceTo(b);
                if (length < 1) continue;
                var along = (b - a) / length;
                var side = along.Orthogonal() * .07f;
                for (float s = 1.5f; s + 3 <= length - 1.5f; s += 6)
                {
                    var p0 = a + along * s;
                    var p1 = a + along * (s + 3);
                    var mid = (p0 + p1) / 2;
                    if (!UrbanRoadAt(p0) || !UrbanRoadAt(p1) || UrbanEdgeDistance(mid) < UrbanPavement(mid) + 1.5f || Layout.TierAt(mid) != 0) continue;
                    if (Layout.Links.Any(l => mid.DistanceTo(Geometry2D.GetClosestPointToSegment(mid, l.From, l.To)) < 3)) continue;
                    var corners = new[] { p0 - side, p1 - side, p1 + side, p0 + side };
                    var uvs = new[] { new Vector2(0, s), new Vector2(0, s + 3), new Vector2(1, s + 3), new Vector2(1, s) };
                    foreach (int k in new[] { 0, 1, 2, 0, 2, 3 })
                    {
                        surface.SetNormal(Vector3.Up);
                        surface.SetUV(uvs[k]);
                        surface.AddVertex(new Vector3(corners[k].X, .03f, corners[k].Y));
                    }
                    dashes++;
                }
            }
        }
        RoadDashes = dashes;
        if (dashes == 0) return;
        surface.SetMaterial(new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/road_marking.gdshader") });
        _stage.AddChild(new MeshInstance3D { Name = "RoadMarkings", Mesh = surface.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Centre-line dashes laid (checks).</summary>
    public int RoadDashes { get; private set; }

    /// <summary>
    /// Urban bits over the walkable ground: a manhole now and then on the road, drains by the kerb, litter and leaves on
    /// the pavements and by the walls, rubble in yards and corners, weeds in the cracks, a puddle here and there on the asphalt. Kept
    /// off markers, supplies and links so nothing hides what the run needs to see.
    /// </summary>
    private void BuildUrbanBits()
    {
        var bits = new Dictionary<string, List<Transform3D>>();
        var rng = new RandomNumberGenerator { Seed = 30030 };
        bool Clear(Vector2 p) => Layout.TierAt(p) == 0 && Layout.Markers.All(m => p.DistanceTo(m.Pos) > 3) && Layout.Supplies.All(s => p.DistanceTo(s.Pos) > 2.5f)
            && Layout.Links.All(l => p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, l.From, l.To)) > 2.5f);
        void Put(string id, Vector2 p, float yaw)
        {
            if (!Clear(p) || PropLibrary.Find(id) == null) return;
            if (!bits.TryGetValue(id, out var list)) bits[id] = list = new List<Transform3D>();
            list.Add(new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(p.X, .012f, p.Y)));
        }
        float AnyYaw() => rng.RandfRange(0, Mathf.Tau);
        for (float x = _urbanArea.Position.X; x < _urbanArea.End.X; x += 1.5f)
        for (float z = _urbanArea.Position.Y; z < _urbanArea.End.Y; z += 1.5f)
        {
            var p = new Vector2(x + rng.RandfRange(0, 1.5f), z + rng.RandfRange(0, 1.5f));
            if (Layout.TierAt(p) != 0) continue;
            float edge = UrbanEdgeDistance(p), pave = UrbanPavement(p);
            bool road = UrbanRoadAt(p), onPavement = road && edge < pave, roadway = road && !onPavement;
            float roll = rng.Randf();
            if (roadway && edge > pave + 3 && (roll -= .015f) < 0) Put("bit_manhole", p, AnyYaw());
            else if (roadway && edge < pave + .9f && (roll -= .08f) < 0) Put("bit_drain", p, 0);
            else if ((onPavement || (!road && edge < 2)) && (roll -= .10f) < 0) Put(rng.Randf() < .55f ? "bit_litter" : "bit_leaves", p, AnyYaw());
            else if (!road && edge < 1.6f && (roll -= .08f) < 0) Put("bit_rubble", p, AnyYaw());
            else if ((onPavement || !road) && (roll -= .05f) < 0) Put("bit_crack_weed", p, AnyYaw());
            // Puddles only on the asphalt: on pale concrete the dark water reads as a hole.
            else if (roadway && (roll -= .006f) < 0) Put("bit_puddle", p, AnyYaw());
        }
        // A drain lies along its kerb: turn it to the wall it is next to.
        if (bits.TryGetValue("bit_drain", out var drains))
            for (int i = 0; i < drains.Count; i++)
            {
                var p = new Vector2(drains[i].Origin.X, drains[i].Origin.Z);
                var g = new Vector2(UrbanEdgeDistance(p + Vector2.Right) - UrbanEdgeDistance(p - Vector2.Right), UrbanEdgeDistance(p + Vector2.Down) - UrbanEdgeDistance(p - Vector2.Down));
                // The drain's long side (model X) runs along the kerb, across the wall's gradient.
                float yaw = g.LengthSquared() > .0001f ? Mathf.Atan2(g.X, g.Y) : 0;
                drains[i] = new Transform3D(new Basis(Vector3.Up, yaw), drains[i].Origin);
            }
        foreach (var (id, list) in bits)
        {
            Scatter(id, list, shadows: false);
            UrbanBits[id] = list.Count;
        }
    }
}
