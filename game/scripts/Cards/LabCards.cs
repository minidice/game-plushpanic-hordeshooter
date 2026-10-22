using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// The six cards of the first lab set (doc/onslaught-card-ideas.md 5장), then M-29's first new normal cards (plan
/// 16.6): tap dance, bouncing rounds (sniper), honk charge (slot 3) and the destiny gun (once a match).
/// </summary>
public enum LabCard { Balloon, Velcro, StuffingBurst, Billiards, HonkReload, RubberDuck, TapDance, BouncingRounds, HonkCharge, DestinyGun }

/// <summary>Three stand-in special cards for the boss-reward cheat (plan 6.3): rules for every gun, one with a price.</summary>
public enum LabSpecial { HeliumParty, LastShot, GlassCannon, DoubleSpecial }

/// <summary>
/// The upgrade cards a base card opens once taken (plan 13.2 card tree): balloon pop radius and damage, velcro clump
/// size and burst damage.
/// </summary>
public enum CardUpgrade { BalloonRadius, BalloonDamage, VelcroSize, VelcroDamage, TapDanceStacks, BounceCount }

/// <summary>
/// Onslaught's card effects (decisions.md M-27, `[임시]`), first built for the card lab: cards switched on while
/// playing, chaining through the shared states of <see cref="Enemy"/> (inflate · float · sticky · flying): a velcro
/// clump pops together, a pop throws monsters into others (billiards), a stuffing burst does the same. A card belongs
/// to the survivor who took it and acts on that survivor's hits (<see cref="HitInfo.Shooter"/>); a lab switch has no
/// owner and acts on every hit, and so does a hit nobody fired. A chain counts for whoever started it (M-29 damage
/// source): a pop, a clump burst, a cue ball, a stuffing burst carries its survivor on to every shove it makes, so
/// kills and later cards credit them. Specials are the team's (M-29). Taken cards open upgrade levels (plan 13.2).
/// Only the deciding PC runs it, except that every PC keeps glass cannon's health price for the HUD. Numbers are
/// guesses in this file, not data yet.
/// </summary>
public partial class LabCards : Node
{
    public static readonly LabCard[] All = { LabCard.Balloon, LabCard.Velcro, LabCard.StuffingBurst, LabCard.Billiards, LabCard.HonkReload, LabCard.RubberDuck };
    public static readonly LabSpecial[] Specials = { LabSpecial.HeliumParty, LabSpecial.LastShot, LabSpecial.GlassCannon, LabSpecial.DoubleSpecial };
    /// <summary>The cards a level-up can offer; the honk and the rubber duck move to slot 3 as weapons (plan 13.1), lab only for now.</summary>
    public static readonly LabCard[] LevelUpCards =
        { LabCard.Balloon, LabCard.Velcro, LabCard.StuffingBurst, LabCard.Billiards, LabCard.TapDance, LabCard.BouncingRounds, LabCard.HonkCharge, LabCard.DestinyGun };
    /// <summary>Weapon kinds billiards works with (weapon_types.csv category): sniper hits are cue balls, shotgun pellets bowl (plan 13.2).</summary>
    public static bool IsSniperType(string? type) => WeaponDatabase.Current.CategoryOfType(type) == WeaponCategory.Sniper;
    public static bool IsShotgunType(string? type) => WeaponDatabase.Current.CategoryOfType(type) == WeaponCategory.Shotgun;

    public static IEnumerable<CardUpgrade> UpgradesOf(LabCard card) => card switch
    {
        LabCard.Balloon => new[] { CardUpgrade.BalloonRadius, CardUpgrade.BalloonDamage },
        LabCard.Velcro => new[] { CardUpgrade.VelcroSize, CardUpgrade.VelcroDamage },
        LabCard.TapDance => new[] { CardUpgrade.TapDanceStacks },
        LabCard.BouncingRounds => new[] { CardUpgrade.BounceCount },
        _ => System.Array.Empty<CardUpgrade>(),
    };
    public static LabCard BaseOf(CardUpgrade upgrade) => upgrade switch
    {
        CardUpgrade.BalloonRadius or CardUpgrade.BalloonDamage => LabCard.Balloon,
        CardUpgrade.TapDanceStacks => LabCard.TapDance,
        CardUpgrade.BounceCount => LabCard.BouncingRounds,
        _ => LabCard.Velcro,
    };
    /// <summary>Velcro size goes 5 → 4 → 3 monsters and bounces 1 → 2 → 3; the others have three levels.</summary>
    public static int MaxLevel(CardUpgrade upgrade) => upgrade is CardUpgrade.VelcroSize or CardUpgrade.BounceCount ? 2 : 3;

