using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// L4D-style AI director (simplified): ambient mobs on a timer, specials on a timer, a crescendo
/// event with continuous waves, and a relax rule that holds mobs back while intensity is high.
/// Everything spawns out of the player's sight and only where the flow field can reach the player.
/// </summary>
public partial class HordeDirector : Node
{
    public const int MaxCommons = 70;
    public const int MaxSpecials = 2;

    public bool Active { get; private set; }
    public bool CrescendoActive { get; private set; }
    public float CrescendoRemaining { get; private set; }
    public float CrescendoDuration { get; private set; }
    public float Intensity { get; private set; }
    public int MobsSpawned { get; private set; }
    public List<Vector3> SpawnPoints { get; } = new();

    private Stage Stage => GetParent<Stage>();
    private readonly RandomNumberGenerator _rng = new();
    private float _mobTimer, _specialTimer, _waveTimer;
    private int _crescendoSpecials;

    public void Begin()
    {
        if (Active) return;
        Active = true;
        _mobTimer = _rng.RandfRange(30, 45);
        _specialTimer = _rng.RandfRange(20, 30);
    }

    public void Stop()
    {
        Active = false;
        CrescendoActive = false;
    }

    public void StartCrescendo(float duration)
    {
        CrescendoActive = true;
        CrescendoDuration = CrescendoRemaining = duration;
        _waveTimer = 1.5f;
        _crescendoSpecials = 0;
        MakeNoise(Stage.Player.GlobalPosition, 60f);
    }

    public void ReportDamage(int damage) => Intensity = Mathf.Min(1, Intensity + damage / 60f);

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Intensity = Mathf.Max(0, Intensity - dt * .04f);
        if (!Active || Stage.Player.IsDead) return;

        if (CrescendoActive)
        {
            CrescendoRemaining -= dt;
            _waveTimer -= dt;
            if (_waveTimer <= 0)
            {
                _waveTimer = _rng.RandfRange(5.5f, 7.5f);
                SpawnMob(_rng.RandiRange(6, 9));
            }
            float elapsed = CrescendoDuration - CrescendoRemaining;
            if (_crescendoSpecials < 2 && elapsed > CrescendoDuration * (_crescendoSpecials + 1) / 3f)
            {
                _crescendoSpecials++;
                SpawnSpecial();
            }
            if (CrescendoRemaining <= 0) CrescendoActive = false;
            return;
        }

        _mobTimer -= dt;
        if (_mobTimer <= 0)
        {
            // Relax: if the player is already under pressure, wait a bit longer.
            if (Intensity > .7f || Stage.Player.Health < 25) _mobTimer = 10;
            else
            {
                SpawnMob(_rng.RandiRange(8, 12));
                _mobTimer = _rng.RandfRange(45, 70);
            }
        }
        _specialTimer -= dt;
        if (_specialTimer <= 0)
        {
            SpawnSpecial();
            _specialTimer = _rng.RandfRange(30, 45);
        }
    }

    public int AliveCommons => Stage.AllEnemies.Count(e => !e.IsDead && !e.IsSpecial);
    public int AliveSpecials => Stage.AllEnemies.Count(e => !e.IsDead && e.IsSpecial);

    public int SpawnMob(int count)
    {
        var point = PickSpawnPoint(14, 36);
        if (point == null) return 0;
        MobsSpawned++;
        return SpawnCluster(point.Value, count);
    }

    /// <summary>Summon a mob from around the player (balloon pop), still out of sight where possible.</summary>
    public int SummonAround(Vector3 center, int count)
    {
        // Shuffle and stop after a handful of hidden cells: a pop can be inside a 600-cell radius.
        var options = Stage.Flow.OpenCellsNear(center, 9, 14).OrderBy(_ => _rng.Randi())
            .Where(p => !Stage.Vision.CanSee(p)).Take(12).ToList();
        if (options.Count == 0) options = Stage.Flow.OpenCellsNear(center, 7, 14).ToList();
        if (options.Count == 0) return SpawnMob(count);
        int spawned = 0;
        for (int i = 0; i < 3 && spawned < count; i++)
            spawned += SpawnCluster(options[_rng.RandiRange(0, options.Count - 1)], (count - spawned + 2 - i) / (3 - i));
        return spawned;
    }

    private int SpawnCluster(Vector3 center, int count)
    {
        int room = MaxCommons - AliveCommons;
        count = Mathf.Min(count, room);
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            var offset = new Vector3(_rng.RandfRange(-1.4f, 1.4f), 0, _rng.RandfRange(-1.4f, 1.4f));
            var at = Stage.Flow.IsBlocked(center + offset) ? center : center + offset;
            Stage.SpawnCommon(at, awake: true);
            spawned++;
        }
        return spawned;
    }

    public Enemy? SpawnSpecial()
    {
        if (AliveSpecials >= MaxSpecials) return null;
        var point = PickSpawnPoint(16, 34);
        if (point == null) return null;
        return Stage.SpawnThemeSpecial(point.Value, awake: true);
    }

    public Vector3? PickSpawnPoint(float minDist, float maxDist)
    {
        var player = Stage.Player.GlobalPosition;
        var valid = SpawnPoints.Where(p =>
        {
            float d = new Vector2(p.X - player.X, p.Z - player.Z).Length();
            return d >= minDist && d <= maxDist && Stage.Flow.Reachable(p) && !Stage.Vision.CanSee(p);
        }).ToList();
        if (valid.Count > 0) return valid[_rng.RandiRange(0, valid.Count - 1)];
        // Nothing hidden in range: take the farthest reachable point we cannot see.
        return SpawnPoints
            .Where(p => Stage.Flow.Reachable(p) && !Stage.Vision.CanSee(p))
            .OrderByDescending(p => p.DistanceTo(player))
            .Select(p => (Vector3?)p)
            .FirstOrDefault();
    }

    /// <summary>Gunfire and pops wake idle infected nearby.</summary>
    public void MakeNoise(Vector3 at, float radius)
    {
        float r2 = radius * radius;
        foreach (var e in Stage.Enemies)
            if (!e.IsDead && e.GlobalPosition.DistanceSquaredTo(at) < r2) e.Awake = true;
    }
}
