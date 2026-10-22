using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Rules of a mode other than Scenario on the stage's <see cref="StageMap"/> (doc/menu-multiplayer-plan.md 3.1):
/// who comes when, how the match ends and the HUD's top bar. Where the base and the doorways are is the map's.
/// The offline game or the host decides; a client only shows the clock-driven text and the bar it is sent.
/// </summary>
public abstract partial class ModeRules : Node
{
    public static ModeRules? Create(GameMode mode) => mode switch
    {
        GameMode.Survival => new SurvivalRules { Name = "SurvivalRules" },
        GameMode.Survivor => new SurvivorRules { Name = "SurvivorRules" },
        _ => null,
    };

    protected Stage Stage => GetParent<Stage>();
    protected ModeTuning Tuning => ModeTuning.Current;
    /// <summary>Which monsters this mode sends when (content/data/modes/&lt;mode&gt;_spawns.csv, decisions.md M-24).</summary>
    public ModeSpawnTable Table => ModeSpawnTable.For(Mode);
    protected readonly RandomNumberGenerator Rng = new();
    /// <summary>Seconds of play so far (the stage clock, which stops when the match ends).</summary>
    protected float Time => Stage.Elapsed;

    public abstract GameMode Mode { get; }
    /// <summary>The stage objective while this mode runs (<see cref="Objective.ModeGoal"/>, or arming first).</summary>
    public virtual Objective InitialObjective => Objective.ModeGoal;
    public abstract string ObjectiveText { get; }
    /// <summary>Text over the top bar; empty hides the bar.</summary>
    public virtual string BarLabel => "";
    /// <summary>0–1 fill of the top bar. A client gets it from the host's snapshot.</summary>
    public float BarFraction { get; protected set; }
    public virtual Color BarColor => UiTheme.Accent;
    /// <summary>A thin second bar under the top bar (Onslaught's experience); empty hides it.</summary>
    public virtual string SubBarLabel => "";
    public virtual float SubBarFraction => 0;
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

    /// <summary>The deciding PC: a mode may take a floor weapon its own way (Onslaught's racks). True = handled.</summary>
    public virtual bool TakePickup(WeaponPickup pickup, Player who) => false;

    /// <summary>Client: the host's bar value from the snapshot.</summary>
    public virtual void ApplyNetBar(float fraction) => BarFraction = fraction;

    /// <summary>Host: a client picked card <paramref name="index"/> of its offer <paramref name="serial"/> (Onslaught, decisions.md M-27).</summary>
    public virtual void PickCard(Player who, int serial, int index) { }

    /// <summary>Client: the host's copy of this survivor's cards and the mode's card state.</summary>
    public virtual void ApplyCardSync(byte[] data) { }
    /// <summary>Client: the host's special card vote (M-29).</summary>
    public virtual void ApplyVoteSync(byte[] data) { }
    /// <summary>Host: a client rerolls card <paramref name="index"/> of its front offer (M-29 plan 16.4).</summary>
    public virtual void Reroll(Player who, int serial, int index) { }
    /// <summary>Host: a client's stamp on the special card vote.</summary>
    public virtual void CastVote(Player who, int serial, int index) { }

    /// <summary>A random open, reachable spot, off every screen if there is one.</summary>
    protected Vector3? PickSpot(IEnumerable<Vector3> candidates)
    {
        var open = candidates.Where(p => !Stage.Flow.IsBlocked(p) && Stage.Flow.Reachable(p)).ToList();
        if (open.Count == 0) return null;
        var hidden = open.Where(p => !Stage.Director.OnScreen(p)).ToList();
        var pool = hidden.Count > 0 ? hidden : open;
        return pool[Rng.RandiRange(0, pool.Count - 1)];
    }

    /// <summary>A spot a step from <paramref name="at"/> so a group does not stack on one point.</summary>
    protected Vector3 Near(Vector3 at)
    {
        var offset = new Vector3(Rng.RandfRange(-1.4f, 1.4f), 0, Rng.RandfRange(-1.4f, 1.4f));
        return Stage.Flow.IsBlocked(at + offset) ? at : at + offset;
    }

    protected static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0, seconds));
        return $"{s / 60}:{s % 60:00}";
    }
}