    // Tap dance: each hit (once a frame) adds a step of walking speed and fire rate, up to the stack cap; a gap ends it.
    public const int TapStacks = 5, TapStacksPerLevel = 3;
    public const float TapSpeedPerStack = .05f, TapFirePerStack = .04f, TapHold = 2.5f;
    // Bouncing rounds: from where the sniper round stopped to the nearest other monster, 70% each time.
    public const float BounceShare = .7f, BounceReach = 10f;
    // Honk charge: 3% of what is left of the slot-3 cooldown per hit (one per shot), at most a second each second.
    public const float HonkChargeShare = .03f, HonkChargePerSecond = 1f;

    // Helium party: a hit sometimes floats a common at once. Last shot: the shot that empties a magazine explodes.
    private const float HeliumChance = .12f, LastShotRadius = 3.2f, LastShotPower = 150f, LastShotDamage = 60f, GlassCannonDamage = 2f, GlassCannonHealth = .5f;

    // Balloon rounds: each hit (one per frame, so a shotgun blast counts once) inflates; the fourth pops it. Named
    // monsters and bosses do not inflate but take the pop. Helium party still floats commons (FloatSeconds).
    public const int BalloonHits = 4;
    private const float FloatSeconds = 4f;
    private const float PopRadius = 3f, PopRadiusPerLevel = .75f, PopPower = 110f, PopDamage = 20f, PopDamagePerLevel = .6f;
    // Velcro: sticky monsters close together drag into one clump; five in a clump burst.
    private const float StickySeconds = 6f, ClumpReach = .6f, ClumpPull = 2.5f, ClumpSlow = .35f;
    public const int ClumpBurstSize = 5;
    private const float ClumpBurstRadius = 3.2f, ClumpBurstPower = 140f, ClumpBurstDamage = 35f, ClumpDamagePerLevel = .5f;
    // Stuffing burst on every death, capped per frame so a chain cannot run away.
    private const float StuffingRadius = 2.6f, StuffingPower = 95f, StuffingDamage = 6f;
    private const int StuffingPerFrame = 8;
    // Billiards: a sniper hit sends its monster off as a cue ball, shotgun pellets each add a bowl; a cue ball flying
    // faster than FlyingSpeed passes most of its push on to whoever it hits, and they become cue balls in turn.
    private const float FlyingSpeed = 4.5f, BilliardsShare = .8f, BilliardsKeep = .55f, BilliardsCooldown = .4f, CueSeconds = 1.5f;
    private const float SniperCuePower = 150f, ShotgunCuePower = 40f;
    // Honk on reload.
    private const float HonkRadius = 3.5f, HonkPower = 130f, HonkDamage = 5f;
    // Rubber duck: every so many hits a squeaky duck drops, gathers commons, then pops.
    private const int DuckEveryHits = 10;
    public const string RubberDuckId = "rubber_duck";

    public Stage Stage { get; set; } = null!;
    // Owner null = a lab switch (every hit); otherwise the survivor who took it.
    private readonly HashSet<(Player? Owner, LabCard Card)> _owned = new();
    private readonly Dictionary<(Player? Owner, CardUpgrade Upgrade), int> _levels = new();
    private readonly HashSet<LabSpecial> _specials = new();
    private readonly Dictionary<Player, ulong> _lastShotFrame = new();
    // Who started each chain: the velcro that stuck a monster, the hit that made it a cue ball.
    private readonly Dictionary<Enemy, Player?> _stuckBy = new(), _cueBy = new();
    // Survivors glass cannon is on right now (joiners get it too, all of it comes off with the card).
    private readonly HashSet<Player> _glassed = new();
    // Tap dance until when, bounce chains waiting for the frame's last pierced hit, honk charge budget.
    private readonly Dictionary<Player, float> _tapUntil = new();
    private readonly Dictionary<Player, ulong> _tapFrame = new(), _honkFrame = new();
    private readonly Dictionary<Player, (Vector3 At, Enemy From, HitInfo Hit)> _bounceFrom = new();
    private readonly Dictionary<Player, float> _honkBudget = new();
    private readonly RandomNumberGenerator _rng = new();
    private bool _bouncing;

