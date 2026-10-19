using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Cached flat materials for blockout geometry and placeholder models.</summary>
public static class Mat
{
    private static readonly Dictionary<string, StandardMaterial3D> Cache = new();

    public static StandardMaterial3D Solid(Color color, float roughness = .85f)
    {
        var key = $"s{color.ToHtml()}{roughness}";
        if (!Cache.TryGetValue(key, out var mat))
        {
            mat = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness };
            Cache[key] = mat;
        }
        return mat;
    }

    public static StandardMaterial3D Solid(string html, float roughness = .85f) => Solid(new Color(html), roughness);

    public static StandardMaterial3D Glow(Color color, float energy = 2f)
    {
        var key = $"g{color.ToHtml()}{energy}";
        if (!Cache.TryGetValue(key, out var mat))
        {
            mat = new StandardMaterial3D
            {
                AlbedoColor = color,
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = energy,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            Cache[key] = mat;
        }
        return mat;
    }

    /// <summary>A unique, fadable unshaded material (not cached).</summary>
    public static StandardMaterial3D Fade(Color color) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    public static MeshInstance3D Mesh(Node parent, Mesh mesh, Vector3 position, Material material, string name = "")
    {
        var node = new MeshInstance3D { Mesh = mesh, Position = position, MaterialOverride = material };
        if (name != "") node.Name = name;
        parent.AddChild(node);
        return node;
    }

    public static MeshInstance3D Box(Node parent, Vector3 position, Vector3 size, Material material, string name = "")
        => Mesh(parent, new BoxMesh { Size = size }, position, material, name);
}
