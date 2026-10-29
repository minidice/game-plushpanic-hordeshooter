using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// The card lab's training log (decisions.md T-22): a log post that never dies, never moves off its spot and never
/// attacks. Hits and shoves show their damage over it and a running DPS (last three seconds), so guns and cards can
/// be compared. Card states still work on it (it puffs up, gets sticky, floats); a pop or a burst cannot kill it.
/// </summary>
public partial class TrainingDummy : Enemy
{
    private const float DpsWindow = 3f, NumberEvery = .15f;

    private Vector3 _home;
    private Label3D _dps = null!;
    private Node3D _log = null!;
    private readonly Queue<(float At, float Damage)> _recent = new();
    private float _clock, _pending, _pendingTimer, _wobble;
    private bool _pendingCrit;

    public override bool Lurable => false;
    public float TotalDamage { get; private set; }
    public float Dps => _recent.Sum(r => r.Damage) / DpsWindow;

    public override void _Ready()
    {
        base._Ready();
        _home = GlobalPosition;
        Rig.Model.Visible = false;
        _log = new Node3D { Name = "Log" };
        Rig.AddChild(_log);
        var bark = Mat.Solid("8b5a2b");
        Mat.Mesh(_log, new CylinderMesh { TopRadius = .36f, BottomRadius = .4f, Height = 1.5f, RadialSegments = 14 }, Vector3.Up * .75f, bark);
        Mat.Mesh(_log, new CylinderMesh { TopRadius = .3f, BottomRadius = .3f, Height = .02f, RadialSegments = 14 }, Vector3.Up * 1.51f, Mat.Solid("d9b27c"));
        // A painted target on the front so it reads as a practice post.
        Mat.Mesh(_log, new CylinderMesh { TopRadius = .2f, BottomRadius = .2f, Height = .02f, RadialSegments = 16 }, new Vector3(0, .95f, .37f), Mat.Solid("e8e1d0"))
            .Rotation = new Vector3(Mathf.Pi * .5f, 0, 0);
        Mat.Mesh(_log, new CylinderMesh { TopRadius = .09f, BottomRadius = .09f, Height = .03f, RadialSegments = 12 }, new Vector3(0, .95f, .38f), Mat.Solid("d94040"))
            .Rotation = new Vector3(Mathf.Pi * .5f, 0, 0);
        _dps = new Label3D
        {
            Font = UiFonts.Get(),
            Position = Vector3.Up * 2.2f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48, OutlineSize = 10, PixelSize = .01f, NoDepthTest = true,
            Modulate = new Color("ffe066"),
        };
        AddChild(_dps);
    }

    protected override Vector3 Think(float dt) => Vector3.Zero;

    /// <summary>Back on its spot after every push (it may still float up when puffed).</summary>
    protected override void AfterMove(float dt)
    {
        GlobalPosition = new Vector3(_home.X, GlobalPosition.Y, _home.Z);
        _wobble = Mathf.MoveToward(_wobble, 0, dt * 4);
        _log.Rotation = new Vector3(Mathf.Sin(_clock * 30) * _wobble * .25f, 0, Mathf.Cos(_clock * 26) * _wobble * .25f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        while (_recent.Count > 0 && _recent.Peek().At < _clock - DpsWindow) _recent.Dequeue();
        if (_pending > 0 && (_pendingTimer -= dt) <= 0) FlushNumber();
        Health = Monster.Hp;
        base._PhysicsProcess(delta);
        _dps.Text = Loc.Format("fmt_cardlab_dps", Mathf.RoundToInt(Dps));
    }

    public override void TakeHit(HitInfo hit)
    {
        if (IsDead) return;
        Record(hit.Damage, hit.Crit);
        Health = Monster.Hp;
        _wobble = 1;
        Stage.Effects.Stuffing(hit.Point, -hit.Direction, new Color("c49a6c"), 4);
        Stage.Sfx.PlayAt("pan_bong", hit.Point, -8, 1.6f);
        Stage.RaiseEnemyHit(this, hit);
        Health = Monster.Hp;
    }

    public override void Shove(Vector3 direction, float power, float damage, Player? by = null)
    {
        if (damage > 0) Record(damage, false);
        _wobble = 1;
        Health = Monster.Hp;
    }

    /// <summary>A log does not die: a lethal pop or burst just leaves it standing.</summary>
    public override void Die(Vector3 direction, float force)
    {
        Health = Monster.Hp;
        Deflate();
    }

    private void Record(float damage, bool crit)
    {
        if (damage <= 0) return;
        TotalDamage += damage;
        _recent.Enqueue((_clock, damage));
        if (_pending <= 0) _pendingTimer = NumberEvery;
        _pending += damage;
        _pendingCrit |= crit;
    }

    /// <summary>Damage within a short moment shows as one number (a shotgun blast is one number, not eight).</summary>
    private void FlushNumber()
    {
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 1.8f + new Vector3(GD.Randf() - .5f, 0, GD.Randf() - .5f) * .6f,
            Mathf.RoundToInt(_pending).ToString(), _pendingCrit ? new Color("ffcf5a") : new Color("f3f4f1"));
        _pending = 0;
        _pendingCrit = false;
    }
}
