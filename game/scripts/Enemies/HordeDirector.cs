using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// L4D-style AI director (simplified): ambient mobs on a timer, specials on a timer, a crescendo
/// event with continuous waves, and a relax rule that holds mobs back while intensity is high.
/// It also fills each area just before the survivors get there and runs loud devices (car alarm).
/// Every arrival should have a visible reason (decisions.md L-4): waves prefer entries off every screen
/// and outside the vision cone, or ledges the monsters visibly jump down from. That is a preference,
/// not a rule: with nothing better the old "outside the cone" spot is used.
/// </summary>
public partial class HordeDirector : Node
{
    public const int MaxCommons = 70;
    public const int MaxSpecials = 2;

    public bool Active { get; private set; }
    public bool CrescendoActive { get; private set; }
    public float CrescendoRemaining { get; private set; }
    public float CrescendoDuration { get; private set; }
    public float AlarmRemaining { get; private set; }
    public float Intensity { get; private set; }
    public int MobsSpawned { get; private set; }
    /// <summary>Fixed places waves come from (hidden spots and ledges); the walked trail is added at run time.</summary>
    public List<SpawnEntry> Entries { get; } = new();
    public EncounterSet? Encounters { get; private set; }
    /// <summary>Areas up to this index are filled (−1: none yet).</summary>
    public int FilledThrough { get; private set; } = -1;
    /// <summary>The entry the last wave or special came from (tests, debugging).</summary>
    public SpawnEntry? LastEntry { get; private set; }
    public SpawnRules Rules => SpawnDatabase.Current.Rules;
    /// <summary>Ledge monsters spread this far along the wall either way.</summary>
    public const float LedgeSpread = 1.8f;
    /// <summary>Entries and specials that do not fit the built level (dropped, also pushed as errors).</summary>
    public List<string> EncounterErrors { get; } = new();

