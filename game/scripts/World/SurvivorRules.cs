using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Onslaught (code Survivor, doc/menu-multiplayer-plan.md 3.1, decisions.md M-26) on its own open field:
/// first every person picks two weapons from racks on the floor (three random ones from weapon_pool each),
/// reserves never run out and slot 3-type weapons recharge on a cooldown instead of reloading. Then masses of commons
/// close in from every side, bigger groups as time goes on; who comes is the mode table (modes/survivor_spawns.csv,
/// seconds since arming). Named ones arrive at set times; each one that falls drops an upgrade rack, and once they are
/// all down the swarm pauses for rest_after_named_s (M-24). At boss_at_s a skill-using boss (<see cref="BossEnemy"/>)
/// arrives; killing it clears the match. Level-up abilities are not in this version.
/// </summary>
public partial class SurvivorRules : ModeRules
{
    /// <summary>Picks every person makes before the swarm starts (slot 1, then slot 2).</summary>
    public const int ArmingPicks = 2;

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
    /// <summary>The weapons lying in the current rack(s) (checks, the HUD prompt).</summary>
    public IReadOnlyList<WeaponPickup> RackItems => _racks.SelectMany(r => r.Items).Where(IsInstanceValid).ToList();
    public int UpgradeRacks { get; private set; }

    private sealed class Rack
    {
        public int Slot;          // 1 or 2 for the arming racks, 0 = an upgrade (goes to the weapon in hand)
        public bool Upgrade;
        public readonly List<WeaponPickup> Items = new();
        public readonly HashSet<Player> Taken = new();
        public Node3D? Label;
    }

    private float _spawnTimer = 1.5f, _namedThrough = -1, _restUntil, _armedAt;
    private readonly List<(Enemy Enemy, Vector3 Last)> _named = new();
    private readonly List<Rack> _racks = new();
    private readonly Dictionary<Player, int> _picks = new();

    /// <summary>Everyone starts by arming (on every PC, so a client's clock starts when the host's does).</summary>
    public override Objective InitialObjective => Objective.ModeArming;

    public override void _Ready()
    {
        base._Ready();
        if (Stage.Authoritative) OpenRack(1);
    }

    public override string ObjectiveText => Stage.State == StageState.Cleared ? Loc.Text("ui_objective_survivor_cleared")
        : Arming ? Loc.Format("fmt_objective_survivor_arm", Mathf.Min(ArmingPicks, _picks.GetValueOrDefault(Stage.Player) + 1), ArmingPicks)
        : BossArrived ? Loc.Text("ui_objective_survivor_boss")
        : RestLeft > 0 ? Loc.Format("fmt_objective_survivor_rest", Mathf.CeilToInt(RestLeft), Clock(Rules.BossAt - MatchTime))
        : _named.Count > 0 ? Loc.Text("ui_objective_survivor_named")
        : Loc.Format("fmt_objective_survivor_hold", Clock(Rules.BossAt - MatchTime));

