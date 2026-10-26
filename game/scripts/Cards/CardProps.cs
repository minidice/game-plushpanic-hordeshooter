using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>What a card puts on the field (decisions.md M-29 plan 16.6), sent to clients as a picture only.</summary>
public enum CardPropKind { Button, Train, JackBox }

/// <summary>
/// The things cards put on the field (M-29 plan 16.6), models by Codex (assets/card-props, I-88 axes): a button that
/// flies to a monster (button missile), a wooden toy train that charges through the swarm (toy train) and a
/// jack-in-the-box that gathers monsters, then springs a goose clown and scares them off. Only the deciding PC's
/// copies hurt; a client's copy (<c>by</c> null, <see cref="Spawn"/> from the host's event) is a picture.
/// </summary>
public static class CardProps
{
    /// <summary>Models from content/data/asset_paths.json (T-26).</summary>
    public static string ButtonModel => AssetPaths.File("card_prop.button_missile");
    public static string TrainModel => AssetPaths.File("card_prop.toy_train");
    public static string JackModel => AssetPaths.File("card_prop.jack_box");

    /// <summary>Puts a prop on the field: a button from <paramref name="a"/> to <paramref name="target"/> (or <paramref name="b"/>), a train from <paramref name="a"/> along <paramref name="b"/>, a box at <paramref name="a"/>.</summary>
    public static Node3D Spawn(Stage stage, CardPropKind kind, Vector3 a, Vector3 b, Player? by = null, Enemy? target = null, float damage = 0)
    {
        Node3D prop = kind switch
        {
            CardPropKind.Button => new ButtonMissile { Stage = stage, From = a, To = b, Target = target, By = by, Damage = damage },
            CardPropKind.Train => new ToyTrain { Stage = stage, Start = a, Direction = b, By = by },
            _ => new JackBox { Stage = stage, By = by },
        };
        stage.Effects.AddChild(prop);
        prop.GlobalPosition = a;
        if (stage.Authoritative) stage.Net?.OnCardProp(kind, a, b);
        return prop;
    }

    /// <summary>A Codex model, or a plain stand-in shape while it is missing.</summary>
    public static Node3D Model(string path, Node3D parent, Mesh fallback, Color color)
    {
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path) is { } scene)
        {
            var model = scene.Instantiate<Node3D>();
            parent.AddChild(model);
            return model;
        }
        var box = new MeshInstance3D { Mesh = fallback, MaterialOverride = Mat.Solid(color.ToHtml(false), .7f) };
        parent.AddChild(box);
        return box;
    }

    /// <summary>Living commons (not named or bosses) within <paramref name="radius"/> of <paramref name="at"/>.</summary>
    public static IEnumerable<Enemy> CommonsNear(Stage stage, Vector3 at, float radius) => stage.Enemies.ToList()
        .Where(e => GodotObject.IsInstanceValid(e) && !e.IsDead && !e.IsSpecial && Flat(e.GlobalPosition - at).Length() <= radius);

    public static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}

/// <summary>Button missile: a button flies to its monster in a blink and hits it as its owner's (their bullet cards apply).</summary>
public partial class ButtonMissile : Node3D
{
    public const float FlightSeconds = .22f;
    public Stage Stage { get; set; } = null!;
    public Vector3 From { get; set; }
    public Vector3 To { get; set; }
    public Enemy? Target { get; set; }
    public Player? By { get; set; }
    public float Damage { get; set; }
    private float _time;
    private Node3D _model = null!;

    public override void _Ready()
    {
        _model = CardProps.Model(CardProps.ButtonModel, this, new CylinderMesh { TopRadius = .1f, BottomRadius = .1f, Height = .05f }, new Color("e5484d"));
        Scale = Vector3.One * 1.3f;
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        var to = Target != null && IsInstanceValid(Target) && !Target.IsDead ? Target.GlobalPosition + Vector3.Up : To;
        float t = Mathf.Min(1, _time / FlightSeconds);
        GlobalPosition = From.Lerp(to, t) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * .6f;
        _model.Rotation = new Vector3(_time * 30, _time * 12, 0);
        if (t < 1) return;
        if (By != null && IsInstanceValid(By) && Target != null && IsInstanceValid(Target) && !Target.IsDead)
            Target.TakeHit(new HitInfo(Damage, CardProps.Flat(to - From).Normalized(), to, false, 12, By, LabCards.ButtonWeaponType));
        Stage.Effects.Impact(to, -CardProps.Flat(to - From).Normalized());
        QueueFree();
    }
}

