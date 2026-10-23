using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Drives one AI squadmate through the same controls a human uses (doc/ai-squad-plan.md).
/// A priority list, re-evaluated five times a second: avoid danger → rescue → revive → heal →
/// fight → pick up → follow → watch. Squadmates never do objectives, never throw, never take
/// floor weapons, and do not walk ahead of the leader except while fighting (decisions.md M-12..M-16).
/// Runs before the survivor's own physics step (negative priority) so its controls apply this frame.
/// </summary>
public partial class SquadBrain : Node
{
    public enum Activity { Idle, Follow, Fight, Rescue, Revive, Heal, Pickup, Evade, Down }

    public Player Bot { get; private set; } = null!;
    public SquadRole Role { get; private set; } = null!;
    public int Slot { get; private set; }
    public Activity Doing { get; private set; } = Activity.Idle;
    public Enemy? FightTarget { get; private set; }
    public float UnseenFarTime { get; private set; }
    public int Teleports { get; private set; }

    private Stage _stage = null!;
    private SquadRules Rules => SquadDatabase.Current.Rules;
    private readonly RandomNumberGenerator _rng = new();
    private float _thinkTimer, _watchTimer, _strafeSign = 1, _clock;
    private Player? _patientTarget;
    private Interactable? _pickupTarget;
    private bool _firePulse;
    private Vector3 _goal, _watchDirection = Vector3.Forward;
    private Interactable? _pickup;
    // Route toward the current goal when it is not the leader (a downed or held teammate, a pickup, a way out).
    private FlowField? _goalFlow;
    private int _goalFlowVersion = -1;
    private float _reviveTime;
    private readonly Dictionary<Player, float> _givenUp = new();
    private float _pickupTime;
    // Items that could not be reached (on a table, behind a prop): left alone for a while.
    private readonly Dictionary<Interactable, float> _unreachable = new();
    private Player? _patient;

    public static SquadBrain Attach(Stage stage, Player bot, SquadRole role, int slot)
    {
        var brain = new SquadBrain { Name = "SquadBrain", _stage = stage, Bot = bot, Role = role, Slot = slot };
        brain.ProcessPhysicsPriority = -10;
        bot.AddChild(brain);
        return brain;
    }

    public override void _Ready()
    {
        _rng.Randomize();
        _thinkTimer = _rng.Randf() * .2f;
        _strafeSign = _rng.Randf() < .5f ? -1 : 1;
        // Times, not set: a team card (glass cannon) may already be on this survivor.
        Bot.DamageMultiplier *= Role.DamageMultiplier;
        Bot.DisplayLabel = $"{Loc.Text(Bot.Definition.DisplayName)} · {Loc.Text(Role.NameKey)}";
        // Starting weapons come from the role's pool; the frying pan stays as nothing better is in slot 1.
        Bot.Weapons.Pickup(ArmoryCatalog.Weapon(Role.Secondary), null);
        Bot.Weapons.Pickup(ArmoryCatalog.Weapon(Role.PickPrimary(_rng)), null);
        Bot.Weapons.SelectSlot(2);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        if (Bot.IsDead || Bot.IsDowned || _stage.State != StageState.Playing)
        {
            Doing = Bot.IsDowned ? Activity.Down : Activity.Idle;
            Bot.HoldRevive(false);
            Bot.SetControls(Vector2.Zero, Bot.AimPoint, false);
            return;
        }
        _thinkTimer -= dt;
        if (_thinkTimer <= 0)
        {
            _thinkTimer = .2f;
            Think();
        }
        UpdateCatchup(dt);
        Act(dt);
    }

    // ---------- deciding ----------

    private Player? Leader => _stage.Player is { IsDead: false } p ? p : null;

