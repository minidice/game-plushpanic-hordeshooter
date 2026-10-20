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

    private MeshInstance3D _landing = null!, _reach = null!;
    private StandardMaterial3D _landingMat = null!;
    private readonly List<MeshInstance3D> _dots = new();
    private float _time;

    public Vector3 Target { get; private set; }

    public static ThrowPreview Create(Node parent)
    {
        var preview = new ThrowPreview { Name = "ThrowPreview" };
        parent.AddChild(preview);
        var dotMat = Mat.Fade(new Color(1, 1, 1, .7f));
        for (int i = 0; i < ArcDots; i++)
            preview._dots.Add(Mat.Mesh(preview, new SphereMesh { Radius = .06f, Height = .12f, RadialSegments = 6, Rings = 3 }, Vector3.Zero, dotMat));
        preview._landingMat = Mat.Fade(new Color(1, .7f, .3f, .7f));
        preview._landing = Mat.Mesh(preview, new TorusMesh { InnerRadius = .94f, OuterRadius = 1f, Rings = 40, RingSegments = 4 }, Vector3.Zero, preview._landingMat);
        preview._reach = Mat.Mesh(preview, new TorusMesh { InnerRadius = .985f, OuterRadius = 1f, Rings = 64, RingSegments = 3 }, Vector3.Zero, Mat.Fade(new Color(1, 1, 1, .18f)));
        return preview;
    }

    public void Show(ThrowableDefinition def, Vector3 from, Vector3 aim, Vector3 playerFeet, float dt)
    {
        _time += dt;
        Target = ThrownItem.ClampTarget(from, aim, def.MaxRange);
        float radius = Mathf.Max(.6f, def.AimRadius);
        _landing.GlobalPosition = new Vector3(Target.X, .06f, Target.Z);
        _landing.Scale = new Vector3(radius, 1, radius) * (1 + .03f * Mathf.Sin(_time * 8));
        var color = KindColor(def.Kind);
        _landingMat.AlbedoColor = new Color(color, .55f + .2f * Mathf.Sin(_time * 8));
        _reach.GlobalPosition = new Vector3(playerFeet.X, .05f, playerFeet.Z);
        _reach.Scale = new Vector3(def.MaxRange, 1, def.MaxRange);

        float flight = ThrownItem.FlightTime(from, Target);
        for (int i = 0; i < _dots.Count; i++)
        {
            float t = flight * (i + 1) / (_dots.Count + 1);
            _dots[i].GlobalPosition = ThrownItem.ArcPoint(from, Target, t);
        }
    }

    /// <summary>A lobbed weapon in hand (mortar, decisions.md C-25): same look, sized to its blast and range.</summary>
    public void ShowLob(WeaponDefinition def, Vector3 from, Vector3 target, Vector3 playerFeet, float dt)
    {
        _time += dt;
        Target = target;
        float radius = Mathf.Max(.6f, def.ExplosionRadius);
        _landing.GlobalPosition = new Vector3(target.X, .06f, target.Z);
        _landing.Scale = new Vector3(radius, 1, radius) * (1 + .03f * Mathf.Sin(_time * 8));
        _landingMat.AlbedoColor = new Color(KindColor(ThrowableKind.Frag), .55f + .2f * Mathf.Sin(_time * 8));
        _reach.GlobalPosition = new Vector3(playerFeet.X, .05f, playerFeet.Z);
        _reach.Scale = new Vector3(def.MaxRange, 1, def.MaxRange);
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
