using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Rules of a mode other than Scenario, played on the Stage 1 map from the warehouse hall
/// (doc/menu-multiplayer-plan.md 3.1): who comes when, how the match ends and the HUD's top bar.
/// The offline game or the host decides; a client only shows the clock-driven text and the bar it is sent.
/// </summary>
public abstract partial class ModeRules : Node
{
    /// <summary>Both modes start in the middle of the warehouse, between its two doorways.</summary>
    public static readonly Vector3 Start = new(66, 0, 1.6f);

    public static ModeRules? Create(GameMode mode) => mode switch
    {
        GameMode.Survival => new SurvivalRules { Name = "SurvivalRules" },
        GameMode.Survivor => new SurvivorRules { Name = "SurvivorRules" },
        _ => null,
    };

    protected Stage Stage => GetParent<Stage>();
    protected ModeTuning Tuning => ModeTuning.Current;
    protected readonly RandomNumberGenerator Rng = new();
    /// <summary>Seconds of play so far (the stage clock, which stops when the match ends).</summary>
    protected float Time => Stage.Elapsed;

    public abstract GameMode Mode { get; }
    public abstract string ObjectiveText { get; }
    /// <summary>Text over the top bar; empty hides the bar.</summary>
    public virtual string BarLabel => "";
    /// <summary>0–1 fill of the top bar. A client gets it from the host's snapshot.</summary>
    public float BarFraction { get; protected set; }
    public virtual Color BarColor => UiTheme.Accent;
    /// <summary>One line for the end screen.</summary>
    public virtual string ResultText => "";
    /// <summary>The thing monsters attack besides the survivors (Survival's base).</summary>
    public virtual BaseCore? Core => null;

    public override void _Ready() => Rng.Randomize();

    public override void _PhysicsProcess(double delta)
    {
        if (Stage.State != StageState.Playing) return;
        float dt = (float)delta;
        Tick(dt);
        if (Stage.Authoritative) Step(dt);
    }

    /// <summary>Every PC: notices and anything that follows the clock.</summary>
    protected virtual void Tick(float dt) { }

    /// <summary>The deciding PC only: spawning and the end of the match.</summary>
    protected abstract void Step(float dt);

    /// <summary>Client: the host's bar value from the snapshot.</summary>
    public virtual void ApplyNetBar(float fraction) => BarFraction = fraction;

    /// <summary>A random open, reachable spot, off every screen if there is one.</summary>
    protected Vector3? PickSpot(IEnumerable<Vector3> candidates)
    {
        var open = candidates.Where(p => !Stage.Flow.IsBlocked(p) && Stage.Flow.Reachable(p)).ToList();
        if (open.Count == 0) return null;
        var hidden = open.Where(p => !Stage.Director.OnScreen(p)).ToList();
        var pool = hidden.Count > 0 ? hidden : open;
        return pool[Rng.RandiRange(0, pool.Count - 1)];
    }

    /// <summary>A hunter (awake from the start) a step from <paramref name="at"/>.</summary>
    protected Enemy SpawnHunter(MonsterDefinition? monster, Vector3 at)
    {
        var offset = new Vector3(Rng.RandfRange(-1.4f, 1.4f), 0, Rng.RandfRange(-1.4f, 1.4f));
        var spot = Stage.Flow.IsBlocked(at + offset) ? at : at + offset;
        return monster == null ? Stage.SpawnCommon(spot, awake: true) : Stage.Spawn(monster, spot, awake: true);
    }

    protected static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0, seconds));
        return $"{s / 60}:{s % 60:00}";
    }
}