    private void Think()
    {
        _pickup = null;
        _patient = null;
        if (DangerEscape() is { } safe) { Doing = Activity.Evade; _goal = safe; return; }
        if (HeldTeammate() is { } captor) { Doing = Activity.Rescue; FightTarget = captor; return; }
        if (DownedTeammate() is { } downed)
        {
            // Clear the zombies off your back first, or the hit resets the bar and you go down too.
            if (CloseThreat() is { } threat) { Doing = Activity.Fight; FightTarget = threat; _goal = Bot.GlobalPosition; return; }
            if (_patientTarget != downed) { _patientTarget = downed; _reviveTime = 0; }
            Doing = Activity.Revive; _patient = downed; _goal = downed.GlobalPosition; return;
        }
        _patientTarget = null;
        if (ShouldHeal()) { Doing = Activity.Heal; return; }
        FightTarget = PickTarget();
        if (FightTarget != null) { Doing = Activity.Fight; _goal = FormationSpot(); return; }
        if (PickupWanted() is { } item)
        {
            if (_pickupTarget != item) { _pickupTarget = item; _pickupTime = 0; }
            Doing = Activity.Pickup; _pickup = item; _goal = item.GlobalPosition; return;
        }
        _pickupTarget = null;
        _goal = FormationSpot();
        Doing = Bot.GlobalPosition.DistanceTo(_goal) > .9f ? Activity.Follow : Activity.Idle;
    }

    /// <summary>Standing in fire or vomit, or in front of a bull about to charge: the nearest way out.</summary>
    private Vector3? DangerEscape()
    {
        var at = Bot.GlobalPosition;
        foreach (var node in GetTree().GetNodesInGroup(SquadGroups.Hazard))
        {
            if (node is not Node3D hazard || !IsInstanceValid(hazard)) continue;
            float radius = hazard.HasMeta("radius") ? (float)hazard.GetMeta("radius") : 3f;
            var away = at - hazard.GlobalPosition; away.Y = 0;
            if (away.Length() > radius + .8f) continue;
            var dir = away.LengthSquared() > .01f ? away.Normalized() : new Vector3(_strafeSign, 0, 0);
            return hazard.GlobalPosition + dir * (radius + 2f);
        }
        foreach (var enemy in _stage.Enemies)
        {
            if (enemy is not BullEnemy { IsDead: false } bull || bull.State is not (BullEnemy.Mode.Windup or BullEnemy.Mode.Charge)) continue;
            var rel = at - bull.GlobalPosition; rel.Y = 0;
            float along = rel.Dot(bull.ChargeDirection);
            if (along < 0 || along > 14f) continue;
            var side = rel - bull.ChargeDirection * along;
            if (side.Length() > 1.6f) continue;
            var sideDir = side.LengthSquared() > .01f ? side.Normalized() : bull.ChargeDirection.Cross(Vector3.Up) * _strafeSign;
            return at + sideDir * 3f;
        }
        return null;
    }

    /// <summary>A captor holding any survivor (rescue comes before everything but escaping danger).</summary>
    private Enemy? HeldTeammate()
    {
        foreach (var survivor in _stage.AlivePlayers)
            if (survivor != Bot && survivor.RestrainingEnemy is { IsDead: false } captor && IsInstanceValid(captor)) return captor;
        return null;
    }

    /// <summary>
    /// The nearest downed teammate nobody else is picking up (picking up comes right after rescues).
    /// A second squadmate keeps fighting instead of stacking on the same patient.
    /// </summary>
    private Player? DownedTeammate()
    {
        Player? best = null;
        float bestD = float.MaxValue;
        foreach (var other in _stage.Players)
        {
            if (other == Bot || !IsInstanceValid(other) || !other.IsDowned) continue;
            if (other.RevivedBy is { } helper && helper != Bot && IsInstanceValid(helper) && helper.Reviving == other) continue;
            if (_givenUp.TryGetValue(other, out float until) && until > _clock) continue;
            float d = other.GlobalPosition.DistanceTo(Bot.GlobalPosition);
            if (d < bestD) { bestD = d; best = other; }
        }
        return best;
    }

    /// <summary>An awake enemy within arm's reach of this squadmate.</summary>
    private Enemy? CloseThreat()
    {
        foreach (var enemy in _stage.Enemies)
            if (!enemy.IsDead && enemy.Awake && enemy.GlobalPosition.DistanceTo(Bot.GlobalPosition) < 3f) return enemy;
        return null;
    }

    private bool ShouldHeal()
    {
        if (Bot.IsHealing) return true;
        if (Bot.Medkits <= 0 || Bot.Health > Bot.MaxHealth * Rules.HealBelow) return false;
        foreach (var enemy in _stage.Enemies)
            if (!enemy.IsDead && enemy.Awake && enemy.GlobalPosition.DistanceTo(Bot.GlobalPosition) < Rules.HealSafeRadius) return false;
        return true;
    }

