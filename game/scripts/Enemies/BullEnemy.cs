using Godot;

namespace HordeShooter;

/// <summary>
/// Headbutt bull (charger slot). Telegraphs, then charges in a straight line: the player is launched,
/// common infected in the path scatter like bowling pins, and hitting a wall leaves it dazed.
/// </summary>
public partial class BullEnemy : Enemy
{
    public enum Mode
    {
        Chase,
        Windup,
        Charge,
        Dazed,
    }

    private BullChargeTrait Charge => Monster.BullCharge!;
    public Mode State { get; private set; } = Mode.Chase;
    public bool HitPlayerThisCharge { get; private set; }

    private float _timer, _cooldown = 2.5f;
    private Vector3 _chargeDir = Vector3.Forward;
    public Vector3 ChargeDirection => _chargeDir;
    private Player? _windupVictim;
    private MeshInstance3D? _telegraph;
    // Each survivor is bowled over at most once per charge.
    private readonly System.Collections.Generic.HashSet<Player> _bowled = new();
    private StandardMaterial3D? _telegraphMat;

    /// <summary>A boss charges only in phases that list the charge skill.</summary>
    protected virtual bool CanCharge => true;

    protected override Vector3 Think(float dt)
    {
        _cooldown -= dt;
        // The telegraph points at one survivor; it does not swing to whoever is nearest mid-windup.
        var player = State == Mode.Windup && _windupVictim is { IsDead: false } locked && IsInstanceValid(locked) ? locked : CurrentTarget(dt);
        if (player == null && State == Mode.Chase) return base.Think(dt);
        var toPlayer = player != null ? player.GlobalPosition - GlobalPosition : _chargeDir;
        toPlayer.Y = 0;
        float dist = toPlayer.Length();

        switch (State)
        {
            case Mode.Windup:
                _timer -= dt;
                if (dist > .1f) _chargeDir = toPlayer / dist;
                FaceOverride = _chargeDir;
                Kick = .5f + Mathf.Sin(_timer * 50) * .5f;
                UpdateTelegraph();
                if (_timer <= 0) BeginCharge();
                return Vector3.Zero;
            case Mode.Charge:
                _timer -= dt;
                FaceOverride = _chargeDir;
                RamThingsInPath();
                if (_timer <= 0) EndCharge(false);
                return _chargeDir * Charge.Speed;
            case Mode.Dazed:
                _timer -= dt;
                Hurt = Mathf.Max(Hurt, .4f);
                if (_timer <= 0) State = Mode.Chase;
                return Vector3.Zero;
        }

        if (CanCharge && Awake && player != null && _cooldown <= 0 && dist < Charge.TriggerRange && dist > Charge.MinRange && HasLineOfSight(dt))
        {
            StartWindup(toPlayer / dist);
            return Vector3.Zero;
        }
        return base.Think(dt);
    }

