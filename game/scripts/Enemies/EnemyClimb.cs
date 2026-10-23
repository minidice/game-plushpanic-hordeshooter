using Godot;

namespace HordeShooter;

/// <summary>
/// Getting up (or down a ladder of) a height tier the way this monster climbs (decisions.md L-16, doc/elevation-tiers.md
/// 4 · 5.4장): wall climbers crawl up a climbable face, ladder users take the ladder, jumpers leap, flyers rise over.
/// The move is scripted from the cliff foot to the floor above; a hard hit knocks a wall climber off.
/// </summary>
public partial class Enemy
{
    /// <summary>Damage taken while clinging to a wall (elevation-tiers.md 5.4, `[임시]`).</summary>
    public const float ClimbDamageScale = 1.5f;
    /// <summary>Seconds per tier: wall 1.5, ladder up 1 / down 0.4, flying 0.6; a jump is quick (`[임시]`).</summary>
    public const float WallClimbSeconds = 1.5f, LadderUpSeconds = 1f, LadderDownSeconds = .4f, FlySeconds = .6f, JumpSeconds = .45f;

    private bool _climbing, _climbDown;
    private MonsterClimb _climbKind;
    private Vector3 _climbFrom, _climbTo;
    private float _climbT, _climbDuration;

    /// <summary>In the middle of a climb (scripted, no walking or attacking).</summary>
    public bool Climbing => _climbing;
    /// <summary>Clinging to a wall: takes more damage and falls off when knocked.</summary>
    public bool ClimbingWall => _climbing && _climbKind == MonsterClimb.Wall;
    /// <summary>How many climbs this monster has started (checks).</summary>
    public int ClimbsStarted { get; private set; }

    /// <summary>Starts a climb when the walk heads into a neighbouring cell up a tier (or down a ladder) its climb allows.</summary>
    private bool TryStartClimb(Vector3 desired)
    {
        var tiers = Stage.Tiers;
        if (!tiers.Any || IsMeleeAttacking) return false;
        var flat = new Vector3(desired.X, 0, desired.Z);
        if (flat.LengthSquared() < .01f) return false;
        var dir = flat.Normalized();
        int here = tiers.Index(GlobalPosition);
        int ahead = tiers.Index(GlobalPosition + dir * (Radius + .45f));
        if (here < 0 || ahead < 0 || here == ahead || tiers.CanStep(here, ahead)) return false;
        if (tiers.StepCost(here, ahead, Monster.Climb) < 0) return false;
        int rise = tiers.TierOfCell(ahead) - tiers.TierOfCell(here);
        // Going down: everyone but a ladder user just walks off the edge and drops.
        if (rise < 0 && Monster.Climb != MonsterClimb.Ladder) return false;
        if (rise == 0) return false;
        var land = GlobalPosition + dir * (Radius + .9f);
        if (tiers.Index(land) != ahead) land = tiers.CellFloor(ahead);
        land.Y = tiers.CellFloor(ahead).Y + .05f;
        int count = Mathf.Abs(rise);
        _climbKind = Monster.Climb;
        _climbDown = rise < 0;
        _climbDuration = _climbKind switch
        {
            MonsterClimb.Ladder => count * (_climbDown ? LadderDownSeconds : LadderUpSeconds),
            MonsterClimb.Jump => JumpSeconds + .1f * count,
            MonsterClimb.Fly => count * FlySeconds,
            _ => count * WallClimbSeconds,
        };
        _climbFrom = GlobalPosition;
        _climbTo = land;
        _climbT = 0;
        _climbing = true;
        ClimbsStarted++;
        CancelMelee();
        Velocity = Vector3.Zero;
        FaceOverride = dir;
        return true;
    }

    private void ClimbStep(float dt)
    {
        // A hard hit knocks a wall climber off; it falls from where it hangs.
        if (ClimbingWall && Knock.Length() > 3f)
        {
            _climbing = false;
            Velocity = new Vector3(Knock.X, 0, Knock.Z);
            return;
        }
        _climbT += dt / Mathf.Max(.05f, _climbDuration);
        float t = Mathf.Clamp(_climbT, 0, 1);
        Vector3 p;
        if (_climbKind == MonsterClimb.Jump)
        {
            // Up first, over second, with a hop on top: clears the edge before crossing it.
            float up = Mathf.Min(1, t / .55f);
            var xz = _climbFrom.Lerp(_climbTo, t);
            p = new Vector3(xz.X, Mathf.Lerp(_climbFrom.Y, _climbTo.Y, up) + 4 * .5f * t * (1 - t), xz.Z);
        }
        else if (_climbDown)
        {
            // Down a ladder: over the rim, then a quick slide.
            float over = Mathf.Min(1, t / .2f), slide = Mathf.Clamp((t - .2f) / .8f, 0, 1);
            var rim = _climbFrom.Lerp(_climbTo, .35f);
            var xz = over < 1 ? _climbFrom.Lerp(rim, over) : rim.Lerp(_climbTo, slide);
            p = new Vector3(xz.X, Mathf.Lerp(_climbFrom.Y, _climbTo.Y, slide * slide), xz.Z);
        }
        else
        {
            // Wall, ladder or flight: rise against the face, then step over onto the floor above.
            const float rise = .8f;
            float y = t < rise ? Mathf.Lerp(_climbFrom.Y, _climbTo.Y + .1f, t / rise) : _climbTo.Y + .1f;
            var xz = t < rise ? _climbFrom : _climbFrom.Lerp(_climbTo, (t - rise) / (1 - rise));
            p = new Vector3(xz.X, y, xz.Z);
        }
        GlobalPosition = p;
        Velocity = Vector3.Zero;
        var face = _climbTo - _climbFrom;
        face.Y = 0;
        if (face.LengthSquared() > .001f) Rig.Face(face);
        Rig.Animate(dt, .6f, Kick, Hurt, Vector3.Zero);
        if (t >= 1) _climbing = false;
    }
}
