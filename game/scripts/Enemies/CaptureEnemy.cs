using Godot;

namespace HordeShooter;

/// <summary>Telegraphed tether/pounce. Holds indefinitely until interrupted or the captor dies.</summary>
public partial class CaptureEnemy : Enemy
{
    public enum Mode { Chase, Windup, Leap, Holding }
    public Mode State { get; private set; }
    public bool IsLeaper => Monster.Behavior == MonsterBehavior.Leaper;
    private CaptureTrait Capture => Monster.Capture!;
    private float _timer, _cooldown = 1.5f, _damageTimer;
    private Vector3 _target, _direction;
    private MeshInstance3D? _marker;
    /// <summary>The survivor being aimed at or held. Fixed from windup until the capture ends.</summary>
    private Player? _victim;

    protected override Vector3 Think(float dt)
    {
        _cooldown -= dt;
        var player = State == Mode.Chase ? CurrentTarget(dt) : _victim;
        if (player == null || !IsInstanceValid(player) || player.IsDead) { CancelCapture(); return State == Mode.Chase ? base.Think(dt) : Vector3.Zero; }
        var to = player.GlobalPosition - GlobalPosition;
        to.Y = 0;
        float distance = to.Length();
        if (State == Mode.Holding)
        {
            if (player.RestrainingEnemy != this) { CancelCapture(); return Vector3.Zero; }
            // A tether cannot pass through new cover; a pin requires actual body contact.
            // These are physical interruptions, never a duration-based self escape.
            float holdRange = IsLeaper ? Radius + player.Definition.ColliderRadius + 1.2f : Capture.Range * 1.25f;
            if (distance > holdRange || !ClearToPlayer()) { CancelCapture(); return Vector3.Zero; }
            FaceOverride = to;
            if (!IsLeaper) player.RestraintPullTarget = GlobalPosition;
            DrawLine(player.GlobalPosition + Vector3.Up * .8f, .06f, new Color("e6b3e9"));
            _damageTimer -= dt;
            if (_damageTimer <= 0)
            {
                player.TakeDamage(AttackDamage, to, 0);
                _damageTimer = AttackInterval;
            }
            return Vector3.Zero;
        }
        if (State == Mode.Windup)
        {
            FaceOverride = _target - GlobalPosition;
            _timer -= dt;
            if (_timer <= 0)
            {
                ClearMarker();
                if (IsLeaper)
                {
                    State = Mode.Leap;
                    _timer = Capture.Range / Capture.Speed + .15f;
                    _direction = (_target - GlobalPosition) with { Y = 0 };
                    _direction = _direction.Normalized();
                    // A pounce up onto a higher tier needs the height to clear its edge (the grab exception, L-16).
                    float rise = player.GlobalPosition.Y - GlobalPosition.Y;
                    Velocity = new Vector3(0, Stage.Tiers.Any && rise > .3f ? Mathf.Sqrt(2 * 20f * (rise + .6f)) : 4f, 0);
                }
                else
                {
                    var miss = player.GlobalPosition - _target; miss.Y = 0;
                    if (miss.Length() < 1.25f && distance <= Capture.Range && ClearToPlayer()) Grab();
                    else CancelCapture();
                }
            }
            return Vector3.Zero;
        }
        if (State == Mode.Leap)
        {
            FaceOverride = _direction;
            _timer -= dt;
            if (distance < Radius + player.Definition.ColliderRadius + .35f && ClearToPlayer())
            {
                Grab();
                return Vector3.Zero;
            }
            if (_timer <= 0) { CancelCapture(); return Vector3.Zero; }
            return _direction * Capture.Speed;
        }
        if (Awake && !player.IsRestrained && !player.IsDowned && _cooldown <= 0 && distance < Capture.Range && distance > 1.5f && ClearTo(player))
        {
            StartWindup(player);
            return Vector3.Zero;
        }
        return base.Think(dt);
    }