/// <summary>
/// Toy train: chugs from where someone went down along <see cref="Direction"/> (toward whoever downed them), and every
/// monster on its track goes tumbling once. Back on their feet, it passes again the other way.
/// </summary>
public partial class ToyTrain : Node3D
{
    public const float Speed = 11f, Length = 16f, Reach = 1.4f, Power = 150f, HitDamage = 25f;
    public Stage Stage { get; set; } = null!;
    public Vector3 Start { get; set; }
    public Vector3 Direction { get; set; }
    public Player? By { get; set; }
    /// <summary>Checks: monsters it knocked.</summary>
    public int Knocked { get; private set; }
    private float _run, _puff;
    private Node3D _model = null!;
    private readonly List<Node3D> _wheels = new();
    private Node3D? _smoke;
    private readonly HashSet<Enemy> _hit = new();

    public override void _Ready()
    {
        Direction = CardProps.Flat(Direction).LengthSquared() > .001f ? CardProps.Flat(Direction).Normalized() : Vector3.Forward;
        _model = CardProps.Model(CardProps.TrainModel, this, new BoxMesh { Size = new Vector3(.9f, 1f, 2.4f) }, new Color("e5484d"));
        for (int i = 0; i < 8; i++) if (_model.FindChild("Wheel_" + i, true, false) is Node3D wheel) _wheels.Add(wheel);
        _smoke = _model.FindChild("Smoke", true, false) as Node3D;
        // The model's front is −Z (I-88): look along the track.
        LookAt(GlobalPosition + Direction, Vector3.Up);
        Stage.Sfx.PlayAt("honk_ability", GlobalPosition + Vector3.Up, 2, 1.6f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _run += Speed * dt;
        GlobalPosition = Start + Direction * _run;
        foreach (var wheel in _wheels) wheel.RotateX(-Speed * dt / .18f);
        _puff -= dt;
        if (_puff <= 0)
        {
            _puff = .12f;
            Stage.Effects.Puff((_smoke?.GlobalPosition ?? GlobalPosition + Vector3.Up * 1.2f) + Vector3.Up * .2f, .45f, new Color("f3f4f1"));
        }
        if (By != null && IsInstanceValid(By))
            foreach (var e in Stage.Enemies.ToList())
            {
                if (!IsInstanceValid(e) || e.IsDead || _hit.Contains(e)) continue;
                var off = CardProps.Flat(e.GlobalPosition - GlobalPosition);
                if (off.Length() > Reach + e.Radius) continue;
                _hit.Add(e);
                Knocked++;
                var side = Direction.Cross(Vector3.Up) * (off.Dot(Direction.Cross(Vector3.Up)) >= 0 ? 1 : -1);
                e.Shove(Direction + side * .8f, e.IsSpecial ? Power * .5f : Power, e.IsSpecial ? HitDamage * .4f : HitDamage, By);
            }
        if (_run >= Length) QueueFree();
    }
}

/// <summary>
/// Jack-in-the-box: where someone (a sidekick too) went down, a box cranks for 1.5 s pulling commons in, then the lid
/// flies open, a spring goose clown pops out, knocks back everything near and scares the commons off for a while.
/// </summary>
public partial class JackBox : Node3D
{
    public const float GatherSeconds = 1.5f, GatherRadius = 9f, PopRadius = 4.5f, PopPower = 160f, PopDamage = 20f, ScareRadius = 8f, ScareSeconds = 3f;
    public Stage Stage { get; set; } = null!;
    public Player? By { get; set; }
    /// <summary>Checks: whether it has popped, and how many monsters it scared.</summary>
    public bool Popped { get; private set; }
    public int Scared { get; private set; }
    private float _time;
    private Node3D _model = null!;
    private Node3D? _lid, _jack, _crank;