    /// <summary>Score: attacking a teammate > special > close. Only enemies in range and in sight.</summary>
    private Enemy? PickTarget()
    {
        Enemy? best = null;
        float bestScore = float.MinValue;
        float range = Mathf.Min(Rules.EngageRange, Bot.Gun.IsMelee ? 3f : Bot.Gun.Def.MaxRange);
        var eye = Bot.GlobalPosition + Vector3.Up * Player.AimHeight;
        foreach (var enemy in _stage.Enemies)
        {
            if (enemy.IsDead || !enemy.Awake) continue;
            float d = enemy.GlobalPosition.DistanceTo(Bot.GlobalPosition);
            if (d > range) continue;
            if (!InSight(eye, enemy)) continue;
            float score = -d;
            if (enemy.Victim is { } victim && victim != Bot && victim.GlobalPosition.DistanceTo(enemy.GlobalPosition) < 3f) score += 40;
            if (enemy.IsSpecial) score += 25;
            if (enemy == FightTarget) score += 4; // keep a target rather than flicking between equals
            if (score > bestScore) { bestScore = score; best = enemy; }
        }
        return best;
    }

    /// <summary>
    /// Line of fire to an enemy. With height tiers (L-15) nothing up a tier can be shot, and below the line is checked at
    /// the squadmate's own height, the way the bullet flies over the edge.
    /// </summary>
    private bool InSight(Vector3 eye, Enemy enemy)
    {
        var target = enemy.GlobalPosition + Vector3.Up * Player.AimHeight;
        var tiers = _stage.Tiers;
        if (tiers.Any)
        {
            int mine = tiers.TierOf(Bot), theirs = tiers.TierOf(enemy);
            if (theirs > mine) return false;
            if (theirs < mine) target = target with { Y = eye.Y };
        }
        return Rays.Clear(Bot, eye, target, Layers.World | Layers.Props);
    }

    private Interactable? PickupWanted()
    {
        var primary = Bot.Loadout.Primary;
        bool lowAmmo = primary != null && primary.Def.Refillable && !primary.Def.InfiniteReserve
            && primary.Def.ReserveMax > 0 && primary.Reserve < primary.Def.ReserveMax * Rules.AmmoLow;
        bool wantsMedkit = Bot.Medkits == 0;
        if (!lowAmmo && !wantsMedkit) return null;
        Interactable? best = null;
        float bestD = Rules.PickupRadius;
        float leaderProgress = Leader != null ? _stage.Progress(Leader.GlobalPosition) : float.MaxValue;
        var leaderAt = Leader?.GlobalPosition ?? Bot.GlobalPosition;
        foreach (var item in _stage.Interactables)
        {
            if (!item.Enabled || !item.Visible) continue;
            if (_unreachable.TryGetValue(item, out float until) && until > _clock) continue;
            // Only on the way: something near the leader, not a detour.
            if (item.GlobalPosition.DistanceTo(leaderAt) > 10f) continue;
            bool kind = (wantsMedkit && Stage.IsMedkit(item)) || (lowAmmo && Stage.IsAmmo(item));
            if (!kind) continue;
            // Not something the leader has not reached yet.
            if (_stage.Progress(item.GlobalPosition) < leaderProgress - 1f) continue;
            float d = item.GlobalPosition.DistanceTo(Bot.GlobalPosition);
            if (d < bestD) { bestD = d; best = item; }
        }
        return best;
    }

    /// <summary>
    /// Formation slot behind the leader along the route to the exit (never ahead of them):
    /// left-back, right-back, straight behind.
    /// </summary>
    public Vector3 FormationSpot()
    {
        var leader = Leader;
        if (leader == null) return Bot.GlobalPosition;
        var forward = _stage.RouteForward(leader.GlobalPosition);
        var side = forward.Cross(Vector3.Up).Normalized();
        float back = Role.FollowDistance;
        float lateral = Slot switch { 0 => -1.4f, 1 => 1.4f, _ => 0f };
        if (Slot >= 2) back += 1.2f;
        var spot = leader.GlobalPosition - forward * back + side * lateral;
        // Height tiers: a slot that falls off the leader's tier is no slot.
        bool Off(Vector3 p) => _stage.Flow.IsBlocked(p) || (_stage.Tiers.Any && _stage.Tiers.TierAt(p) != _stage.Tiers.TierAt(leader.GlobalPosition));
        if (Off(spot)) spot = leader.GlobalPosition - forward * back;
        if (Off(spot)) spot = leader.GlobalPosition;
        return spot with { Y = Bot.GlobalPosition.Y };
    }

