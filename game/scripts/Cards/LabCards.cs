using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>The six cards of the first lab set (doc/onslaught-card-ideas.md 5장).</summary>
public enum LabCard { Balloon, Velcro, StuffingBurst, Billiards, HonkReload, RubberDuck }

/// <summary>
/// Card lab prototype (decisions.md M-27, `[임시]`): six Onslaught cards that can be switched on and off while
/// playing, to see whether the goofy chains are fun and readable before the real card system exists. Every card
/// works on the shared states of <see cref="Enemy"/> (inflate · float · sticky · flying), so they chain by
/// themselves: a velcro clump floats together, a balloon pop throws monsters into others (billiards), a stuffing
/// burst does the same. Only the deciding PC runs it. Numbers are lab guesses in this file, not data yet.
/// </summary>
public partial class LabCards : Node
{
    public static readonly LabCard[] All = { LabCard.Balloon, LabCard.Velcro, LabCard.StuffingBurst, LabCard.Billiards, LabCard.HonkReload, LabCard.RubberDuck };

    // Balloon rounds: each hit inflates, a full monster floats; shooting a floating one pops it.
    private const float InflatePerHit = .12f, InflatePerDamage = 1 / 250f, FloatSeconds = 4f;
    private const float PopRadius = 3f, PopPower = 110f, PopDamage = 20f;
    // Velcro: sticky monsters close together drag into one clump; five in a clump burst.
    private const float StickySeconds = 6f, ClumpReach = .6f, ClumpPull = 2.5f, ClumpSlow = .35f;
    private const int ClumpBurstSize = 5;
    private const float ClumpBurstRadius = 3.2f, ClumpBurstPower = 140f, ClumpBurstDamage = 35f;
    // Stuffing burst on every death, capped per frame so a chain cannot run away.
    private const float StuffingRadius = 2.6f, StuffingPower = 95f, StuffingDamage = 6f;
    private const int StuffingPerFrame = 8;
    // Billiards: a monster flying faster than this passes most of its push on to whoever it hits.
    private const float FlyingSpeed = 4.5f, BilliardsShare = .8f, BilliardsKeep = .55f, BilliardsCooldown = .4f;
    // Honk on reload.
    private const float HonkRadius = 3.5f, HonkPower = 130f, HonkDamage = 5f;
    // Rubber duck: every so many hits a squeaky duck drops, gathers commons, then pops.
    private const int DuckEveryHits = 10;

    public Stage Stage { get; set; } = null!;
    private readonly HashSet<LabCard> _on = new();
    private readonly Dictionary<Enemy, float> _billiardsReady = new();
    private readonly Dictionary<Enemy, int> _clumpOf = new();
    private readonly Dictionary<Player, bool> _wasReloading = new();
    private readonly HashSet<Enemy> _popping = new();
    private int _stuffingThisFrame, _hitsSinceDuck;
    private float _clock;
    private ThrowableDefinition? _duck;

    // Counters for checks and the lab overlay.
    public int Floats { get; private set; }
    public int Pops { get; private set; }
    public int LargestClump { get; private set; }
    public int ClumpBursts { get; private set; }
    public int StuffingBursts { get; private set; }
    public int BilliardHits { get; private set; }
    public int Honks { get; private set; }
    public int Ducks { get; private set; }

    /// <summary>How many monsters are in <paramref name="enemy"/>'s velcro clump this frame (0 = none).</summary>
    public int ClumpSize(Enemy enemy) => _clumpOf.TryGetValue(enemy, out int clump) ? _clumpOf.Count(c => c.Value == clump) : 0;

    public bool IsOn(LabCard card) => _on.Contains(card);

    /// <summary>Loc key of a card: name_card_balloon, desc_card_stuffing_burst…</summary>
    public static string Key(string prefix, LabCard card) => prefix + card switch
    {
        LabCard.Balloon => "balloon",
        LabCard.Velcro => "velcro",
        LabCard.StuffingBurst => "stuffing_burst",
        LabCard.Billiards => "billiards",
        LabCard.HonkReload => "honk_reload",
        _ => "rubber_duck",
    };
    public void Set(LabCard card, bool on) { if (on) _on.Add(card); else _on.Remove(card); }
    public void Toggle(LabCard card) => Set(card, !IsOn(card));

    public override void _Ready()
    {
        Stage.EnemyHit += OnHit;
        Stage.EnemyKilled += OnKilled;
    }

