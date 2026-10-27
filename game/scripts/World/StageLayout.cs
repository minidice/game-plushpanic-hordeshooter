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
    /// <summary>A point beat. <see cref="Kind"/> picks how it is built (blocked: bridge | shutter; mission: pump | panel), <see cref="Model"/> overrides its prop.</summary>
    public sealed record Marker(string Type, Vector2 Pos, string Label, string Kind = "", string Model = "");
    public sealed record Supply(string Kind, Vector2 Pos);
    /// <summary>A zone of the stage plan (decisions.md L-31): "large" or "small", branch = a reward or mission detour off the main route.</summary>
    public sealed record Zone(string Id, string Size, bool Branch, Vector2[] Poly, string Label);
    /// <summary>A kit model placed by the data (yaw in degrees, solid = the manifest's recommended collider).</summary>
    public sealed record PropSpot(string Id, Vector2 Pos, float Yaw, bool Solid, float Scale);
    /// <summary>
    /// A lamp: a plain street lamp by default, or a guiding light (level guide map, L-33) with its own colour, energy and range;
    /// Post = false hangs the light without a lamp post (a sign or a door light).
    /// </summary>
    public sealed record LampSpot(Vector2 Pos, string Color = "ffe1ab", float Energy = 1.1f, float Range = 9, bool Post = true);

    public int Stage { get; private set; }
    /// <summary>The scenario stage the safehouse hatch leads to in the large campaign (0: none, decisions.md L-30).</summary>
    public int NextStage { get; private set; }
    /// <summary>"forest" (outdoor kit, groves outside the edge) or "urban" (asphalt, buildings and fences outside the edge).</summary>
    public string Theme { get; private set; } = "forest";
    /// <summary>Hand-placed dressing written in code for this layout ("stage3": LayoutMapEnvironment's lists), or empty.</summary>
    public string Dressing { get; private set; } = "";
    /// <summary>Language keys the layout overrides (title, route_start, crescendo_objective, crescendo_hold, crescendo_alarm, crescendo_done).</summary>
    public Dictionary<string, string> Keys { get; } = new();
    public float TierHeight { get; private set; } = TierMap.Height;
    /// <summary>x0, z0, x1, z1.</summary>
    public Rect2 Bounds { get; private set; }
    public List<Region> Regions { get; } = new();
    public List<Edge> Edges { get; } = new();
    public List<Link> Links { get; } = new();
    public Dictionary<string, Vector2[]> Routes { get; } = new();
    public List<Marker> Markers { get; } = new();
    public List<Supply> Supplies { get; } = new();
    public List<Zone> Zones { get; } = new();
    public List<PropSpot> Props { get; } = new();
    /// <summary>Lamps: <c>[x, z]</c> is a street lamp, an object <c>{pos, color, energy, range, post}</c> a guiding light.</summary>
    public List<LampSpot> Lamps { get; } = new();
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
            if (root.TryGetProperty("next_stage", out var ns)) layout.NextStage = ns.GetInt32();
            if (root.TryGetProperty("theme", out var theme)) layout.Theme = theme.GetString() ?? "forest";
            if (root.TryGetProperty("dressing", out var dressing)) layout.Dressing = dressing.GetString() ?? "";
            if (root.TryGetProperty("keys", out var keys))
                foreach (var key in keys.EnumerateObject()) layout.Keys[key.Name] = key.Value.GetString() ?? "";
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
                        m.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : "",
                        m.TryGetProperty("kind", out var mk) ? mk.GetString() ?? "" : "",
                        m.TryGetProperty("model", out var mm) ? mm.GetString() ?? "" : ""));
            if (root.TryGetProperty("supplies", out var supplies))
                foreach (var s in supplies.EnumerateArray())
                    layout.Supplies.Add(new Supply(s.GetProperty("kind").GetString() ?? "", Point(s.GetProperty("pos"))));
            if (root.TryGetProperty("zones", out var zones))
                foreach (var z in zones.EnumerateArray())
                    layout.Zones.Add(new Zone(z.GetProperty("id").GetString() ?? "", z.GetProperty("size").GetString() ?? "",
                        z.TryGetProperty("branch", out var br) && br.GetBoolean(), Points(z.GetProperty("poly")),
                        z.TryGetProperty("label", out var zl) ? zl.GetString() ?? "" : ""));
            if (root.TryGetProperty("props", out var props))
                foreach (var p in props.EnumerateArray())
                    layout.Props.Add(new PropSpot(p.GetProperty("id").GetString() ?? "", Point(p.GetProperty("pos")),
                        p.TryGetProperty("yaw", out var yaw) ? yaw.GetSingle() : 0, !p.TryGetProperty("solid", out var so) || so.GetBoolean(),
                        p.TryGetProperty("scale", out var sc) ? sc.GetSingle() : 1));
            if (root.TryGetProperty("lamps", out var lamps))
                foreach (var l in lamps.EnumerateArray())
                    layout.Lamps.Add(l.ValueKind == JsonValueKind.Array ? new LampSpot(Point(l)) : new LampSpot(Point(l.GetProperty("pos")),
                        l.TryGetProperty("color", out var lc) ? lc.GetString() ?? "ffe1ab" : "ffe1ab",
                        l.TryGetProperty("energy", out var le) ? le.GetSingle() : 1.1f,
                        l.TryGetProperty("range", out var lr) ? lr.GetSingle() : 9,
                        !l.TryGetProperty("post", out var lp) || lp.GetBoolean()));
            foreach (var link in layout.Links)
                if (link.Type is not ("ramp" or "ladder" or "drop" or "zipline" or "gondola"))
                    layout.Errors.Add($"{where}: link type '{link.Type}' is not ramp/ladder/drop/zipline/gondola");
            foreach (var zone in layout.Zones)
                if (zone.Size is not ("large" or "small"))
                    layout.Errors.Add($"{where}: zone '{zone.Id}' size '{zone.Size}' is not large/small (L-31)");
            foreach (var link in layout.Links)
                if (link.Type == "ladder" && layout.ClimbAxis(link.From, link.To) is { } up && (up.X < 0 || up.Y > 0))
                    layout.Errors.Add($"{where}: ladder {link.From} → {link.To} climbs {(up.X < 0 ? "west" : "south")}, away from the camera; ladders climb north or east (L-32)");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            layout.Errors.Add($"{where}: {e.Message}");
        }
        return layout;
    }

    public Marker? FindMarker(string type) => Markers.Find(m => m.Type == type);

    /// <summary>The zone at <paramref name="p"/> (later zones cover earlier ones), or null.</summary>
    public Zone? ZoneAt(Vector2 p)
    {
        for (int i = Zones.Count - 1; i >= 0; i--)
            if (Geometry2D.IsPointInPolygon(p, Zones[i].Poly)) return Zones[i];
        return null;
    }

    /// <summary>
    /// The unit axis a link climbs along (from the low tier toward the high one, the main axis of <paramref name="a"/> →
    /// <paramref name="b"/>), or null when the two ends stand on the same tier or off the map. North is −Y (−z).
    /// </summary>
    public Vector2? ClimbAxis(Vector2 a, Vector2 b)
    {
        int ta = TierAt(a), tb = TierAt(b);
        if (ta < 0 || tb < 0 || ta == tb) return null;
        var toHigh = tb > ta ? b - a : a - b;
        return Mathf.Abs(toHigh.X) >= Mathf.Abs(toHigh.Y) ? new Vector2(Mathf.Sign(toHigh.X), 0) : new Vector2(0, Mathf.Sign(toHigh.Y));
    }

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
