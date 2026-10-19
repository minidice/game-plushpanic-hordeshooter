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
}