    private Stage Stage => GetParent<Stage>();
    private readonly RandomNumberGenerator _rng = new();
    private readonly RandomNumberGenerator _placeRng = new() { Seed = 1 };
    private float _mobTimer, _specialTimer, _waveTimer, _alarmWaveTimer;
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
        AlarmRemaining = 0;
    }

    public void StartCrescendo(float duration)
    {
        CrescendoActive = true;
        CrescendoDuration = CrescendoRemaining = duration;
        _waveTimer = 1.5f;
        _crescendoSpecials = 0;
        MakeNoise(Stage.Leader.GlobalPosition, Rules.NoiseAlarm);
    }

    /// <summary>A loud device went off at <paramref name="at"/>: wakes the area and sends hunters for a while.</summary>
    public void StartAlarm(Vector3 at)
    {
        AlarmRemaining = Rules.AlarmTime;
        _alarmWaveTimer = 1f;
        MakeNoise(at, Rules.NoiseAlarm);
    }

    public void ReportDamage(int damage) => Intensity = Mathf.Min(1, Intensity + damage / 60f);

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Intensity = Mathf.Max(0, Intensity - dt * .04f);
        // Runs while any survivor lives (the host may be dead while the party plays on, I-73).
        if (!Stage.AlivePlayers.Any()) return;

        // Alarms are the survivors' own doing, so they run even before the director's timers start.
        // Tests that switch the director off still hear the alarm but get no waves.
        if (AlarmRemaining > 0)
        {
            AlarmRemaining -= dt;
            _alarmWaveTimer -= dt;
            if (_alarmWaveTimer <= 0 && Stage.DirectorEnabled)
            {
                _alarmWaveTimer = Rules.AlarmWaveEvery;
                SpawnMob(_rng.RandiRange(Rules.AlarmWaveMin, Rules.AlarmWaveMax));
            }
        }
        if (!Active) return;

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

    // ---------- waves ----------

    public int SpawnMob(int count)
    {
        if (!Stage.Authoritative) return 0;
        var entry = PickEntry(14, 36);
        if (entry == null) return 0;
        MobsSpawned++;
        return SpawnWave(entry, count);
    }

    /// <summary>Summon a mob from around the player (balloon pop): off screen and out of sight where possible.</summary>
    public int SummonAround(Vector3 center, int count)
    {
        if (!Stage.Authoritative) return 0;
        // Shuffle and stop after a handful of good cells: a pop can be inside a 600-cell radius.
        var cells = Stage.Flow.OpenCellsNear(center, 9, 14).OrderBy(_ => _rng.Randi()).ToList();
        var options = cells.Where(p => !OnScreen(p) && !Stage.Vision.CanSee(p) && !IsProtected(p)).Take(12).ToList();
        if (options.Count == 0) options = cells.Where(p => !OnScreen(p) && !IsProtected(p)).Take(12).ToList();
        if (options.Count == 0) options = cells.Where(p => !Stage.Vision.CanSee(p) && !IsProtected(p)).Take(12).ToList();
        if (options.Count == 0) options = Stage.Flow.OpenCellsNear(center, 7, 14).Where(p => !IsProtected(p)).ToList();
        if (options.Count == 0) return SpawnMob(count);
        int spawned = 0;
        for (int i = 0; i < 3 && spawned < count; i++)
        {
            var at = options[_rng.RandiRange(0, options.Count - 1)];
            spawned += SpawnWave(new SpawnEntry { Kind = EntryKind.Hidden, At = at }, (count - spawned + 2 - i) / (3 - i));
        }
        return spawned;
    }

    /// <summary>Sends <paramref name="count"/> hunters in from <paramref name="entry"/> (ledges jump down).</summary>
    public int SpawnWave(SpawnEntry entry, int count)
    {
        if (!Stage.Authoritative) return 0;
        int room = MaxCommons - AliveCommons;
        count = Mathf.Min(count, room);
        // A ledge drop spreads along the wall top; the others bunch around the spot.
        var along = new Vector3(-entry.Dir.Z, 0, entry.Dir.X);
        for (int i = 0; i < count; i++)
        {
            if (entry.Kind == EntryKind.Ledge)
            {
                var enemy = Stage.SpawnCommon(entry.At + along * _rng.RandfRange(-LedgeSpread, LedgeSpread) + Vector3.Up * .05f, awake: true);
                enemy.LeapDown(entry.Dir);
                continue;
            }
            var offset = new Vector3(_rng.RandfRange(-1.4f, 1.4f), 0, _rng.RandfRange(-1.4f, 1.4f));
            var at = Stage.Flow.IsBlocked(entry.Ground + offset) ? entry.Ground : entry.Ground + offset;
            Stage.SpawnCommon(at, awake: true);
        }
        return Mathf.Max(0, count);
    }

    public Enemy? SpawnSpecial()
    {
        if (!Stage.Authoritative) return null;
        if (AliveSpecials >= MaxSpecials) return null;
        // Specials walk in; jumping off a wall is for the commons.
        var entry = PickEntry(16, 34, ledges: false);
        if (entry == null) return null;
        return Stage.SpawnThemeSpecial(entry.Ground, awake: true);
    }

    /// <summary>
    /// Where the next wave comes from, best first (spawn-design.md 1장):
    /// 1) off every screen and outside the cone, or a ledge (its jump is the reason);
    /// 2) off every screen; 3) outside the cone; 4) the farthest reachable entry outside the cone.
    /// Candidates are the stage entries in range plus a walked-trail spot well behind the leader.
    /// </summary>
    public SpawnEntry? PickEntry(float minDist, float maxDist, bool ledges = true)
    {
        var player = Stage.Leader.GlobalPosition;
        var candidates = new List<SpawnEntry>();
        foreach (var e in Entries)
        {
            if (e.Kind == EntryKind.Ledge && !ledges) continue;
            float d = Flat(e.Ground - player).Length();
            if (d < minDist || d > maxDist || IsProtected(e.Ground) || !Stage.Flow.Reachable(e.Ground)) continue;
            candidates.Add(e);
        }
        if (Stage.TrailSpotBehind(Rules.TrailMin, Rules.TrailMax) is { } trail && !IsProtected(trail) && Stage.Flow.Reachable(trail))
            candidates.Add(new SpawnEntry { Kind = EntryKind.Trail, At = trail });

        var pick = candidates.Where(e => e.Kind == EntryKind.Ledge || (!OnScreen(e.At) && !Stage.Vision.CanSee(e.At))).ToList();
        if (pick.Count == 0) pick = candidates.Where(e => e.Kind != EntryKind.Ledge && !OnScreen(e.At)).ToList();
        if (pick.Count == 0) pick = candidates.Where(e => !Stage.Vision.CanSee(e.Ground)).ToList();
        if (pick.Count > 0) return LastEntry = pick[_rng.RandiRange(0, pick.Count - 1)];
        // Nothing in range: take the farthest reachable entry we cannot see.
        return LastEntry = Entries
            .Where(e => (ledges || e.Kind != EntryKind.Ledge) && Stage.Flow.Reachable(e.Ground) && !Stage.Vision.CanSee(e.Ground) && !IsProtected(e.Ground))
            .OrderByDescending(e => e.Ground.DistanceTo(player))
            .FirstOrDefault();
    }

    /// <summary>On any survivor's screen (with a body-sized margin).</summary>
    public bool OnScreen(Vector3 at) => Stage.Effects.IsOnScreen(at + Vector3.Up, 1.2f);

    /// <summary>Near a stair or sewer landing: nothing waits or appears there (decisions.md L-11).</summary>
    public bool IsProtected(Vector3 at)
    {
        if (Encounters == null) return false;
        float r2 = Rules.ProtectRadius * Rules.ProtectRadius;
        foreach (var p in Encounters.Protected)
            if (Flat(p - at).LengthSquared() < r2) return true;
        return false;
    }

    // ---------- pre-placed monsters ----------

    /// <summary>Takes the stage's areas and entries and fills the start area and the next one.</summary>
    public void UseEncounters(EncounterSet set)
    {
        Encounters = set;
        Entries.Clear();
        EncounterErrors.Clear();
        foreach (var e in set.Entries)
        {
            // A ledge must drop onto open floor along its whole spread, not onto a car or a crate.
            var along = new Vector3(-e.Dir.Z, 0, e.Dir.X);
            bool open = e.Kind != EntryKind.Ledge
                ? !Stage.Flow.IsBlocked(e.Ground)
                : new[] { -LedgeSpread, 0, LedgeSpread }.All(o => !Stage.Flow.IsBlocked(e.Ground + along * o));
            if (open) Entries.Add(e);
            else EncounterErrors.Add($"{e.Kind} entry at ({e.At.X:0.#}, {e.At.Z:0.#}) lands on a wall or prop");
        }
        foreach (var g in set.Groups.Where(g => g.Special >= 0))
            if (Stage.Flow.IsBlocked(g.At) || IsProtected(g.At))
                EncounterErrors.Add($"special in {g.Area} at ({g.At.X:0.#}, {g.At.Z:0.#}) is inside a wall or a protected landing");
        foreach (var error in EncounterErrors) GD.PushError("[spawn] " + error);
        FilledThrough = -1;
        UpdateAreas();
    }

    /// <summary>
    /// Fills every area up to one past the furthest survivor (decisions.md L-7): monsters are already there
    /// before anyone arrives, instead of appearing out of thin air.
    /// </summary>
    public void UpdateAreas()
    {
        // Only the host places monsters; a client gets them as spawn events (multiplayer-sync-design.md 3장).
        if (!Stage.Authoritative) return;
        if (Encounters == null || Encounters.Areas.Count == 0) return;
        int furthest = 0;
        foreach (var p in Stage.AlivePlayers)
            furthest = Mathf.Max(furthest, Encounters.AreaIndex(p.GlobalPosition.X));
        int target = Mathf.Min(furthest + 1, Encounters.Areas.Count - 1);
        while (FilledThrough < target) Fill(Encounters.Areas[++FilledThrough]);
    }

    private void Fill(EncounterArea area)
    {
        foreach (var group in Encounters!.Groups.Where(g => g.Area == area.Id))
        {
            if (group.Special >= 0)
            {
                if (!Stage.Flow.IsBlocked(group.At) && !IsProtected(group.At))
                    Stage.SpawnInitialSpecial(group.Special, group.At).Reaction = group.Reaction;
                continue;
            }
            Scatter(group);
        }
    }

    /// <summary>Random spots in the group's rectangle; off screen first, then anywhere open.</summary>
    private void Scatter(EncounterGroup group)
    {
        int placed = 0;
        for (int pass = 0; pass < 2 && placed < group.Count; pass++)
            for (int attempt = 0; attempt < group.Count * 20 && placed < group.Count; attempt++)
            {
                var r = group.Rect;
                var p = new Vector3(_placeRng.RandfRange(r.Position.X, r.End.X), 0, _placeRng.RandfRange(r.Position.Y, r.End.Y));
                if (Stage.Flow.IsBlocked(p) || IsProtected(p) || (pass == 0 && OnScreen(p))) continue;
                Stage.SpawnCommon(p, false).Reaction = group.Reaction;
                placed++;
            }
    }

    // ---------- waking ----------

    /// <summary>Gunfire, pops and devices wake idle monsters in the radius. Noise does not chain further.</summary>
    public void MakeNoise(Vector3 at, float radius)
    {
        float r2 = radius * radius;
        foreach (var e in Stage.Enemies)
            if (!e.IsDead && e.GlobalPosition.DistanceSquaredTo(at) < r2) e.Wake(alertOthers: false);
    }

    /// <summary>One hop of the alert chain: idle monsters near <paramref name="source"/> wake too (decisions.md L-5).</summary>
    public void AlertAround(Enemy source)
    {
        float r2 = Rules.AlertRadius * Rules.AlertRadius;
        var at = source.GlobalPosition;
        foreach (var e in Stage.Enemies)
            if (e != source && !e.IsDead && !e.Awake && e.GlobalPosition.DistanceSquaredTo(at) < r2) e.Wake(alertOthers: false);
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
