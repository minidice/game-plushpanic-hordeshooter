using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// One shared path field toward the player instead of per-enemy pathfinding (doc/idea.md 8.4).
/// The grid is built from the same boxes as the level walls; doors toggle cells at runtime.
/// </summary>
public sealed class FlowField
{
    public const int Unreachable = int.MaxValue;

    public float MinX { get; }
    public float MinZ { get; }
    public float Cell { get; }
    public int Width { get; }
    public int Depth { get; }

    private readonly bool[] _blocked;
    private readonly int[] _cost;
    private int[] _targets = System.Array.Empty<int>();
    private bool _dirty = true;

    private static readonly (int dx, int dz, int cost)[] Neighbors =
    {
        (1, 0, 10), (-1, 0, 10), (0, 1, 10), (0, -1, 10),
        (1, 1, 14), (1, -1, 14), (-1, 1, 14), (-1, -1, 14),
    };

    public FlowField(float minX, float minZ, float maxX, float maxZ, float cell = 1f)
    {
        MinX = minX;
        MinZ = minZ;
        Cell = cell;
        Width = Mathf.CeilToInt((maxX - minX) / cell);
        Depth = Mathf.CeilToInt((maxZ - minZ) / cell);
        _blocked = new bool[Width * Depth];
        _cost = new int[Width * Depth];
        Array.Fill(_cost, Unreachable);
    }

    /// <summary>Same walls, independent costs: used for a second target such as the lure.</summary>
    public FlowField CopyLayout()
    {
        var copy = new FlowField(MinX, MinZ, MinX + Width * Cell, MinZ + Depth * Cell, Cell);
        System.Array.Copy(_blocked, copy._blocked, _blocked.Length);
        return copy;
    }

    public int Index(Vector3 p)
    {
        int x = (int)Mathf.Floor((p.X - MinX) / Cell);
        int z = (int)Mathf.Floor((p.Z - MinZ) / Cell);
        return InBounds(x, z) ? z * Width + x : -1;
    }

    public Vector3 Center(int index) => new(MinX + (index % Width + .5f) * Cell, 0, MinZ + (index / Width + .5f) * Cell);

    private bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

    /// <summary>Mark (or clear) every cell whose center lies inside an XZ box grown by <paramref name="pad"/>.</summary>
    public void SetBlocked(Vector3 center, Vector3 size, float pad, bool blocked = true)
    {
        float x0 = center.X - size.X / 2 - pad, x1 = center.X + size.X / 2 + pad;
        float z0 = center.Z - size.Z / 2 - pad, z1 = center.Z + size.Z / 2 + pad;
        for (int z = 0; z < Depth; z++)
        for (int x = 0; x < Width; x++)
        {
            float cx = MinX + (x + .5f) * Cell, cz = MinZ + (z + .5f) * Cell;
            if (cx >= x0 && cx <= x1 && cz >= z0 && cz <= z1) _blocked[z * Width + x] = blocked;
        }
        _dirty = true;
    }

    public bool IsBlocked(Vector3 p)
    {
        int i = Index(p);
        return i < 0 || _blocked[i];
    }

    /// <summary>Rebuild costs toward <paramref name="target"/> if the target cell or the walls changed.</summary>
    public bool Update(Vector3 target)
    {
        _single[0] = target;
        return Update(_single);
    }
    private readonly Vector3[] _single = new Vector3[1];
    private readonly List<int> _cells = new();

    /// <summary>
    /// Costs toward the nearest of several targets (every living survivor), so each zombie walks to
    /// whoever is closest by path. Rebuilds only when a target cell or the walls changed.
    /// </summary>
    public bool Update(IReadOnlyList<Vector3> targets)
    {
        var cells = _cells;
        cells.Clear();
        foreach (var target in targets)
        {
            int index = Index(target);
            if (index < 0) continue;
            if (_blocked[index]) index = NearestOpen(index, target);
            if (index >= 0 && !cells.Contains(index)) cells.Add(index);
        }
        if (cells.Count == 0) return false;
        cells.Sort();
        if (!_dirty && cells.Count == _targets.Length && cells.SequenceEqual(_targets)) return false;
        _targets = cells.ToArray();
        _dirty = false;
        Build(_targets);
        return true;
    }

