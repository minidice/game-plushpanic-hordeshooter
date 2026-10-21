using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Onslaught (code Survivor, decisions.md M-26 · M-27) on its own open field. Every person first picks three cards: a
/// slot 1 gun, a slot 2 gun and a slot 3 special weapon (<see cref="ArmingCards"/>); reserves never run out and slot 3
/// kinds recharge instead of reloading. Then masses of commons close in from every side; who comes is the mode table
/// (modes/survivor_spawns.csv, seconds since arming). Every kill fills a shared experience bar; each level gives every
/// person a level-up card. The run is cut into stretches (mode_rules.json survivor.segments_s): at the end of each
/// one a boss comes; a middle boss down gives everyone a special card and starts the next stretch's clock, the last
/// one down clears the match. Cards are drawn and applied by the deciding PC; a client is sent its own queue
/// (<see cref="CardCatalog"/>) and only sends back which card it picked.
/// </summary>
public partial class SurvivorRules : ModeRules
{
    /// <summary>Picks every person makes before the swarm starts (slot 1, 2 and 3).</summary>
    public const int ArmingPicks = 3;
    public const float SyncEvery = .5f;

    public override GameMode Mode => GameMode.Survivor;
    public SurvivorTuning Rules => Tuning.Survivor;
    /// <summary>The boss once it is out (the host's, or the client's puppet of it).</summary>
    public BossEnemy? Boss { get; private set; }
    public bool BossArrived { get; private set; }
    public int SpawnedThisMatch { get; private set; }
    public int NamedSent { get; private set; }
    /// <summary>Still picking weapons: no swarm, the clock has not started (shared through the objective).</summary>
    public bool Arming => Stage.Objective == Objective.ModeArming;
    /// <summary>Seconds since everyone armed (the schedule's clock); 0 while arming.</summary>
    public float MatchTime => Arming ? 0 : Mathf.Max(0, Time - _armedAt);
    /// <summary>Seconds of the rest after a named wave (no swarm groups), 0 when none.</summary>
    public float RestLeft => Mathf.Max(0, _restUntil - MatchTime);

    // ---------- stretches and bosses ----------
    /// <summary>Which stretch is running (0-based); its boss is the last one when it is the last stretch.</summary>
    public int Segment { get; private set; }
    public int Segments => Rules.Segments.Count;
    public bool FinalSegment => Segment >= Segments - 1;
    public float SegmentTime => Mathf.Max(0, MatchTime - _segmentStart);
    public float SegmentLength => Rules.Segments[Mathf.Min(Segment, Segments - 1)];
    /// <summary>Seconds until this stretch's boss comes (0 once it is out).</summary>
    public float BossIn => BossArrived ? 0 : Mathf.Max(0, SegmentLength - SegmentTime);

    // ---------- experience ----------
    public int Level { get; private set; } = 1;
    public float Xp { get; private set; }
    public float XpNeeded => Rules.XpFirst * Mathf.Pow(Rules.XpGrowth, Level - 1);
    public float XpFraction => _netXp >= 0 ? _netXp : Mathf.Clamp(Xp / XpNeeded, 0, 1);

    // ---------- cards ----------
    /// <summary>The card effects (decisions.md T-22's lab engine; the deciding PC runs them).</summary>
    public LabCards Cards { get; private set; } = null!;
    /// <summary>This PC's survivor's waiting offers (a client's is the host's copy).</summary>
    public CardQueue LocalQueue { get; } = new();
    public CardWindow Window { get; private set; } = null!;

    private float _spawnTimer = 1.5f, _namedThrough = -1, _restUntil, _armedAt, _segmentStart, _syncTimer, _netXp = -1;
    private readonly List<(Enemy Enemy, Vector3 Last)> _named = new();
    private readonly Dictionary<Player, CardQueue> _queues = new();
    private readonly HashSet<Player> _armed = new();
    private readonly HashSet<int> _pendingPicks = new();
    private readonly Dictionary<Player, (int Version, int Level, int Segment, bool Boss)> _sent = new();

    /// <summary>Everyone starts by arming (on every PC, so a client's clock starts when the host's does).</summary>
    public override Objective InitialObjective => Objective.ModeArming;

