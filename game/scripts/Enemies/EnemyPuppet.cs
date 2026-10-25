using Godot;

namespace HordeShooter;

/// <summary>
/// A client's copy of a host enemy (doc/multiplayer-sync-design.md 3장): no AI, no health of its own. It walks to the
/// positions it is sent, plays the attacks the host announces, and shows hits as pictures only. It dies when the host
/// says so, and then becomes a normal local corpse.
/// </summary>
public partial class Enemy
{
    /// <summary>Same on every PC; the host numbers enemies in spawn order.</summary>
    public int NetId { get; set; }
    public bool IsPuppet { get; private set; }
    public NetTrack? Track { get; private set; }

    public void MakePuppet()
    {
        IsPuppet = true;
        Track = new NetTrack();
    }

    private void PuppetStep(float dt)
    {
        var net = Stage.Net;
        if (net == null || Track == null || !Track.Sample(net.RenderTime, out var pos, out var facing)) return;
        var before = GlobalPosition;
        GlobalPosition = pos;
        var velocity = dt > 0 ? (pos - before) / dt : Vector3.Zero;
        if (velocity.LengthSquared() > 25 * 25) velocity = Vector3.Zero; // first placement, not a dash
        Velocity = velocity;
        var flags = (NetEnemyFlags)Track.Flags;
        Awake = flags.HasFlag(NetEnemyFlags.Awake);
        if (flags.HasFlag(NetEnemyFlags.Stunned)) Hurt = Mathf.Max(Hurt, .35f);
        if (_meleeTime >= 0)
        {
            _meleeTime += dt;
            HunterMeleeSwing();
            if (_meleeTime > MeleeWindup + MeleeRecovery) CancelMelee();
        }
        var flat = new Vector3(velocity.X, 0, velocity.Z);
        Footsteps(flat.Length() * dt);
        Rig.Face(IsMeleeAttacking ? _meleeDirection : facing);
        Rig.ResetEnemyAttackPose();
        Rig.Animate(dt, Mathf.Min(1, flat.Length() / Speed), Kick, Hurt, velocity);
        CommonWeaponPose(dt);
        if (IsMeleeAttacking && !HunterMelee) Rig.ApplyEnemyAttackPose(_meleeTime, MeleeWindup, MeleeRecovery);
        if (this is BullEnemy)
        {
            if (flags.HasFlag(NetEnemyFlags.Charging)) Rig.ApplyBullChargePose(1f);
            else if (flags.HasFlag(NetEnemyFlags.Winding)) Rig.ApplyBullChargePose(.65f);
        }
    }

    /// <summary>The host's enemy started a swing at <paramref name="victim"/>.</summary>
    public void PuppetMelee(Player? victim, Vector3 direction)
    {
        _meleeTime = 0;
        BeginHunterMelee();
        _meleeDirection = direction;
        RevealTo(victim, MeleeWindup + MeleeRecovery + VisionSystem.RevealAfterHit);
    }

    /// <summary>The host's special stopped its attack: clear the warning lines and markers.</summary>
    public void PuppetEndAttack() { CancelMelee(); if (this is RangedEnemy ranged) ranged.EndPuppetShot(); else OnStunned(); }

    /// <summary>A hit seen on a client: the flinch and the stuffing, but the host keeps the health.</summary>
    private void PuppetHit(Vector3 point, Vector3 direction, bool crit)
    {
        var flat = new Vector3(direction.X, 0, direction.Z);
        flat = flat.LengthSquared() > .0001f ? flat.Normalized() : Vector3.Forward;
        Rig.FlashHit(crit);
        Hurt = 1;
        Rig.Jolt(flat * 4f);
        Stage.Effects.Stuffing(point, -flat, Definition.StuffingColor, crit ? 16 : 5);
        Stage.Sfx.PlayAt("zombie_hit", point);
    }

    /// <summary>The host reported a hit on this enemy (someone else's shot).</summary>
    public void ShowNetHit(Vector3 point, Vector3 direction, bool crit = false) => PuppetHit(point, direction, crit);
}