    public void StartWindup(Player? victim = null)
    {
        if (IsDead || StunTime > 0) return;
        victim ??= Target ?? Stage.NearestAlivePlayer(GlobalPosition);
        if (victim == null) return;
        _victim = victim;
        State = Mode.Windup;
        _timer = Capture.Windup;
        RevealTo(victim, Capture.Windup + VisionSystem.RevealAfterHit);
        Stage.Net?.OnEnemyAttack(this, NetAttack.Capture, victim, victim.GlobalPosition);
        _target = victim.GlobalPosition;
        Awake = true;
        DrawLine(_target + Vector3.Up * .04f, IsLeaper ? .65f : .24f, new Color("f9b34c"));
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition + Vector3.Up, -5, IsLeaper ? 1.5f : .7f);
    }

    private bool ClearToPlayer() => _victim != null && ClearTo(_victim);
    private bool ClearTo(Player player)
    {
        // Height tiers (L-16): grabs are the exception that reaches across tiers. The line is checked at the higher body's
        // chest height, so the cliff between them does not count but a wall up there does.
        if (Stage.Tiers.Any && Stage.Tiers.TierOf(this) != Stage.Tiers.TierOf(player))
        {
            float y = Mathf.Max(GlobalPosition.Y, player.GlobalPosition.Y) + .7f;
            return Rays.Clear(this, GlobalPosition with { Y = y }, player.GlobalPosition with { Y = y }, Layers.MovementWorld | Layers.Props);
        }
        var body = GlobalPosition + Vector3.Up * .7f;
        if (!Rays.Clear(this, body, player.GlobalPosition + Vector3.Up * .7f, Layers.MovementWorld | Layers.Props))
            return false;
        return IsLeaper || (Rays.Clear(this, body, Rig.Muzzle.GlobalPosition, Layers.MovementWorld | Layers.Props) &&
            Rays.Clear(this, Rig.Muzzle.GlobalPosition, player.GlobalPosition + Vector3.Up * .8f, Layers.MovementWorld | Layers.Props));
    }

    /// <summary>The survivor currently held (null when not holding anyone).</summary>
    public Player? Held => State == Mode.Holding ? _victim : null;

    private void Grab()
    {
        if (_victim == null || !_victim.TryRestrain(this)) { CancelCapture(); return; }
        State = Mode.Holding;
        _damageTimer = AttackInterval;
        Velocity = Vector3.Zero;
        if (!IsLeaper) _victim.RestraintPullTarget = GlobalPosition;
    }

    private void CancelCapture()
    {
        if (State != Mode.Chase) Stage.Net?.OnEnemyAttackEnd(this);
        ReleaseAll();
        _victim = null;
        State = Mode.Chase;
        _cooldown = Capture.Cooldown;
        ClearMarker();
    }

    protected override void AfterMove(float dt)
    {
        if (State != Mode.Leap) return;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
            if (GetSlideCollision(i).GetCollider() is StaticBody3D && GetSlideCollision(i).GetNormal().Y < .5f)
            { CancelCapture(); break; }
    }

    /// <summary>Lets go of anyone this captor holds (ReleaseRestraint ignores survivors held by someone else).</summary>
    private void ReleaseAll()
    {
        foreach (var survivor in Stage.Players)
            if (IsInstanceValid(survivor)) survivor.ReleaseRestraint(this);
    }

    private void DrawLine(Vector3 target, float width, Color color)
    {
        var from = State == Mode.Holding && !IsLeaper ? Rig.Muzzle.GlobalPosition
            : GlobalPosition + Vector3.Up * (State == Mode.Holding ? .8f : .04f);
        float distance = from.DistanceTo(target);
        if (_marker == null)
            _marker = Mat.Box(Stage.Effects, Vector3.Zero, Vector3.One, Mat.Fade(new Color(color, .65f)));
        _marker.Scale = new Vector3(width, .035f, Mathf.Max(.01f, distance));
        _marker.GlobalPosition = (from + target) * .5f;
        if (distance > .01f) _marker.LookAt(target, Vector3.Up);
    }

    private void ClearMarker() { if (IsInstanceValid(_marker)) _marker!.QueueFree(); _marker = null; }
    protected override void OnStunned() => CancelCapture();
    public override void Die(Vector3 direction, float force) { CancelCapture(); base.Die(direction, force); }
    public override void _ExitTree()
    {
        ReleaseAll();
        ClearMarker();
    }
}