    public override void _Ready()
    {
        base._Ready();
        Cards = new LabCards { Name = "CardEffects", Stage = Stage };
        AddChild(Cards);
        Window = new CardWindow { Name = "CardWindow", Stage = Stage, Queue = LocalQueue };
        if (!Stage.Authoritative) Window.PickHandler = index =>
        {
            // The pick names its offer; until the host confirms it, that offer stays hidden here even if an older copy arrives.
            if (LocalQueue.Front is not { } offer) return;
            _pendingPicks.Add(offer.Serial);
            Stage.Net?.RequestCardPick(offer.Serial, index);
            LocalQueue.DropFront();
        };
        AddChild(Window);
        if (Stage.Authoritative) Stage.EnemyKilled += OnKilled;
    }

    public override void _ExitTree()
    {
        if (Stage.Authoritative) Stage.EnemyKilled -= OnKilled;
    }

    /// <summary>A survivor's queue on the deciding PC (the local one is <see cref="LocalQueue"/>).</summary>
    public CardQueue QueueOf(Player who)
    {
        if (who == Stage.Player) return LocalQueue;
        if (!_queues.TryGetValue(who, out var queue)) _queues[who] = queue = new CardQueue();
        return queue;
    }

    private int ArmingLeft(Player who) => QueueOf(who).Offers.Count(o => o.Kind == CardKind.Arming);

    public override string ObjectiveText => Stage.State == StageState.Cleared ? Loc.Text("ui_objective_survivor_cleared")
        : Arming ? Loc.Format("fmt_objective_survivor_arm", Mathf.Clamp(ArmingPicks - ArmingLeft(Stage.Player) + 1, 1, ArmingPicks), ArmingPicks)
        : BossArrived ? Loc.Text(FinalSegment ? "ui_objective_survivor_boss" : "ui_objective_survivor_midboss")
        : RestLeft > 0 ? Loc.Format("fmt_objective_survivor_rest", Mathf.CeilToInt(RestLeft), Clock(BossIn))
        : _named.Count > 0 ? Loc.Text("ui_objective_survivor_named")
        : Loc.Format("fmt_objective_survivor_hold", Clock(BossIn));

    public override string BarLabel => BossArrived
        ? Loc.Format("fmt_bar_boss", Loc.Text(FinalSegment ? "name_monster_boss_final" : "name_monster_boss"), Mathf.CeilToInt(BarFraction * 100))
        : Loc.Format("fmt_bar_boss_segment", Segment + 1, Segments, Clock(BossIn));
    public override Color BarColor => BossArrived ? UiTheme.Danger : UiTheme.Accent;
    public override string SubBarLabel => Loc.Format("fmt_hud_level", Level);
    public override float SubBarFraction => XpFraction;
    public override string ResultText => Stage.State == StageState.Cleared
        ? Loc.Text("msg_result_survivor_clear")
        : BossArrived ? Loc.Format("fmt_result_survivor_boss", Mathf.CeilToInt(BarFraction * 100)) : Loc.Format("fmt_result_survivor_lost", Clock(MatchTime));

    private bool _wasArming = true;

    protected override void Tick(float dt)
    {
        // Every survivor here keeps unlimited reserves; slot-3 kinds recharge on a cooldown (M-26).
        foreach (var p in Stage.Players)
            if (IsInstanceValid(p)) p.Loadout.UseOnslaughtAmmo(Rules.SpecialCooldown);
        if (_wasArming && !Arming)
        {
            // The clock starts when everyone has armed (a client sees it through the shared objective).
            _wasArming = false;
            _armedAt = Time;
            Stage.ShowNotice(Loc.Text("msg_survivor_armed"), 3);
        }
        // A client learns about the boss from its spawn event (it is a puppet there).
        if (Boss == null && !Stage.Authoritative && Stage.AllEnemies.OfType<BossEnemy>().FirstOrDefault() is { } puppet) Arrive(puppet);
        if (!BossArrived) BarFraction = Mathf.Clamp(SegmentTime / SegmentLength, 0, 1);
        else if (Stage.Authoritative && Boss != null) BarFraction = IsInstanceValid(Boss) && !Boss.IsDead ? Boss.Health / Boss.MaxHealth : 0;
        if (!Stage.Authoritative) return;

        // Everyone (also who joins later) gets the three arming offers once; the clock waits for the first ones.
        foreach (var p in Humans.Where(p => _armed.Add(p)))
            foreach (var offer in ArmingCards.Draw(Rules.WeaponPool, Rng)) QueueOf(p).Add(offer);
        if (Arming && Humans.Any() && Humans.All(p => ArmingLeft(p) == 0)) Stage.SetModeObjective(Objective.ModeGoal);
        SyncClients(dt);
    }

