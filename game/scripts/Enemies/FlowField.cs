using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// One shared path field toward the player instead of per-enemy pathfinding (doc/idea.md 8.4).
/// The grid is built from the same boxes as the level walls; doors toggle cells at runtime.
/// Big maps (the Stage 2 size draft is 660 × 215 cells, I-109): every cell's step costs are worked out once per layout
/// and kept, the search runs on a bucket queue (costs are small whole numbers), and stamping a box only visits the cells
/// around it. The costs come out exactly as the plain Dijkstra over the whole grid gave them (tests/FlowBench).
/// </summary>
public sealed class FlowField
{
    public const int Unreachable = int.MaxValue;

    public float MinX { get; }
    public float MinZ { get; }
    public float Cell { get; }
    public int Width { get; }
    public int Depth { get; }

    /// <summary>Height tiers of the same cells (decisions.md L-15); shared by every copy. Null or flat: no tier rules.</summary>
    public TierMap? Tiers
    {
        get => _tiers;
        set { _tiers = value; _dirty = true; }
    }
    /// <summary>Who walks this field: the steps between tiers it may take (L-16). Survivors and the route use ladders.</summary>
    public MonsterClimb Mover
    {
        get => _mover;
        set { if (_mover != value) _dirty = true; _mover = value; }
    }
    private TierMap? _tiers;
    private MonsterClimb _mover = MonsterClimb.Ladder;
    /// <summary>Survivors' fields also take one-way rides (ziplines, L-18); monsters' fields never do.</summary>
    public bool Riders { get; set; }