    // ---------- acting ----------

    private void Act(float dt)
    {
        // On a ladder the climb drives; nothing to decide until the top or the bottom.
        if (Bot.OnLadder || Bot.OnZipline) { Bot.SetControls(Vector2.Zero, Bot.AimPoint, false); return; }
        var move = Vector3.Zero;
        var aim = Bot.GlobalPosition + _watchDirection * 5 + Vector3.Up * Player.AimHeight;
        bool fire = false;
        bool revive = false;
        ManageWeapons();
        switch (Doing)
        {
            case Activity.Revive:
                if (_patient != null && IsInstanceValid(_patient) && _patient.IsDowned)
                {
                    float d = _patient.GlobalPosition.DistanceTo(Bot.GlobalPosition);
                    if (d > Rules.ReviveRange * .8f)
                    {
                        move = Steer(_patient.GlobalPosition, 1f, toLeader: false);
                        _reviveTime += dt;
                        // Could not reach them (blocked, too far): leave it to someone else for a while.
                        if (_reviveTime > 10f) { _givenUp[_patient] = _clock + 6f; _thinkTimer = 0; }
                    }
                    else revive = true;
                    aim = _patient.GlobalPosition + Vector3.Up * Player.AimHeight;
                }
                break;
            case Activity.Evade:
                move = Steer(_goal, 1f, toLeader: false);
                break;
            case Activity.Rescue:
            case Activity.Fight:
                if (FightTarget != null && IsInstanceValid(FightTarget) && !FightTarget.IsDead)
                {
                    var targetPoint = FightTarget.GlobalPosition + Vector3.Up * Player.AimHeight;
                    aim = targetPoint;
                    float d = FightTarget.GlobalPosition.DistanceTo(Bot.GlobalPosition);
                    bool clear = ClearOfTeammates(targetPoint);
                    bool inRange = d <= (Bot.Gun.IsMelee ? Bot.Gun.Def.MeleeRange + .5f : Bot.Gun.Def.MaxRange * .9f);
                    fire = clear && inRange && Bot.Gun.CanFire;
                    // Fight from the formation slot (keep up with the leader); only a rescue closes in on the enemy.
                    float pace = Bot.GlobalPosition.DistanceTo(_goal) > 5f ? 1f : .7f;
                    move = Doing == Activity.Rescue || !inRange ? Approach(FightTarget.GlobalPosition) : Steer(_goal, pace);
                    if (!clear) move += Bot.AimDirection.Cross(Vector3.Up) * _strafeSign;
                }
                else move = Steer(_goal, .8f);
                break;
            case Activity.Heal:
                if (!Bot.IsHealing) Bot.PressHeal();
                break;
            case Activity.Pickup:
                move = Steer(_goal, 1f, toLeader: false);
                if (_pickup != null && IsInstanceValid(_pickup))
                {
                    var flat = _pickup.GlobalPosition - Bot.GlobalPosition; flat.Y = 0;
                    _pickupTime += dt;
                    if (flat.Length() < _pickup.Radius)
                    {
                        _stage.TakePickup(_pickup, Bot);
                        _thinkTimer = 0;
                    }
                    else if (_pickupTime > 4f)
                    {
                        _unreachable[_pickup] = _clock + 20f;
                        _thinkTimer = 0;
                    }
                }
                break;
            case Activity.Follow:
                float dist = Bot.GlobalPosition.DistanceTo(_goal);
                move = Steer(_goal, dist > 4 ? 1f : .6f);
                break;
            default:
                _watchTimer -= dt;
                if (_watchTimer <= 0) PickWatchDirection();
                break;
        }
        // Semi-automatic weapons need a fresh press per shot; automatic ones are held.
        if (!Bot.Gun.Def.Automatic) { bool press = fire && !_firePulse; _firePulse = fire && press; fire = press; }
        Bot.HoldRevive(revive);
        if (TakeLadder(move) || TakeZipline()) return;
        Bot.SetControls(new Vector2(move.X, move.Z).LimitLength(1), aim, fire);
    }

