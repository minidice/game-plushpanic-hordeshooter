using Godot;

namespace HordeShooter;

/// <summary>
/// States that Onslaught cards leave on a monster (doc/onslaught-card-ideas.md 1장, decisions.md M-27). The card lab
/// (<see cref="LabCards"/>) sets them; the monster only moves and looks the part.
/// </summary>
public partial class Enemy
{
    /// <summary>Height a balloon-shot monster floats at, low enough that flat bullets still reach it.</summary>
    public const float FloatHeight = .55f;

    /// <summary>Balloon round build-up, 0–1; at 1 the monster floats.</summary>
    public float Inflate { get; private set; }
    /// <summary>Seconds left floating (둥실): it does nothing and drifts up.</summary>
    public float FloatTime { get; private set; }
    public bool Floating => FloatTime > 0;
    /// <summary>Seconds left sticky (찍찍이): sticky monsters near each other clump together.</summary>
    public float StickyTime { get; set; }
    public bool Sticky => StickyTime > 0;
    /// <summary>Running away from <see cref="FleeFrom"/> for this long (the jack-in-the-box, M-29).</summary>
    public float FleeTime { get; private set; }
    public Vector3 FleeFrom { get; private set; }

    /// <summary>Scared off: runs away from <paramref name="from"/> for <paramref name="seconds"/> (a common only).</summary>
    public void Scare(Vector3 from, float seconds)
    {
        if (IsDead || IsPuppet || IsSpecial) return;
        CancelMelee();
        Wake();
        FleeFrom = from;
        FleeTime = Mathf.Max(FleeTime, seconds);
    }
    /// <summary>Push speed right now (m/s); above a few m/s it is flying from a hit.</summary>
    public float KnockSpeed => new Vector3(Knock.X, 0, Knock.Z).Length();
    public Vector3 KnockDirection => KnockSpeed > .001f ? new Vector3(Knock.X, 0, Knock.Z) / KnockSpeed : Vector3.Zero;

    private Vector3 _cardPull;
    private float _cardSlow = 1;
    private MeshInstance3D? _stickyMark;

    /// <summary>Blows the monster up a little; true when it just started floating.</summary>
    public bool AddInflate(float amount, float floatSeconds)
    {
        if (IsDead || Floating) return false;
        Inflate = Mathf.Min(1, Inflate + amount);
        Rig.Scale = Vector3.One * (1 + Inflate * .45f);
        if (Inflate < 1) return false;
        FloatTime = floatSeconds;
        CancelMelee();
        return true;
    }

    /// <summary>Lets the air out (popped or time over).</summary>
    public void Deflate()
    {
        Inflate = 0;
        FloatTime = 0;
        if (Rig != null) Rig.Scale = Vector3.One;
    }

    /// <summary>This frame's extra drag toward a clump (m/s) and how much of its own walk is left (0–1).</summary>
    public void CardDrag(Vector3 pull, float slow)
    {
        _cardPull += pull;
        _cardSlow = Mathf.Min(_cardSlow, slow);
    }

    /// <summary>Sheds part of its push (it passed the hit on to someone else).</summary>
    public void DampKnock(float keep) => Knock *= keep;

    /// <summary>The push it is moving with (m/s), shared out inside a velcro clump.</summary>
    public Vector3 KnockVelocity
    {
        get => Knock;
        set => Knock = value;
    }

    /// <summary>Card states applied to this frame's walk; true = it does nothing of its own (floating).</summary>
    private bool CardStep(ref Vector3 desired, float dt)
    {
        if (StickyTime > 0) StickyTime -= dt;
        UpdateStickyMark();
        desired = desired * _cardSlow + _cardPull;
        _cardPull = Vector3.Zero;
        _cardSlow = 1;
        if (!Floating) return false;
        FloatTime -= dt;
        if (FloatTime <= 0) Deflate();
        return Floating;
    }

    /// <summary>Floating: rise to <see cref="FloatHeight"/> and bob there instead of falling.</summary>
    private float FloatLift(float dt)
    {
        float bob = Mathf.Sin((float)Time.GetTicksMsec() * .004f + GetInstanceId() % 7) * .08f;
        return (FloatHeight + bob - GlobalPosition.Y) * 4f;
    }

    private void UpdateStickyMark()
    {
        if (Sticky && _stickyMark == null)
        {
            _stickyMark = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = Radius + .02f, OuterRadius = Radius + .12f, Rings = 16, RingSegments = 4 },
                MaterialOverride = Mat.Glow(new Color("c6ff3d"), 1.4f),
                Position = Vector3.Up * .9f,
            };
            AddChild(_stickyMark);
        }
        else if (!Sticky && _stickyMark != null)
        {
            _stickyMark.QueueFree();
            _stickyMark = null;
        }
    }
}
