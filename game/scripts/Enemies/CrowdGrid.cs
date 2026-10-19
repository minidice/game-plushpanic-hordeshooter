using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Spatial hash rebuilt once per physics frame so separation steering stays O(n).</summary>
public sealed class CrowdGrid
{
    private const float CellSize = 1.5f;
    private const float Spacing = .35f;
    private readonly Dictionary<long, List<Enemy>> _cells = new();
    private readonly Stack<List<Enemy>> _pool = new();

    private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    public void Rebuild(IEnumerable<Enemy> enemies)
    {
        foreach (var list in _cells.Values)
        {
            list.Clear();
            _pool.Push(list);
        }
        _cells.Clear();
        foreach (var e in enemies)
        {
            if (e.IsDead) continue;
            var p = e.GlobalPosition;
            long key = Key(Mathf.FloorToInt(p.X / CellSize), Mathf.FloorToInt(p.Z / CellSize));
            if (!_cells.TryGetValue(key, out var list))
            {
                list = _pool.Count > 0 ? _pool.Pop() : new List<Enemy>();
                _cells[key] = list;
            }
            list.Add(e);
        }
    }

    /// <summary>Push-apart vector (XZ) away from overlapping neighbours.</summary>
    public Vector3 Separation(Enemy self)
    {
        var p = self.GlobalPosition;
        int cx = Mathf.FloorToInt(p.X / CellSize), cz = Mathf.FloorToInt(p.Z / CellSize);
        var push = Vector3.Zero;
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
            foreach (var other in list)
            {
                if (other == self || other.IsDead) continue;
                var d = p - other.GlobalPosition;
                d.Y = 0;
                float want = self.Radius + other.Radius + Spacing;
                float len = d.Length();
                if (len >= want) continue;
                if (len < .001f) d = new Vector3((self.GetInstanceId() & 1) == 0 ? 1 : -1, 0, .3f);
                push += d.Normalized() * (1 - len / want);
            }
        }
        return push.LimitLength(1.5f);
    }
}