    /// <summary>When the way to the leader is a zipline from right here (L-18), ride it.</summary>
    private bool TakeZipline()
    {
        if (_stage.Ziplines.Count == 0 || Leader == null) return false;
        int here = _stage.Tiers.Index(Bot.GlobalPosition);
        foreach (var line in _stage.Ziplines)
        {
            if (line.StartCell != here) continue;
            if (_stage.LeaderFlow.NextCell(Bot.GlobalPosition) == line.EndCell) return Bot.RideZipline(line);
        }
        return false;
    }

    /// <summary>When the route steps onto a ladder (L-16), climb it the way a survivor would.</summary>
    private bool TakeLadder(Vector3 move)
    {
        var tiers = _stage.Tiers;
        if (!tiers.Any || _stage.Ladders.Count == 0 || move.LengthSquared() < .01f) return false;
        int here = tiers.Index(Bot.GlobalPosition);
        int ahead = tiers.Index(Bot.GlobalPosition + new Vector3(move.X, 0, move.Z).Normalized() * (Bot.Definition.ColliderRadius + .5f));
        if (here < 0 || ahead < 0 || here == ahead || tiers.LadderEnd(here) != ahead) return false;
        foreach (var ladder in _stage.Ladders)
        {
            if (ladder.BottomCell == here && ladder.TopCell == ahead) return Bot.ClimbLadder(ladder, up: true);
            if (ladder.TopCell == here && ladder.BottomCell == ahead) return Bot.ClimbLadder(ladder, up: false);
        }
        return false;
    }

    /// <summary>Reload when empty; fall back to the pistol when the primary is dry; back to it after a refill.</summary>
    private void ManageWeapons()
    {
        var gun = Bot.Gun;
        var primary = Bot.Loadout.Primary;
        if (Bot.Loadout.ActiveSlot == 2 && primary != null && primary.IsEmpty) { Bot.PressSlot(1); return; }
        if (Bot.Loadout.ActiveSlot == 1 && primary != null && !primary.IsEmpty && !gun.Reloading) { Bot.PressSlot(2); return; }
        if (!gun.IsMelee && gun.Mag == 0 && !gun.Reloading && (gun.Def.InfiniteReserve || gun.Reserve > 0)) Bot.PressReload();
        else if (FightTarget == null && !gun.IsMelee && !gun.Reloading && gun.Mag < gun.Def.MagSize * .5f && (gun.Def.InfiniteReserve || gun.Reserve > 0)) Bot.PressReload();
    }

    /// <summary>Do not shoot through a teammate (friendly fire is off, but it reads badly).</summary>
    private bool ClearOfTeammates(Vector3 target)
    {
        var from = Bot.GlobalPosition + Vector3.Up * Player.AimHeight;
        var line = target - from; line.Y = 0;
        float length = line.Length();
        if (length < .01f) return true;
        var dir = line / length;
        foreach (var other in _stage.AlivePlayers)
        {
            if (other == Bot) continue;
            var rel = other.GlobalPosition - Bot.GlobalPosition; rel.Y = 0;
            float along = rel.Dot(dir);
            // A teammate shoulder to shoulder is beside the shot, not in front of it.
            if (along <= 1.2f || along >= length - .3f) continue;
            if ((rel - dir * along).Length() < other.Definition.ColliderRadius + .1f) return false;
        }
        return true;
    }

    /// <summary>
    /// Walk straight when the way is open for the whole body; otherwise follow a route field — the shared one
    /// toward the leader for formation moves, or this squadmate's own one toward any other goal.
    /// </summary>
    private Vector3 Steer(Vector3 goal, float pace, bool toLeader = true)
    {
        var to = goal - Bot.GlobalPosition; to.Y = 0;
        float d = to.Length();
        if (d < .35f) return Vector3.Zero;
        float speed = Mathf.Min(pace, d / 1.5f + .2f);
        // Height tiers: straight only on the same tier; another tier is reached by the route field (ramps, drop edges).
        bool sameTier = !_stage.Tiers.Any || _stage.Tiers.TierAt(goal) == _stage.Tiers.TierAt(Bot.GlobalPosition);
        if (d < 8f && sameTier && WideClear(goal)) return to / d * speed;
        FlowField field;
        if (toLeader) field = _stage.LeaderFlow;
        else
        {
            if (_goalFlow == null || _goalFlowVersion != _stage.FlowVersion)
            {
                _goalFlow = _stage.Flow.CopyLayout();
                _goalFlow.Riders = true;
                _goalFlowVersion = _stage.FlowVersion;
            }
            _goalFlow.Update(goal);
            field = _goalFlow;
        }
        var flow = Rays.FlowDirection(Bot, field);
        return (flow != Vector3.Zero || field.IsBlocked(Bot.GlobalPosition) ? flow : to / d) * speed;
    }