    /// <summary>Special weapon ids the destiny gun draws from (the mode's slot-3 pool).</summary>
    public IReadOnlyList<string> DestinyPool { get; set; } = System.Array.Empty<string>();
    /// <summary>Honk charge is only offered when slot 3 recharges at least this long (survivor.cooldown_card_min_s).</summary>
    public float HonkChargeMinCooldown { get; set; } = 20;
    /// <summary>Checks: what the destiny gun gave each survivor (slot, weapon id).</summary>
    public Dictionary<Player, (int Slot, string Weapon)> DestinyGiven { get; } = new();
    private readonly Dictionary<Enemy, float> _billiardsReady = new();
    private readonly Dictionary<Enemy, float> _cueUntil = new();
    private readonly Dictionary<Enemy, ulong> _inflatedFrame = new();
    private readonly Dictionary<Enemy, int> _clumpOf = new();
    private readonly Dictionary<Player, bool> _wasReloading = new();
    private readonly HashSet<Enemy> _popping = new();
    private int _stuffingThisFrame, _hitsSinceDuck;
    private float _clock;

    // Counters for checks and the lab overlay.
    public int Floats { get; private set; }
    public int Pops { get; private set; }
    public int LargestClump { get; private set; }
    public int ClumpBursts { get; private set; }
    public int StuffingBursts { get; private set; }
    public int BilliardHits { get; private set; }
    public int Honks { get; private set; }
    public int Ducks { get; private set; }
    public int LastShots { get; private set; }
    public int Bounces { get; private set; }

    /// <summary>How many monsters are in <paramref name="enemy"/>'s velcro clump this frame (0 = none).</summary>
    public int ClumpSize(Enemy enemy) => _clumpOf.TryGetValue(enemy, out int clump) ? _clumpOf.Count(c => c.Value == clump) : 0;

    /// <summary>Anyone has the card (or the lab switched it on).</summary>
    public bool IsOn(LabCard card) => _owned.Any(o => o.Card == card);
    public bool IsOn(LabSpecial card) => _specials.Contains(card);
    /// <summary><paramref name="who"/> took the card themself (a lab switch does not count).</summary>
    public bool Owns(Player who, LabCard card) => _owned.Contains((who, card));
    /// <summary>Whether a hit by <paramref name="shooter"/> uses the card: theirs or a lab switch; a hit nobody fired uses anyone's.</summary>
    public bool Has(LabCard card, Player? shooter) => shooter == null ? IsOn(card) : _owned.Contains((null, card)) || _owned.Contains((shooter, card));

    /// <summary><paramref name="who"/>'s own level of an upgrade (0 = not taken).</summary>
    public int LevelOf(Player who, CardUpgrade upgrade) => _levels.GetValueOrDefault((who, upgrade));
    /// <summary>The level a hit by <paramref name="shooter"/> works with; a hit nobody fired (or a clump) uses the best one.</summary>
    public int Level(CardUpgrade upgrade, Player? shooter = null) => shooter == null
        ? _levels.Where(l => l.Key.Upgrade == upgrade).Select(l => l.Value).DefaultIfEmpty(0).Max()
        : Mathf.Max(_levels.GetValueOrDefault((shooter, upgrade)), _levels.GetValueOrDefault((null, upgrade)));
    /// <summary>One more level of an upgrade for <paramref name="who"/> (null = the lab), up to its last.</summary>
    public void Upgrade(CardUpgrade upgrade, Player? who = null) =>
        _levels[(who, upgrade)] = Mathf.Min(MaxLevel(upgrade), _levels.GetValueOrDefault((who, upgrade)) + 1);

    /// <summary>Every card, upgrade and special off (the lab's cheat reset).</summary>
    public void ClearAll()
    {
        _owned.Clear();
        _levels.Clear();
        foreach (var s in Specials) Set(s, false);
    }

