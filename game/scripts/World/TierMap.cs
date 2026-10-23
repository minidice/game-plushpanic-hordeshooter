using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Height tiers (decisions.md L-15, doc/elevation-tiers.md): every flow-field cell has a whole tier 0, 1, 2… of
/// <see cref="Height"/> metres. Combat and walking compare tiers instead of real 3D geometry: shots go down a tier but
/// not up, melee reaches one tier across an edge, and the walkable steps between tiers are ramps (both ways) and drop
/// edges (down only). A map that paints nothing stays flat and every caller keeps its old flat-ground path
/// (<see cref="Any"/> is false), so the existing stages behave exactly as before.
/// </summary>
public sealed class TierMap
{
    public const float Height = 1.2f;
    /// <summary>How close to a tier edge two fighters must stand to reach each other across it (elevation-tiers.md 6장).</summary>
    public const float EdgeBand = .8f;

    [Flags]
    private enum CellFlag : byte { None = 0, Ramp = 1, Drop = 2, Climb = 4 }

    /// <summary>
    /// Extra flow cost of going up or down one tier per mover, in flow units (10 = one metre). Seconds × walking pace
    /// (~4.4 m/s): ladder up 1 s, down 0.4 s; wall climb 1.5 s; a jump or a float is quick (doc/elevation-tiers.md 4장).
    /// </summary>
    public const int LadderUpCost = 44, LadderDownCost = 18, WallUpCost = 66, WallDownCost = 20, JumpCost = 10, FlyCost = 6;
    /// <summary>The highest a jumper gets up in one go (L-16).</summary>
    public const int MaxJumpTiers = 2;

    public float MinX { get; }
    public float MinZ { get; }
    public float Cell { get; }
    public int Width { get; }
    public int Depth { get; }
    /// <summary>False until a map paints a tier or a ramp: all tier rules are skipped on flat maps.</summary>
    public bool Any { get; private set; }
    public int MaxTier { get; private set; }
    public int MinTier { get; private set; }

    private readonly sbyte[] _tier;
    private readonly float[] _ground;
    private readonly CellFlag[] _flags;
    /// <summary>Ladder ends: each cell maps to the cell at the ladder's other end.</summary>
    private readonly Dictionary<int, int> _ladders = new();
    /// <summary>One-way rides for survivors only (ziplines, L-18): out of a cell, and into a cell, with their flow cost.</summary>
    private readonly Dictionary<int, List<(int to, int cost)>> _ridesOut = new();
    private readonly Dictionary<int, List<(int from, int cost)>> _ridesInto = new();

    public TierMap(float minX, float minZ, int width, int depth, float cell)
    {
        MinX = minX;
        MinZ = minZ;
        Width = width;
        Depth = depth;
        Cell = cell;
        _tier = new sbyte[width * depth];
        _ground = new float[width * depth];
        _flags = new CellFlag[width * depth];
    }

    public TierMap(FlowField flow) : this(flow.MinX, flow.MinZ, flow.Width, flow.Depth, flow.Cell) { }

    public static int TierOfHeight(float y) => Mathf.RoundToInt(y / Height);

    /// <summary>
    /// The tier whose floor a point is on or just above: a blast or a shot at chest height still belongs to the
    /// floor under it, while a point resting on a tier's floor counts as that tier.
    /// </summary>
    public static int FloorTier(float y) => Mathf.FloorToInt((y + .05f) / Height);

    public int Index(Vector3 p)
    {
        int x = (int)Mathf.Floor((p.X - MinX) / Cell);
        int z = (int)Mathf.Floor((p.Z - MinZ) / Cell);
        return x >= 0 && z >= 0 && x < Width && z < Depth ? z * Width + x : -1;
    }

    /// <summary>The tier painted under <paramref name="p"/> (0 off the map).</summary>
    public int TierAt(Vector3 p)
    {
        int i = Index(p);
        return i < 0 ? 0 : _tier[i];
    }

    /// <summary>Floor height under <paramref name="p"/>: the tier's top, or partway up a ramp.</summary>
    public float GroundY(Vector3 p)
    {
        int i = Index(p);
        return i < 0 ? 0 : _ground[i];
    }

    /// <summary>The tier a body stands on, from its height (falling bodies count where they are).</summary>
    public int TierOf(Node3D body) => TierOfHeight(body.GlobalPosition.Y);

    // ---------- painting (StageMap) ----------

    private void Paint(float x0, float x1, float z0, float z1, Action<int, float, float> cell)
    {
        for (int z = 0; z < Depth; z++)
        for (int x = 0; x < Width; x++)
        {
            float cx = MinX + (x + .5f) * Cell, cz = MinZ + (z + .5f) * Cell;
            if (cx >= x0 && cx <= x1 && cz >= z0 && cz <= z1) cell(z * Width + x, cx, cz);
        }
    }