    /// <summary>Exposed for tests and scripted moments.</summary>
    public void StartWindup(Vector3 direction)
    {
        if (IsDead || StunTime > 0) return;
        CancelMelee();
        State = Mode.Windup;
        _timer = Charge.WindupTime;
        _chargeDir = direction.Normalized();
        _windupVictim = Target ?? Stage.NearestAlivePlayer(GlobalPosition);
        RevealTo(_windupVictim, Charge.WindupTime + 1.5f + VisionSystem.RevealAfterHit);
        Stage.Net?.OnEnemyAttack(this, NetAttack.Charge, _windupVictim, GlobalPosition + _chargeDir * 6f);
        Awake = true;
        ShowTelegraph();
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition + Vector3.Up);
    }

    private void BeginCharge()
    {
        State = Mode.Charge;
        _timer = Charge.ChargeTime;
        HitPlayerThisCharge = false;
        _bowled.Clear();
        // Pass through the player body; the hit is resolved by distance so the bull keeps its momentum.
        CollisionMask = Layers.MovementWorld | Layers.Props;
        HideTelegraph();
        Stage.Cam.AddShake(.12f);
    }

    private void EndCharge(bool wall)
    {
        Stage.Net?.OnEnemyAttackEnd(this);
        CollisionMask = Layers.MovementWorld | Layers.Player | Layers.Props;
        _cooldown = Charge.Cooldown;
        if (!wall)
        {
            State = Mode.Chase;
            return;
        }
        State = Mode.Dazed;
        _timer = Charge.DazedTime;
        Stage.Sfx.PlayAt("bull_bonk", GlobalPosition + Vector3.Up);
        Stage.Cam.AddShake(.35f);
        Stage.Effects.Impact(GlobalPosition + _chargeDir * Radius + Vector3.Up, -_chargeDir);
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 2.6f, "@_@", new Color("ffe07a"));
    }

    protected override void AfterMove(float dt)
    {
        if (State != Mode.Charge) return;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            switch (collision.GetCollider())
            {
                case RigidBody3D body:
                    body.ApplyCentralImpulse(_chargeDir * 6f + Vector3.Up * 3f);
                    if (body is ExplosiveBarrel barrel) barrel.Ignite();
                    break;
                case StaticBody3D when collision.GetNormal().Dot(-_chargeDir) > .55f:
                    EndCharge(true);
                    return;
            }
        }
    }

    private void RamThingsInPath()
    {
        foreach (var player in Stage.AlivePlayers)
        {
            if (_bowled.Contains(player)) continue;
            var rel = player.GlobalPosition - GlobalPosition;
            rel.Y = 0;
            if (rel.Length() < Radius + player.Definition.ColliderRadius + .35f)
            {
                _bowled.Add(player);
                HitPlayerThisCharge = true;
                player.TakeDamage(Charge.PlayerDamage * DamageScale, _chargeDir, 0);
                player.Launch(_chargeDir * 10f + Vector3.Up * 7f, 1.5f);
                Stage.Effects.FloatingText(player.GlobalPosition + Vector3.Up * 2.8f, "BONK!", new Color("ffb36b"));
                Stage.Sfx.PlayAt("bull_bonk", player.GlobalPosition + Vector3.Up);
                Stage.Cam.AddShake(.5f);
            }
        }

        foreach (var other in Stage.Enemies)
        {
            if (other == this || other.IsDead) continue;
            var rel = other.GlobalPosition - GlobalPosition;
            rel.Y = 0;
            if (rel.Length() > Radius + other.Radius + .35f || rel.Dot(_chargeDir) < -.2f) continue;
            // Bowling pins: fling sideways and forward.
            float side = Mathf.Sign(_chargeDir.Cross(rel).Y);
            if (side == 0) side = 1;
            var fling = (_chargeDir + _chargeDir.Cross(Vector3.Up) * side * -.7f).Normalized();
            other.Shove(fling, Charge.BowlPower, other.IsSpecial ? 60f : 999f);
        }
    }

    protected override void OnStunned()
    {
        HideTelegraph();
        if (State is Mode.Windup or Mode.Charge) EndCharge(false);
    }

    public override void TakeHit(HitInfo hit)
    {
        if (State == Mode.Dazed) hit = hit with { Damage = hit.Damage * Charge.DazedDamageMultiplier };
        base.TakeHit(hit);
    }

    public override void Die(Vector3 direction, float force)
    {
        HideTelegraph();
        base.Die(direction, force * .6f);
    }

    private void ShowTelegraph()
    {
        if (_telegraph != null) return;
        _telegraphMat = Mat.Fade(new Color(1, .25f, .2f, .35f));
        _telegraph = Mat.Box(Stage.Effects, GlobalPosition, new Vector3(Radius * 2, .02f, Charge.Speed * Charge.ChargeTime * .7f), _telegraphMat);
        UpdateTelegraph();
    }

    private void UpdateTelegraph()
    {
        if (_telegraph == null || _telegraphMat == null) return;
        float length = ((BoxMesh)_telegraph.Mesh).Size.Z;
        _telegraph.GlobalPosition = GlobalPosition + _chargeDir * length * .5f + Vector3.Up * .03f;
        _telegraph.LookAt(_telegraph.GlobalPosition + _chargeDir, Vector3.Up);
        _telegraphMat.AlbedoColor = new Color(1, .25f, .2f, .25f + .25f * Mathf.Abs(Mathf.Sin(_timer * 18)));
    }

    private void HideTelegraph()
    {
        _telegraph?.QueueFree();
        _telegraph = null;
    }

    public override void _ExitTree() => HideTelegraph();
}
