using Godot;

namespace HordeShooter;

/// <summary>
/// How a map is seen (decisions.md L-2, L-14): angles, the default and Tab-close view heights (metres of ground across the
/// screen height), and orthographic or perspective with its field of view.
/// </summary>
public readonly record struct CameraView(float Pitch, float Yaw, float WideSize, float CloseSize, bool Perspective, float Fov)
{
    /// <summary>
    /// Every map's view since 2026-10-27 (L-2, the user's reference shot): looking well down at 60° with a light perspective,
    /// wide enough to see around the survivor. The yaw stays −30° so the +X route still runs to the screen's upper right (L-1).
    /// </summary>
    public static readonly CameraView Default = new(CameraRig.Pitch, CameraRig.Yaw, CameraRig.WideSize, CameraRig.CloseSize, true, CameraRig.DefaultFov);
}

/// <summary>Quarter-view camera that follows the player, leans toward the aim, and shakes. Ortho or perspective per map.</summary>
public partial class CameraRig : Camera3D
{
    public const float Pitch = 60f;
    // Stage 1 runs along +X. Negative yaw projects that route toward screen upper-right.
    public const float Yaw = -30f;
    public const float Distance = 34f;
    public const float WideSize = 21f;
    public const float CloseSize = 12.5f;
    /// <summary>Field of view of <see cref="CameraView.Default"/>: enough depth to read heights, little edge stretch.</summary>
    public const float DefaultFov = 34f;
    /// <summary>An orthographic view eases into a perspective one from this narrow field of view (nearly flat).</summary>
    private const float FlatFov = 16f;

    public Node3D? Target { get; set; }
    public Player? Player { get; set; }
    public bool Close { get; set; }
    /// <summary>The map's view (set before the rig enters the tree; <see cref="Arrive"/> eases into it).</summary>
    public CameraView View { get; set; } = CameraView.Default;
    /// <summary>Depth fog that follows the camera's distance, so only the far ground is hazed (outdoor maps).</summary>
    public Godot.Environment? Haze { get; set; }
    /// <summary>The map's camera bounds (x, z): the focus stays inside, so the view never runs off the dressed ground.</summary>
    public Rect2? Bounds { get; set; }
    /// <summary>Metres from the focus to the camera now.</summary>
    public float FocusDistance { get; private set; } = Distance;
    /// <summary>Angles now (they ease from the last stage's view after a stage change).</summary>
    public float CurrentPitch => _pitch;
    /// <summary>Metres of ground across the screen height now (the orthographic size, or the perspective equivalent at the focus).</summary>
    public float ViewSize => View.Perspective ? _size : Size;
    public bool Arriving => _blend < 1;

    private Vector3 _focus;
    private float _trauma;
    private readonly RandomNumberGenerator _rng = new();
    private float _pitch = Pitch, _yaw = Yaw, _fov = FlatFov, _size = WideSize;
    private CameraView _from;
    private float _blend = 1, _blendSeconds = 1;

    public override void _Ready()
    {
        Projection = View.Perspective ? ProjectionType.Perspective : ProjectionType.Orthogonal;
        Far = 120;
        Current = true;
        if (_blend >= 1) Settle(View);
        if (Target != null) _focus = Target.GlobalPosition;
        Place(_focus, Vector3.Zero);
    }

    /// <summary>
    /// Start in <paramref name="from"/> (the stage just left) and ease into <see cref="View"/> over
    /// <paramref name="seconds"/>: the reference clip's view change from the steep indoor shot to the lower outdoor one.
    /// </summary>
    public void Arrive(CameraView from, float seconds)
    {
        _from = from;
        _blendSeconds = Mathf.Max(.01f, seconds);
        _blend = 0;
        _pitch = from.Pitch;
        _yaw = from.Yaw;
        _fov = from.Perspective ? from.Fov : FlatFov;
        _size = from.CloseSize;
        if (!View.Perspective) Size = _size;
    }

    private void Settle(CameraView view)
    {
        _pitch = view.Pitch;
        _yaw = view.Yaw;
        _fov = view.Perspective ? view.Fov : FlatFov;
        _size = view.WideSize;
        if (!view.Perspective) Size = view.WideSize;
    }

    public void AddShake(float amount) => _trauma = Mathf.Min(1, _trauma + amount);

