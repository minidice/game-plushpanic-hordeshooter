using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// What you see while aiming a throwable: a faint ring for the max throw distance around you, the arc
/// the item will fly, and a ring on the landing spot sized to its effect radius.
/// </summary>
public partial class ThrowPreview : Node3D
{
    private const int ArcDots = 14;

    private GroundMarker _landing = null!, _reach = null!;
    private readonly List<MeshInstance3D> _dots = new();
    private float _time;

    public Vector3 Target { get; private set; }

    public static ThrowPreview Create(Node parent)
    {
        var preview = new ThrowPreview { Name = "ThrowPreview" };
        parent.AddChild(preview);
        var dotMat = Mat.Fade(new Color(.86f, .96f, 1, .82f));
        for (int i = 0; i < ArcDots; i++)
            preview._dots.Add(Mat.Mesh(preview, new SphereMesh { Radius = .045f, Height = .09f, RadialSegments = 10, Rings = 5 }, Vector3.Zero, dotMat));
        preview._landing = GroundMarker.Circle(preview, Vector3.Zero, 1, KindColor(ThrowableKind.Frag));
        preview._landing.Name = "Landing";
        preview._reach = GroundMarker.Circle(preview, Vector3.Zero, 1, new Color(.6f, .83f, 1), 0, .28f, .025f);
        preview._reach.Name = "Reach";
        return preview;
    }

    /// <param name="tiers">Height tiers (L-15): the ring sits on the tier it lands on. Null or flat: on the ground plane.</param>
    public void Show(ThrowableDefinition def, Vector3 from, Vector3 aim, Vector3 playerFeet, float dt, TierMap? tiers = null)
    {
        _time += dt;
        Target = ThrownItem.ClampTarget(from, aim, def.MaxRange);
        if (tiers is { Any: true }) Target = Target with { Y = tiers.GroundY(Target) + .2f };
        float radius = Mathf.Max(.6f, def.AimRadius);
        _landing.GlobalPosition = new Vector3(Target.X, Target.Y - .2f + .06f, Target.Z);
        _landing.SetRadius(radius);
        var color = KindColor(def.Kind);
        _landing.SetTint(color);
        _landing.SetOpacity(.86f + .08f * Mathf.Sin(_time * 4));
        _reach.GlobalPosition = new Vector3(playerFeet.X, tiers is { Any: true } ? playerFeet.Y + .05f : .05f, playerFeet.Z);
        _reach.SetRadius(def.MaxRange);

        float flight = ThrownItem.FlightTime(from, Target);
        for (int i = 0; i < _dots.Count; i++)
        {
            float t = flight * (i + 1) / (_dots.Count + 1);
            _dots[i].GlobalPosition = ThrownItem.ArcPoint(from, Target, t);
        }
    }

    /// <summary>A lobbed weapon in hand (mortar, decisions.md C-25): same look, sized to its blast and range.</summary>
    public void ShowLob(WeaponDefinition def, Vector3 from, Vector3 target, Vector3 playerFeet, float dt, TierMap? tiers = null)
    {
        _time += dt;
        Target = target;
        float radius = Mathf.Max(.6f, def.ExplosionRadius);
        _landing.GlobalPosition = new Vector3(target.X, target.Y + .06f, target.Z);
        _landing.SetRadius(radius);
        _landing.SetTint(KindColor(ThrowableKind.Frag));
        _landing.SetOpacity(.86f + .08f * Mathf.Sin(_time * 4));
        _reach.GlobalPosition = new Vector3(playerFeet.X, tiers is { Any: true } ? playerFeet.Y + .05f : .05f, playerFeet.Z);
        _reach.SetRadius(def.MaxRange);
        float flight = ThrownItem.FlightTime(from, target);
        for (int i = 0; i < _dots.Count; i++)
            _dots[i].GlobalPosition = ThrownItem.ArcPoint(from, target, flight * (i + 1) / (_dots.Count + 1));
    }

    public static Color KindColor(ThrowableKind kind) => kind switch
    {
        ThrowableKind.Fire => new Color("ff7a2a"),
        ThrowableKind.Flash => new Color("f0f4ff"),
        ThrowableKind.Lure => new Color("ff7ab8"),
        _ => new Color("ffb347"),
    };
}