    public override void ApplyNetBar(float fraction)
    {
        if (BossArrived) BarFraction = fraction;
    }

    protected override void Step(float dt)
    {
        if (Arming) return;
        if (!BossArrived && SegmentTime >= SegmentLength) SpawnBoss();
        // A boss that left without dying (a check removed it) counts as down.
        if (BossArrived && (Boss == null || !IsInstanceValid(Boss))) BossDown();
        if (!BossArrived) UpdateNamed();
        _spawnTimer -= dt;
        if (_spawnTimer > 0 || RestLeft > 0) return;
        _spawnTimer = Rules.SpawnEvery;
        SpawnGroup();
    }

    /// <summary>The people who pick cards (AI squadmates keep their own weapons).</summary>
    private IEnumerable<Player> Humans => Stage.AlivePlayers.Where(p => !p.IsBot);

    // ---------- experience and cards ----------

    private void OnKilled(Enemy enemy, Vector3 direction, float force)
    {
        if (Stage.State != StageState.Playing) return;
        bool boss = enemy is BossEnemy && enemy == Boss;
        // A boss first gives its special card, then its experience (and maybe a level-up card after it).
        if (boss) BossDown();
        AddXp(enemy.Monster.Tier switch { MonsterTier.Boss => Rules.XpBoss, MonsterTier.Named => Rules.XpNamed, _ => Rules.XpCommon });
    }

    /// <summary>Shared experience; every level-up gives every person a level-up offer.</summary>
    public void AddXp(float amount)
    {
        if (!Stage.Authoritative || Arming) return;
        Xp += amount;
        while (Xp >= XpNeeded)
        {
            Xp -= XpNeeded;
            Level++;
            foreach (var p in Humans) QueueOf(p).Add(CardCatalog.LevelUp(Cards, Rng));
            Stage.ShowNotice(Loc.Format("fmt_level_up", Level), 2);
            Stage.Sfx.Play("pickup", 0, 1.4f);
        }
    }

    /// <summary>A client picked card <paramref name="index"/> of offer <paramref name="serial"/>; a pick for an offer already gone is ignored.</summary>
    public override void PickCard(Player who, int serial, int index)
    {
        var queue = QueueOf(who);
        if (Stage.Authoritative && queue.Front?.Serial == serial) queue.Choose(who, index);
    }

    /// <summary>Host: each client gets its queue, the level and the stretch when they change, and the bar twice a second.</summary>
    private void SyncClients(float dt)
    {
        if (Stage.Net is not { Role: NetRole.Host } net) return;
        _syncTimer -= dt;
        bool periodic = _syncTimer <= 0;
        if (periodic) _syncTimer = SyncEvery;
        foreach (var p in Humans.Where(p => p != Stage.Player))
        {
            var queue = QueueOf(p);
            var now = (queue.Version, Level, Segment, BossArrived);
            if (!periodic && _sent.TryGetValue(p, out var last) && last == now) continue;
            if (net.SendCards(p, EncodeFor(p))) _sent[p] = now;
        }
    }

    public byte[] EncodeFor(Player who)
    {
        var w = new NetWriter();
        w.U16(Level);
        w.Unit(XpFraction);
        w.U8(Segment);
        w.F32(SegmentTime);
        w.U8(BossArrived ? 1 : 0);
        CardCatalog.Write(w, QueueOf(who).Offers);
        return w.ToArray();
    }

    /// <summary>Client: the host's view of this survivor's cards and the run.</summary>
    public override void ApplyCardSync(byte[] data)
    {
        var r = new NetReader(data);
        Level = r.U16();
        _netXp = Mathf.Clamp(r.Unit(), 0, 1);
        Segment = r.U8();
        _segmentStart = MatchTime - r.F32();
        bool bossOut = r.U8() == 1;
        if (!bossOut && BossArrived) { BossArrived = false; Boss = null; }
        var offers = CardCatalog.Read(r);
        _pendingPicks.RemoveWhere(serial => offers.All(o => o.Serial != serial));
        LocalQueue.Replace(offers.Where(o => !_pendingPicks.Contains(o.Serial)));
    }

    // ---------- named and swarm ----------

