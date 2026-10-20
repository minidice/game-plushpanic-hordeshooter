using Godot;

namespace HordeShooter;

/// <summary>
/// Downed state and reviving (game option "revive", decisions.md M-17/M-18, doc/ai-squad-plan.md 6장).
/// At 0 health a survivor goes down instead of dying while they have downs left this life; a teammate
/// holding [E] beside them for a few seconds brings them back. Taking damage interrupts the reviver.
/// A downed survivor bleeds out; hits make it faster. Medkits and the safehouse reset the down count.
/// </summary>
public partial class Player
{
    public bool IsDowned { get; private set; }
    /// <summary>Seconds left before a downed survivor bleeds out.</summary>
    public float BleedLeft { get; private set; }
    /// <summary>Times this survivor went down in the current life.</summary>
    public int DownCount { get; private set; }
    /// <summary>One more down would be death (shown as a warning).</summary>
    public bool OnLastDown => DownCount >= Rules.DownsPerLife;
    /// <summary>0..1 while someone is picking this survivor up.</summary>
    public float ReviveProgress { get; private set; }
    /// <summary>The downed teammate this survivor is picking up right now.</summary>
    public Player? Reviving { get; private set; }
    /// <summary>Who is picking this survivor up. Only they fill (or reset) the bar.</summary>
    public Player? RevivedBy { get; private set; }

    private static SquadRules Rules => SquadDatabase.Current.Rules;
    private bool _reviveHeld;
    private float _lastBleedHit = -10f, _bleedClock;

    /// <summary>Hold [E] (bots: their brain) to pick up a downed teammate in reach.</summary>
    public void HoldRevive(bool held) => _reviveHeld = held;

    /// <summary>A down only makes sense if someone else is still standing to pick you up.</summary>
    private bool CanGoDown()
    {
        if (!Stage.ReviveEnabled || IsDowned || DownCount >= Rules.DownsPerLife) return false;
        foreach (var other in Stage.Players)
            if (other != this && IsInstanceValid(other) && !other.IsDead && !other.IsDowned) return true;
        return false;
    }

    private void GoDown()
    {
        ReleaseRestraint();
        IsDowned = true;
        DownCount++;
        Health = 0;
        BleedLeft = Rules.BleedOut;
        ReviveProgress = 0;
        IsHealing = false;
        StunTime = 0;
        _tumble = 0;
        _rollTime = 0;
        _push = Vector3.Zero;
        Gun.CancelReload();
        Weapons.EndThrowAim();
        Weapons.CancelMeleeSwing();
        StopReviving();
        Velocity = Vector3.Zero;
        Rig.ResetPose();
        Stage.Sfx.PlayAt("honk_long", GlobalPosition + Vector3.Up, -6);
        Stage.OnSurvivorDowned(this);
    }

    /// <summary>Downed: no moving or shooting; bleed out unless picked up.</summary>
    private void UpdateDowned(float dt)
    {
        _bleedClock += dt;
        BleedLeft -= dt;
        Velocity = new Vector3(0, IsOnFloor() ? -.1f : Velocity.Y - Gravity * dt, 0);
        MoveAndSlide();
        Rig.Pose.Rotation = new Vector3(-1.35f, 0, 0);
        if (BleedLeft <= 0 && Stage.Authoritative) Die(Vector3.Zero);
    }

    /// <summary>Hit while downed: no health to lose, the bleed-out runs faster instead.</summary>
    private void BleedFromHit()
    {
        // Fire and vomit tick many times a second: count at most one hit per half second.
        if (_bleedClock - _lastBleedHit < .5f) return;
        _lastBleedHit = _bleedClock;
        BleedLeft -= Rules.BleedHit;
        Rig.FlashHit();
        if (BleedLeft <= 0 && Stage.Authoritative) Die(Vector3.Zero);
    }

    /// <summary>Back on their feet with a little health.</summary>
    public void Revive()
    {
        if (!IsDowned) return;
        IsDowned = false;
        ReviveProgress = 0;
        RevivedBy = null;
        Health = Mathf.Min(MaxHealth, Rules.ReviveHealth);
        BleedLeft = 0;
        Rig.ResetPose();
        Stage.Sfx.PlayAt("heal", GlobalPosition + Vector3.Up);
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 2.6f, Loc.Text("msg_revived"), new Color("7dff9a"));
    }

    /// <summary>Medkit or safehouse: the next fall is a down again, not death.</summary>
    public void ResetDowns() => DownCount = 0;

    /// <summary>Reviver side: keeps filling the target's bar while [E] is held, standing still and in reach.</summary>
    private void UpdateReviving(float dt, bool moving)
    {
        if (!_reviveHeld || moving || IsDowned || IsRestrained || IsStunned || IsRolling || IsHealing)
        {
            StopReviving();
            return;
        }
        var target = Reviving ?? DownedTeammateInReach();
        if (target == null || !IsInstanceValid(target) || !target.IsDowned ||
            target.GlobalPosition.DistanceTo(GlobalPosition) > Rules.ReviveRange + .3f)
        {
            StopReviving();
            return;
        }
        // Someone else already has them: wait rather than doubling the speed or resetting their bar.
        if (target.RevivedBy is { } other && other != this && IsInstanceValid(other) && other.Reviving == target) return;
        target.RevivedBy = this;
        Reviving = target;
        target.ReviveProgress += dt / Mathf.Max(.1f, Rules.ReviveTime);
        // A client fills the bar but the host decides when the teammate is back up.
        if (target.ReviveProgress >= 1 && Stage.Authoritative)
        {
            target.Revive();
            Reviving = null;
        }
    }

    /// <summary>Damage, moving away or letting go: the bar starts over (decisions.md M-17).</summary>
    private void StopReviving()
    {
        if (Reviving != null && IsInstanceValid(Reviving) && Reviving.RevivedBy == this)
        {
            Reviving.ReviveProgress = 0;
            Reviving.RevivedBy = null;
        }
        Reviving = null;
    }

    public Player? DownedTeammateInReach()
    {
        Player? best = null;
        float bestD = Rules.ReviveRange;
        foreach (var other in Stage.Players)
        {
            if (other == this || !IsInstanceValid(other) || !other.IsDowned) continue;
            float d = other.GlobalPosition.DistanceTo(GlobalPosition);
            if (d <= bestD) { bestD = d; best = other; }
        }
        return best;
    }

    /// <summary>Game over: a downed survivor does not keep bleeding into the end screen.</summary>
    public void EndDowned()
    {
        if (!IsDowned) return;
        IsDowned = false;
        Health = 0;
        RevivedBy = null;
    }

    /// <summary>Respawned squadmates start hurt (squad_rules.json respawn_health).</summary>
    public void SetSpawnHealth(int health) => Health = Mathf.Clamp(health, 1, MaxHealth);
}
