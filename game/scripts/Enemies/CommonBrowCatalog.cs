using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HordeShooter;

public sealed record CommonBrowPreset(string Id, string Label, string Shape, float Width, float Thickness, float Angle, float LeftHeight, float RightHeight, float LeftAngle, float RightAngle);
public sealed record CommonBrowAttachment(string Id, float[] Left, float[] Right, float Yaw);
public sealed record CommonBrowTraits(CommonBrowPreset Preset, CommonBrowAttachment Attachment);
public sealed record CommonBrowDocument(int Version, CommonBrowPreset[] Presets, CommonBrowAttachment[] Species);

/// <summary>Deterministic head-local facial strips; shared geometry never carries rig-local status state.</summary>
public sealed class CommonBrowCatalog
{
    public const string DataPath = "res://content/data/common_brows.json";
    private static CommonBrowCatalog? _current;
    public static CommonBrowCatalog Current => _current ??= FromText(FileAccess.GetFileAsString(DataPath));
    public static void Reload() => _current = null;
    public IReadOnlyList<CommonBrowPreset> Presets { get; private set; } = Array.Empty<CommonBrowPreset>();
    public IReadOnlyDictionary<string, CommonBrowAttachment> Species { get; private set; } = new Dictionary<string, CommonBrowAttachment>();
    private readonly List<string> _errors = new();
    public IReadOnlyList<string> Errors => _errors;
    private readonly Dictionary<CommonBrowPreset, ArrayMesh> _meshes = new();
    private readonly StandardMaterial3D _material = new() { ResourceName = "fixed_common_brow", AlbedoColor = new Color("242125"), Roughness = .92f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };

    public static CommonBrowCatalog FromText(string text)
    {
        var c = new CommonBrowCatalog();
        try
        {
            using (var json = JsonDocument.Parse(text)) { Duplicates(json.RootElement); var root = json.RootElement; Required(root, "version", "presets", "species"); foreach (var p in root.GetProperty("presets").EnumerateArray()) Required(p,"id","label","shape","width","thickness","angle","left_height","right_height","left_angle","right_angle"); foreach (var a in root.GetProperty("species").EnumerateArray()) Required(a,"id","left","right","yaw"); }
            var d = JsonSerializer.Deserialize<CommonBrowDocument>(text, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new FormatException("empty catalog");
            if (d.Version != 1 || d.Presets == null || d.Presets.Length != 10 || d.Species == null || d.Species.Length != 4) throw new FormatException("requires version 1, ten presets and four species");
            if (d.Presets.Any(p => p == null) || d.Species.Any(s => s == null)) throw new FormatException("null entries");
            if (d.Presets.Select(p => p.Id).Distinct().Count() != 10) throw new FormatException("duplicate preset id");
            foreach (var p in d.Presets)
            {
                if (!Safe(p.Id) || string.IsNullOrWhiteSpace(p.Label) || p.Label.Length > 40 || p.Shape is not ("straight" or "arched" or "tapered")) throw new FormatException("invalid preset id/label/shape");
                if (!Bound(p.Width,.055f,.15f) || !Bound(p.Thickness,.006f,.025f) || !Bound(p.Angle,-25,25) || !Bound(p.LeftHeight,-.012f,.012f) || !Bound(p.RightHeight,-.012f,.012f) || !Bound(p.LeftAngle,-12,12) || !Bound(p.RightAngle,-12,12)) throw new FormatException($"preset {p.Id}: out of bounds");
            }
            if (d.Presets.Select(p => (p.Shape,p.Width,p.Thickness,p.Angle,p.LeftHeight,p.RightHeight,p.LeftAngle,p.RightAngle)).Distinct().Count() != 10) throw new FormatException("requires ten distinct composed presets");
            var species = new Dictionary<string,CommonBrowAttachment>();
            foreach (var s in d.Species)
            {
                if (s.Id is not ("parrot" or "eagle" or "lion" or "reindeer") || !species.TryAdd(s.Id,s) || !Coordinates(s.Left) || !Coordinates(s.Right) || !Bound(s.Yaw,0,65)) throw new FormatException("invalid species attachment");
                if (s.Left[0] >= 0 || s.Right[0] <= 0) throw new FormatException("left/right attachment signs");
            }
            c.Presets = d.Presets; c.Species = species;
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException or NullReferenceException or InvalidOperationException) { c._errors.Add("common_brows.json: " + e.Message); }
        return c;
    }
    private static void Required(JsonElement element, params string[] keys) { foreach (var key in keys) if (!element.TryGetProperty(key,out _)) throw new FormatException("missing property " + key); }
    private static bool Safe(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 40 && s.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
    private static bool Bound(float v,float min,float max) => float.IsFinite(v) && v >= min && v <= max;
    private static bool Coordinates(float[] a) => a != null && a.Length == 3 && Bound(a[0],-.4f,.4f) && Bound(a[1],.3f,.55f) && Bound(a[2],.15f,.4f);
    private static void Duplicates(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(); foreach (var p in e.EnumerateObject()) { if (!names.Add(p.Name)) throw new FormatException("duplicate key " + p.Name); Duplicates(p.Value); } }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) Duplicates(item);
    }
    public CommonBrowTraits? Resolve(CommonIdentity id)
    {
        if (_errors.Count > 0) throw new InvalidOperationException(string.Join("; ",_errors));
        if (id.Variant is < 0 or > 9) throw new ArgumentException("brow variant must be 0..9");
        return Species.TryGetValue(id.Species,out var s) ? new(Presets[id.Variant],s) : null;
    }
    public void Apply(CharacterRig rig, CommonIdentity id)
    {
        var traits = Resolve(id);
        if (rig.Head == null) return;
        foreach (string name in new[] { "common_brow_left", "common_brow_right" })
        {
            var existing = rig.Head.GetNodeOrNull<MeshInstance3D>(name);
            if (traits == null) { if (existing != null) { rig.Head.RemoveChild(existing); existing.QueueFree(); } continue; }
            var node = existing ?? new MeshInstance3D { Name = name };
            if (existing == null) rig.Head.AddChild(node);
            var p = traits.Preset; var a = traits.Attachment;
            if (!_meshes.TryGetValue(p,out var mesh)) { mesh = Build(p); _meshes.Add(p,mesh); }
            bool left = name.EndsWith("left"); float sign = left ? -1 : 1; var xyz = left ? a.Left : a.Right;
            node.Mesh = mesh; node.MaterialOverride = _material;
            node.Position = new Vector3(xyz[0],xyz[1] + (left ? p.LeftHeight : p.RightHeight),xyz[2]);
            node.RotationDegrees = new Vector3(0,sign*a.Yaw,sign*p.Angle + (left ? p.LeftAngle : p.RightAngle));
            node.Visible = true;
        }
    }
    private static ArrayMesh Build(CommonBrowPreset p)
    {
        // A shallow ribbon with tapered ends and a subtle surface-following crown.
        var vertices = new List<Vector3>(); var indices = new List<int>();
        const int segments = 8;
        for (int i=0;i<=segments;i++)
        {
            float t=(float)i/segments, x=(t-.5f)*p.Width;
            float crown=p.Shape == "arched" ? MathF.Sin(t*MathF.PI)*p.Thickness*.65f : 0;
            float thickness=p.Thickness*(p.Shape == "straight" ? .7f+.3f*MathF.Sin(t*MathF.PI) : .12f+.88f*MathF.Sin(t*MathF.PI));
            float z=-MathF.Pow(2*t-1,2)*.004f;
            vertices.Add(new(x,crown-thickness*.5f,z)); vertices.Add(new(x,crown+thickness*.5f,z));
            if(i<segments) { int n=i*2; indices.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2}); }
        }
        var arrays=new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=vertices.ToArray(); arrays[(int)Mesh.ArrayType.Index]=indices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal]=Enumerable.Repeat(Vector3.Back,vertices.Count).ToArray();
        var mesh=new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays); return mesh;
    }
}