    /// <summary>The balloon pop around a monster popped by <paramref name="shooter"/>'s hit.</summary>
    public float PopRadiusFor(Player? shooter) => PopRadius + PopRadiusPerLevel * Level(CardUpgrade.BalloonRadius, shooter);
    public float PopDamageFor(Player? shooter) => PopDamage * (1 + PopDamagePerLevel * Level(CardUpgrade.BalloonDamage, shooter));
    /// <summary>How many monsters make a velcro clump burst (5, 4 at level 1, 3 at level 2); by default the best anyone has.</summary>
    public int ClumpSizeToBurst => ClumpSizeFor(null);
    /// <summary>The burst size of a clump <paramref name="owner"/> stuck together (null = the best level anyone has).</summary>
    public int ClumpSizeFor(Player? owner) => ClumpBurstSize - Level(CardUpgrade.VelcroSize, owner);

    /// <summary>
    /// Switches a special on or off for the whole team (M-29; <paramref name="who"/>, the picker, no longer matters).
    /// Glass cannon doubles every survivor's damage and halves their max health at once.
    /// </summary>
    public void Set(LabSpecial card, bool on, Player? who = null)
    {
        if (on) _specials.Add(card); else _specials.Remove(card);
        KeepGlassCannon();
    }

    /// <summary>The team's specials as bits (bit n = <see cref="Specials"/>[n]), for the client card sync.</summary>
    public int SpecialMask => Specials.Select((s, i) => _specials.Contains(s) ? 1 << i : 0).Sum();

    /// <summary>Client: the host's specials, so glass cannon's health price shows here too.</summary>
    public void SetSpecialsFromHost(int mask)
    {
        for (int i = 0; i < Specials.Length; i++)
            if ((mask & 1 << i) != 0) _specials.Add(Specials[i]); else _specials.Remove(Specials[i]);
        KeepGlassCannon();
    }

    /// <summary>Glass cannon on every survivor while it is on (a survivor who joins later gets it too), off for all when it goes.</summary>
    private void KeepGlassCannon()
    {
        if (IsOn(LabSpecial.GlassCannon))
        {
            foreach (var p in Stage.Players)
                if (IsInstanceValid(p) && _glassed.Add(p)) Glass(p, true);
            return;
        }
        foreach (var p in _glassed.Where(IsInstanceValid)) Glass(p, false);
        _glassed.Clear();
    }

    private static void Glass(Player p, bool on)
    {
        p.DamageMultiplier = on ? p.DamageMultiplier * GlassCannonDamage : p.DamageMultiplier / GlassCannonDamage;
        p.MaxHealthScale = on ? p.MaxHealthScale * GlassCannonHealth : p.MaxHealthScale / GlassCannonHealth;
    }

    public static string Key(string prefix, LabSpecial card) => prefix + card switch
    {
        LabSpecial.HeliumParty => "helium_party",
        LabSpecial.LastShot => "last_shot",
        LabSpecial.DoubleSpecial => "double_special",
        _ => "glass_cannon",
    };

    /// <summary>Loc key of a card: name_card_balloon, desc_card_stuffing_burst…</summary>
    public static string Key(string prefix, LabCard card) => prefix + card switch
    {
        LabCard.Balloon => "balloon",
        LabCard.Velcro => "velcro",
        LabCard.StuffingBurst => "stuffing_burst",
        LabCard.Billiards => "billiards",
        LabCard.HonkReload => "honk_reload",
        LabCard.TapDance => "tap_dance",
        LabCard.BouncingRounds => "bouncing_rounds",
        LabCard.HonkCharge => "honk_charge",
        LabCard.DestinyGun => "destiny_gun",
        _ => "rubber_duck",
    };

    /// <summary>Loc key of an upgrade card: name_card_u_balloon_radius…</summary>
    public static string Key(string prefix, CardUpgrade upgrade) => prefix + "u_" + upgrade switch
    {
        CardUpgrade.BalloonRadius => "balloon_radius",
        CardUpgrade.BalloonDamage => "balloon_damage",
        CardUpgrade.VelcroSize => "velcro_size",
        CardUpgrade.TapDanceStacks => "tap_dance_stacks",
        CardUpgrade.BounceCount => "bounce_count",
        _ => "velcro_damage",
    };