    public void SnapTo(Vector3 focus)
    {
        _focus = focus;
        Place(_focus, Vector3.Zero);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector3 goal = _focus;
        if (Player is { IsDead: true, Corpse: not null }) goal = Player.Corpse.GlobalPosition;
        else if (Target != null)
        {
            goal = Target.GlobalPosition + Vector3.Up * .8f;
            if (Player is { IsDead: false })
            {
                var lead = Player.AimPoint - Player.GlobalPosition;
                lead.Y = 0;
                // Aiming looks further toward the cursor, like a scope (decisions.md C-26): as far as the gun's aim_view
                // (rifle 1, shotgun 0); beyond 1 the zoom-out below makes room so the survivor stays on screen.
                float aim = Player.AimBlend * Player.Gun.Def.AimView;
                float lean = Mathf.Min(aim, 1);
                goal += lead.LimitLength((14f + 12f * lean) * Mathf.Max(1, aim)) * (.22f + .26f * lean);
            }
        }
        if (Bounds is { } b) goal = new Vector3(Mathf.Clamp(goal.X, b.Position.X, b.End.X), goal.Y, Mathf.Clamp(goal.Z, b.Position.Y, b.End.Y));
        _focus = _focus.Lerp(goal, 1 - Mathf.Exp(-dt * 5f));
        float wanted = (Close ? View.CloseSize : View.WideSize) * AimZoom;
        if (_blend < 1)
        {
            // Arrival: angles, field of view and zoom ease together from the last stage's view (smoothstep).
            _blend = Mathf.Min(1, _blend + dt / _blendSeconds);
            float t = _blend * _blend * (3 - 2 * _blend);
            _pitch = Mathf.Lerp(_from.Pitch, View.Pitch, t);
            _yaw = Mathf.Lerp(_from.Yaw, View.Yaw, t);
            _fov = Mathf.Lerp(_from.Perspective ? _from.Fov : FlatFov, View.Perspective ? View.Fov : FlatFov, t);
            _size = Mathf.Lerp(_from.CloseSize, wanted, t);
            if (!View.Perspective) Size = _size;
        }
        else if (View.Perspective) _size = Mathf.Lerp(_size, wanted, 1 - Mathf.Exp(-dt * 8f));
        else Size = Mathf.Lerp(Size, wanted, 1 - Mathf.Exp(-dt * 8f));

        _trauma = Mathf.Max(0, _trauma - dt * 1.6f);
        float shake = _trauma * _trauma;
        var offset = new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1), 0) * shake * .6f;
        Place(_focus, offset);
    }

    /// <summary>Zoom-out while aiming a gun whose aim_view is above 1 (a sniper sees about 1.5× further than a rifle).</summary>
    public float AimZoom => Player is { IsDead: false } p ? 1 + Mathf.Max(0, p.Gun.Def.AimView - 1) * .8f * p.AimBlend : 1;

    private void Place(Vector3 focus, Vector3 shake)
    {
        float a = Mathf.DegToRad(_pitch);
        float yaw = Mathf.DegToRad(_yaw);
        if (View.Perspective)
        {
            // The same metres of ground across the screen height as an orthographic view of that size.
            Fov = _fov;
            FocusDistance = _size * .5f / Mathf.Tan(Mathf.DegToRad(_fov) * .5f);
        }
        else FocusDistance = Distance;
        Position = focus + new Vector3(Mathf.Sin(yaw)*Mathf.Cos(a), Mathf.Sin(a), Mathf.Cos(yaw)*Mathf.Cos(a))*FocusDistance;
        LookAt(focus, Vector3.Up);
        Position += GlobalTransform.Basis * shake;
        if (Haze != null)
        {
            Haze.FogDepthBegin = FocusDistance + 6f;
            Haze.FogDepthEnd = FocusDistance + 46f;
        }
    }

    /// <summary>Screen-relative keyboard directions on the ground, independent of camera pitch.</summary>
    public Vector3 GroundMovement(Vector2 input)
    {
        var right=GlobalBasis.X;right.Y=0;
        var down=GlobalBasis.Z;down.Y=0;
        return (right.Normalized()*input.X+down.Normalized()*input.Y).LimitLength(1);
    }
}
