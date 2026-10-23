using Godot;
using System.Collections.Generic;

namespace HordeShooter;

public readonly record struct RayHit(Vector3 Position, Vector3 Normal, GodotObject? Collider, Rid Rid);

/// <summary>
/// Raycasts without leaving garbage behind. Every query/result/exclude list is a RefCounted wrapper;
/// with shotguns firing a dozen rays per shot, leaving them to the .NET finalizer races Godot's own
/// cleanup and can abort the engine ("gchandle.is_released"). Here they are disposed immediately.
/// </summary>
public static class Rays
{
    public static RayHit? Cast(Node3D context, Vector3 from, Vector3 to, uint mask, IReadOnlyList<Rid>? exclude = null)
    {
        using var query = PhysicsRayQueryParameters3D.Create(from, to, mask);
        if (exclude is { Count: > 0 })
        {
            // The typed wrapper is not disposable; dispose the untyped array it shares.
            using var raw = new Godot.Collections.Array();
            foreach (var rid in exclude) raw.Add(rid);
            query.Exclude = new Godot.Collections.Array<Rid>(raw);
        }
        using var hit = context.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return null;
        return new RayHit(hit["position"].AsVector3(), hit["normal"].AsVector3(), hit["collider"].AsGodotObject(), hit["rid"].AsRid());
    }

    public static bool Clear(Node3D context, Vector3 from, Vector3 to, uint mask) => Cast(context, from, to, mask) == null;

    /// <summary>
    /// Recover from the free portion of a grid cell marked by wall padding. A cheap adjacent cell may be across
    /// a thin wall; seek a nearby reachable cell on the same floor whose centre line is physically clear instead.
    /// Ordinary open-cell following stays on the cached field. Physics still owns the mover's body clearance.
    /// </summary>
    public static Vector3 FlowDirection(Node3D mover, FlowField field)
    {
        var from = mover.GlobalPosition;
        if (!field.IsBlocked(from)) return field.Direction(from);
        Vector3 best = Vector3.Zero;
        float distance = float.PositiveInfinity;
        foreach (var cell in field.OpenCellsNear(from, 0, 2.5f))
        {
            var to = cell with { Y = from.Y };
            float d = from.DistanceSquaredTo(to);
            if (d >= distance) continue;
            bool sameFloor = true;
            if (field.Tiers is { Any: true } tiers)
                for (int step = 1; step <= 10; step++)
                    if (Mathf.Abs(tiers.GroundY(from.Lerp(to, step / 10f)) - tiers.GroundY(from)) > .1f) { sameFloor = false; break; }
            if (!sameFloor || !Clear(mover, from + Vector3.Up * .5f, to + Vector3.Up * .5f, Layers.World | Layers.Props)) continue;
            distance = d;
            best = (to - from).Normalized();
        }
        return best;
    }
}