    private readonly bool[] _blocked;
    private readonly int[] _cost;
    private int[] _targets = System.Array.Empty<int>();
    private bool _dirty = true;
    /// <summary>
    /// Per cell and neighbour direction (<see cref="Neighbors"/> order): what it costs a walker standing on that neighbour
    /// to step into the cell, or −1 when it may not. All of it again when the tiers or the mover change; when only walls
    /// changed (a door), just the cells next to a changed one (<see cref="_stepsWalls"/> is the walls it was made for).
    /// </summary>
    private short[] _steps = Array.Empty<short>();
    private bool[]? _stepsWalls;
    private int _stepsMax;
    private MonsterClimb _stepsMover;
    private int _stepsTierVersion = -1;
    private bool _wallsChanged;
    private List<int>[] _ring = Array.Empty<List<int>>();

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
        var copy = new FlowField(MinX, MinZ, MinX + Width * Cell, MinZ + Depth * Cell, Cell) { Tiers = Tiers, Mover = Mover, Riders = Riders };
        System.Array.Copy(_blocked, copy._blocked, _blocked.Length);
        copy.TakeSteps(this);
        return copy;
    }

    /// <summary>Works out the step costs now (a load screen) instead of on the first rebuild mid-fight.</summary>
    public void PrepareSteps() => EnsureSteps();

    /// <summary>Takes <paramref name="other"/>'s walls; its own step costs are kept and patched where walls differ.</summary>
    public void CopyWallsFrom(FlowField other)
    {
        System.Array.Copy(other._blocked, _blocked, _blocked.Length);
        _dirty = _wallsChanged = true;
    }

    /// <summary>
    /// Starts from <paramref name="other"/>'s step costs and the walls they were made for (a copy whose mover changes
    /// works them out again; one whose walls differ patches them).
    /// </summary>
    private void TakeSteps(FlowField other)
    {
        if (other._stepsWalls == null || other._steps.Length != _cost.Length * 8) return;
        _steps = (short[])other._steps.Clone();
        _stepsWalls = (bool[])other._stepsWalls.Clone();
        _stepsMax = other._stepsMax;
        _stepsMover = other._stepsMover;
        _stepsTierVersion = other._stepsTierVersion;
        _wallsChanged = true;
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
        var (xa, xb) = Span(x0, x1, MinX, Cell, Width);
        var (za, zb) = Span(z0, z1, MinZ, Cell, Depth);
        for (int z = za; z <= zb; z++)
        for (int x = xa; x <= xb; x++)
        {
            float cx = MinX + (x + .5f) * Cell, cz = MinZ + (z + .5f) * Cell;
            if (cx >= x0 && cx <= x1 && cz >= z0 && cz <= z1) _blocked[z * Width + x] = blocked;
        }
        _dirty = _wallsChanged = true;
    }

    /// <summary>
    /// Grid columns (or rows) whose centres can fall inside [<paramref name="a0"/>, <paramref name="a1"/>], one cell wider on
    /// each side so float rounding never drops one; callers still test each centre. Empty when the range misses the grid.
    /// </summary>
    public static (int First, int Last) Span(float a0, float a1, float min, float cell, int count)
    {
        float first = Mathf.Floor((a0 - min) / cell - .5f) - 1, last = Mathf.Ceil((a1 - min) / cell - .5f) + 1;
        return ((int)Mathf.Clamp(first, 0, count), (int)Mathf.Clamp(last, -1, count - 1));
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

    /// <summary>Makes <see cref="_steps"/> fit the current walls, tiers and mover.</summary>
    private void EnsureSteps()
    {
        var tiers = Tiers is { Any: true } t ? t : null;
        if (_stepsWalls == null || _steps.Length != _cost.Length * 8 || _stepsMover != Mover || _stepsTierVersion != (Tiers?.Version ?? -1))
        {
            _steps = new short[_cost.Length * 8];
            _stepsMax = 0;
            for (int cell = 0; cell < _cost.Length; cell++) _stepsMax = Math.Max(_stepsMax, StepRow(cell, tiers));
            _stepsWalls = (bool[])_blocked.Clone();
            _stepsMover = Mover;
            _stepsTierVersion = Tiers?.Version ?? -1;
            _wallsChanged = false;
            return;
        }
        if (!_wallsChanged) return;
        // A cell's steps read only its own and its 8 neighbours' walls: redo the rows around each changed cell.
        for (int cell = 0; cell < _cost.Length; cell++)
        {
            if (_blocked[cell] == _stepsWalls[cell]) continue;
            int x = cell % Width, z = cell / Width;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (InBounds(x + dx, z + dz)) _stepsMax = Math.Max(_stepsMax, StepRow((z + dz) * Width + x + dx, tiers));
        }
        System.Array.Copy(_blocked, _stepsWalls, _blocked.Length);
        _wallsChanged = false;
    }

    /// <summary>One cell's row of <see cref="_steps"/>; returns its dearest step.</summary>
    private int StepRow(int current, TierMap? tiers)
    {
        int cx = current % Width, cz = current / Width, max = 0;
        for (int k = 0; k < Neighbors.Length; k++)
        {
            var (dx, dz, step) = Neighbors[k];
            int nx = cx + dx, nz = cz + dz;
            short cost = -1;
            if (!_blocked[current] && InBounds(nx, nz))
            {
                int n = nz * Width + nx;
                // Costs spread from the target outward, so a walker would step from n to current.
                int extra = _blocked[n] || !CanStep(cx, cz, dx, dz) ? -1 : tiers?.StepCost(n, current, Mover) ?? 0;
                if (extra >= 0)
                {
                    cost = (short)(step + extra);
                    max = Math.Max(max, cost);
                }
            }
            _steps[current * 8 + k] = cost;
        }
        return max;
    }

    /// <summary>
    /// Dijkstra from the targets outward on a bucket queue (Dial): costs are whole numbers and no single step costs more
    /// than the ring is long, so a ring of buckets indexed by cost replaces the heap.
    /// </summary>
    private void Build(int[] starts)
    {
        EnsureSteps();
        var tiers = Tiers is { Any: true } t ? t : null;
        bool rides = tiers != null && Riders;
        int longest = Math.Max(_stepsMax, rides ? tiers!.MaxRideCost : 0);
        int size = 1;
        while (size <= longest) size <<= 1;
        if (_ring.Length != size)
        {
            _ring = new List<int>[size];
            for (int i = 0; i < size; i++) _ring[i] = new List<int>();
        }
        else foreach (var bucket in _ring) bucket.Clear();
        int mask = size - 1;
        Array.Fill(_cost, Unreachable);
        int pending = 0;
        foreach (int start in starts)
        {
            _cost[start] = 0;
            _ring[0].Add(start);
            pending++;
        }
        var offsets = new int[Neighbors.Length];
        for (int k = 0; k < Neighbors.Length; k++) offsets[k] = Neighbors[k].dz * Width + Neighbors[k].dx;
        for (int cost = 0; pending > 0; cost++)
        {
            var bucket = _ring[cost & mask];
            while (bucket.Count > 0)
            {
                int current = bucket[^1];
                bucket.RemoveAt(bucket.Count - 1);
                pending--;
                if (_cost[current] != cost) continue;
                int row = current * 8;
                for (int k = 0; k < offsets.Length; k++)
                {
                    int step = _steps[row + k];
                    if (step < 0) continue;
                    int n = current + offsets[k], next = cost + step;
                    if (next >= _cost[n]) continue;
                    _cost[n] = next;
                    _ring[next & mask].Add(n);
                    pending++;
                }
                if (!rides) continue;
                foreach (var (from, ride) in tiers!.RidesInto(current))
                {
                    if (_blocked[from]) continue;
                    int next = cost + ride;
                    if (next >= _cost[from]) continue;
                    _cost[from] = next;
                    _ring[next & mask].Add(from);
                    pending++;
                }
            }
        }
    }

    /// <summary>Diagonals may not cut wall corners.</summary>
    private bool CanStep(int x, int z, int dx, int dz)
    {
        if (dx == 0 || dz == 0) return true;
        int here = z * Width + x, sideX = z * Width + x + dx, sideZ = (z + dz) * Width + x;
        if (_blocked[sideX] || _blocked[sideZ]) return false;
        // Height tiers: nor may they cut a corner over a lower tier (the body would drop off the corner).
        return Tiers is not { Any: true } tiers || (tiers.StepCost(here, sideX, Mover) == 0 && tiers.StepCost(here, sideZ, Mover) == 0);
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
            if (Tiers is { Any: true } tiers && tiers.StepCost(i, n, Mover) < 0) continue;
            if (_cost[n] < best)
            {
                best = _cost[n];
                bestIndex = n;
            }
        }
        if (Riders && Tiers is { Any: true } rideTiers)
            foreach (var (to, ride) in rideTiers.RidesOut(i))
                // At the top the cell's own cost is the ride's, so an equal cost still means "ride" when no neighbour is lower.
                if (!_blocked[to] && _cost[to] != Unreachable && (bestIndex < 0 ? _cost[to] + ride <= best : _cost[to] + ride < best))
                {
                    best = _cost[to] + ride;
                    bestIndex = to;
                }
        if (bestIndex < 0) return Vector3.Zero;
        var dir = Center(bestIndex) - new Vector3(p.X, 0, p.Z);
        return dir.LengthSquared() > .0001f ? dir.Normalized() : Vector3.Zero;
    }

    /// <summary>The cell the field leads to next from <paramref name="p"/> (a neighbour, or a ride's far end), or −1.</summary>
    public int NextCell(Vector3 p)
    {
        var dir = Direction(p);
        if (dir == Vector3.Zero) return -1;
        int i = Index(p);
        if (Riders && Tiers is { Any: true } tiers)
            foreach (var (to, _) in tiers.RidesOut(i))
                if ((Center(to) - new Vector3(p.X, 0, p.Z)).Normalized().Dot(dir) > .999f) return to;
        return Index(p + dir * Cell);
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
