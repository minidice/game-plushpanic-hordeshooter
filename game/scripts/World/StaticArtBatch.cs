using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Combines a small, static decoration group by material and shadow mode.
/// Call only on a visual-only root; gameplay nodes, animated doors and lights never belong here.</summary>
internal static class StaticArtBatch
{
    public static void Bake(Node3D root)
    {
        var groups = new Dictionary<(Material, GeometryInstance3D.ShadowCastingSetting), SurfaceTool>();
        var inverse = root.GlobalTransform.AffineInverse();
        foreach (var node in root.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var part = (MeshInstance3D)node;
            if (part.Mesh == null) continue;
            for (int i = 0; i < part.Mesh.GetSurfaceCount(); i++)
            {
                var material = part.MaterialOverride ?? part.GetSurfaceOverrideMaterial(i) ?? part.Mesh.SurfaceGetMaterial(i);
                if (material == null) continue;
                var key = (material, part.CastShadow);
                if (!groups.TryGetValue(key, out var surface))
                {
                    surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
                    groups.Add(key, surface);
                }
                // Primitive BoxMesh is indexed, while StageArt's bevel mesh is not. AppendFrom does not
                // reconcile mixed representations: an index buffer would make earlier bevel faces disappear.
                using var source = new SurfaceTool();
                source.CreateFrom(part.Mesh, i);
                source.Deindex();
                using var triangles = source.Commit();
                surface.AppendFrom(triangles, 0, inverse * part.GlobalTransform);
            }
        }
        foreach (Node child in root.GetChildren()) child.Free();
        foreach (var ((material, shadows), surface) in groups)
        {
            surface.SetMaterial(material);
            surface.Index();
            root.AddChild(new MeshInstance3D { Mesh = surface.Commit(), CastShadow = shadows });
            surface.Dispose();
        }
    }
}