    public override string BarLabel => BossArrived
        ? Loc.Format("fmt_bar_boss", Loc.Text("name_monster_boss"), Mathf.CeilToInt(BarFraction * 100))
        : Loc.Format("fmt_bar_boss_eta", Clock(Rules.BossAt - MatchTime));
    public override Color BarColor => BossArrived ? UiTheme.Danger : UiTheme.Accent;
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
        if (!BossArrived) BarFraction = Mathf.Clamp(MatchTime / Rules.BossAt, 0, 1);
        else if (Stage.Authoritative && Boss != null) BarFraction = IsInstanceValid(Boss) && !Boss.IsDead ? Boss.Health / Boss.MaxHealth : 0;
    }

    public override void ApplyNetBar(float fraction)
    {
        if (BossArrived) BarFraction = fraction;
    }

    protected override void Step(float dt)
    {
        if (Arming) return;
        if (!BossArrived && MatchTime >= Rules.BossAt) SpawnBoss();
        if (BossArrived && (Boss == null || !IsInstanceValid(Boss) || Boss.IsDead))
        {
            Stage.EndMode(true, "msg_survivor_clear");
            return;
        }
        if (!BossArrived) UpdateNamed();
        _spawnTimer -= dt;
        if (_spawnTimer > 0 || RestLeft > 0) return;
        _spawnTimer = Rules.SpawnEvery;
        SpawnGroup();
    }

    // ---------- weapon racks ----------

    /// <summary>The people who pick weapons (AI squadmates keep their own).</summary>
    private IEnumerable<Player> Humans => Stage.AlivePlayers.Where(p => !p.IsBot);

    private void OpenRack(int slot, Vector3? at = null, bool upgrade = false)
    {
        var center = at ?? Stage.Map.PlayerStart + new Vector3(0, 0, -3.2f);
        var rack = new Rack { Slot = slot, Upgrade = upgrade };
        var pool = Rules.WeaponPool.OrderBy(_ => Rng.Randi()).Take(Rules.RackSize).ToList();
        for (int i = 0; i < pool.Count; i++)
        {
            var spot = center + new Vector3((i - (pool.Count - 1) * .5f) * 2.4f, 0, 0);
            if (Stage.Flow.IsBlocked(spot)) spot = center;
            var def = ArmoryCatalog.Weapon(pool[i]);
            var item = WeaponPickup.ForWeapon(Stage, spot, def);
            item.Prompt = Loc.Format(upgrade ? "fmt_use_rack_upgrade" : "fmt_use_rack", def.DisplayName, slot);
            rack.Items.Add(item);
        }
        var label = new Label3D
        {
            Text = Loc.Text(upgrade ? "ui_floor_rack_upgrade" : "ui_floor_rack"), Position = center + new Vector3(0, .03f, 1.4f),
            RotationDegrees = new Vector3(-90, 0, 0), FontSize = 64, PixelSize = .012f, Modulate = new Color("ffcf5c", .9f), OutlineSize = 0,
        };
        Stage.AddChild(label);
        rack.Label = label;
        _racks.Add(rack);
        if (upgrade)
        {
            UpgradeRacks++;
            Stage.ShowNotice(Loc.Text("msg_rack_upgrade"), 3);
        }
    }

    /// <summary>A weapon from a rack goes to that rack's slot (an upgrade to the weapon in hand), with Onslaught's ammo rules.</summary>
    public override bool TakePickup(WeaponPickup pickup, Player who)
    {
        var rack = _racks.FirstOrDefault(r => r.Items.Contains(pickup));
        if (rack == null || pickup.Weapon is not { } def) return false;
        if (who.IsBot || rack.Taken.Contains(who)) return true;
        if (!rack.Upgrade && _picks.GetValueOrDefault(who) + 1 != rack.Slot) return true;
        int slot = rack.Upgrade ? (who.Loadout.ActiveSlot == 3 ? 2 : who.Loadout.ActiveSlot) : rack.Slot;
        who.Weapons.PlaceInSlot(slot, def);
        rack.Taken.Add(who);
        rack.Items.Remove(pickup);
        Stage.RemovePickupFromWorld(pickup);
        Stage.Effects.FloatingText(who.GlobalPosition + Vector3.Up * 2.6f, def.DisplayName, new Color("ffcf5c"));
        if (who.IsLocal) Stage.Sfx.Play("pickup");
        else Stage.Sfx.PlayAt("pickup", who.GlobalPosition + Vector3.Up);
        if (!rack.Upgrade) _picks[who] = rack.Slot;
        // An arming rack closes when every person has taken from it; an upgrade rack after the first pick.
        if (rack.Upgrade || Humans.All(rack.Taken.Contains)) CloseRack(rack);
        if (!rack.Upgrade && Humans.All(p => _picks.GetValueOrDefault(p) >= rack.Slot))
        {
            if (rack.Slot < ArmingPicks) OpenRack(rack.Slot + 1);
            else Stage.SetModeObjective(Objective.ModeGoal);
        }
        return true;
    }

    private void CloseRack(Rack rack)
    {
        foreach (var item in rack.Items.Where(IsInstanceValid)) Stage.RemovePickupFromWorld(item);
        rack.Items.Clear();
        rack.Label?.QueueFree();
        _racks.Remove(rack);
    }

    // ---------- named and swarm ----------

    /// <summary>Named rows that are due arrive; each one down drops an upgrade rack, the last one down starts the rest.</summary>
    private void UpdateNamed()
    {
        bool had = _named.Count > 0;
        for (int i = _named.Count - 1; i >= 0; i--)
        {
            var (enemy, last) = _named[i];
            if (IsInstanceValid(enemy) && !enemy.IsDead) { _named[i] = (enemy, enemy.GlobalPosition); continue; }
            _named.RemoveAt(i);
            OpenRack(0, last, upgrade: true);
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

    /// <summary>Group size grows from group_start to group_end until the boss, then drops to boss_group_scale of that.</summary>
    public int GroupSize => BossArrived
        ? Mathf.Max(1, Mathf.RoundToInt(Rules.GroupEnd * Rules.BossGroupScale))
        : Mathf.RoundToInt(Mathf.Lerp(Rules.GroupStart, Rules.GroupEnd, Mathf.Clamp(MatchTime / Rules.BossAt, 0, 1)));

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

    /// <summary>Brings the boss out now (also used by checks to skip the wait).</summary>
    public BossEnemy SpawnBoss()
    {
        var leader = Stage.Leader.GlobalPosition;
        var at = PickSpot(Stage.Flow.OpenCellsNear(leader, 11, 16)) ?? leader + Vector3.Right * 10;
        var boss = Stage.SpawnEnemy<BossEnemy>(MonsterDatabase.Get(BossEnemy.MonsterId), at, awake: true);
        Arrive(boss);
        return boss;
    }

    private void Arrive(BossEnemy boss)
    {
        Boss = boss;
        BossArrived = true;
        BarFraction = 1;
        Stage.ShowNotice(Loc.Text("msg_survivor_boss"), 4);
        Stage.Sfx.Play("bull_snort", 4, .6f);
        Stage.Cam.AddShake(.45f);
    }
}
