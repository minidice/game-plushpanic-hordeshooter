using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

public sealed record SurfaceProfile(string Id, string Label, string Family, string Step, string Hit, string Breaking,
    float StepDb, float HitDb, float BreakDb);

/// <summary>Audio-only material presets. Physical and destruction rules remain in ObjectInteractions.</summary>
public sealed class SurfaceProfiles
{
    public const string DataPath = "res://content/data/surface_profiles.json";
    private static SurfaceProfiles? _current;
    public static SurfaceProfiles Current => _current ??= Parse(FileAccess.GetFileAsString(DataPath));
    // Studio reloads authored values after an atomic save. Running games keep their session data.
    public static void Reload() => _current = Parse(FileAccess.GetFileAsString(DataPath));
    public Dictionary<string, SurfaceProfile> Profiles { get; } = new();
    public Dictionary<string, string?> Objects { get; } = new();
    public Dictionary<string, string> Families { get; } = new();
    public SurfaceProfile Default { get; private set; } = null!;
    private sealed record Zone(Rect2 Rect, string Profile);
    private sealed record Ground(string Default, Zone[] Zones);
    private sealed record Layout(string Default, Dictionary<string,string> Regions, string? Route, float MainWidth, float SideWidth);
    private readonly Dictionary<string, Ground> _ground = new();
    private readonly Dictionary<int, Layout> _layouts = new();

    public SurfaceProfile ForObject(string id) => Objects.TryGetValue(id, out string? profile) && profile != null ? Profiles[profile] : Default;
    public SurfaceProfile GroundAt(string map, Vector3 position)
    {
        if (!_ground.TryGetValue(map, out var ground)) return Default;
        var p = new Vector2(position.X, position.Z);
        return Profiles[ground.Zones.LastOrDefault(z => z.Rect.HasPoint(p))?.Profile ?? ground.Default];
    }
    public SurfaceProfile LayoutAt(StageLayout layout, Vector3 position)
    {
        if (!_layouts.TryGetValue(layout.Stage, out var data)) return Default;
        var p = new Vector2(position.X, position.Z);
        var region = layout.Regions.LastOrDefault(r => Math.Abs(position.Y - r.Tier * layout.TierHeight) < .45f && Geometry2D.IsPointInPolygon(p, r.Poly));
        string id = region != null && data.Regions.TryGetValue(region.Id, out var mapped) ? mapped : data.Default;
        if (id == "grass" && data.Route != null)
        {
            foreach (var (route, points) in layout.Routes)
                for (int i = 1; i < points.Length; i++)
                    if (p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, points[i - 1], points[i])) < (route.StartsWith("main") ? data.MainWidth : data.SideWidth))
                        return Profiles[data.Route];
            foreach (var link in layout.Links.Where(l => l.Type == "ramp"))
                if (p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, link.From, link.To)) < Math.Min(2.5f, (link.Width - 1) / 2))
                    return Profiles[data.Route];
        }
        return Profiles[id];
    }

    public static SurfaceProfiles Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var result = new SurfaceProfiles();
        static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() is { Length: > 0 } value ? value : throw new FormatException("Empty surface field: " + name);
        static float Gain(JsonElement element, string name)
        {
            float value = element.GetProperty(name).GetSingle();
            return float.IsFinite(value) && value >= -24 && value <= 6 ? value : throw new FormatException("Invalid surface gain: " + name);
        }
        foreach (var f in root.GetProperty("families").EnumerateObject())
            if (!result.Families.TryAdd(f.Name, f.Value.GetString() ?? "")) throw new FormatException("Duplicate family: " + f.Name);
        foreach (var p in root.GetProperty("profiles").EnumerateObject())
        {
            var v = p.Value;
            string family = Text(v, "family");
            if (!result.Families.ContainsKey(family)) throw new FormatException("Unknown family: " + family);
            var profile = new SurfaceProfile(p.Name, Text(v,"label"), family, Text(v,"step"), Text(v,"hit"), Text(v,"breaking"), Gain(v,"step_db"), Gain(v,"hit_db"), Gain(v,"break_db"));
            foreach (string sound in new[] { profile.Step, profile.Hit, profile.Breaking })
                if (SoundRecipes.Find(sound) == null) throw new FormatException("Unknown surface sound: " + sound);
            if (!result.Profiles.TryAdd(p.Name, profile)) throw new FormatException("Duplicate profile: " + p.Name);
        }
        string Reference(string id) => result.Profiles.ContainsKey(id) ? id : throw new FormatException("Unknown surface profile: " + id);
        result.Default = result.Profiles[Reference(Text(root,"default_profile"))];
        foreach (var p in root.GetProperty("objects").EnumerateObject())
        {
            string? id = p.Value.ValueKind == JsonValueKind.Null ? null : Reference(p.Value.GetString() ?? "");
            if (!result.Objects.TryAdd(p.Name,id)) throw new FormatException("Duplicate object: " + p.Name);
        }
        foreach (var p in root.GetProperty("ground").EnumerateObject())
        {
            var zones = new List<Zone>();
            foreach (var z in p.Value.GetProperty("zones").EnumerateArray())
            {
                var rect = z.GetProperty("rect").EnumerateArray().Select(v=>v.GetSingle()).ToArray();
                if (rect.Length != 4 || rect.Any(v=>!float.IsFinite(v)) || rect[2] <= rect[0] || rect[3] <= rect[1]) throw new FormatException("Invalid ground rectangle");
                zones.Add(new(new Rect2(rect[0],rect[1],rect[2]-rect[0],rect[3]-rect[1]),Reference(Text(z,"profile"))));
            }
            result._ground.Add(p.Name, new(Reference(Text(p.Value,"default")),zones.ToArray()));
        }
        foreach (var p in root.GetProperty("layouts").EnumerateObject())
        {
            var regions = new Dictionary<string,string>();
            foreach (var r in p.Value.GetProperty("regions").EnumerateObject()) regions.Add(r.Name,Reference(r.Value.GetString() ?? ""));
            string? route = p.Value.GetProperty("route_profile").GetString();
            float main = p.Value.GetProperty("main_half_width").GetSingle(), side = p.Value.GetProperty("side_half_width").GetSingle();
            if (!float.IsFinite(main) || !float.IsFinite(side) || main <= 0 || side <= 0) throw new FormatException("Invalid route width");
            result._layouts.Add(int.Parse(p.Name),new(Reference(Text(p.Value,"default")),regions,route == null ? null : Reference(route),main,side));
        }
        return result;
    }
}