    private void Build(int[] starts)
    {
        Array.Fill(_cost, Unreachable);
        var open = new PriorityQueue<int, int>();
        foreach (int start in starts)
        {
            _cost[start] = 0;
            open.Enqueue(start, 0);
        }
        while (open.TryDequeue(out int current, out int cost))
        {
            if (cost > _cost[current]) continue;
            int cx = current % Width, cz = current / Width;
            foreach (var (dx, dz, step) in Neighbors)
            {
                int nx = cx + dx, nz = cz + dz;
                if (!InBounds(nx, nz)) continue;
                int n = nz * Width + nx;
                if (_blocked[n] || !CanStep(cx, cz, dx, dz)) continue;
                int next = cost + step;
                if (next >= _cost[n]) continue;
                _cost[n] = next;
                open.Enqueue(n, next);
            }
        }
    }

    /// <summary>Diagonals may not cut wall corners.</summary>
    private bool CanStep(int x, int z, int dx, int dz)
    {
        if (dx == 0 || dz == 0) return true;
        return !_blocked[z * Width + x + dx] && !_blocked[(z + dz) * Width + x];
    }

    /// <summary>
    /// Open cell closest to <paramref name="target"/>. Scan order must not decide: next to a thin wall the
    /// first open cell found can be on the far side of it.
    /// </summary>
    private int NearestOpen(int index, Vector3 target)
    {
        int x = index % Width, z = index / Width;
        int best = -1;
        float bestDist = float.MaxValue;
        for (int dz = -3; dz <= 3; dz++)
        for (int dx = -3; dx <= 3; dx++)
        {
            int nx = x + dx, nz = z + dz;
            if (!InBounds(nx, nz) || _blocked[nz * Width + nx]) continue;
            var c = Center(nz * Width + nx);
            float d = (c.X - target.X) * (c.X - target.X) + (c.Z - target.Z) * (c.Z - target.Z);
            if (d < bestDist)
            {
                bestDist = d;
                best = nz * Width + nx;
            }
        }
        return best;
    }

    public int Distance(Vector3 p)
    {
        int i = Index(p);
        if (i < 0) return Unreachable;
        if (_blocked[i])
        {
            int best = Unreachable;
            int x = i % Width, z = i / Width;
            foreach (var (dx, dz, _) in Neighbors)
                if (InBounds(x + dx, z + dz)) best = Math.Min(best, _cost[(z + dz) * Width + x + dx]);
            return best;
        }
        return _cost[i];
    }

    public bool Reachable(Vector3 p) => Distance(p) != Unreachable;

    /// <summary>Unit XZ direction to walk from <paramref name="p"/> toward the target, or zero if stuck.</summary>
    public Vector3 Direction(Vector3 p)
    {
        int i = Index(p);
        if (i < 0) return Vector3.Zero;
        int x = i % Width, z = i / Width;
        int best = _blocked[i] ? Unreachable : _cost[i];
        int bestIndex = -1;
        foreach (var (dx, dz, _) in Neighbors)
        {
            int nx = x + dx, nz = z + dz;
            if (!InBounds(nx, nz)) continue;
            int n = nz * Width + nx;
            if (_blocked[n] || (!_blocked[i] && !CanStep(x, z, dx, dz))) continue;
            if (_cost[n] < best)
            {
                best = _cost[n];
                bestIndex = n;
            }
        }
        if (bestIndex < 0) return Vector3.Zero;
        var dir = Center(bestIndex) - new Vector3(p.X, 0, p.Z);
        return dir.LengthSquared() > .0001f ? dir.Normalized() : Vector3.Zero;
    }

    public IEnumerable<Vector3> OpenCellsNear(Vector3 center, float minRadius, float maxRadius)
    {
        int ci = Index(center);
        if (ci < 0) yield break;
        int r = Mathf.CeilToInt(maxRadius / Cell);
        int x = ci % Width, z = ci / Width;
        for (int dz = -r; dz <= r; dz++)
        for (int dx = -r; dx <= r; dx++)
        {
            int nx = x + dx, nz = z + dz;
            if (!InBounds(nx, nz)) continue;
            int n = nz * Width + nx;
            if (_blocked[n] || _cost[n] == Unreachable) continue;
            var c = Center(n);
            float d = new Vector2(c.X - center.X, c.Z - center.Z).Length();
            if (d >= minRadius && d <= maxRadius) yield return c;
        }
    }
}