    public override void _Ready()
    {
        _model = CardProps.Model(CardProps.JackModel, this, new BoxMesh { Size = new Vector3(.75f, .8f, .75f) }, new Color("ffd23f"));
        _lid = _model.FindChild("Lid", true, false) as Node3D;
        _jack = _model.FindChild("Jack", true, false) as Node3D;
        _crank = _model.FindChild("Crank", true, false) as Node3D;
        Stage.Sfx.PlayAt("balloon_inflate", GlobalPosition + Vector3.Up, 0, .7f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (!Popped)
        {
            _crank?.RotateX(dt * 9f);
            // A little shake as it winds up.
            _model.Position = new Vector3(Mathf.Sin(_time * 40) * .02f * _time, 0, 0);
            if (By != null)
                foreach (var e in CardProps.CommonsNear(Stage, GlobalPosition, GatherRadius))
                    e.CardDrag(CardProps.Flat(GlobalPosition - e.GlobalPosition).Normalized() * 3.2f, .4f);
            if (_time >= GatherSeconds) Pop();
            return;
        }
        float open = Mathf.Min(1, (_time - GatherSeconds) / .18f);
        if (_lid != null) _lid.Rotation = new Vector3(Mathf.DegToRad(-110) * open, 0, 0);
        if (_jack != null)
        {
            // Springs up past the top, then bobs.
            float bob = Mathf.Sin((_time - GatherSeconds) * 14) * .12f * Mathf.Max(0, 1 - (_time - GatherSeconds));
            _jack.Position = new Vector3(_jack.Position.X, .9f * open + bob, _jack.Position.Z);
        }
        if (_time >= GatherSeconds + 2.2f) QueueFree();
    }

    private void Pop()
    {
        Popped = true;
        _model.Position = Vector3.Zero;
        var at = GlobalPosition;
        Stage.Sfx.PlayAt("honk_ability", at + Vector3.Up, 6, 1.3f);
        Stage.Effects.Puff(at + Vector3.Up * .8f, PopRadius, new Color("ffd23f"));
        Stage.Effects.FloatingText(at + Vector3.Up * 2.6f, Loc.Text("fx_card_jack_box"), new Color("ffd23f"));
        if (By == null) return;
        foreach (var e in Stage.Enemies.ToList())
        {
            if (!IsInstanceValid(e) || e.IsDead) continue;
            var away = CardProps.Flat(e.GlobalPosition - at);
            float d = away.Length();
            if (d <= PopRadius) e.Shove(d > .01f ? away : Vector3.Forward, PopPower * (1 - d / PopRadius * .5f), PopDamage, IsInstanceValid(By) ? By : null);
            if (d <= ScareRadius && !e.IsDead && !e.IsSpecial) { e.Scare(at, ScareSeconds); Scared++; }
        }
    }
}

/// <summary>Small card effects made of Codex sprites (assets/effects/cards): a flame that rises and fades.</summary>
public static class CardFx
{
    private static Texture2D? _flame;
    private static bool _looked;

    public static void Flame(Stage stage, Vector3 at)
    {
        if (!_looked) { _flame = TeamCardView.Fx("fx_flame"); _looked = true; }
        if (_flame == null) return;
        var sprite = new Sprite3D
        {
            Texture = _flame, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = .004f, Modulate = new Color("ff9b4a"),
            Shaded = false, Transparent = true,
        };
        stage.Effects.AddChild(sprite);
        sprite.GlobalPosition = at;
        var tween = sprite.CreateTween().SetParallel();
        tween.TweenProperty(sprite, "position", sprite.Position + Vector3.Up * .7f, .45f);
        tween.TweenProperty(sprite, "modulate", new Color(1, .35f, .1f, 0), .45f);
        tween.Chain().TweenCallback(Callable.From(sprite.QueueFree));
    }
}
