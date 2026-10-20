using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Survival MVP (doc/menu-multiplayer-plan.md 3.1): hold the warehouse generator through timed waves.
/// Waves come in through the two doorways (street side and yard side), later ones with armed animals.
/// Lasting to the end of the last wave clears the match; the generator breaking or the squad falling loses it.
/// The schedule follows the clock, so a client can show the same wave text without being sent it.
/// </summary>
public partial class SurvivalRules : ModeRules
{
    /// <summary>The generator stands in the open lane between the hall's shelves.</summary>
    public static readonly Vector3 CorePosition = new(68, 0, 1.6f);

    // Far enough from the doorways that a group is off screen while the player guards the core.
    private static readonly Vector3[] StreetGate = Spots(40, 49, -4.5f, 4.5f);
    private static readonly Vector3[] YardGate = Spots(89, 99, -5.5f, 5.5f);

    public override GameMode Mode => GameMode.Survival;
    public override BaseCore? Core => _core;
    public SurvivalTuning Rules => Tuning.Survival;
    public int WaveCount => Rules.Waves.Count;
    public int SpawnedThisMatch { get; private set; }

    private BaseCore _core = null!;
    private int _shownWave = -1;
    private bool _shownBreak;
    private float _spawnTimer;

    public override void _Ready()
    {
        base._Ready();
        _core = BaseCore.Create(Stage, CorePosition, Rules.CoreHp);
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
        var wave = Rules.Waves[index];
        _spawnTimer = wave.Every;
        SpawnGroup(wave, index);
    }

    /// <summary>One group through one doorway. Veterans arrive once per wave, with the first group.</summary>
    public int SpawnGroup(SurvivalWave wave, int index)
    {
        int alive = Stage.AllEnemies.Count(e => !e.IsDead);
        int count = Mathf.Min(wave.Group, Rules.MaxAlive - alive);
        if (count <= 0) return 0;
        var gate = Rng.Randf() < .5f ? StreetGate : YardGate;
        if (PickSpot(gate) is not { } at) return 0;
        var armed = MonsterDatabase.Get("armed_animal");
        for (int i = 0; i < count; i++)
            SpawnHunter(Rng.Randf() * 100 < wave.ArmedPct ? armed : null, at);
        if (_veteransSent < index + 1)
        {
            _veteransSent = index + 1;
            for (int i = 0; i < wave.Veterans; i++) SpawnHunter(MonsterDatabase.Get("armed_veteran"), at);
        }
        SpawnedThisMatch += count;
        return count;
    }
    private int _veteransSent;

    private static Vector3[] Spots(float x0, float x1, float z0, float z1)
    {
        var spots = new List<Vector3>();
        for (float x = x0; x <= x1; x += 1.5f)
            for (float z = z0; z <= z1; z += 1.5f)
                spots.Add(new Vector3(x, 0, z));
        return spots.ToArray();
    }
}