    private void Touch(int tier)
    {
        MaxTier = Math.Max(MaxTier, tier);
        MinTier = Math.Min(MinTier, tier);
        if (tier != 0) Any = true;
    }

    /// <summary>A flat floor at <paramref name="tier"/>: its cells stop being ramps or drop edges.</summary>
    public void PaintTier(float x0, float x1, float z0, float z1, int tier)
    {
        Touch(tier);
        Paint(x0, x1, z0, z1, (i, _, _) =>
        {
            _tier[i] = (sbyte)tier;
            _ground[i] = tier * Height;
            _flags[i] = CellFlag.None;
        });
    }

    /// <summary>
    /// A ramp from <paramref name="lowTier"/> up one tier, rising along <paramref name="up"/> (a unit X or Z direction).
    /// Its cells belong to the low tier and connect to the tier above at the high end.
    /// </summary>
    public void PaintRamp(float x0, float x1, float z0, float z1, int lowTier, Vector3 up)
    {
        Touch(lowTier);
        Touch(lowTier + 1);
        Any = true;
        Paint(x0, x1, z0, z1, (i, cx, cz) =>
        {
            float t = up.X > .5f ? (cx - x0) / (x1 - x0) : up.X < -.5f ? (x1 - cx) / (x1 - x0)
                : up.Z > .5f ? (cz - z0) / (z1 - z0) : (z1 - cz) / (z1 - z0);
            _tier[i] = (sbyte)lowTier;
            _ground[i] = (lowTier + Mathf.Clamp(t, 0, 1)) * Height;
            _flags[i] = CellFlag.Ramp;
        });
    }

    /// <summary>Cells on a tier's rim from which anyone may step off and drop to the tier below (one way).</summary>
    public void PaintDrop(float x0, float x1, float z0, float z1) =>
        Paint(x0, x1, z0, z1, (i, _, _) => _flags[i] |= CellFlag.Drop);

    /// <summary>
    /// A climbable stretch of cliff (rock, rough wall): paint the cells on both sides of the edge. Wall climbers go up or
    /// down where either cell is marked; smooth walls, fences and containers are simply not painted (L-16).
    /// </summary>
    public void PaintClimb(float x0, float x1, float z0, float z1) =>
        Paint(x0, x1, z0, z1, (i, _, _) => _flags[i] |= CellFlag.Climb);

    /// <summary>A ladder between two neighbouring cells, one at the cliff foot and one on the tier above.</summary>
    public void AddLadder(Vector3 bottom, Vector3 top)
    {
        int a = Index(bottom), b = Index(top);
        if (a < 0 || b < 0 || a == b) return;
        _ladders[a] = b;
        _ladders[b] = a;
        Any = true;
    }

    /// <summary>
    /// A one-way ride from <paramref name="from"/> to <paramref name="to"/> taking <paramref name="seconds"/> (a zipline).
    /// Only fields that let survivors ride (<see cref="FlowField.Riders"/>) use it; monsters never do (L-18).
    /// </summary>
    public void AddRide(Vector3 from, Vector3 to, float seconds)
    {
        int a = Index(from), b = Index(to);
        if (a < 0 || b < 0 || a == b) return;
        int cost = Mathf.RoundToInt(seconds * 44);
        if (!_ridesOut.TryGetValue(a, out var outs)) _ridesOut[a] = outs = new();
        outs.Add((b, cost));
        if (!_ridesInto.TryGetValue(b, out var ins)) _ridesInto[b] = ins = new();
        ins.Add((a, cost));
        Any = true;
    }

    public IReadOnlyList<(int to, int cost)> RidesOut(int cell) => _ridesOut.TryGetValue(cell, out var l) ? l : Array.Empty<(int, int)>();
    public IReadOnlyList<(int from, int cost)> RidesInto(int cell) => _ridesInto.TryGetValue(cell, out var l) ? l : Array.Empty<(int, int)>();

    /// <summary>The cell at the other end of a ladder starting in <paramref name="cell"/>, or −1.</summary>
    public int LadderEnd(int cell) => _ladders.TryGetValue(cell, out int other) ? other : -1;

    // ---------- walking ----------

    /// <summary>
    /// Can a walker step from cell <paramref name="from"/> to the neighbour <paramref name="to"/>? Same tier always;
    /// up or down one tier across a ramp's high end; down any number of tiers from a drop edge. Nothing else.
    /// </summary>
    public bool CanStep(int from, int to)
    {
        int tf = _tier[from], tt = _tier[to];
        if (tf == tt) return true;
        if (tt == tf + 1 && (_flags[from] & CellFlag.Ramp) != 0) return true;
        if (tf == tt + 1 && (_flags[to] & CellFlag.Ramp) != 0) return true;
        return tt < tf && (_flags[from] & CellFlag.Drop) != 0;
    }

