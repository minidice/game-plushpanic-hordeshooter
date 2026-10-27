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
    /// <summary>
    /// A wave comes from two entries on opposite sides of the leader, this share from behind (L4D: 75% of mobs from behind),
    /// and each entry lets its monsters out a few at a time (Alien Swarm: batches every few seconds), so they do not arrive
    /// in one file. pacing-pressure-plan.md 4.2.
    /// </summary>
    public const float BehindShare = .65f;
    public const int BatchMin = 4, BatchMax = 6;
    public const float BatchGapMin = 3, BatchGapMax = 5;
    /// <summary>The second entry of the last wave, or null when it came from one (tests, debugging).</summary>
    public SpawnEntry? LastSecondEntry { get; private set; }
    /// <summary>Monsters of started waves still waiting at their entries.</summary>
    public int PendingCommons => _batches.Sum(b => b.Count);
    /// <summary>Entries and specials that do not fit the built level (dropped, also pushed as errors).</summary>
    public List<string> EncounterErrors { get; } = new();

    private Stage Stage => GetParent<Stage>();
    private readonly RandomNumberGenerator _rng = new();
    private readonly RandomNumberGenerator _placeRng = new() { Seed = 1 };
    private float _mobTimer, _specialTimer, _waveTimer, _alarmWaveTimer;
    private int _crescendoSpecials;
    private readonly List<Batch> _batches = new();
    private sealed class Batch { public SpawnEntry Entry = null!; public int Count; public float Wait; }
    /// <summary>The raid area the leader was in last frame (-1: none), to start a mob soon after walking into a new one.</summary>
    private int _raidArea = -1;
    /// <summary>Seconds of timed raids still held: the start, or the rest after a crescendo (stage2-floorplan.md 6장).</summary>
    private float _raidHold;
    /// <summary>The mission's way-back groups are out (L-27).</summary>
    public bool MissionRefilled { get; private set; }

    public void Begin()
    {
        if (Active) return;
        Active = true;
        if (Encounters?.Raids is { } raids)
        {
            _raidHold = raids.StartDelay;
            _mobTimer = _rng.RandfRange(raids.MobMin, raids.MobMax);
            _specialTimer = _rng.RandfRange(raids.SpecialMin, raids.SpecialMax);
            return;
        }
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

    /// <summary>Checks only: the start or after-crescendo hold ends now.</summary>
    public void EndRaidHoldForTest() => _raidHold = 0;
    /// <summary>Checks only: the crescendo runs out on the next physics frame.</summary>
    public void EndCrescendoForTest() { if (CrescendoActive) CrescendoRemaining = 0; }

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
                SpawnMob(_rng.RandiRange(Rules.AlarmWaveMin, Rules.AlarmWaveMax), trickle: false);
            }
        }
        TickBatches(dt);
        if (!Active) return;

        if (CrescendoActive)
        {
            CrescendoRemaining -= dt;
            _waveTimer -= dt;
            if (_waveTimer <= 0)
            {
                _waveTimer = _rng.RandfRange(5.5f, 7.5f);
                SpawnMob(_rng.RandiRange(6, 9), trickle: false);
            }
            float elapsed = CrescendoDuration - CrescendoRemaining;
            if (_crescendoSpecials < 2 && elapsed > CrescendoDuration * (_crescendoSpecials + 1) / 3f)
            {
                _crescendoSpecials++;
                SpawnSpecial();
            }
            if (CrescendoRemaining <= 0)
            {
                CrescendoActive = false;
                if (Encounters?.Raids is { } after) _raidHold = after.AfterCrescendo;
            }
            return;
        }

        if (Encounters?.Raids is { } raids)
        {
            _raidHold = Mathf.Max(0, _raidHold - dt);
            if (RaidsHeld) { _raidArea = -1; return; }
            int area = Encounters.AreaIndex(Stage.Leader.GlobalPosition.X);
            if (area != _raidArea)
            {
                // Walking into a large zone starts its pressure soon, whatever the running timer says.
                _raidArea = area;
                if (raids.FirstInAreaMax > 0) _mobTimer = Mathf.Min(_mobTimer, _rng.RandfRange(raids.FirstInAreaMin, raids.FirstInAreaMax));
            }
            TickRaids(dt, raids);
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

    /// <summary>
    /// Timed raids wait (stage2-floorplan.md 6장): at the start and after a crescendo, while a survivor is inside a safe zone
    /// (L-24), and while the leader is outside the travel areas. False on a stage without raid rules.
    /// </summary>
    public bool RaidsHeld
    {
        get
        {
            if (Encounters?.Raids is not { } raids) return false;
            if (_raidHold > 0 || InSafeZone()) return true;
            var areas = Encounters.Areas;
            return areas.Count > 0 && !raids.Areas.Contains(areas[Encounters.AreaIndex(Stage.Leader.GlobalPosition.X)].Id);
        }
    }

    private void TickRaids(float dt, RaidRules raids)
    {
        _mobTimer -= dt;
        if (_mobTimer <= 0)
        {
            // Rest after a peak: the team is hurt or the last fight was intense.
            if (Intensity > .7f || Stage.Player.Health < 25) _mobTimer = _rng.RandfRange(raids.RestMin, raids.RestMax);
            else
            {
                SpawnMob(_rng.RandiRange(raids.MobCountMin, raids.MobCountMax));
                _mobTimer = _rng.RandfRange(raids.MobMin, raids.MobMax);
            }
        }
        _specialTimer -= dt;
        if (_specialTimer <= 0)
        {
            SpawnSpecial();
            _specialTimer = _rng.RandfRange(raids.SpecialMin, raids.SpecialMax);
        }
    }

    /// <summary>A living person stands inside a safe zone (AI squadmates do not count: one stuck there must not stop the raids).</summary>
    public bool InSafeZone()
    {
        if (Encounters == null || Encounters.SafeZones.Count == 0) return false;
        foreach (var p in Stage.AlivePlayers)
            if (!p.IsBot)
                foreach (var zone in Encounters.SafeZones)
                if (Flat(p.GlobalPosition - zone.At).Length() < zone.Inside) return true;
        return false;
    }

    public int AliveCommons => Stage.AllEnemies.Count(e => !e.IsDead && !e.IsSpecial);
    public int AliveSpecials => Stage.AllEnemies.Count(e => !e.IsDead && e.IsSpecial);

    // ---------- waves ----------

    /// <summary>
    /// A wave of <paramref name="count"/> hunters (scaled for the party): split between an entry behind the leader and one
    /// on the other side (<see cref="BehindShare"/>). A timed mob lets a few out at a time; <paramref name="trickle"/> off
    /// (alarm and crescendo pulses, already a wave every few seconds) sends them all at once. Returns how many are on their way.
    /// </summary>
    public int SpawnMob(int count, bool trickle = true)
    {
        if (!Stage.Authoritative) return 0;
        bool behindFirst = _rng.Randf() < BehindShare;
        var first = PickEntry(14, 36, side: behindFirst ? 1 : -1);
        if (first == null) return 0;
        MobsSpawned++;
        int total = Stage.ScaleCommons(count);
        var second = total >= BatchMin * 2 ? PickEntry(14, 36, side: behindFirst ? -1 : 1, apartFrom: first) : null;
        LastSecondEntry = second;
        LastEntry = first;
        int behind = Mathf.CeilToInt(total * BehindShare);
        int firstCount = second == null ? total : behindFirst ? behind : total - behind;
        int sent = Release(first, firstCount, trickle);
        if (second != null) sent += Release(second, total - firstCount, trickle);
        return sent;
    }

    /// <summary>Lets the first few out at once and queues the rest at the same entry, a few every few seconds.</summary>
    private int Release(SpawnEntry entry, int count, bool trickle)
    {
        int room = MaxCommons - AliveCommons - PendingCommons;
        count = Mathf.Min(count, Mathf.Max(0, room));
        if (count <= 0) return 0;
        int now = trickle ? Mathf.Min(count, _rng.RandiRange(BatchMin, BatchMax)) : count;
        Place(entry, now);
        if (count > now) _batches.Add(new Batch { Entry = entry, Count = count - now, Wait = _rng.RandfRange(BatchGapMin, BatchGapMax) });
        return count;
    }

    private void TickBatches(float dt)
    {
        if (!Stage.Authoritative || _batches.Count == 0) return;
        for (int i = _batches.Count - 1; i >= 0; i--)
        {
            var batch = _batches[i];
            batch.Wait -= dt;
            if (batch.Wait > 0) continue;
            int n = Mathf.Min(batch.Count, _rng.RandiRange(BatchMin, BatchMax));
            Place(batch.Entry, n);
            batch.Count -= n;
            batch.Wait = _rng.RandfRange(BatchGapMin, BatchGapMax);
            if (batch.Count <= 0) _batches.RemoveAt(i);
        }
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
        // Scaled for the party once here, then placed as is.
        count = Stage.ScaleCommons(count);
        int spawned = 0;
        for (int i = 0; i < 3 && spawned < count; i++)
        {
            var at = options[_rng.RandiRange(0, options.Count - 1)];
            spawned += Place(new SpawnEntry { Kind = EntryKind.Hidden, At = at }, (count - spawned + 2 - i) / (3 - i));
        }
        return spawned;
    }

    /// <summary>Sends <paramref name="count"/> hunters (scaled for the party, M-5) in from <paramref name="entry"/>.</summary>
    public int SpawnWave(SpawnEntry entry, int count) => Stage.Authoritative ? Place(entry, Stage.ScaleCommons(count)) : 0;

    /// <summary>Exactly <paramref name="count"/> hunters (up to the swarm cap) from <paramref name="entry"/> (ledges jump down).</summary>
    private int Place(SpawnEntry entry, int count)
    {
        int room = MaxCommons - AliveCommons;
        count = Mathf.Min(count, room);
        // A ledge drop spreads along the wall top; the others bunch around the spot.
        var along = new Vector3(-entry.Dir.Z, 0, entry.Dir.X);
        var identities = Stage.CommonPack(entry.Ground, Mathf.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            if (entry.Kind == EntryKind.Ledge)
            {
                var enemy = Stage.SpawnCommonIdentity(identities[i], entry.At + along * _rng.RandfRange(-LedgeSpread, LedgeSpread) + Vector3.Up * .05f, awake: true);
                enemy.LeapDown(entry.Dir);
                continue;
            }
            var offset = new Vector3(_rng.RandfRange(-1.4f, 1.4f), 0, _rng.RandfRange(-1.4f, 1.4f));
            var at = Stage.Flow.IsBlocked(entry.Ground + offset) ? entry.Ground : entry.Ground + offset;
            Stage.SpawnCommonIdentity(identities[i], at, awake: true);
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
    /// <paramref name="side"/> +1 prefers entries behind the leader on the route, -1 ahead (falls back to any);
    /// <paramref name="apartFrom"/> asks for an entry on the other side of the leader from that one (null if there is none).
    public SpawnEntry? PickEntry(float minDist, float maxDist, bool ledges = true, int side = 0, SpawnEntry? apartFrom = null)
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
        if (apartFrom != null)
        {
            var away = Flat(apartFrom.Ground - player).Normalized();
            candidates = candidates.Where(e => e != apartFrom && Flat(e.Ground - player).Normalized().Dot(away) < 0).ToList();
            if (candidates.Count == 0) return null;
        }
        if (side != 0 && Stage.ExitFlow is { } exit)
        {
            int lead = exit.Distance(player);
            var sided = candidates.Where(e => exit.Distance(e.Ground) > lead == side > 0).ToList();
            if (sided.Count > 0) candidates = sided;
        }

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

    /// <summary>
    /// Near a stair or sewer landing (decisions.md L-11), or a safe zone (L-24: wider for waves than for waiting monsters):
    /// nothing waits or appears there. <paramref name="wave"/> false asks about monsters placed before anyone arrives.
    /// </summary>
    public bool IsProtected(Vector3 at, bool wave = true)
    {
        if (Encounters == null) return false;
        float r2 = Rules.ProtectRadius * Rules.ProtectRadius;
        foreach (var p in Encounters.Protected)
            if (Flat(p - at).LengthSquared() < r2) return true;
        foreach (var zone in Encounters.SafeZones)
            if (Flat(zone.At - at).Length() < (wave ? zone.NoWave : zone.NoPlace)) return true;
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
            if (Stage.Flow.IsBlocked(g.At) || IsProtected(g.At, wave: false))
                EncounterErrors.Add($"special in {g.Area} at ({g.At.X:0.#}, {g.At.Z:0.#}) is inside a wall or a protected landing");
        foreach (var error in EncounterErrors) GD.PushError("[spawn] " + error);
        FilledThrough = -1;
        MissionRefilled = false;
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
        foreach (var group in Encounters!.Groups.Where(g => g.Area == area.Id && g.After == "")) Place(group);
    }

    private void Place(EncounterGroup group)
    {
        if (group.Special >= 0)
        {
            if (!Stage.Flow.IsBlocked(group.At) && !IsProtected(group.At, wave: false))
                Stage.SpawnInitialSpecial(group.Special, group.At).Reaction = group.Reaction;
            return;
        }
        Scatter(group);
    }

    /// <summary>
    /// The mission is done: its way back fills again (decisions.md L-27), off screen first like any waiting group. Once per
    /// stage; only the host places monsters.
    /// </summary>
    public void FillAfterMission()
    {
        if (!Stage.Authoritative || Encounters == null || MissionRefilled) return;
        MissionRefilled = true;
        foreach (var group in Encounters.Groups.Where(g => g.After == "mission")) Place(group);
    }

    /// <summary>Random spots in the group's rectangle; off screen first, then anywhere open.</summary>
    private void Scatter(EncounterGroup group)
    {
        // Waiting commons scale with the party too (named ones in Fill do not, M-5).
        int want = group.Fixed ? group.Count : Stage.ScaleCommons(group.Count);
        var identities = Stage.CommonPack(new Vector3(group.Rect.GetCenter().X, 0, group.Rect.GetCenter().Y), want, group.Area);
        int placed = 0;
        for (int pass = 0; pass < 2 && placed < want; pass++)
            for (int attempt = 0; attempt < want * 20 && placed < want; attempt++)
            {
                var r = group.Rect;
                var p = new Vector3(_placeRng.RandfRange(r.Position.X, r.End.X), 0, _placeRng.RandfRange(r.Position.Y, r.End.Y));
                if (Stage.Flow.IsBlocked(p) || IsProtected(p, wave: false) || (pass == 0 && OnScreen(p))) continue;
                Stage.SpawnCommonIdentity(identities[placed], p, false).Reaction = group.Reaction;
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
