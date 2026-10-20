using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Survival MVP (doc/menu-multiplayer-plan.md 3.1): hold the warehouse generator through timed waves.
/// Waves come in through the two doorways (street side and yard side). Who comes is the mode table
/// (modes/survival_spawns.csv, wave numbers): later waves mix in armed animals, and named ones arrive as a wave starts.
/// Lasting to the end of the last wave clears the match; the generator breaking or the squad falling loses it.
/// The schedule follows the clock, so a client can show the same wave text without being sent it.
/// </summary>
public partial class SurvivalRules : ModeRules
{
    public override GameMode Mode => GameMode.Survival;
    public override BaseCore? Core => _core;
    public SurvivalTuning Rules => Tuning.Survival;
    public int WaveCount => Rules.Waves.Count;
    public int SpawnedThisMatch { get; private set; }
    /// <summary>Named monsters the table sent so far (checks).</summary>
    public int NamedSent { get; private set; }
    private int _namedThroughWave;

    private BaseCore _core = null!;
    private int _shownWave = -1;
    private bool _shownBreak;
    private float _spawnTimer;

    public override void _Ready()
    {
        base._Ready();
        _core = BaseCore.Create(Stage, Stage.Map.CoreSpot, Rules.CoreHp);
        _core.Destroyed += () =>
        {
            if (Stage.Authoritative) Stage.EndMode(false, "msg_survival_core_lost");
        };
        BarFraction = 1;
    }

    /// <summary>Where the clock is: the current wave (0-based, −1 before the first), whether it is running, and seconds left in that part.</summary>
    public (int Wave, bool Running, float Left) Phase(float time)
    {
        float t = time - Rules.Prep;
        if (t < 0) return (-1, false, -t);
        for (int i = 0; i < Rules.Waves.Count; i++)
        {
            float duration = Rules.Waves[i].Duration;
            if (t < duration) return (i, true, duration - t);
            t -= duration;
            if (i == Rules.Waves.Count - 1) break;
            if (t < Rules.Break) return (i, false, Rules.Break - t);
            t -= Rules.Break;
        }
        return (Rules.Waves.Count - 1, false, 0);
    }

    public override string ObjectiveText
    {
        get
        {
            var (wave, running, left) = Phase(Time);
            if (Stage.State == StageState.Cleared) return Loc.Text("ui_objective_survival_cleared");
            if (wave < 0) return Loc.Format("fmt_objective_survival_prep", Mathf.CeilToInt(left));
            return running
                ? Loc.Format("fmt_objective_survival_wave", wave + 1, WaveCount, Mathf.CeilToInt(left))
                : Loc.Format("fmt_objective_survival_break", wave + 2, Mathf.CeilToInt(left));
        }
    }

    public override string BarLabel => Loc.Format("fmt_bar_core", Mathf.CeilToInt(BarFraction * 100));
    public override Color BarColor => BarFraction > .35f ? UiTheme.Heal : UiTheme.Danger;
    public override string ResultText => Stage.State == StageState.Cleared
        ? Loc.Format("fmt_result_survival_clear", WaveCount, Mathf.CeilToInt(BarFraction * 100))
        : Loc.Format("fmt_result_survival_lost", Mathf.Max(1, Phase(Time).Wave + 1), WaveCount);

    public override void ApplyNetBar(float fraction)
    {
        base.ApplyNetBar(fraction);
        _core.SetHealthFraction(fraction);
    }

    protected override void Tick(float dt)
    {
        if (Stage.Authoritative) BarFraction = _core.Health / _core.MaxHealth;
        var (wave, running, _) = Phase(Time);
        if (running && wave != _shownWave)
        {
            _shownWave = wave;
            _shownBreak = false;
            Stage.ShowNotice(Loc.Format("fmt_survival_wave_start", wave + 1, WaveCount), 3);
            Stage.Sfx.Play("sonar");
            Stage.Cam.AddShake(.2f);
        }
        else if (!running && wave >= 0 && wave == _shownWave && !_shownBreak && wave < WaveCount - 1)
        {
            _shownBreak = true;
            Stage.ShowNotice(Loc.Text("msg_survival_wave_held"), 3);
        }
    }

    protected override void Step(float dt)
    {
        if (Time >= Rules.Total)
        {
            Stage.EndMode(true, "msg_survival_clear");
            return;
        }
        var (index, running, _) = Phase(Time);
        if (!running) { _spawnTimer = .5f; return; }
        _spawnTimer -= dt;
        if (_spawnTimer > 0) return;
        if (index + 1 > _namedThroughWave)
        {
            SpawnNamed(_namedThroughWave, index + 1);
            _namedThroughWave = index + 1;
        }
        var wave = Rules.Waves[index];
        _spawnTimer = wave.Every;
        SpawnGroup(wave, index);
    }

    /// <summary>The table's named rows for waves (<paramref name="after"/>, <paramref name="upTo"/>], through a doorway.</summary>
    private void SpawnNamed(int after, int upTo)
    {
        foreach (var row in Table.NamedDue(after, upTo, Stage.ThemeId))
        {
            if (PickSpot(Gate()) is not { } at) continue;
            for (int i = 0; i < row.Count; i++) ModeSpawnTable.Spawn(Stage, row, at);
            NamedSent += row.Count;
        }
    }

    /// <summary>One group through one doorway; each member is a weighted pick from the table's rows for this wave.</summary>
    public int SpawnGroup(SurvivalWave wave, int index)
    {
        int alive = Stage.AllEnemies.Count(e => !e.IsDead);
        int count = Mathf.Min(wave.Group, Rules.MaxAlive - alive);
        if (count <= 0) return 0;
        if (PickSpot(Gate()) is not { } at) return 0;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (Table.PickCommon(index + 1, Stage.ThemeId, Rng) is not { } row) break;
            ModeSpawnTable.Spawn(Stage, row, Near(at));
            spawned++;
        }
        SpawnedThisMatch += spawned;
        return spawned;
    }

    /// <summary>One of the map's doorways at random; with none, a ring 16–24 m around the generator.</summary>
    private IEnumerable<Vector3> Gate()
    {
        var gates = Stage.Map.Gates;
        return gates.Count > 0 ? gates[Rng.RandiRange(0, gates.Count - 1)] : Stage.Flow.OpenCellsNear(_core.GlobalPosition, 16, 24);
    }
}