    /// <summary>Switches a card on or off: for <paramref name="who"/>, or as a lab switch (null; off then clears every owner).</summary>
    public void Set(LabCard card, bool on, Player? who = null)
    {
        if (on) _owned.Add((who, card));
        else _owned.RemoveWhere(o => o.Card == card && (who == null || o.Owner == who));
        if (on && card == LabCard.DestinyGun && who != null) GiveDestinyGun(who);
    }
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
        if (_glassed.Count > 0 || IsOn(LabSpecial.GlassCannon)) KeepGlassCannon();
        if (!Stage.Authoritative || Stage.State != StageState.Playing) return;
        float dt = (float)delta;
        _clock += dt;
        TapDanceEnds();
        if (_bounceFrom.Count > 0) BounceAll();
        foreach (var p in _honkBudget.Keys.ToList()) _honkBudget[p] = Mathf.Min(HonkChargePerSecond, _honkBudget[p] + dt * HonkChargePerSecond);
        _stuffingThisFrame = 0;
        if (IsOn(LabCard.Velcro)) Clumps();
        if (_cueUntil.Count > 0) Billiards();
        if (IsOn(LabCard.HonkReload)) HonkOnReload();
        else _wasReloading.Clear();
    }

    // ---------- hits and deaths ----------

    private void OnHit(Enemy enemy, HitInfo hit)
    {
        if (!Stage.Authoritative) return;
        var shooter = hit.Shooter;
        bool helium = IsOn(LabSpecial.HeliumParty) && !enemy.IsSpecial;
        bool balloon = Has(LabCard.Balloon, shooter);
        if ((balloon || helium) && enemy.Floating) Pop(enemy, hit.Direction, shooter);
        else if (balloon && !enemy.IsSpecial && _inflatedFrame.GetValueOrDefault(enemy) != Engine.GetPhysicsFrames())
        {
            // One step per frame: the pellets of one shotgun blast count once.
            _inflatedFrame[enemy] = Engine.GetPhysicsFrames();
            enemy.AddInflate(1f / BalloonHits, 0);
            if (enemy.Inflate >= 1) Pop(enemy, hit.Direction, shooter);
            else if (!enemy.IsDead) Stage.Sfx.PlayAt("balloon_inflate", enemy.GlobalPosition + Vector3.Up, -6, 1 + enemy.Inflate);
        }
        else if (helium && !enemy.IsDead && GD.Randf() < HeliumChance && enemy.AddInflate(1, FloatSeconds)) StartFloating(enemy);
        if (Has(LabCard.Billiards, shooter) && !enemy.IsDead && (IsSniperType(hit.WeaponType) || IsShotgunType(hit.WeaponType)))
            Cue(enemy, hit);
        if (shooter != null && IsInstanceValid(shooter) && !_bouncing && IsGunHit(shooter, hit))
        {
            ulong frame = Engine.GetPhysicsFrames();
            if (Has(LabCard.TapDance, shooter) && _tapFrame.GetValueOrDefault(shooter) != frame)
            {
                _tapFrame[shooter] = frame;
                shooter.TapStacks = Mathf.Min(TapMax(shooter), shooter.TapStacks + 1);
                _tapUntil[shooter] = _clock + TapHold;
            }
            // The round's last pierced monster wins: the bounce starts where the shot stopped (plan 16.6).
            if (Has(LabCard.BouncingRounds, shooter) && IsSniperType(hit.WeaponType)) _bounceFrom[shooter] = (enemy.GlobalPosition, enemy, hit);
            if (Has(LabCard.HonkCharge, shooter) && _honkFrame.GetValueOrDefault(shooter) != frame) HonkCharge(shooter, frame);
        }
        // The shooter's own magazine, and only a hit of the gun in their hands (not a rocket landing later or a pet's bite).
        if (IsOn(LabSpecial.LastShot) && !_bouncing && shooter != null && IsInstanceValid(shooter) && shooter.Gun is { Mag: 0, IsMelee: false } gun
            && gun.Def.TypeId == hit.WeaponType && _lastShotFrame.GetValueOrDefault(shooter) != Engine.GetPhysicsFrames())
        {
            _lastShotFrame[shooter] = Engine.GetPhysicsFrames();
            LastShot(hit.Point, shooter);
        }
        if (Has(LabCard.Velcro, shooter) && !enemy.IsDead)
        {
            enemy.StickyTime = StickySeconds;
            _stuckBy[enemy] = shooter;
        }
        if (IsOn(LabCard.RubberDuck) && ++_hitsSinceDuck >= DuckEveryHits && Stage.Lure == null) DropDuck(hit.Point, shooter);
    }

    private void OnKilled(Enemy enemy, Vector3 direction, float force)
    {
        _stuckBy.Remove(enemy);
        _cueBy.Remove(enemy);
        // The killer's card (anyone's when nobody dealt the kill), and the burst counts for them.
        var killer = enemy.LastHitBy != null && IsInstanceValid(enemy.LastHitBy) ? enemy.LastHitBy : null;
        if (!Has(LabCard.StuffingBurst, killer) || _stuffingThisFrame >= StuffingPerFrame) return;
        _stuffingThisFrame++;
        StuffingBursts++;
        var at = enemy.GlobalPosition + Vector3.Up * .7f;
        Stage.Effects.Stuffing(at, Vector3.Up, enemy.Definition.StuffingColor, 40);
        Stage.Effects.Puff(at, StuffingRadius, enemy.Definition.StuffingColor);
        ShoveAround(enemy.GlobalPosition, StuffingRadius, StuffingPower, StuffingDamage, enemy, killer);
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

    /// <summary>A full (or floating) monster bursts: it dies and throws its neighbours, and its clump-mates pop with it.</summary>
    private void Pop(Enemy enemy, Vector3 direction, Player? shooter = null)
    {
        if (enemy.IsDead || !_popping.Add(enemy)) return;
        Pops++;
        float radius = PopRadiusFor(shooter);
        var at = enemy.GlobalPosition + Vector3.Up * .9f;
        Stage.Sfx.PlayAt("balloon_pop", at);
        Stage.Effects.Puff(at, radius, new Color("ff9ccc"));
        Stage.Effects.Stuffing(at, Vector3.Up, enemy.Definition.StuffingColor, 30);
        Stage.Effects.FloatingText(at + Vector3.Up * 1.2f, Loc.Text("fx_card_pop"), new Color("ff9ccc"));
        enemy.Deflate();
        var mates = _clumpOf.TryGetValue(enemy, out int clump)
            ? _clumpOf.Where(c => c.Value == clump && c.Key != enemy && IsInstanceValid(c.Key) && !c.Key.IsSpecial).Select(c => c.Key).ToList()
            : new List<Enemy>();
        enemy.Shove(direction, 60, enemy.Health + 1, shooter);
        // Clump-mates go first, so the push below does not kill them before they get to pop.
        foreach (var mate in mates) Pop(mate, mate.GlobalPosition - enemy.GlobalPosition, shooter);
        ShoveAround(enemy.GlobalPosition, radius, PopPower, PopDamageFor(shooter), enemy, shooter);
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
            var owner = ClumpOwner(members);
            if (members.Count >= ClumpSizeFor(owner))
            {
                ClumpBurst(members, center, owner);
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

    /// <summary>Whose clump it is: the survivor who stuck most of it (null when none of it was anyone's).</summary>
    private Player? ClumpOwner(List<Enemy> members) => members
        .Select(e => _stuckBy.GetValueOrDefault(e)).Where(p => p != null && IsInstanceValid(p))
        .GroupBy(p => p).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

    private void ClumpBurst(List<Enemy> members, Vector3 center, Player? owner)
    {
        ClumpBursts++;
        Stage.Effects.Explosion(center, ClumpBurstRadius, new Color("c6ff3d"));
        Stage.Sfx.PlayAt("balloon_pop", center + Vector3.Up, 3, .7f);
        Stage.Effects.FloatingText(center + Vector3.Up * 2.6f, Loc.Text("fx_card_velcro_burst"), new Color("c6ff3d"));
        foreach (var e in members) e.StickyTime = 0;
        ShoveAround(center, ClumpBurstRadius, ClumpBurstPower, ClumpBurstDamage * (1 + ClumpDamagePerLevel * Level(CardUpgrade.VelcroDamage, owner)), null, owner);
    }

    // ---------- billiards ----------

    /// <summary>
    /// A sniper hit sends its monster off hard along the shot (관통 당구); each shotgun pellet adds a smaller push
    /// (산탄 볼링). Either way it becomes a cue ball for a moment.
    /// </summary>
    private void Cue(Enemy enemy, HitInfo hit)
    {
        bool sniper = IsSniperType(hit.WeaponType);
        enemy.Shove(hit.Direction, sniper ? SniperCuePower : ShotgunCuePower, 0, hit.Shooter);
        _cueUntil[enemy] = _clock + CueSeconds;
        _cueBy[enemy] = hit.Shooter;
    }

    /// <summary>A cue ball flying from a hit knocks into others: they fly on with most of its push and become cue balls.</summary>
    private void Billiards()
    {
        var alive = Stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead).ToList();
        foreach (var flier in alive)
        {
            if (flier.IsDead || flier.KnockSpeed < FlyingSpeed || _cueUntil.GetValueOrDefault(flier) < _clock) continue;
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
                // The ball it hits plays on for whoever made the first cue.
                var by = _cueBy.GetValueOrDefault(flier);
                if (by != null && !IsInstanceValid(by)) by = null;
                other.Shove(dir, power, 6 + power * .05f, by);
                _cueUntil[other] = _clock + CueSeconds;
                _cueBy[other] = by;
                flier.DampKnock(BilliardsKeep);
                if (flier.KnockSpeed < FlyingSpeed) break;
            }
        }
        foreach (var gone in _billiardsReady.Keys.Where(e => !IsInstanceValid(e)).ToList()) _billiardsReady.Remove(gone);
        foreach (var done in _cueUntil.Where(c => !IsInstanceValid(c.Key) || c.Value < _clock).Select(c => c.Key).ToList()) _cueUntil.Remove(done);
        foreach (var gone in _inflatedFrame.Keys.Where(e => !IsInstanceValid(e)).ToList()) _inflatedFrame.Remove(gone);
        foreach (var gone in _cueBy.Keys.Where(e => !IsInstanceValid(e)).ToList()) _cueBy.Remove(gone);
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
        ShoveAround(at, HonkRadius, HonkPower, HonkDamage, null, who);
    }

    // ---------- rubber duck ----------

    private void DropDuck(Vector3 at, Player? by)
    {
        _hitsSinceDuck = 0;
        Ducks++;
        // The same duck the slot-3 rubber duck throws (throwables.csv rubber_duck).
        var ground = new Vector3(at.X, 0, at.Z);
        var side = new Vector3(GD.Randf() - .5f, 0, GD.Randf() - .5f).Normalized() * 1.2f;
        ThrownItem.Throw(Stage, ArmoryCatalog.Throwables[RubberDuckId], ground + Vector3.Up * 1.2f, ground + side, by != null && IsInstanceValid(by) ? by : null);
        Stage.Effects.FloatingText(at + Vector3.Up * 1.6f, Loc.Text("fx_card_duck"), new Color("ffd23f"));
    }

    // ---------- tap dance · bouncing rounds · honk charge · destiny gun (M-29 plan 16.6) ----------

    /// <summary>A hit of the gun in the shooter's hands (not a rocket landing later, a pet's bite or a card's blast).</summary>
    private static bool IsGunHit(Player shooter, HitInfo hit) => shooter.Gun is { IsMelee: false } gun && gun.Def.TypeId == hit.WeaponType;

    /// <summary>How many tap dance steps <paramref name="who"/> can stack (5, +3 a level).</summary>
    public int TapMax(Player who) => TapStacks + TapStacksPerLevel * Level(CardUpgrade.TapDanceStacks, who);

    private void TapDanceEnds()
    {
        foreach (var (who, until) in _tapUntil.ToList())
        {
            if (IsInstanceValid(who) && until > _clock) continue;
            if (IsInstanceValid(who)) who.TapStacks = 0;
            _tapUntil.Remove(who);
        }
    }

    /// <summary>Each waiting chain bounces from where its round stopped: 1 + level times, 70% each, to the nearest other monster.</summary>
    private void BounceAll()
    {
        foreach (var (shooter, (at, from, hit)) in _bounceFrom.ToList())
        {
            _bounceFrom.Remove(shooter);
            if (!IsInstanceValid(shooter)) continue;
            var visited = new HashSet<Enemy> { from };
            var pos = at;
            float damage = hit.Damage;
            int times = 1 + Level(CardUpgrade.BounceCount, shooter);
            for (int i = 0; i < times; i++)
            {
                var next = Stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead && !visited.Contains(e) && Flat(e.GlobalPosition - pos).Length() <= BounceReach)
                    .OrderBy(e => Flat(e.GlobalPosition - pos).LengthSquared()).FirstOrDefault();
                if (next == null) break;
                visited.Add(next);
                damage *= BounceShare;
                var to = next.GlobalPosition + Vector3.Up;
                var dir = Flat(to - pos).Normalized();
                WeaponShotEffect.Play(Stage.Effects, shooter.Gun.Def, pos + Vector3.Up, pos + Vector3.Up, to, false, muzzleFlash: false);
                Bounces++;
                _bouncing = true;
                next.TakeHit(new HitInfo(damage, dir, to, false, hit.Power * .5f, shooter, hit.WeaponType));
                _bouncing = false;
                pos = next.GlobalPosition;
            }
        }
    }

    /// <summary>A hit takes 3% off what is left of the shooter's slot-3 cooldown (once a shot, at most a second a second).</summary>
    private void HonkCharge(Player shooter, ulong frame)
    {
        _honkFrame[shooter] = frame;
        if (shooter.Loadout.Special is not { CooldownLeft: > 0 } special) return;
        float budget = _honkBudget.TryGetValue(shooter, out float b) ? b : HonkChargePerSecond;
        float cut = Mathf.Min(special.CooldownLeft * HonkChargeShare, budget);
        _honkBudget[shooter] = budget - cut;
        special.ShortenCooldown(cut);
    }

    /// <summary>The destiny gun: slot 1 or 2 (whichever has a gun, at random) becomes a random special weapon other than slot 3's.</summary>
    private void GiveDestinyGun(Player who)
    {
        var pool = DestinyPool.Where(id => id != who.Loadout.Special?.Def.Id && ArmoryCatalog.Weapons.ContainsKey(id)).ToList();
        if (pool.Count == 0) return;
        var slots = who.Loadout.Primary != null ? new[] { 1, 2 } : new[] { 1 };
        _rng.Randomize();
        int slot = slots[_rng.RandiRange(0, slots.Length - 1)];
        var def = ArmoryCatalog.Weapon(pool[_rng.RandiRange(0, pool.Count - 1)]);
        who.Weapons.PlaceInSlot(slot, def);
        DestinyGiven[who] = (slot, def.Id);
        Stage.Effects.FloatingText(who.GlobalPosition + Vector3.Up * 2.6f, Loc.Format("fmt_card_destiny_fx", def.DisplayName), new Color("c08bff"));
    }

    // ---------- specials ----------

    /// <summary>The magazine's last round goes off where it hits, for whoever fired it.</summary>
    private void LastShot(Vector3 at, Player shooter)
    {
        LastShots++;
        Stage.Effects.Explosion(at, LastShotRadius, new Color("ffb347"));
        Stage.Sfx.PlayAt("explosion", at + Vector3.Up);
        Stage.Effects.FloatingText(at + Vector3.Up * 2f, Loc.Text("fx_card_last_shot"), new Color("ffb347"));
        ShoveAround(at, LastShotRadius, LastShotPower, LastShotDamage, null, shooter);
    }

    // ---------- shared ----------

    private void ShoveAround(Vector3 center, float radius, float power, float damage, Enemy? except, Player? by)
    {
        foreach (var e in Stage.Enemies.ToList())
        {
            if (e == except || !IsInstanceValid(e) || e.IsDead) continue;
            var away = Flat(e.GlobalPosition - center);
            float d = away.Length();
            if (d > radius) continue;
            float falloff = 1 - d / radius * .6f;
            e.Shove(d > .01f ? away : Vector3.Forward, power * falloff, damage * falloff, by);
        }
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