    /// <summary>
    /// Extra flow cost for <paramref name="mover"/> to go from cell <paramref name="from"/> to its neighbour
    /// <paramref name="to"/>, or −1 when it cannot (L-16): everyone walks a tier, ramps and drop edges; ladder users and
    /// wall climbers also take ladders; wall climbers climb painted walls; jumpers go up or down two tiers anywhere;
    /// flyers anywhere.
    /// </summary>
    public int StepCost(int from, int to, MonsterClimb mover)
    {
        if (CanStep(from, to)) return 0;
        int tf = _tier[from], tt = _tier[to];
        int rise = tt - tf, tiers = Math.Abs(rise);
        if ((mover is MonsterClimb.Ladder or MonsterClimb.Wall) && _ladders.TryGetValue(from, out int end) && end == to)
            return tiers * (rise > 0 ? LadderUpCost : LadderDownCost);
        return mover switch
        {
            MonsterClimb.Wall when ((_flags[from] | _flags[to]) & CellFlag.Climb) != 0 => tiers * (rise > 0 ? WallUpCost : WallDownCost),
            MonsterClimb.Jump when tiers <= MaxJumpTiers => tiers * JumpCost,
            MonsterClimb.Fly => tiers * FlyCost,
            _ => -1,
        };
    }

    public int TierOfCell(int cell) => _tier[cell];

    /// <summary>The cells' centres and floors (for a climb's start and end).</summary>
    public Vector3 CellFloor(int cell) => new(MinX + (cell % Width + .5f) * Cell, _ground[cell], MinZ + (cell / Width + .5f) * Cell);

    /// <summary>Whether walking (not being shoved) from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
    public bool CanWalk(Vector3 from, Vector3 to)
    {
        if (!Any) return true;
        int a = Index(from), b = Index(to);
        if (a < 0 || b < 0 || a == b) return true;
        return CanStep(a, b);
    }

    // ---------- fighting ----------

    /// <summary>
    /// Bodies whose floor-plan circle the line from <paramref name="origin"/> along <paramref name="direction"/> crosses
    /// within <paramref name="range"/>, nearest first. Shots fired from a tier pass over everything on lower tiers, so
    /// targets down there are found on the floor plan instead of by the ray (elevation-tiers.md 6장).
    /// </summary>
    public static List<(T body, float along)> Along<T>(IEnumerable<T> bodies, Func<T, float> radius, Func<T, bool> include,
        Vector3 origin, Vector3 direction, float range) where T : Node3D
    {
        var flat = new Vector3(direction.X, 0, direction.Z);
        var list = new List<(T, float)>();
        if (flat.LengthSquared() < .0001f) return list;
        flat = flat.Normalized();
        foreach (var body in bodies)
        {
            if (!GodotObject.IsInstanceValid(body) || !include(body)) continue;
            var to = body.GlobalPosition - origin;
            to.Y = 0;
            float along = to.Dot(flat);
            if (along <= 0 || along > range) continue;
            if ((to - flat * along).Length() > radius(body)) continue;
            list.Add((body, along));
        }
        list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        return list;
    }

    /// <summary>Is <paramref name="p"/> within <see cref="EdgeBand"/> of a cell of <paramref name="tier"/>?</summary>
    public bool NearTier(Vector3 p, int tier, float band = EdgeBand)
    {
        int i = Index(p);
        if (i < 0) return false;
        float reach = band + Cell * .5f;
        int r = Mathf.CeilToInt(reach / Cell);
        int x = i % Width, z = i / Width;
        for (int dz = -r; dz <= r; dz++)
        for (int dx = -r; dx <= r; dx++)
        {
            int nx = x + dx, nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= Width || nz >= Depth) continue;
            int n = nz * Width + nx;
            if (_tier[n] != tier) continue;
            float cx = MinX + (nx + .5f) * Cell, cz = MinZ + (nz + .5f) * Cell;
            if (new Vector2(cx - p.X, cz - p.Z).Length() <= reach) return true;
        }
        return false;
    }

    /// <summary>
    /// Melee between two bodies (elevation-tiers.md 6장): the same tier, or one tier apart with both standing at the
    /// edge between them. Two or more tiers apart never reach.
    /// </summary>
    public bool MeleeReach(Vector3 a, Vector3 b)
    {
        int ta = TierOfHeight(a.Y), tb = TierOfHeight(b.Y);
        if (ta == tb) return true;
        if (Math.Abs(ta - tb) != 1) return false;
        return NearTier(a, tb) && NearTier(b, ta);
    }

    /// <summary>
    /// The cursor's point on the ground (elevation-tiers.md 6장): the ray meets each tier's aim plane from the top down
    /// and stops at the first tier that is actually painted there. Null when it misses every plane.
    /// </summary>
    public Vector3? AimPoint(Vector3 origin, Vector3 direction, float aimHeight)
    {
        for (int t = MaxTier; t >= MinTier; t--)
        {
            var hit = new Plane(Vector3.Up, t * Height + aimHeight).IntersectsRay(origin, direction);
            if (hit is { } p && TierAt(p) == t) return p;
        }
        return null;
    }
}
