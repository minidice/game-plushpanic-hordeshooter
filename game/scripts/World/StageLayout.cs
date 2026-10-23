using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter;

/// <summary>
/// A stage layout in the map data format, first version (decisions.md T-24 option B, doc/stage2-floorplan.md 8장):
/// regions (polygons with a height tier), edges (climbable or not), links (ramp, ladder, drop, zipline, gondola),
/// routes, markers and supply spots, in world metres with +z toward the camera. <see cref="LayoutMap"/> builds a
/// graybox map from it; tools/floorplan/render_floorplan.py draws the same file as a plan.
/// </summary>
public sealed class StageLayout
{
    public sealed record Region(string Id, int Tier, Vector2[] Poly);
    public sealed record Edge(string Kind, bool Climbable, Vector2[] Line);
    public sealed record Link(string Type, Vector2 From, Vector2 To, float Width);
    public sealed record Marker(string Type, Vector2 Pos, string Label);
    public sealed record Supply(string Kind, Vector2 Pos);

    public int Stage { get; private set; }
    public float TierHeight { get; private set; } = TierMap.Height;
    /// <summary>x0, z0, x1, z1.</summary>
    public Rect2 Bounds { get; private set; }
    public List<Region> Regions { get; } = new();
    public List<Edge> Edges { get; } = new();
    public List<Link> Links { get; } = new();
    public Dictionary<string, Vector2[]> Routes { get; } = new();
    public List<Marker> Markers { get; } = new();
    public List<Supply> Supplies { get; } = new();
    public List<string> Errors { get; } = new();

    public static StageLayout Load(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            var missing = new StageLayout();
            missing.Errors.Add($"{path}: file not found");
            return missing;
        }
        return FromText(FileAccess.GetFileAsString(path), path);
    }

    public static StageLayout FromText(string text, string where)
    {
        var layout = new StageLayout();
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            layout.Stage = root.TryGetProperty("stage", out var st) ? st.GetInt32() : 0;
            if (root.TryGetProperty("tier_height_m", out var th)) layout.TierHeight = th.GetSingle();
            if (!Mathf.IsEqualApprox(layout.TierHeight, TierMap.Height))
                layout.Errors.Add($"{where}: tier_height_m {layout.TierHeight} differs from the game's {TierMap.Height}; the game's is used");
            var b = root.GetProperty("bounds");
            layout.Bounds = new Rect2(b[0].GetSingle(), b[1].GetSingle(), b[2].GetSingle() - b[0].GetSingle(), b[3].GetSingle() - b[1].GetSingle());
            foreach (var r in root.GetProperty("regions").EnumerateArray())
                layout.Regions.Add(new Region(r.GetProperty("id").GetString() ?? "", r.GetProperty("tier").GetInt32(), Points(r.GetProperty("poly"))));
            if (root.TryGetProperty("edges", out var edges))
                foreach (var e in edges.EnumerateArray())
                    layout.Edges.Add(new Edge(e.GetProperty("kind").GetString() ?? "", e.GetProperty("climbable").GetBoolean(), Points(e.GetProperty("line"))));
            if (root.TryGetProperty("links", out var links))
                foreach (var l in links.EnumerateArray())
                    layout.Links.Add(new Link(l.GetProperty("type").GetString() ?? "", Point(l.GetProperty("from")), Point(l.GetProperty("to")),
                        l.TryGetProperty("width", out var w) ? w.GetSingle() : 3));
            if (root.TryGetProperty("routes", out var routes))
                foreach (var route in routes.EnumerateObject()) layout.Routes[route.Name] = Points(route.Value);
            if (root.TryGetProperty("markers", out var markers))
                foreach (var m in markers.EnumerateArray())
                    layout.Markers.Add(new Marker(m.GetProperty("type").GetString() ?? "", Point(m.GetProperty("pos")),
                        m.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : ""));
            if (root.TryGetProperty("supplies", out var supplies))
                foreach (var s in supplies.EnumerateArray())
                    layout.Supplies.Add(new Supply(s.GetProperty("kind").GetString() ?? "", Point(s.GetProperty("pos"))));
            foreach (var link in layout.Links)
                if (link.Type is not ("ramp" or "ladder" or "drop" or "zipline" or "gondola"))
                    layout.Errors.Add($"{where}: link type '{link.Type}' is not ramp/ladder/drop/zipline/gondola");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            layout.Errors.Add($"{where}: {e.Message}");
        }
        return layout;
    }

    public Marker? FindMarker(string type) => Markers.Find(m => m.Type == type);

    /// <summary>The tier of the last region containing <paramref name="p"/> (later regions cover earlier ones), or −1 off every region.</summary>
    public int TierAt(Vector2 p)
    {
        for (int i = Regions.Count - 1; i >= 0; i--)
            if (Geometry2D.IsPointInPolygon(p, Regions[i].Poly)) return Regions[i].Tier;
        return -1;
    }

    private static Vector2 Point(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle());

    private static Vector2[] Points(JsonElement e)
    {
        var list = new List<Vector2>();
        foreach (var p in e.EnumerateArray()) list.Add(Point(p));
        return list.ToArray();
    }
}
