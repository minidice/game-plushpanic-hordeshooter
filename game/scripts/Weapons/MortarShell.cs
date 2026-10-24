using Godot;

namespace HordeShooter;

/// <summary>
/// Indirect fire: the shell arcs over walls and lands exactly on the marked spot after
/// <see cref="WeaponDefinition.FlightTime"/>. The marker on the ground warns everyone (you included).
/// </summary>
public partial class MortarShell : Node3D
{
    private Stage _stage = null!;
    private Vector3 _from, _target;
    private float _time, _flight, _apex, _radius, _damage, _selfShare, _knockback;
    private Player? _by;
    private MeshInstance3D _marker = null!;
    private StandardMaterial3D _markerMat = null!;

    public Vector3 Target => _target;

    /// <param name="by">Who fired it (the blast counts for them, see <see cref="Blast.Explode"/>).</param>
    public static MortarShell Fire(Stage stage, Vector3 from, Vector3 target, WeaponDefinition def, Player? by = null)
    {
        target.Y = stage.Tiers.GroundY(target);
        var shell = new MortarShell
        {
            Name = "MortarShell",
            _stage = stage,
            _from = from,
            _target = target,
            _flight = def.FlightTime,
            _apex = 6f + from.DistanceTo(target) * .25f,
            _radius = def.ExplosionRadius,
            _damage = def.ExplosionDamage,
            _selfShare = def.ExplosionSelfShare,
            _knockback = def.ExplosionKnockback,
            _by = by,
        };
        stage.Effects.AddChild(shell);
        shell.GlobalPosition = from;
        Mat.Mesh(shell, new SphereMesh { Radius = .13f, Height = .34f }, Vector3.Zero, Mat.Solid("4b5236", .5f));
        // Falling whistle rides along with the shell, so you hear where it is coming down.
        stage.Sfx.Attach("mortar_whistle", shell);

        shell._markerMat = Mat.Fade(new Color(1, .3f, .25f, .5f));
        shell._marker = Mat.Mesh(stage.Effects, new TorusMesh { InnerRadius = .92f, OuterRadius = 1f, Rings = 32, RingSegments = 4 },
            target + Vector3.Up * .06f, shell._markerMat);
        shell._marker.Scale = new Vector3(def.ExplosionRadius, 1, def.ExplosionRadius);
        return shell;
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        float t = Mathf.Min(1, _time / _flight);
        var flat = _from.Lerp(_target, t);
        GlobalPosition = flat + Vector3.Up * (_apex * 4 * t * (1 - t));
        _markerMat.AlbedoColor = new Color(1, .3f, .25f, .25f + .5f * t * Mathf.Abs(Mathf.Sin(_time * 14)));
        if (t < 1) return;
        _marker.QueueFree();
        Blast.Explode(_stage, _target, _radius, _damage, new Color("ffa040"), selfShare: _selfShare, knockbackPower: _knockback,
            by: _by != null && IsInstanceValid(_by) ? _by : null, effectSource: "mortar");
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_marker)) _marker.QueueFree();
    }
}
