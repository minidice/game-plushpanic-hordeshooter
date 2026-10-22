using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HordeShooter;

/// <summary>Exact support vertices of each rigid mesh, cached independently of pose and scale.</summary>
internal sealed class CorpseSurface
{
    private sealed class Points(Mesh mesh)
    {
        public readonly Vector3[] Vertices = Hull(mesh);
        private static Vector3[] Hull(Mesh mesh)
        {
            // clean removes duplicate/interior points; simplify=false preserves the surface extrema.
            using var shape = mesh.CreateConvexShape(clean: true, simplify: false);
            return shape.Points;
        }
    }
    private static readonly ConditionalWeakTable<Mesh, Points> Cache = new();
    private readonly List<(MeshInstance3D Mesh, Vector3[] Points)> _parts = new();
    public CorpseSurface(Node root) => Collect(root);
    private void Collect(Node node)
    {
        if (node is MeshInstance3D { Mesh: not null } mesh && mesh.IsVisibleInTree() && !mesh.HasMeta("authored_grip"))
            _parts.Add((mesh, Cache.GetValue(mesh.Mesh, m => new Points(m)).Vertices));
        foreach (Node child in node.GetChildren()) Collect(child);
    }
    public float Bottom()
    {
        float bottom = float.PositiveInfinity;
        foreach (var (mesh, points) in _parts)
        {
            if (!GodotObject.IsInstanceValid(mesh) || !mesh.IsVisibleInTree()) continue;
            var world = mesh.GlobalTransform;
            var vertical = new Vector3(world.Basis.X.Y, world.Basis.Y.Y, world.Basis.Z.Y);
            foreach (var p in points) bottom = Mathf.Min(bottom, vertical.Dot(p) + world.Origin.Y);
        }
        return bottom;
    }
}
