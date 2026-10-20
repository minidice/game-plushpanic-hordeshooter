using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Survivor MVP (doc/menu-multiplayer-plan.md 3.1): masses of weak commons close in from every side,
/// bigger groups as time goes on; at boss_at_s a skill-using boss (<see cref="BossEnemy"/>) arrives.
/// Killing it clears the match. Level-up abilities are not in this first version.
/// </summary>
public partial class SurvivorRules : ModeRules
{
    public override GameMode Mode => GameMode.Survivor;
    public SurvivorTuning Rules => Tuning.Survivor;
    /// <summary>The boss once it is out (the host's, or the client's puppet of it).</summary>
    public BossEnemy? Boss { get; private set; }
    public bool BossArrived { get; private set; }
    public int SpawnedThisMatch { get; private set; }

    private float _spawnTimer = 1.5f;

    public override string ObjectiveText => Stage.State == StageState.Cleared ? Loc.Text("ui_objective_survivor_cleared")
        : BossArrived ? Loc.Text("ui_objective_survivor_boss")
        : Loc.Format("fmt_objective_survivor_hold", Clock(Rules.BossAt - Time));

    public override string BarLabel => BossArrived
        ? Loc.Format("fmt_bar_boss", Loc.Text("name_monster_boss"), Mathf.CeilToInt(BarFraction * 100))
        : Loc.Format("fmt_bar_boss_eta", Clock(Rules.BossAt - Time));
    public override Color BarColor => BossArrived ? UiTheme.Danger : UiTheme.Accent;
    public override string ResultText => Stage.State == StageState.Cleared
        ? Loc.Text("msg_result_survivor_clear")
        : BossArrived ? Loc.Format("fmt_result_survivor_boss", Mathf.CeilToInt(BarFraction * 100)) : Loc.Format("fmt_result_survivor_lost", Clock(Time));

    protected override void Tick(float dt)
    {
        // A client learns about the boss from its spawn event (it is a puppet there).
        if (Boss == null && !Stage.Authoritative && Stage.AllEnemies.OfType<BossEnemy>().FirstOrDefault() is { } puppet) Arrive(puppet);
        if (!BossArrived) BarFraction = Mathf.Clamp(Time / Rules.BossAt, 0, 1);
        else if (Stage.Authoritative && Boss != null) BarFraction = IsInstanceValid(Boss) && !Boss.IsDead ? Boss.Health / Boss.MaxHealth : 0;
    }

    public override void ApplyNetBar(float fraction)
    {
        if (BossArrived) BarFraction = fraction;
    }

    protected override void Step(float dt)
    {
        if (!BossArrived && Time >= Rules.BossAt) SpawnBoss();
        if (BossArrived && (Boss == null || !IsInstanceValid(Boss) || Boss.IsDead))
        {
            Stage.EndMode(true, "msg_survivor_clear");
            return;
        }
        _spawnTimer -= dt;
        if (_spawnTimer > 0) return;
        _spawnTimer = Rules.SpawnEvery;
        SpawnGroup();
    }

    /// <summary>Group size grows from group_start to group_end until the boss, then drops to boss_group_scale of that.</summary>
    public int GroupSize => BossArrived
        ? Mathf.Max(1, Mathf.RoundToInt(Rules.GroupEnd * Rules.BossGroupScale))
        : Mathf.RoundToInt(Mathf.Lerp(Rules.GroupStart, Rules.GroupEnd, Mathf.Clamp(Time / Rules.BossAt, 0, 1)));

    /// <summary>A group of weak commons on a ring around a random survivor, off screen where possible.</summary>
    public int SpawnGroup()
    {
        int count = Mathf.Min(GroupSize, HordeDirector.MaxCommons - Stage.Director.AliveCommons);
        if (count <= 0) return 0;
        var survivors = Stage.AlivePlayers.Where(p => !p.IsBot).ToList();
        if (survivors.Count == 0) return 0;
        var center = survivors[Rng.RandiRange(0, survivors.Count - 1)].GlobalPosition;
        if (PickSpot(Stage.Flow.OpenCellsNear(center, Rules.SpawnMin, Rules.SpawnMax)) is not { } at) return 0;
        for (int i = 0; i < count; i++) SpawnHunter(null, at).ScaleHealth(Rules.CommonHpScale);
        SpawnedThisMatch += count;
        return count;
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
