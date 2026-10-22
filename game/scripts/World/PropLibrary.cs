using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace HordeShooter;

/// <summary>How a prop's manifest says it should collide (box / cylinder / none / compound = the map builds its own).</summary>
public sealed record PropCollision(string Kind, Vector3 Size, Vector3 Center, float Radius, float Height);

public sealed record PropInfo(string Id, string File, Vector3 Dimensions, PropCollision Collision, string[] OcclusionParts);

/// <summary>
/// Static set-dressing models with the outdoor kit contract (<c>plushpanic_outdoor_v1</c>, prototypes/outdoor-kit/doc/
/// integration-guide.md): metres, +Y up, the lowest point at y = 0, <c>Keep</c> parts and <c>Occlude_*</c> parts that may
/// later fade when they hide someone. Three folders follow it: the outdoor kit, the stage transition props (L-14) and
/// the edge kit for ground nobody can reach (A-23: no collider, no back faces, no fading parts).
/// Each manifest gives a model's size and a recommended collider; models carry no physics of their own.
/// </summary>
public static class PropLibrary
{
    public static readonly string[] Folders = { "res://assets/outdoor-kit", "res://assets/stage-props", "res://assets/edge-kit" };

    private static Dictionary<string, PropInfo>? _props;
    private static readonly Dictionary<string, PackedScene> _scenes = new();
    private static readonly List<string> _errors = new();
    public static IReadOnlyList<string> Errors { get { Load(); return _errors; } }
    public static IReadOnlyDictionary<string, PropInfo> All => Load();

    public static PropInfo? Find(string id) => Load().GetValueOrDefault(id);

    /// <summary>A fresh copy of the model, or null (reported once) when the id or file is missing.</summary>
    public static Node3D? Instance(string id)
    {
        if (Find(id) is not { } info) { Report($"unknown prop '{id}'"); return null; }
        if (!_scenes.TryGetValue(id, out var scene))
        {
            scene = ResourceLoader.Exists(info.File) ? GD.Load<PackedScene>(info.File) : null!;
            if (scene == null) { Report($"{info.File} is missing or not imported"); return null; }
            _scenes[id] = scene;
        }
        var node = scene.Instantiate<Node3D>();
        node.Name = id;
        return node;
    }

    private static void Report(string error)
    {
        if (_errors.Contains(error)) return;
        _errors.Add(error);
        GD.PushError("[props] " + error);
    }

    private static Dictionary<string, PropInfo> Load()
    {
        if (_props != null) return _props;
        _props = new Dictionary<string, PropInfo>();
        foreach (var folder in Folders)
        {
            string path = folder + "/manifest.json";
            if (!FileAccess.FileExists(path)) { Report($"{path} is missing"); continue; }
            try
            {
                using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(path));
                foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    string id = a.GetProperty("id").GetString() ?? "";
                    if (id == "" || _props.ContainsKey(id)) { Report($"{path}: empty or duplicated id '{id}'"); continue; }
                    var dims = Vec(a, "dimensions_m");
                    var parts = new List<string>();
                    if (a.TryGetProperty("occlusion_parts", out var occ))
                        foreach (var p in occ.EnumerateArray()) parts.Add(p.GetString() ?? "");
                    _props[id] = new PropInfo(id, folder + "/" + a.GetProperty("file").GetString(), dims, ReadCollision(a.GetProperty("collision")), parts.ToArray());
                }
            }
            catch (System.Exception e) { Report($"{path}: {e.Message}"); }
        }
        return _props;
    }

    private static PropCollision ReadCollision(JsonElement c)
    {
        string kind = c.TryGetProperty("kind", out var k) ? k.GetString() ?? "none" : "none";
        float radius = c.TryGetProperty("radius", out var r) ? r.GetSingle() : 0;
        float height = c.TryGetProperty("height", out var h) ? h.GetSingle() : 0;
        var size = c.TryGetProperty("size", out _) ? Vec(c, "size") : Vector3.Zero;
        var center = c.TryGetProperty("center", out _) ? Vec(c, "center") : Vector3.Zero;
        return new PropCollision(kind, size, center, radius, height);
    }

    private static Vector3 Vec(JsonElement el, string key)
    {
        var a = el.GetProperty(key);
        return new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());
    }
}