    public override void _ExitTree()
    {
        Stage.EnemyHit -= OnHit;
        Stage.EnemyKilled -= OnKilled;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Stage.Authoritative || Stage.State != StageState.Playing) return;
        float dt = (float)delta;
        _clock += dt;
        _stuffingThisFrame = 0;
        if (IsOn(LabCard.Velcro)) Clumps();
        if (IsOn(LabCard.Billiards)) Billiards();
        if (IsOn(LabCard.HonkReload)) HonkOnReload();
        else _wasReloading.Clear();
    }

    // ---------- hits and deaths ----------

    private void OnHit(Enemy enemy, HitInfo hit)
    {
        if (!Stage.Authoritative) return;
        if (IsOn(LabCard.Balloon) && enemy is not BossEnemy)
        {
            if (enemy.Floating) Pop(enemy, hit.Direction);
            else if (enemy.AddInflate(InflatePerHit + hit.Damage * InflatePerDamage, FloatSeconds)) StartFloating(enemy);
            else if (!enemy.IsDead) Stage.Sfx.PlayAt("balloon_inflate", enemy.GlobalPosition + Vector3.Up, -6, 1 + enemy.Inflate);
        }
        if (IsOn(LabCard.Velcro) && !enemy.IsDead) enemy.StickyTime = StickySeconds;
        if (IsOn(LabCard.RubberDuck) && ++_hitsSinceDuck >= DuckEveryHits && Stage.Lure == null) DropDuck(hit.Point);
    }

    private void OnKilled(Enemy enemy, Vector3 direction, float force)
    {
        if (!IsOn(LabCard.StuffingBurst) || _stuffingThisFrame >= StuffingPerFrame) return;
        _stuffingThisFrame++;
        StuffingBursts++;
        var at = enemy.GlobalPosition + Vector3.Up * .7f;
        Stage.Effects.Stuffing(at, Vector3.Up, enemy.Definition.StuffingColor, 40);
        Stage.Effects.Puff(at, StuffingRadius, enemy.Definition.StuffingColor);
        ShoveAround(enemy.GlobalPosition, StuffingRadius, StuffingPower, StuffingDamage, enemy);
    }

    // ---------- balloon ----------

    private void StartFloating(Enemy enemy)
    {
        Floats++;
        Stage.Sfx.PlayAt("balloon_inflate", enemy.GlobalPosition + Vector3.Up, 0, 1.6f);
        Stage.Effects.FloatingText(enemy.GlobalPosition + Vector3.Up * 2.4f, Loc.Text("fx_card_float"), new Color("ff9ccc"));
        // A velcro clump goes up together.
        if (_clumpOf.TryGetValue(enemy, out int clump))
            foreach (var other in _clumpOf.Where(c => c.Value == clump && c.Key != enemy && IsInstanceValid(c.Key)).Select(c => c.Key).ToList())
                if (other.AddInflate(1, FloatSeconds)) Floats++;
    }

    /// <summary>A floating monster bursts: it dies and throws its neighbours (and pops its floating clump-mates).</summary>
    private void Pop(Enemy enemy, Vector3 direction)
    {
        if (enemy.IsDead || !_popping.Add(enemy)) return;
        Pops++;
        var at = enemy.GlobalPosition + Vector3.Up * .9f;
        Stage.Sfx.PlayAt("balloon_pop", at);
        Stage.Effects.Puff(at, PopRadius, new Color("ff9ccc"));
        Stage.Effects.Stuffing(at, Vector3.Up, enemy.Definition.StuffingColor, 30);
        Stage.Effects.FloatingText(at + Vector3.Up * 1.2f, Loc.Text("fx_card_pop"), new Color("ff9ccc"));
        enemy.Deflate();
        var mates = _clumpOf.TryGetValue(enemy, out int clump)
            ? _clumpOf.Where(c => c.Value == clump && c.Key != enemy && IsInstanceValid(c.Key) && c.Key.Floating).Select(c => c.Key).ToList()
            : new List<Enemy>();
        enemy.Shove(direction, 60, enemy.Health + 1);
        // Clump-mates go first, so the push below does not kill them before they get to pop.
        foreach (var mate in mates) Pop(mate, mate.GlobalPosition - enemy.GlobalPosition);
        ShoveAround(enemy.GlobalPosition, PopRadius, PopPower, PopDamage, enemy);
        _popping.Remove(enemy);
    }

    // ---------- velcro ----------

    /// <summary>Sticky monsters touching each other form clumps (this frame), drag together and burst at five.</summary>
    private void Clumps()
    {
        _clumpOf.Clear();
        var sticky = Stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead && e.Sticky).ToList();
        if (sticky.Count < 2) return;
        var parent = Enumerable.Range(0, sticky.Count).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        for (int i = 0; i < sticky.Count; i++)
            for (int j = i + 1; j < sticky.Count; j++)
            {
                float reach = sticky[i].Radius + sticky[j].Radius + ClumpReach;
                if (Flat(sticky[i].GlobalPosition - sticky[j].GlobalPosition).LengthSquared() < reach * reach) parent[Find(i)] = Find(j);
            }
        foreach (var group in Enumerable.Range(0, sticky.Count).GroupBy(Find).Where(g => g.Count() >= 2))
        {
            var members = group.Select(i => sticky[i]).ToList();
            LargestClump = Mathf.Max(LargestClump, members.Count);
            var center = members.Aggregate(Vector3.Zero, (sum, e) => sum + e.GlobalPosition) / members.Count;
            if (members.Count >= ClumpBurstSize)
            {
                ClumpBurst(members, center);
                continue;
            }
            // Stuck together: a hit on one moves the whole clump.
            var shared = members.Aggregate(Vector3.Zero, (sum, e) => sum + e.KnockVelocity) / members.Count;
            foreach (var e in members)
            {
                _clumpOf[e] = group.Key;
                e.KnockVelocity = shared;
                var pull = Flat(center - e.GlobalPosition) * ClumpPull;
                e.CardDrag(pull.LimitLength(4f), ClumpSlow);
            }
        }
    }

    private void ClumpBurst(List<Enemy> members, Vector3 center)
    {
        ClumpBursts++;
        Stage.Effects.Explosion(center, ClumpBurstRadius, new Color("c6ff3d"));
        Stage.Sfx.PlayAt("balloon_pop", center + Vector3.Up, 3, .7f);
        Stage.Effects.FloatingText(center + Vector3.Up * 2.6f, Loc.Text("fx_card_velcro_burst"), new Color("c6ff3d"));
        foreach (var e in members) e.StickyTime = 0;
        ShoveAround(center, ClumpBurstRadius, ClumpBurstPower, ClumpBurstDamage, null);
    }

    // ---------- billiards ----------

    /// <summary>A monster flying from a hit knocks into others: they fly on with most of its push.</summary>
    private void Billiards()
    {
        var alive = Stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead).ToList();
        foreach (var flier in alive)
        {
            if (flier.IsDead || flier.KnockSpeed < FlyingSpeed) continue;
            foreach (var other in alive)
            {
                if (other == flier || other.IsDead || _billiardsReady.GetValueOrDefault(other) > _clock) continue;
                var gap = Flat(other.GlobalPosition - flier.GlobalPosition);
                float reach = flier.Radius + other.Radius + .25f;
                if (gap.LengthSquared() > reach * reach) continue;
                var dir = (gap.Normalized() + flier.KnockDirection).Normalized();
                float power = flier.KnockSpeed / Knockback.SpeedPerPower * BilliardsShare;
                _billiardsReady[other] = _clock + BilliardsCooldown;
                _billiardsReady[flier] = _clock + BilliardsCooldown;
                BilliardHits++;
                Stage.Sfx.PlayAt("bull_bonk", other.GlobalPosition + Vector3.Up, -4, 1.5f);
                if (BilliardHits % 3 == 1) Stage.Effects.FloatingText(other.GlobalPosition + Vector3.Up * 2.2f, Loc.Text("fx_card_billiards"), new Color("ffe066"));
                other.Shove(dir, power, 6 + power * .05f);
                flier.DampKnock(BilliardsKeep);
                if (flier.KnockSpeed < FlyingSpeed) break;
            }
        }
        foreach (var gone in _billiardsReady.Keys.Where(e => !IsInstanceValid(e)).ToList()) _billiardsReady.Remove(gone);
    }

    // ---------- honk ----------

    private void HonkOnReload()
    {
        foreach (var p in Stage.AlivePlayers)
        {
            bool reloading = p.Gun.Reloading;
            if (reloading && !_wasReloading.GetValueOrDefault(p)) Honk(p);
            _wasReloading[p] = reloading;
        }
    }

    private void Honk(Player who)
    {
        Honks++;
        var at = who.GlobalPosition;
        Stage.Sfx.PlayAt("honk", at + Vector3.Up, 6, .8f);
        Stage.Effects.Puff(at + Vector3.Up * .6f, HonkRadius, new Color("f3f4f1"));
        Stage.Effects.FloatingText(at + Vector3.Up * 2.8f, Loc.Text("fx_card_honk"), new Color("f3f4f1"));
        ShoveAround(at, HonkRadius, HonkPower, HonkDamage, null);
    }

    // ---------- rubber duck ----------

    private void DropDuck(Vector3 at)
    {
        _hitsSinceDuck = 0;
        Ducks++;
        _duck ??= new ThrowableDefinition
        {
            Id = "lab_duck",
            NameKey = "name_card_rubber_duck",
            Kind = ArmoryCatalog.Throwables["lure"].Kind,
            MaxCarry = 1,
            MaxRange = 6,
            Color = new Color("ffd23f"),
            Effects =
            {
                new AttractEffect("duck_attract", 12, 3.5f, .3f),
                new ExplodeEffect("duck_pop", 3.2f, 40, 130, 0),
            },
        };
        var ground = new Vector3(at.X, 0, at.Z);
        var side = new Vector3(GD.Randf() - .5f, 0, GD.Randf() - .5f).Normalized() * 1.2f;
        ThrownItem.Throw(Stage, _duck, ground + Vector3.Up * 1.2f, ground + side);
        Stage.Effects.FloatingText(at + Vector3.Up * 1.6f, Loc.Text("fx_card_duck"), new Color("ffd23f"));
    }

    // ---------- shared ----------

    private void ShoveAround(Vector3 center, float radius, float power, float damage, Enemy? except)
    {
        foreach (var e in Stage.Enemies.ToList())
        {
            if (e == except || !IsInstanceValid(e) || e.IsDead) continue;
            var away = Flat(e.GlobalPosition - center);
            float d = away.Length();
            if (d > radius) continue;
            float falloff = 1 - d / radius * .6f;
            e.Shove(d > .01f ? away : Vector3.Forward, power * falloff, damage * falloff);
        }
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
