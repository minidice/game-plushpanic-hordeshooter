using Godot;

namespace HordeShooter;

/// <summary>
/// Survivors on a ladder (decisions.md L-16 · L-18, doc/elevation-tiers.md 5.1장): up at a steady pace, down fast with
/// a jump off near the bottom. No shooting or using things on the way, damage still lands but nothing knocks them off,
/// and survivors on the same ladder pass through each other. Arriving at the top shoves whoever stands at the exit.
/// </summary>
public partial class Player
{
    /// <summary>Seconds per tier (`[임시]`).</summary>
    public const float LadderUpSeconds = 1f, LadderDownSeconds = .4f;

    private Ladder? _ladder;
    private bool _ladderUp;
    private float _ladderT, _ladderTime;
    private Vector3 _ladderFrom;
    private uint _maskBeforeLadder;

    public bool OnLadder => _ladder != null;
    public Ladder? CurrentLadder => _ladder;

    /// <summary>Gets on <paramref name="ladder"/> at the bottom (<paramref name="up"/>) or the top. False if it cannot now.</summary>
    public bool ClimbLadder(Ladder ladder, bool up)
    {
        if (OnGondola || OnZipline || OnLadder || IsDead || IsDowned || IsRestrained || IsRolling) return false;
        _ladder = ladder;
        IsAiming=false; AimBlend=0; _aimHeld=false;
        _ladderUp = up;
        _ladderFrom = GlobalPosition;
        _ladderT = 0;
        _ladderTime = ladder.Tiers * (up ? LadderUpSeconds : LadderDownSeconds);
        Velocity = Vector3.Zero;
        _push = Vector3.Zero;
        Weapons.EndThrowAim();
        Weapons.CancelMeleeSwing();
        IsHealing = false;
        // Several on one ladder: they pass through each other there (and only there).
        _maskBeforeLadder = CollisionMask;
        CollisionMask &= ~Layers.Player;
        return true;
    }

    /// <param name="arrived">False when they let go partway (downed, grabbed, teleported): no shove at the top.</param>
    private void EndLadder(bool arrived = true)
    {
        if (_ladder == null) return;
        var ladder = _ladder;
        _ladder = null;
        CollisionMask = _maskBeforeLadder;
        Velocity = Vector3.Zero;
        CommitNetPosition();
        if (!_ladderUp || !arrived) return;
        // Stepping off at the top shoves whoever is standing in the way.
        foreach (var other in Stage.AlivePlayers)
        {
            if (other == this || other.OnLadder) continue;
            var away = other.GlobalPosition - ladder.Top;
            away.Y = 0;
            if (away.Length() > 1f) continue;
            var dir = away.LengthSquared() > .01f ? away.Normalized() : ((ladder.Top - ladder.Bottom) with { Y = 0 }).Normalized();
            other.Shove(dir * 6f);
        }
    }

    // ---------- zipline (L-18) ----------

    private Zipline? _zipline;
    private float _zipT;

    public bool OnZipline => _zipline != null;

    /// <summary>Hooks onto <paramref name="line"/> at its top: down to its foot, no shooting on the way.</summary>
    public bool RideZipline(Zipline line)
    {
        if (OnGondola || OnLadder || OnZipline || IsDead || IsDowned || IsRestrained || IsRolling) return false;
        _zipline = line;
        IsAiming=false; AimBlend=0; _aimHeld=false;
        _zipT = 0;
        Velocity = Vector3.Zero;
        _push = Vector3.Zero;
        Weapons.EndThrowAim();
        Weapons.CancelMeleeSwing();
        IsHealing = false;
        _maskBeforeLadder = CollisionMask;
        CollisionMask &= ~Layers.Player;
        return true;
    }

    private void EndZipline()
    {
        if (_zipline == null) return;
        _zipline = null;
        Rig.EndZiplinePose();
        CollisionMask = _maskBeforeLadder;
        Velocity = Vector3.Zero;
        CommitNetPosition();
    }

    private void ZiplineStep(float dt)
    {
        var line = _zipline!;
        _zipT += dt;
        float t = line.ProgressAt(_zipT);
        bool releasing=_zipT>=line.TravelSeconds;
        float hop=Mathf.Clamp((_zipT-line.TravelSeconds)/Zipline.DismountSeconds,0,1);
        GlobalPosition = releasing
            ? line.RidePoint(Zipline.ReleaseProgress).Lerp(line.Landing+Vector3.Up*.05f,hop)+Vector3.Up*(.3f*Mathf.Sin(hop*Mathf.Pi))
            : line.RidePoint(t*Zipline.ReleaseProgress);
        Velocity = Vector3.Zero;
        var face = (line.End - line.Start) with { Y = 0 };
        if (face.LengthSquared() > .001f) Rig.Face(face);
        Rig.Animate(dt, 0, 0, _hurt, Vector3.Zero);
        Rig.ZiplinePose(releasing,hop,line.SpeedAt(_zipT)/Zipline.MaxSpeed);
        if (_zipT >= line.Seconds) EndZipline();
    }

    private void LadderStep(float dt)
    {
        var ladder = _ladder!;
        _ladderT += dt / Mathf.Max(.05f, _ladderTime);
        float t = Mathf.Clamp(_ladderT, 0, 1);
        Vector3 p;
        if (_ladderUp)
        {
            // To the foot, steadily up the face, then over the rim onto the tier.
            if (t < .1f) p = _ladderFrom.Lerp(ladder.Foot, t / .1f);
            else if (t < .85f) p = ladder.Foot.Lerp(new Vector3(ladder.Foot.X, ladder.Rim.Y + .05f, ladder.Foot.Z), (t - .1f) / .75f);
            else p = new Vector3(ladder.Foot.X, ladder.Rim.Y + .05f, ladder.Foot.Z).Lerp(ladder.Top + Vector3.Up * .05f, (t - .85f) / .15f);
        }
        else
        {
            // Over the rim, a fast slide, and a hop off the last stretch.
            var hang = new Vector3(ladder.Foot.X, ladder.Rim.Y, ladder.Foot.Z);
            if (t < .15f) p = _ladderFrom.Lerp(hang, t / .15f);
            else if (t < .8f) p = hang.Lerp(new Vector3(ladder.Foot.X, ladder.Foot.Y + .6f, ladder.Foot.Z), (t - .15f) / .65f);
            else
            {
                float hop = (t - .8f) / .2f;
                var from = new Vector3(ladder.Foot.X, ladder.Foot.Y + .6f, ladder.Foot.Z);
                p = from.Lerp(ladder.Bottom, hop) + Vector3.Up * (.3f * Mathf.Sin(hop * Mathf.Pi));
            }
        }
        GlobalPosition = p;
        Velocity = Vector3.Zero;
        var face = (ladder.Top - ladder.Bottom) with { Y = 0 };
        if (face.LengthSquared() > .001f) Rig.Face(face);
        Rig.Animate(dt, .7f, 0, _hurt, Vector3.Zero);
        if (t >= 1) EndLadder();
    }
}