    /// <summary>Named rows that are due arrive; the last one down starts a short rest.</summary>
    private void UpdateNamed()
    {
        bool had = _named.Count > 0;
        for (int i = _named.Count - 1; i >= 0; i--)
        {
            var (enemy, _) = _named[i];
            if (IsInstanceValid(enemy) && !enemy.IsDead) { _named[i] = (enemy, enemy.GlobalPosition); continue; }
            _named.RemoveAt(i);
        }
        if (had && _named.Count == 0 && Rules.RestAfterNamed > 0)
        {
            _restUntil = MatchTime + Rules.RestAfterNamed;
            Stage.ShowNotice(Loc.Format("fmt_survivor_rest", Mathf.CeilToInt(Rules.RestAfterNamed)), 3);
        }
        foreach (var row in Table.NamedDue(_namedThrough, MatchTime, Stage.ThemeId).ToList())
        {
            var leader = Stage.Leader.GlobalPosition;
            if (PickSpot(Stage.Flow.OpenCellsNear(leader, 11, 16)) is not { } at) continue;
            for (int i = 0; i < row.Count; i++)
            {
                var enemy = ModeSpawnTable.Spawn(Stage, row, Near(at));
                _named.Add((enemy, enemy.GlobalPosition));
            }
            NamedSent += row.Count;
            Stage.ShowNotice(Loc.Text("msg_survivor_named"), 3);
        }
        _namedThrough = MatchTime;
    }

    /// <summary>Group size grows from group_start to group_end over the whole run, and drops to boss_group_scale while a boss is out.</summary>
    public int GroupSize => BossArrived
        ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(Rules.GroupStart, Rules.GroupEnd, Progress) * Rules.BossGroupScale))
        : Mathf.RoundToInt(Mathf.Lerp(Rules.GroupStart, Rules.GroupEnd, Progress));

    /// <summary>How far through the run (0–1): finished stretches plus this one's clock.</summary>
    private float Progress => Mathf.Clamp((Rules.Segments.Take(Segment).Sum() + Mathf.Min(SegmentTime, SegmentLength)) / Rules.Total, 0, 1);

    /// <summary>A group of commons on a ring around a random person, off screen where possible.</summary>
    public int SpawnGroup()
    {
        int count = Mathf.Min(GroupSize, HordeDirector.MaxCommons - Stage.Director.AliveCommons);
        if (count <= 0) return 0;
        var survivors = Humans.ToList();
        if (survivors.Count == 0) return 0;
        var center = survivors[Rng.RandiRange(0, survivors.Count - 1)].GlobalPosition;
        if (PickSpot(Stage.Flow.OpenCellsNear(center, Rules.SpawnMin, Rules.SpawnMax)) is not { } at) return 0;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (Table.PickCommon(MatchTime, Stage.ThemeId, Rng) is not { } row) break;
            ModeSpawnTable.Spawn(Stage, row, Near(at));
            spawned++;
        }
        SpawnedThisMatch += spawned;
        return spawned;
    }

    // ---------- bosses ----------

    /// <summary>This stretch's boss comes now (also used by checks to skip the wait), with the stretch's health share.</summary>
    public BossEnemy SpawnBoss()
    {
        var leader = Stage.Leader.GlobalPosition;
        var at = PickSpot(Stage.Flow.OpenCellsNear(leader, 11, 16)) ?? leader + Vector3.Right * 10;
        var boss = Stage.SpawnEnemy<BossEnemy>(MonsterDatabase.Get(BossEnemy.MonsterId), at, awake: true);
        boss.SetHealthScale(Rules.SegmentBossHp[Mathf.Min(Segment, Rules.SegmentBossHp.Count - 1)]);
        Arrive(boss);
        return boss;
    }

    private void Arrive(BossEnemy boss)
    {
        Boss = boss;
        BossArrived = true;
        BarFraction = 1;
        Stage.ShowNotice(Loc.Text(FinalSegment ? "msg_survivor_boss" : "msg_survivor_midboss"), 4);
        Stage.Sfx.Play("bull_snort", 4, .6f);
        Stage.Cam.AddShake(.45f);
    }

    /// <summary>The last boss down clears the match; a middle one gives everyone a special card and starts the next stretch.</summary>
    private void BossDown()
    {
        if (!BossArrived) return;
        if (FinalSegment)
        {
            Stage.EndMode(true, "msg_survivor_clear");
            return;
        }
        BossArrived = false;
        Boss = null;
        foreach (var p in Humans) QueueOf(p).Add(CardCatalog.BossReward(Cards, Rng));
        Segment++;
        _segmentStart = MatchTime;
        Stage.ShowNotice(Loc.Format("fmt_survivor_midboss_down", Segment, Segments), 4);
    }
}
