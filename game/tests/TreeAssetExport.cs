using Godot;
using System;
using System.Linq;
using System.Text.Json;

namespace HordeShooter.Tests;

/// <summary>Explicit authoring tool: snapshots the approved procedural tree geometry for portable GLB generation.</summary>
public partial class TreeAssetExport : Node
{
    public override void _Ready()
    {
        try
        {
            var meshes = new[] { ("pine", FoliageArt.Kind.Pine, 0), ("broadleaf", FoliageArt.Kind.Broadleaf, 0), ("oak", FoliageArt.Kind.Broadleaf, 2) }
                .Select(item =>
                {
                    var arrays = FoliageArt.Mesh(item.Item2, item.Item3).SurfaceGetArrays(0);
                    return new
                    {
                        id = item.Item1,
                        vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(v => new[] { v.X, v.Y, v.Z }),
                        normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array().Select(v => new[] { v.X, v.Y, v.Z }),
                        colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray().Select(c => new[] { c.R, c.G, c.B, c.A }),
                        indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array()
                    };
                });
            string path = ProjectSettings.GlobalizePath("res://../prototypes/outdoor-kit/source/tree-geometry.json");
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new { revision = "foliage-art-2026-10-24", meshes }));
            GD.Print("TREE_ASSET_EXPORT_PASS " + path);
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
