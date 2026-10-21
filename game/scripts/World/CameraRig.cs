using Godot;

namespace HordeShooter;

/// <summary>55° orthographic quarter-view camera that follows the player, leans toward the aim, and shakes.</summary>
public partial class CameraRig : Camera3D
{
    public const float Pitch = 55f;
    // Stage 1 runs along +X. Negative yaw projects that route toward screen upper-right.
    public const float Yaw = -30f;
    public const float Distance = 34f;
    public const float WideSize = 17f;
    public const float CloseSize = 10f;

    public Node3D? Target { get; set; }
    public Player? Player { get; set; }
    public bool Close { get; set; }

    private Vector3 _focus;
    private float _trauma;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = WideSize;
        Far = 120;
        Current = true;
        if (Target != null) _focus = Target.GlobalPosition;
        Place(_focus, Vector3.Zero);
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
        _focus = _focus.Lerp(goal, 1 - Mathf.Exp(-dt * 5f));
        Size = Mathf.Lerp(Size, (Close ? CloseSize : WideSize) * AimZoom, 1 - Mathf.Exp(-dt * 8f));

        _trauma = Mathf.Max(0, _trauma - dt * 1.6f);
        float shake = _trauma * _trauma;
        var offset = new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1), 0) * shake * .6f;
        Place(_focus, offset);
    }

    /// <summary>Zoom-out while aiming a gun whose aim_view is above 1 (a sniper sees about 1.5× further than a rifle).</summary>
    public float AimZoom => Player is { IsDead: false } p ? 1 + Mathf.Max(0, p.Gun.Def.AimView - 1) * .8f * p.AimBlend : 1;

    private void Place(Vector3 focus, Vector3 shake)
    {
        float a = Mathf.DegToRad(Pitch);
        float yaw = Mathf.DegToRad(Yaw);
        Position = focus + new Vector3(Mathf.Sin(yaw)*Mathf.Cos(a), Mathf.Sin(a), Mathf.Cos(yaw)*Mathf.Cos(a))*Distance;
        LookAt(focus, Vector3.Up);
        Position += GlobalTransform.Basis * shake;
    }

    /// <summary>Screen-relative keyboard directions on the ground, independent of camera pitch.</summary>
    public Vector3 GroundMovement(Vector2 input)
    {
        var right=GlobalBasis.X;right.Y=0;
        var down=GlobalBasis.Z;down.Y=0;
        return (right.Normalized()*input.X+down.Normalized()*input.Y).LimitLength(1);
    }
}