    /// <summary>Straight line to the goal is open for the whole body width, not just its centre (door frames).</summary>
    private bool WideClear(Vector3 goal)
    {
        var knee = Vector3.Up * .5f;
        var dir = goal - Bot.GlobalPosition; dir.Y = 0;
        var side = dir.LengthSquared() > .0001f ? dir.Normalized().Cross(Vector3.Up) * (Bot.Definition.ColliderRadius + .05f) : Vector3.Zero;
        foreach (var offset in new[] { Vector3.Zero, side, -side })
            if (!Rays.Clear(Bot, Bot.GlobalPosition + knee + offset, goal + knee + offset, Layers.MovementWorld | Layers.Props)) return false;
        return true;
    }

    /// <summary>Close in on a target, but never beyond the role's chase distance ahead of the leader.</summary>
    private Vector3 Approach(Vector3 target)
    {
        var leader = Leader;
        if (leader != null)
        {
            float ahead = _stage.Progress(leader.GlobalPosition) - _stage.Progress(Bot.GlobalPosition);
            if (ahead > Role.ChaseAhead && Doing != Activity.Rescue) return Vector3.Zero;
        }
        // Close enough to shoot cleanly; crowding the captor only blocks each other's shots.
        if (target.DistanceTo(Bot.GlobalPosition) < 4f && WideClear(target)) return Vector3.Zero;
        return Steer(target, 1f, toLeader: false);
    }

    private void PickWatchDirection()
    {
        _watchTimer = _rng.RandfRange(1.5f, 3.5f);
        var forward = Leader != null ? _stage.RouteForward(Leader.GlobalPosition) : Vector3.Right;
        // Each slot covers its own sector, so the squad looks around instead of staring the same way.
        float angle = Slot switch { 0 => 70f, 1 => -70f, _ => 180f } + _rng.RandfRange(-35f, 35f);
        _watchDirection = forward.Rotated(Vector3.Up, Mathf.DegToRad(angle));
    }

    // ---------- catching up ----------

    /// <summary>
    /// Far from every human and off every screen for a while: reappear on the route the leader already
    /// walked, behind them and off screen, then walk in (decisions.md M-15).
    /// </summary>
    private void UpdateCatchup(float dt)
    {
        var leader = Leader;
        // Held, stunned, healing or picking someone up: stay put (a teleport would break a hold, M-15).
        if (leader == null || Bot.IsRestrained || Bot.IsStunned || Bot.IsHealing || Bot.Reviving != null || Doing is Activity.Revive or Activity.Rescue)
        { UnseenFarTime = 0; return; }
        bool far = leader.GlobalPosition.DistanceTo(Bot.GlobalPosition) > Rules.CatchupDistance;
        bool unseen = !_stage.Effects.IsOnScreen(Bot.GlobalPosition + Vector3.Up, 1.2f);
        UnseenFarTime = far && unseen ? UnseenFarTime + dt : 0;
        if (UnseenFarTime < Rules.CatchupAfter) return;
        if (_stage.TrailSpotBehind(Rules.CatchupBehindMin, Rules.CatchupBehindMax) is not { } spot) return;
        // Never arrive further back than where the squadmate already is.
        if (_stage.Progress(spot) > _stage.Progress(Bot.GlobalPosition)) return;
        Bot.TeleportTo(spot);
        UnseenFarTime = 0;
        Teleports++;
        _thinkTimer = 0;
    }
}

public static class SquadGroups
{
    /// <summary>Ground hazards squadmates step out of (fire, vomit). Members carry a "radius" meta.</summary>
    public const string Hazard = "squad_hazard";
}
