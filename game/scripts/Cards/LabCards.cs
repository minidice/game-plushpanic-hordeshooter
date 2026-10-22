using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// The six cards of the first lab set (doc/onslaught-card-ideas.md 5장), then M-29's first new normal cards (plan
/// 16.6): tap dance, bouncing rounds (sniper), honk charge (slot 3) and the destiny gun (once a match).
/// </summary>
public enum LabCard { Balloon, Velcro, StuffingBurst, Billiards, HonkReload, RubberDuck, TapDance, BouncingRounds, HonkCharge, DestinyGun, ButtonMissile }

/// <summary>Three stand-in special cards for the boss-reward cheat (plan 6.3): rules for every gun, one with a price.</summary>
public enum LabSpecial { HeliumParty, LastShot, GlassCannon, DoubleSpecial, ToyTrain, JackBox, Gather, MadScientist, HonkBeam, StaticFluff }

/// <summary>
/// The upgrade cards a base card opens once taken (plan 13.2 card tree): balloon pop radius and damage, velcro clump
/// size and burst damage.
/// </summary>
public enum CardUpgrade { BalloonRadius, BalloonDamage, VelcroSize, VelcroDamage, TapDanceStacks, BounceCount, ButtonEvery, ButtonCount }

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
    public static readonly LabSpecial[] Specials =
        { LabSpecial.HeliumParty, LabSpecial.LastShot, LabSpecial.GlassCannon, LabSpecial.DoubleSpecial, LabSpecial.ToyTrain, LabSpecial.JackBox,
          LabSpecial.Gather, LabSpecial.MadScientist, LabSpecial.HonkBeam, LabSpecial.StaticFluff };
    /// <summary>The cards a level-up can offer; the honk and the rubber duck move to slot 3 as weapons (plan 13.1), lab only for now.</summary>
    public static readonly LabCard[] LevelUpCards =
        { LabCard.Balloon, LabCard.Velcro, LabCard.StuffingBurst, LabCard.Billiards, LabCard.TapDance, LabCard.BouncingRounds, LabCard.HonkCharge, LabCard.DestinyGun,
          LabCard.ButtonMissile };
    /// <summary>Weapon kinds billiards works with (weapon_types.csv category): sniper hits are cue balls, shotgun pellets bowl (plan 13.2).</summary>
    public static bool IsSniperType(string? type) => WeaponDatabase.Current.CategoryOfType(type) == WeaponCategory.Sniper;
    public static bool IsShotgunType(string? type) => WeaponDatabase.Current.CategoryOfType(type) == WeaponCategory.Shotgun;

    public static IEnumerable<CardUpgrade> UpgradesOf(LabCard card) => card switch
    {
        LabCard.Balloon => new[] { CardUpgrade.BalloonRadius, CardUpgrade.BalloonDamage },
        LabCard.Velcro => new[] { CardUpgrade.VelcroSize, CardUpgrade.VelcroDamage },
        LabCard.TapDance => new[] { CardUpgrade.TapDanceStacks },
        LabCard.BouncingRounds => new[] { CardUpgrade.BounceCount },
        LabCard.ButtonMissile => new[] { CardUpgrade.ButtonEvery, CardUpgrade.ButtonCount },
        _ => System.Array.Empty<CardUpgrade>(),
    };
    public static LabCard BaseOf(CardUpgrade upgrade) => upgrade switch
    {
        CardUpgrade.BalloonRadius or CardUpgrade.BalloonDamage => LabCard.Balloon,
        CardUpgrade.TapDanceStacks => LabCard.TapDance,
        CardUpgrade.BounceCount => LabCard.BouncingRounds,
        CardUpgrade.ButtonEvery or CardUpgrade.ButtonCount => LabCard.ButtonMissile,
        _ => LabCard.Velcro,
    };
    /// <summary>Velcro size goes 5 → 4 → 3 monsters and bounces 1 → 2 → 3; the others have three levels.</summary>
    public static int MaxLevel(CardUpgrade upgrade) => upgrade is CardUpgrade.VelcroSize or CardUpgrade.BounceCount or CardUpgrade.ButtonEvery or CardUpgrade.ButtonCount ? 2 : 3;

    // Tap dance: each hit (once a frame) adds a step of walking speed and fire rate, up to the stack cap; a gap ends it.
    public const int TapStacks = 5, TapStacksPerLevel = 3;
    public const float TapSpeedPerStack = .05f, TapFirePerStack = .04f, TapHold = 2.5f;
    // Bouncing rounds: from where the sniper round stopped to the nearest other monster, 70% each time.
    public const float BounceShare = .7f, BounceReach = 10f;
    // Honk charge: 3% of what is left of the slot-3 cooldown per hit (one per shot), at most a second each second.
    public const float HonkChargeShare = .03f, HonkChargePerSecond = 1f;
    // Button missile: every 6th hit (5, 4 with its upgrade) of a pistol, SMG, rifle or LMG sends a button (2, 3 with the other).
    public const int ButtonEveryHits = 6;
    public const float ButtonDamage = 8f, ButtonReach = 8f;
    /// <summary>The weapon type a button's hit carries: the bullet cards act on it, the gun cards (tap dance, last shot) do not.</summary>
    public const string ButtonWeaponType = "button";

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
    private readonly Dictionary<Player, int> _buttonHits = new();
    private readonly Dictionary<Player, ulong> _buttonFrame = new();

    /// <summary>Checks: buttons sent, trains run and boxes set.</summary>
    public int Buttons { get; private set; }
    public int Trains { get; private set; }
    public int Boxes { get; private set; }
    public ToyTrain? LastTrain { get; private set; }
    public JackBox? LastBox { get; private set; }

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
        if (card == LabSpecial.DoubleSpecial) SpecialShots = on ? Mathf.Max(SpecialShots, 2) : 1;
        if (!on) _specialLevel.Remove(card);
        if (card == LabSpecial.MadScientist) { if (on) RollMad(); else ClearMad(); }
        if (card == LabSpecial.Gather && !on) ClearGather();
        if (card == LabSpecial.HonkBeam && !on) { BeamShooter = null; BeamClock = 0; }
        if (card == LabSpecial.StaticFluff) { if (on) DealPoles(); else ClearPoles(); }
        TeamVersion++;
        KeepGlassCannon();
    }

    private readonly Dictionary<LabSpecial, int> _specialLevel = new();
    /// <summary>Goes up whenever a team card or its level changes (the host resends the card sync).</summary>
    public int TeamVersion { get; private set; }

    /// <summary>A team card's level: 0 off, 1 taken, 2–3 with its upgrades (M-29: like "special weapon N shots").</summary>
    public int SpecialLevel(LabSpecial card) => IsOn(card) ? Mathf.Max(1, _specialLevel.GetValueOrDefault(card, 1)) : 0;
    /// <summary>Team cards that come back as upgrades in the special window (special weapon twice has its own shots).</summary>
    public static int MaxSpecialLevel(LabSpecial card) => card is LabSpecial.Gather or LabSpecial.MadScientist or LabSpecial.HonkBeam or LabSpecial.StaticFluff ? 3 : 1;

    /// <summary>The upgrade of a team card: on, at <paramref name="level"/>.</summary>
    public void SetSpecialLevel(LabSpecial card, int level)
    {
        _specials.Add(card);
        _specialLevel[card] = Mathf.Clamp(level, 1, MaxSpecialLevel(card));
        if (card == LabSpecial.MadScientist) foreach (var p in Stage.Players.Where(IsInstanceValid)) p.MadLevel = SpecialLevel(card);
        TeamVersion++;
    }

    /// <summary>
    /// How many times slot 3 goes off per shot (1 without the card; "special weapon twice" makes it 2, and taken again
    /// as an upgrade 3 and then 4, user 2026-10-22).
    /// </summary>
    public int SpecialShots { get; private set; } = 1;
    public const int MaxSpecialShots = 4;
    /// <summary>The price of the extra shots (user 2026-10-22: not free): slot 3 recharges ×1.5 per extra shot step (2 → ×1.5, 3 → ×2, 4 → ×2.5).</summary>
    public const float CooldownPerExtraShot = .5f;
    public float SpecialCooldownScale => 1 + CooldownPerExtraShot * (SpecialShots - 1);


    /// <summary>The "special weapon N shots" upgrade: slot 3 goes off <paramref name="shots"/> times (2–4).</summary>
    public void SetSpecialShots(int shots)
    {
        _specials.Add(LabSpecial.DoubleSpecial);
        SpecialShots = Mathf.Clamp(Mathf.Max(SpecialShots, shots), 2, MaxSpecialShots);
        TeamVersion++;
    }

    /// <summary>The team's specials as bits (bit n = <see cref="Specials"/>[n]).</summary>
    public int SpecialMask => Specials.Select((s, i) => _specials.Contains(s) ? 1 << i : 0).Sum();

    /// <summary>Host → client card sync: every team card's level (special weapon twice: its shots) and gather!'s state.</summary>
    public void WriteTeam(NetWriter w)
    {
        w.U8(Specials.Length);
        foreach (var s in Specials) w.U8(s == LabSpecial.DoubleSpecial && IsOn(s) ? SpecialShots : SpecialLevel(s));
        w.I32(GatherAnchor != null && IsInstanceValid(GatherAnchor) ? GatherAnchor.PeerId : -1);
        w.I32(GatherNext != null && IsInstanceValid(GatherNext) ? GatherNext.PeerId : -1);
        w.U8(GatherEffect);
        w.F32(GatherClock);
        w.I32(BeamShooter != null && IsInstanceValid(BeamShooter) ? BeamShooter.PeerId : -1);
        w.F32(BeamClock);
        w.Yaw(BeamAim);
        w.F32(StaticClock);
    }

    /// <summary>Client: the host's team cards, so glass cannon's price, slot 3's recharge and gather!'s ring show here too.</summary>
    public void ReadTeam(NetReader r)
    {
        int n = r.U8();
        for (int i = 0; i < n; i++)
        {
            int level = r.U8();
            if (i >= Specials.Length) continue;
            var s = Specials[i];
            if (level == 0) { _specials.Remove(s); _specialLevel.Remove(s); continue; }
            _specials.Add(s);
            if (s == LabSpecial.DoubleSpecial) SpecialShots = Mathf.Clamp(level, 2, MaxSpecialShots);
            else _specialLevel[s] = level;
        }
        if (!IsOn(LabSpecial.DoubleSpecial)) SpecialShots = 1;
        int anchor = r.I32(), next = r.I32();
        GatherAnchor = Stage.Players.FirstOrDefault(p => IsInstanceValid(p) && p.PeerId == anchor && anchor >= 0);
        GatherNext = Stage.Players.FirstOrDefault(p => IsInstanceValid(p) && p.PeerId == next && next >= 0);
        GatherEffect = r.U8();
        GatherClock = r.F32();
        int beam = r.I32();
        BeamShooter = Stage.Players.FirstOrDefault(p => IsInstanceValid(p) && p.PeerId == beam && beam >= 0);
        BeamClock = r.F32();
        BeamAim = r.Yaw();
        StaticClock = r.F32();
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
        LabSpecial.ToyTrain => "toy_train",
        LabSpecial.JackBox => "jack_box",
        LabSpecial.Gather => "gather",
        LabSpecial.MadScientist => "mad_scientist",
        LabSpecial.HonkBeam => "honk_beam",
        LabSpecial.StaticFluff => "static_fluff",
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
        LabCard.ButtonMissile => "button_missile",
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
        CardUpgrade.ButtonEvery => "button_every",
        CardUpgrade.ButtonCount => "button_count",
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
        Stage.SurvivorDowned += OnDowned;
        Stage.SurvivorRevived += OnRevived;
        Stage.CompanionDowned += OnCompanionDowned;
    }

    public override void _ExitTree()
    {
        Stage.EnemyHit -= OnHit;
        Stage.EnemyKilled -= OnKilled;
        Stage.SurvivorDowned -= OnDowned;
        Stage.SurvivorRevived -= OnRevived;
        Stage.CompanionDowned -= OnCompanionDowned;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_glassed.Count > 0 || IsOn(LabSpecial.GlassCannon)) KeepGlassCannon();
        if (!Stage.Authoritative || Stage.State != StageState.Playing) { GatherView(); return; }
        float dt = (float)delta;
        _clock += dt;
        if (IsOn(LabSpecial.MadScientist)) foreach (var p in Stage.Players.Where(p => IsInstanceValid(p) && p.MadSize == 0 && !p.IsDead)) RollMad(p);
        if (IsOn(LabSpecial.Gather)) GatherStep(dt);
        if (IsOn(LabSpecial.HonkBeam)) BeamStep(dt);
        if (IsOn(LabSpecial.StaticFluff)) StaticStep(dt);
        GatherView();
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
            // Gather!'s fire rounds: the hit sets the monster burning, hotter the more are in the ring.
            if (shooter.GatherFire > 0 && !enemy.IsDead) enemy.Ignite(GatherBurnDps * (1 + shooter.GatherFire), GatherBurnSeconds, shooter);
            // Static fluff: next to a same-pole teammate, the round jumps on.
            if (shooter.Charged && IsOn(LabSpecial.StaticFluff) && _staticFrame.GetValueOrDefault(shooter) != frame)
            {
                _staticFrame[shooter] = frame;
                StaticChain(shooter, enemy, hit);
            }
            if (Has(LabCard.ButtonMissile, shooter) && IsButtonGun(hit.WeaponType) && _buttonFrame.GetValueOrDefault(shooter) != frame)
            {
                _buttonFrame[shooter] = frame;
                _buttonHits[shooter] = _buttonHits.GetValueOrDefault(shooter) + 1;
                if (_buttonHits[shooter] >= ButtonEvery(shooter)) SendButtons(shooter, enemy);
            }
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

    // ---------- button missile · toy train · jack-in-the-box (M-29 plan 16.6, batch 2) ----------

    /// <summary>The guns that send buttons: pistols, SMGs, rifles and LMGs (rapid fire).</summary>
    public static bool IsButtonGun(string? type) =>
        WeaponDatabase.Current.CategoryOfType(type) is WeaponCategory.Pistol or WeaponCategory.Smg or WeaponCategory.Rifle or WeaponCategory.Lmg;

    /// <summary>Hits between buttons for <paramref name="who"/> (6, then 5 and 4).</summary>
    public int ButtonEvery(Player who) => ButtonEveryHits - Level(CardUpgrade.ButtonEvery, who);

    /// <summary>Buttons fly to the nearest other monsters within reach (one, then two and three).</summary>
    private void SendButtons(Player shooter, Enemy from)
    {
        _buttonHits[shooter] = 0;
        int count = 1 + Level(CardUpgrade.ButtonCount, shooter);
        var targets = Stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead && e != from && Flat(e.GlobalPosition - from.GlobalPosition).Length() <= ButtonReach)
            .OrderBy(e => Flat(e.GlobalPosition - from.GlobalPosition).LengthSquared()).Take(count).ToList();
        foreach (var target in targets)
        {
            Buttons++;
            CardProps.Spawn(Stage, CardPropKind.Button, from.GlobalPosition + Vector3.Up * 1.2f, target.GlobalPosition + Vector3.Up, shooter, target,
                ButtonDamage * shooter.OutgoingDamage);
        }
    }

    private void OnDowned(Player who)
    {
        if (!Stage.Authoritative || !IsInstanceValid(who)) return;
        // The train runs back up the line the downing hit came along.
        if (IsOn(LabSpecial.ToyTrain)) RunTrain(who, -who.LastHitDirection, 2f);
        if (IsOn(LabSpecial.JackBox)) SetBox(who.GlobalPosition, who);
    }

    private void OnRevived(Player who)
    {
        // The mad scientist rolls again for anyone back on their feet.
        if (Stage.Authoritative && IsInstanceValid(who) && IsOn(LabSpecial.MadScientist)) RollMad(who);
        if (!Stage.Authoritative || !IsInstanceValid(who) || !IsOn(LabSpecial.ToyTrain)) return;
        // Once more the other way: from out by the attackers, back past them.
        RunTrain(who, who.LastHitDirection, ToyTrain.Length - 2f);
    }

    private void OnCompanionDowned(Companion who)
    {
        if (Stage.Authoritative && IsOn(LabSpecial.JackBox) && IsInstanceValid(who.Survivor)) SetBox(who.GlobalPosition, who.Survivor);
    }

    private void RunTrain(Player who, Vector3 direction, float startBack)
    {
        Trains++;
        var dir = Flat(direction).LengthSquared() > .001f ? Flat(direction).Normalized() : Vector3.Forward;
        var start = new Vector3(who.GlobalPosition.X, 0, who.GlobalPosition.Z) - dir * startBack;
        LastTrain = (ToyTrain)CardProps.Spawn(Stage, CardPropKind.Train, start, dir, who);
    }

    private void SetBox(Vector3 at, Player owner)
    {
        Boxes++;
        LastBox = (JackBox)CardProps.Spawn(Stage, CardPropKind.JackBox, new Vector3(at.X, 0, at.Z), Vector3.Zero, owner);
    }

    // ---------- mad scientist goose · gather! (M-29 plan 16.6, batch 3) ----------

    /// <summary>Checks: how many times the mad scientist rolled someone's size.</summary>
    public int MadRolls { get; private set; }

    /// <summary>Everyone (or <paramref name="only"/>) turns big or small at random, at the card's level (no choosing).</summary>
    public void RollMad(Player? only = null)
    {
        foreach (var p in only != null ? new[] { only } : Stage.Players.ToArray())
        {
            if (!IsInstanceValid(p) || p.IsDead) continue;
            MadRolls++;
            _rng.Randomize();
            p.MadLevel = SpecialLevel(LabSpecial.MadScientist);
            p.MadSize = _rng.Randf() < .5f ? 1 : -1;
            Stage.Effects.FloatingText(p.GlobalPosition + Vector3.Up * 2.8f, Loc.Text(p.MadSize > 0 ? "fx_card_mad_big" : "fx_card_mad_small"),
                p.MadSize > 0 ? new Color("c08bff") : new Color("7dff9a"));
        }
    }

    /// <summary>Host: a new stretch starts (after a middle boss): the mad scientist rolls everyone again.</summary>
    public void NewStretch()
    {
        if (Stage.Authoritative && IsOn(LabSpecial.MadScientist)) RollMad();
    }

    private void ClearMad()
    {
        foreach (var p in Stage.Players.Where(IsInstanceValid)) p.MadSize = 0;
    }

    // Gather!: an anchor survivor every 30 s (not the last one), a ring around them; each one inside adds to the bonus
    // of everyone inside. The bonus turns damage → crit damage → fire rounds, one per anchor.
    public const float GatherPeriod = 30f, GatherWarn = 5f, GatherBurnDps = 6f, GatherBurnSeconds = 3f;
    public Player? GatherAnchor { get; private set; }
    /// <summary>Who is next, shown blinking for the last 5 s.</summary>
    public Player? GatherNext { get; private set; }
    /// <summary>0 damage, 1 crit damage, 2 fire rounds.</summary>
    public int GatherEffect { get; private set; }
    public float GatherClock { get; private set; }
    public float GatherRadius => SpecialLevel(LabSpecial.Gather) switch { 3 => 9f, 2 => 7.5f, _ => 6f };
    public float GatherPerPerson => SpecialLevel(LabSpecial.Gather) switch { 3 => .15f, 2 => .12f, _ => .10f };
    /// <summary>Who is in the ring now (anchor included).</summary>
    public IEnumerable<Player> GatherMembers => GatherAnchor == null || !IsInstanceValid(GatherAnchor) ? Enumerable.Empty<Player>()
        : Stage.Players.Where(p => IsInstanceValid(p) && !p.IsDead && Flat(p.GlobalPosition - GatherAnchor.GlobalPosition).Length() <= GatherRadius);

    private void GatherStep(float dt)
    {
        var alive = Stage.Players.Where(p => IsInstanceValid(p) && !p.IsDead).ToList();
        if (GatherAnchor == null || !IsInstanceValid(GatherAnchor) || GatherAnchor.IsDead) { GatherAnchor = PickOther(alive, null); GatherClock = 0; TeamVersion++; }
        GatherClock += dt;
        if (GatherNext == null && GatherClock >= GatherPeriod - GatherWarn) { GatherNext = PickOther(alive, GatherAnchor); TeamVersion++; }
        if (GatherClock >= GatherPeriod)
        {
            GatherAnchor = GatherNext ?? PickOther(alive, GatherAnchor);
            GatherNext = null;
            GatherEffect = (GatherEffect + 1) % 3;
            GatherClock = 0;
            TeamVersion++;
        }
        var members = GatherMembers.ToList();
        float bonus = members.Count * GatherPerPerson;
        foreach (var p in Stage.Players.Where(IsInstanceValid))
        {
            bool inside = members.Contains(p);
            p.GatherDamage = inside && GatherEffect == 0 ? bonus : 0;
            p.GatherCrit = inside && GatherEffect == 1 ? bonus : 0;
            p.GatherFire = inside && GatherEffect == 2 ? bonus : 0;
        }
    }

    /// <summary>Checks: skips gather!'s clock forward.</summary>
    public void AdvanceGatherForTest(float seconds) => GatherStep(seconds);

    private Player? PickOther(List<Player> from, Player? not)
    {
        var choices = from.Where(p => p != not).ToList();
        if (choices.Count == 0) return from.FirstOrDefault();
        _rng.Randomize();
        return choices[_rng.RandiRange(0, choices.Count - 1)];
    }

    private void ClearGather()
    {
        GatherAnchor = GatherNext = null;
        GatherClock = 0;
        foreach (var p in Stage.Players.Where(IsInstanceValid)) p.GatherDamage = p.GatherCrit = p.GatherFire = 0;
    }

    // Honk beam (16.6.2): every 40 s (32, 25 with its upgrades) someone other than last time gets 3 s of a red cone
    // following their aim (locked for the last 0.5 s), then a honk beam the size of the flashlight cone: commons in it
    // burst, named and bosses melt for 12 s (double damage, slower, fly further), teammates in it are dazed 2 s and
    // weak 10 s (not the downed). Firing it resets the shooter's slot-3 recharge.
    public const float BeamTelegraph = 3f, BeamLock = .5f, BeamMelt = 12f, BeamDaze = 2f, BeamWeak = 10f;
    public Player? BeamShooter { get; private set; }
    /// <summary>Seconds into the beam's cycle; the telegraph runs over its last 3 s.</summary>
    public float BeamClock { get; private set; }
    public Vector3 BeamAim { get; private set; } = Vector3.Forward;
    public float BeamPeriod => SpecialLevel(LabSpecial.HonkBeam) switch { 3 => 25f, 2 => 32f, _ => 40f };
    public bool BeamTelegraphing => BeamShooter != null && BeamClock >= BeamPeriod - BeamTelegraph;
    /// <summary>Checks: beams fired, monsters burst, named melted, teammates dazed.</summary>
    public int Beams { get; private set; }
    public int BeamBursts { get; private set; }
    public int BeamMelts { get; private set; }
    public int BeamDazes { get; private set; }
    private Player? _lastBeamShooter;

    private void BeamStep(float dt)
    {
        BeamClock += dt;
        var alive = Stage.Players.Where(p => IsInstanceValid(p) && !p.IsDead && !p.IsDowned).ToList();
        if (BeamShooter == null && BeamClock >= BeamPeriod - BeamTelegraph)
        {
            BeamShooter = PickOther(alive, _lastBeamShooter);
            TeamVersion++;
        }
        if (BeamShooter == null) return;
        if (!IsInstanceValid(BeamShooter) || BeamShooter.IsDead) { BeamShooter = null; return; }
        // Follows the aim until the last half second, then holds still so the others can get out.
        if (BeamClock < BeamPeriod - BeamLock) BeamAim = Flat(BeamShooter.AimDirection).Normalized();
        if (BeamClock >= BeamPeriod) FireBeam();
    }

    /// <summary>Checks: skips the honk beam's clock forward.</summary>
    public void AdvanceBeamForTest(float seconds)
    {
        while (seconds > 0) { float step = Mathf.Min(seconds, .1f); BeamStep(step); seconds -= step; }
    }

    private void FireBeam()
    {
        var shooter = BeamShooter!;
        var at = shooter.GlobalPosition;
        var aim = BeamAim;
        Beams++;
        Stage.Sfx.PlayAt("honk", at + Vector3.Up, 10, .6f);
        Stage.Effects.FloatingText(at + Vector3.Up * 3f, Loc.Text("fx_card_honk_beam"), new Color("ffd23f"));
        _view?.Flash(at, aim);
        float half = Mathf.DegToRad(VisionSystem.NormalHalfAngle);
        bool InCone(Vector3 p)
        {
            var to = Flat(p - at);
            float d = to.Length();
            return d <= VisionSystem.Range && (d < .5f || to.Normalized().AngleTo(aim) <= half);
        }
        foreach (var e in Stage.Enemies.ToList())
        {
            if (!IsInstanceValid(e) || e.IsDead || !InCone(e.GlobalPosition)) continue;
            if (!e.IsSpecial)
            {
                BeamBursts++;
                Stage.Effects.Stuffing(e.GlobalPosition + Vector3.Up * .7f, aim, e.Definition.StuffingColor, 24);
                e.Shove(aim, 90, e.Health + 1, shooter);
                continue;
            }
            BeamMelts++;
            bool boss = e.Monster.Tier == MonsterTier.Boss;
            e.Weaken(BeamMelt, boss ? .3f : .6f, boss ? 1.5f : 2f);
        }
        foreach (var p in Stage.Players.ToList())
        {
            if (p == shooter || !IsInstanceValid(p) || p.IsDead || p.IsDowned || !InCone(p.GlobalPosition)) continue;
            BeamDazes++;
            p.DazeTime = BeamDaze;
            p.WeakTime = BeamDaze + BeamWeak;
        }
        // The one who fired it gets slot 3 back at once.
        shooter.Loadout.Special?.ShortenCooldown(999);
        _lastBeamShooter = shooter;
        BeamShooter = null;
        BeamClock = 0;
        TeamVersion++;
    }

    // Static fluff (16.6.3): every 30 s everyone gets + or − (with 3+, both kinds at least once). Opposite poles within
    // 4 m spark and lose 3% health a second (never below 20%); the same pole within 4 m charges both: their rounds jump on
    // to 1–2 monsters near what they hit (2, 3 with its upgrades), harder with each level.
    public const float StaticPeriod = 30f, StaticWarn = 3f, StaticReach = 4f, StaticDrain = .03f, StaticFloor = .2f, StaticJump = 6f;
    public float StaticClock { get; private set; }
    /// <summary>Checks: rounds that jumped on.</summary>
    public int StaticJumps { get; private set; }
    private readonly Dictionary<Player, ulong> _staticFrame = new();

    private void DealPoles()
    {
        var people = Stage.Players.Where(p => IsInstanceValid(p) && !p.IsDead).OrderBy(_ => _rng.Randf()).ToList();
        _rng.Randomize();
        for (int i = 0; i < people.Count; i++) people[i].Pole = _rng.Randf() < .5f ? 1 : -1;
        // Three or more: at least one of each.
        if (people.Count >= 3 && people.All(p => p.Pole == people[0].Pole)) people[^1].Pole = -people[0].Pole;
        StaticClock = 0;
        TeamVersion++;
    }

    private void ClearPoles()
    {
        foreach (var p in Stage.Players.Where(IsInstanceValid)) { p.Pole = 0; p.Charged = false; }
        StaticClock = 0;
    }

    private void StaticStep(float dt)
    {
        StaticClock += dt;
        if (StaticClock >= StaticPeriod) DealPoles();
        var people = Stage.Players.Where(p => IsInstanceValid(p) && !p.IsDead).ToList();
        foreach (var p in people)
        {
            if (p.Pole == 0) p.Pole = _rng.Randf() < .5f ? 1 : -1;
            p.Charged = false;
        }
        for (int i = 0; i < people.Count; i++)
            for (int j = i + 1; j < people.Count; j++)
            {
                var a = people[i];
                var b = people[j];
                if (Flat(a.GlobalPosition - b.GlobalPosition).Length() > StaticReach) continue;
                if (a.Pole == b.Pole) { a.Charged = b.Charged = true; continue; }
                a.Drain(a.MaxHealth * StaticDrain * dt, StaticFloor);
                b.Drain(b.MaxHealth * StaticDrain * dt, StaticFloor);
            }
    }

    /// <summary>Checks: skips static fluff's clock forward.</summary>
    public void AdvanceStaticForTest(float seconds)
    {
        while (seconds > 0) { float step = Mathf.Min(seconds, .1f); StaticStep(step); seconds -= step; }
    }

    private void StaticChain(Player shooter, Enemy from, HitInfo hit)
    {
        int level = SpecialLevel(LabSpecial.StaticFluff);
        int count = level switch { 3 => 3, 2 => 2, _ => _rng.RandiRange(1, 2) };
        float share = .4f + .15f * (level - 1);
        var targets = Stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead && e != from && Flat(e.GlobalPosition - from.GlobalPosition).Length() <= StaticJump)
            .OrderBy(e => Flat(e.GlobalPosition - from.GlobalPosition).LengthSquared()).Take(count).ToList();
        foreach (var t in targets)
        {
            StaticJumps++;
            _view?.Bolt(from.GlobalPosition + Vector3.Up, t.GlobalPosition + Vector3.Up);
            _bouncing = true;
            t.TakeHit(new HitInfo(Mathf.Max(1, hit.Damage * share), Flat(t.GlobalPosition - from.GlobalPosition).Normalized(), t.GlobalPosition + Vector3.Up,
                false, hit.Power * .3f, shooter, "static"));
            _bouncing = false;
        }
    }

    private TeamCardView? _view;
    /// <summary>The team cards' rings, cones, marks and HUD lines on every PC.</summary>
    private void GatherView()
    {
        if (!IsOn(LabSpecial.Gather) && !IsOn(LabSpecial.HonkBeam) && !IsOn(LabSpecial.StaticFluff)) { _view?.Hide(); return; }
        if (_view == null || !IsInstanceValid(_view))
        {
            _view = new TeamCardView { Name = "TeamCardView", Cards = this, Stage = Stage };
            Stage.AddChild(_view);
        }
        _view.Refresh();
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
